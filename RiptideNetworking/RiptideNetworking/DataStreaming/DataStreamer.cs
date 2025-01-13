// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using Riptide.Transports;
using Riptide.Utils;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.DataStreaming
{
    // struct s2cMessageHeader
    //   message riptide header id (4  bits)
    //   sequence          (32 bits)
    //   numChunks         (16 bits)
    //   numFragments      (32 bits) ~5 TB max limit
    //   fragmentIndex     (32 bits)
    //   chunk0data...

    // summary:
    //   - send pending buffers using congestion control
    //   - invoke event on completion
    internal class DataStreamer
    {
        public const int sequenceBits = 32;
        public const int numChunksBits = 16;
        public const int numFragmentsBits = 32; // ~5 TB max limit with ~1200 B packets
        public const int fragmentIndexBits = 32;
        public const int fragmentHandleBits = 32;

        /// <summary>
        /// Minimum amount of bits for the header, excluding any riptide message headers.
        /// </summary>
        public const int numHeaderBits = sequenceBits + numChunksBits + numFragmentsBits + fragmentIndexBits + fragmentHandleBits;
        public const int numChunkHeaderBits = numFragmentsBits + fragmentIndexBits + fragmentHandleBits;

        private uint sequence = 1;

        private readonly IConnectionDSStatusProvider _connectionDataStreamStatus;
        private readonly IMessageCreator _messageCreator;
        private readonly IMessageSender _messageSender;
        private readonly IReceiverRTTProvider receiverRTTProvider;
        private readonly int maxPayloadSize;
        private readonly int maxHeaderSize;

        public DataStreamer(IConnectionDSStatusProvider connectionDataStreamStatusProvider,
                            IMessageCreator messageCreator,
                            IMessageSender messageSender,
                            IReceiverRTTProvider receiverRTTProvider,
                            int maxPayloadSize,
                            int maxHeaderSize)
        {
            _connectionDataStreamStatus = connectionDataStreamStatusProvider;
            _messageCreator = messageCreator;
            _messageSender = messageSender;
            this.maxPayloadSize = maxPayloadSize;
            this.maxHeaderSize = maxHeaderSize;
            this.receiverRTTProvider = receiverRTTProvider;
        }

        public struct tickstat_t
        {
            public int countSentFullBuffers;
            public int countSentPartialMessages;
        }

        public tickstat_t GetLastTickStat() => lastTickStat;
        private tickstat_t lastTickStat;

        private static List<ChunkPtr> chunkIndices = new List<ChunkPtr>();
        public void Tick(double dt)
        {
            lastTickStat = default;
            chunkIndices.Clear();
            ConnectionDataStreamStatus dataStreamStatus = _connectionDataStreamStatus.GetConnectionDSStatus();

            // calculate how many bytes we may send
            int maxSendableBytes = (int)(dataStreamStatus.Cwnd - dataStreamStatus.BytesInFlight);

            if (maxSendableBytes <= 0)
                return;

            long sendableBits = ((long)maxSendableBytes) * 8;
            Message message = null;
            int numChunksWriteBit = 0;

            if (maxSendableBytes >= maxPayloadSize)
            {
                // stages:
                // 1) try send each chunk in their own packet
                // 2) pack any last (smaller) chunks in the same packet and send, if possible
                for (int i = 0; i < dataStreamStatus.PendingBuffers.Count; i++)
                {
                    PendingBuffer current = dataStreamStatus.PendingBuffers[i];

                    int totalChunks = current.NumTotalChunks();
                    while (true) // iterate chunks
                    {
                        if (message == null)
                        {
                            message = InitNewMessage(out numChunksWriteBit);
                        }

                        int index = current.SeekNextWaitingIndex(0);

                        // could not find a chunk that we could send
                        if (index < 0)
                            break;

                        ArraySlice<byte> buffer = current.GetBuffer(index);
                        bool canWrite = CanSerializeBufferInBits(buffer, Math.Min(sendableBits, message.UnwrittenBits));
                        if (!canWrite)
                            break;

                        // only 1 chunk in this message
                        message.SetBits(1, numChunksBits, numChunksWriteBit);

                        // write fragment header
                        message.AddBits((uint)(int)current.Handle, fragmentHandleBits);
                        message.AddBits((uint)current.NumTotalChunks(), numFragmentsBits);
                        message.AddBits((uint)index, fragmentIndexBits);

                        message.AddVarULong((ulong)buffer.Length);
                        message.AddBytes(buffer.Array, buffer.StartIndex, buffer.Length, includeLength: false);
                        sendableBits -= message.BytesInUse * 8;

                        dataStreamStatus.BytesInFlight += message.WrittenBits / 8;

                        _messageSender.Send(message);
                        lastTickStat.countSentFullBuffers++;

                        current.SetChunkState(index, PendingChunkState.OnFlight);

                        if (dataStreamStatus.SendWindow.IsFull)
                        {
                            dataStreamStatus.SendWindow.Resize(dataStreamStatus.SendWindow.Capacity * 2);
                        }

                        dataStreamStatus.SendWindow.Push(new PayloadInfo(
                            sequence,
                            MyMath.IntCeilDiv(message.WrittenBits, 8),
                            new List<ChunkPtr> { new ChunkPtr(current, index) }
                        ));

                        sequence++;
                        message = null;

                        if (sendableBits < maxHeaderSize * 8 + 8)
                        {
                            // can not write even a single header + one byte
                            return;
                        }
                    }
                }
            }

            int addedChunks = 0;
            for (int i = 0; i < dataStreamStatus.PendingBuffers.Count; i++)
            {
                if (message == null)
                {
                    addedChunks = 0;
                    message = InitNewMessage(out numChunksWriteBit);
                }

                PendingBuffer current = dataStreamStatus.PendingBuffers[i];
                int lastChunkIndex = current.GetLastChunkIndex();

                if (current.GetChunkState(lastChunkIndex) != PendingChunkState.Waiting)
                {
                    continue;
                }

                ArraySlice<byte> buffer = current.GetBuffer(lastChunkIndex);
                bool canWrite = CanSerializeBufferInBits(buffer, Math.Min(sendableBits, message.UnwrittenBits));

                if (!canWrite)
                    continue;

                // write fragment header
                message.AddBits((uint)(int)current.Handle, fragmentHandleBits);
                message.AddBits((uint)current.NumTotalChunks(), numFragmentsBits);
                message.AddBits((uint)lastChunkIndex, fragmentIndexBits);

                message.AddBytes(buffer.Array, buffer.StartIndex, buffer.Length);
                sendableBits -= message.WrittenBits;

                chunkIndices.Add(new ChunkPtr(current, lastChunkIndex));

                addedChunks++;

                int minExtraFragmentBits = fragmentIndexBits + numFragmentsBits + 8;
                if (message.UnwrittenBits < minExtraFragmentBits)
                {
                    // message is full
                    FinalizeSendMultipleChunkMessage(message, numChunksWriteBit, addedChunks, dataStreamStatus);
                    message = null;
                }

                if (sendableBits < minExtraFragmentBits)
                {
                    // can not write a single additional chunk with single byte
                    break;
                }
            }

            if (message != null && addedChunks > 0)
            {
                FinalizeSendMultipleChunkMessage(message, numChunksWriteBit, addedChunks, dataStreamStatus);
                message = null;
            }

            dataStreamStatus.CwndIncrementTimer -= (float)dt;
            if (dataStreamStatus.CwndIncrementTimer < 0f)
                dataStreamStatus.CwndIncrementTimer = 0f;

            if (Math.Abs(dataStreamStatus.CwndIncrementTimer) <= 0.0001f &&
                dataStreamStatus.State == CongestionControlState.CongestionAvoidance &&
                receiverRTTProvider.get_rtt_ms() > 0)
            {
                dataStreamStatus.IncrementCwnd();
                dataStreamStatus.CwndIncrementTimer = receiverRTTProvider.get_rtt_ms() / 1000f;
            }
        }

        private void FinalizeSendMultipleChunkMessage(Message message, int numChunksWriteBit, int addedChunks, ConnectionDataStreamStatus dataStreamStatus)
        {
            message.SetBits((uint)addedChunks, numChunksBits, numChunksWriteBit);
            _messageSender.Send(message);
            lastTickStat.countSentPartialMessages++;
            dataStreamStatus.BytesInFlight += message.WrittenBits / 8;
            sequence++;

            List<ChunkPtr> containedChunkPtrs = new List<ChunkPtr>(chunkIndices.Count);

            foreach (ChunkPtr ptr in chunkIndices)
            {
                PendingBuffer buffer = ptr.Buffer;
                AssertUtil.True(buffer != null, "buffer != null");

                buffer.SetChunkState(ptr.ChunkIndex, PendingChunkState.OnFlight);
                containedChunkPtrs.Add(ptr);
            }

            dataStreamStatus.SendWindow.Push(new PayloadInfo(
                sequence,
                message.WrittenBits / 8,
                containedChunkPtrs
            ));

            chunkIndices.Clear();
        }

        private Message InitNewMessage(out int numChunksWriteBit)
        {
            Message message = _messageCreator.Create();
            message.AddVarULong(sequence);

            numChunksWriteBit = message.WrittenBits;

            // reserve bits for writing how many chunks are included in the message,
            // this is useful when we write multiple smaller chunks in a single message.
            message.ReserveBits(numChunksBits);
            return message;
        }

        private bool CanSerializeBufferInBits(ArraySlice<byte> buffer, long bits)
        {
            int buffsizeWithFragmentHeaderInBits = buffer.Length * 8 + numFragmentsBits + fragmentIndexBits;
            return buffsizeWithFragmentHeaderInBits <= bits;
        }

        public static int debugValue = 0;
        private uint clientRecvSequence;

        public struct ackstat_t
        {
            public int acksReceived;
        }

        private ackstat_t lastAckStat;
        public ackstat_t GetLastAckStat() => lastAckStat;
        public void ResetLastAckStat() => lastAckStat = default;

        private uint minSequenceForSlowStartIncrement;
        public void HandleChunkAck(Message message)
        {
            lastAckStat.acksReceived++;

            uint sequence = message.GetUInt();
            ulong ackMask = message.GetULong();

            // reject acks for duplicate & out of order
            if (clientRecvSequence >= sequence)
            {
                return;
            }

            clientRecvSequence = sequence;

            ConnectionDataStreamStatus streamStatus = _connectionDataStreamStatus.GetConnectionDSStatus();

            while (streamStatus.SendWindow.Count > 0)
            {
                var envelope = streamStatus.SendWindow.Peek();

                int distance = (int)(envelope.Sequence - clientRecvSequence);

                if (distance > 0)
                {
                    break;
                }

                streamStatus.BytesInFlight -= envelope.Size;

                AssertUtil.True(streamStatus.BytesInFlight >= 0, "streamStatus.BytesInFlight >= 0");

                // distance == 0 or less, that means
                // we have delivered enveloper or it has been lost

                streamStatus.SendWindow.Pop();

                bool packetLost;
                if ((ackMask & (1UL << -distance)) == 0UL
                    || distance < -DataStreamSettings.ackMaskBitCount)
                {
                    packetLost = true;

                    if (streamStatus.State != CongestionControlState.SlowStart)
                    {
                        streamStatus.SlowStartThreshold = streamStatus.Cwnd / 2;
                        streamStatus.ResetCwnd();
                        streamStatus.State = CongestionControlState.SlowStart;

                        // invalidate any sequences in the send window, so they wont increment cwnd
                        // since it was just reset
                        minSequenceForSlowStartIncrement = streamStatus.SendWindow.PeekLast().Sequence + 1;
                        AssertUtil.True(minSequenceForSlowStartIncrement != 0, "minSequenceForSlowStartIncrement != 0");
                    }
                }
                else
                {
                    packetLost = false;
                    if (streamStatus.State == CongestionControlState.SlowStart &&
                        envelope.Sequence >= minSequenceForSlowStartIncrement)
                    {
                        streamStatus.IncrementCwnd();

                        if (streamStatus.Cwnd >= streamStatus.SlowStartThreshold)
                        {
                            streamStatus.State = CongestionControlState.CongestionAvoidance;
                            streamStatus.CwndIncrementTimer = receiverRTTProvider.get_rtt_ms() / 1000f;
                        }
                        debugValue++;
                    }
                }

                if (packetLost)
                {
                    SetChunkStates(envelope.Buffers, PendingChunkState.Waiting);
                }
                else
                {
                    SetChunkStates(envelope.Buffers, PendingChunkState.Delivered);
                }
            }
        }

        private void SetChunkStates(List<ChunkPtr> list, PendingChunkState state)
        {
            AssertUtil.True(list != null, "list != null");
            AssertUtil.True(list.Count > 0, "list.Count > 0");

            foreach (var ptr in list)
            {
                int chunkIndex = ptr.ChunkIndex;
                PendingBuffer buffer = ptr.Buffer;
                AssertUtil.True(buffer != null, "buffer != null");
                buffer.SetChunkState(chunkIndex, state);

                if (buffer.IsDelivered())
                {
                    OnDelivered?.Invoke(buffer);
                }
            }
        }


        public event Action<PendingBuffer> OnDelivered;
    }
}
