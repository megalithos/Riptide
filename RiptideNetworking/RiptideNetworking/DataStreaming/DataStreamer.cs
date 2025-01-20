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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Riptide.DataStreaming
{
    // struct streamPacket
    //   message riptide header id      (4  bits)  // riptide header
    //   sequence                       (32 bits)  // subheader
    //   containedFragments             (16 bits)  // subheader
    //   totalFragments                 (32 bits)  // fragment header, required for reassembly
    //   fragmentIndex                  (32 bits)  // fragment header, required for reaseembly
    //   fragmentHandle                 (32 bits)  // fragment header, unique identifier for the whole buffer
    //   arraySize                      (32 bits)  // fragment header, size of the fragment data
    //   chunk0data...

    // whole packet is called payload.
    // each packet will have one and only one riptide header and subheader.
    // but may have multiple fragment headers.

    // todo:
    //   - last chunks (small) are not batched into one packet. it might not even be
    //     necessary since you would use this whole data streaming system
    //     to mainly stream large buffers, so the minimal gains might not
    //     be worth the efforts.

    // summary:
    //   - send pending buffers using congestion control
    //   - invoke event on completion
    internal class DataStreamer
    {
        // subheader
        public const int sequenceBits = 32;
        public const int payloadFragmentCountBits = 32;

        // fragment header
        public const int totalFragmentsBits = 16;
        public const int fragmentIndexBits = 32;
        public const int fragmentHandleBits = 32;
        public const int arraySizeBits = 32;

        public const int totalSubheaderBits = sequenceBits + payloadFragmentCountBits;
        public const int totalFragmentHeaderBits = totalFragmentsBits + fragmentIndexBits + fragmentHandleBits + arraySizeBits;
        private int minMessageBitsForSend;

        private uint sequence = 1;

        private readonly IConnectionDSStatusProvider _connectionDataStreamStatus;
        private readonly IMessageCreator _messageCreator;
        private readonly IMessageSender _messageSender;
        private readonly IReceiverRTTProvider receiverRTTProvider;
        private readonly int maxPayloadSize;
        private readonly int riptideHeaderSizeBits;
        private readonly int maxSendPacketsPerTick;

        public DataStreamer(IConnectionDSStatusProvider connectionDataStreamStatusProvider,
                            IMessageCreator messageCreator,
                            IMessageSender messageSender,
                            IReceiverRTTProvider receiverRTTProvider,
                            int maxPayloadSizeBytes,
                            int riptideHeaderSizeBits,
                            int maxSendPacketsPerTick = 0)
        {
            _connectionDataStreamStatus = connectionDataStreamStatusProvider;
            _messageCreator = messageCreator;
            _messageSender = messageSender;
            this.maxPayloadSize = maxPayloadSizeBytes;
            this.riptideHeaderSizeBits = riptideHeaderSizeBits;
            this.receiverRTTProvider = receiverRTTProvider;

            // +8 for one byte
            this.minMessageBitsForSend = riptideHeaderSizeBits + totalSubheaderBits + totalFragmentHeaderBits + 8;
            this.maxSendPacketsPerTick = maxSendPacketsPerTick;
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
            UpdateAndProcessExpiredEnvelopes(dt, dataStreamStatus);
            UpdateCwndTimerAndIncrementCwnd(dt, dataStreamStatus);

            // calculate how many bytes we may send
            int maxSendableBytes = (int)(dataStreamStatus.Cwnd - dataStreamStatus.BytesInFlight);
            long sendableBits = ((long)maxSendableBytes) * 8;

            if (sendableBits <= minMessageBitsForSend)
                return;

            Message message = null;
            int numChunksWriteBit = 0;
            bool breakOuter = false;
            int counter = 0;

            for (int i = 0; i < dataStreamStatus.PendingBuffers.Count; i++)
            {
                if (breakOuter)
                    break;

                PendingBuffer current = dataStreamStatus.PendingBuffers[i];
                int totalChunks = current.NumTotalChunks();
                while (true)
                {
                    if (message == null)
                        message = InitNewMessage(out numChunksWriteBit);

                    int index = current.SeekNextWaitingIndex(0);

                    // could not find a chunk that we could send
                    if (index < 0)
                        break;

                    ArraySlice<byte> buffer = current.GetBuffer(index);
                    bool canWrite = CanSerializeBufferInBitsIncludingFragmentHeaderSize(buffer, Math.Min(sendableBits, message.UnwrittenBits));
                    if (!canWrite)
                        break;

                    // only 1 chunk in this message
                    message.SetBits(1, payloadFragmentCountBits, numChunksWriteBit);

                    // write fragment header
                    message.AddBits((uint)(int)current.Handle, fragmentHandleBits);
                    message.AddBits((uint)current.NumTotalChunks(), totalFragmentsBits);
                    message.AddBits((uint)index, fragmentIndexBits);

                    message.AddBits((ulong)buffer.Length, arraySizeBits);
                    message.AddBytes(buffer.Array, buffer.StartIndex, buffer.Length, includeLength: false);
                    sendableBits -= message.BytesInUse * 8;

                    dataStreamStatus.BytesInFlight += message.BytesInUse;

                    _messageSender.Send(message);
                    counter++;
                    lastTickStat.countSentFullBuffers++;

                    current.SetChunkState(index, PendingChunkState.OnFlight);
                    current.PopWaitingIndex();

                    if (dataStreamStatus.SendWindow.IsFull)
                    {
                        dataStreamStatus.SendWindow.Resize(dataStreamStatus.SendWindow.Capacity * 2);
                    }

                    AddPayloadInfoToSendWindow(message, new List<ChunkPtr>(1) { new ChunkPtr(current, index) });
                    sequence++;

                    message = null;

                    if (maxSendPacketsPerTick > 0 && counter >= maxSendPacketsPerTick)
                    {
                        breakOuter = true;
                        break;
                    }

                    if (sendableBits <= minMessageBitsForSend)
                    {
                        // can not write even a single header + one byte
                        breakOuter = true;
                        break;
                    }
                }
            }
        }

        private void UpdateCwndTimerAndIncrementCwnd(double dt, ConnectionDataStreamStatus dataStreamStatus)
        {
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

        private void UpdateAndProcessExpiredEnvelopes(double dt, ConnectionDataStreamStatus dataStreamStatus)
        {
            // decrement expiration timers
            for (int i = 0; i < dataStreamStatus.SendWindow.Count; i++)
            {
                PayloadInfo info = dataStreamStatus.SendWindow[i];
                info.ExpirationTimer -= (float)dt;
                dataStreamStatus.SendWindow[i] = info;
            }

            // expire envelopes in order
            while (dataStreamStatus.SendWindow.Count > 0)
            {
                var envelope = dataStreamStatus.SendWindow.Peek();
                if (envelope.ExpirationTimer > 0)
                    break;

                dataStreamStatus.SendWindow.Pop();
                ProcessEnvelope(envelope, true);
            }
        }

        private void FinalizeSendMultipleChunkMessage(Message message, int numChunksWriteBit, int addedChunks, ConnectionDataStreamStatus dataStreamStatus)
        {
            message.SetBits((uint)addedChunks, payloadFragmentCountBits, numChunksWriteBit);
            _messageSender.Send(message);
            lastTickStat.countSentPartialMessages++;
            dataStreamStatus.BytesInFlight += message.BytesInUse;
            sequence++;

            List<ChunkPtr> containedChunkPtrs = new List<ChunkPtr>(chunkIndices.Count);

            foreach (ChunkPtr ptr in chunkIndices)
            {
                PendingBuffer buffer = ptr.Buffer;
                AssertUtil.True(buffer != null, "buffer != null");

                buffer.SetChunkState(ptr.ChunkIndex, PendingChunkState.OnFlight);
                containedChunkPtrs.Add(ptr);
            }

            AddPayloadInfoToSendWindow(message, containedChunkPtrs);

            chunkIndices.Clear();
        }

        private Message InitNewMessage(out int numChunksWriteBit)
        {
            Message message = _messageCreator.Create();
            message.AddVarULong(sequence);

            numChunksWriteBit = message.WrittenBits;

            // reserve bits for writing how many chunks are included in the message,
            // this is useful when we write multiple smaller chunks in a single message.
            message.ReserveBits(payloadFragmentCountBits);
            return message;
        }

        private bool CanSerializeBufferInBitsIncludingFragmentHeaderSize(ArraySlice<byte> buffer, long bits)
        {
            int buffsizeWithFragmentHeaderInBits = buffer.Length * 8 + totalFragmentHeaderBits;
            return buffsizeWithFragmentHeaderInBits <= bits;
        }

        private void AddPayloadInfoToSendWindow(Message message, List<ChunkPtr> containedChunkPtrs)
        {
            var dataStreamStatus = _connectionDataStreamStatus.GetConnectionDSStatus();

            int rtt = receiverRTTProvider.get_rtt_ms();
            if (rtt < 0)
                rtt = 500;

            dataStreamStatus.SendWindow.Push(new PayloadInfo(
                sequence,
                message.BytesInUse,
                containedChunkPtrs,
                (float)Math.Max(rtt / 1000f * 2, 0.1) 
            ));
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

        private uint minValidSequence;
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

                // distance == 0 or less, that means
                // we have delivered enveloper or it has been lost
                streamStatus.SendWindow.Pop();

                bool lost = (ackMask & (1UL << -distance)) == 0UL
                    || distance < -DataStreamSettings.ackMaskBitCount;

                ProcessEnvelope(envelope, lost);
            }
        }

        private void ProcessEnvelope(PayloadInfo envelope, bool lost)
        {
            var streamStatus = _connectionDataStreamStatus.GetConnectionDSStatus();
            streamStatus.BytesInFlight -= envelope.Size;

            if (lost)
            {
                foreach (var chunkptr in envelope.Buffers)
                {
                    chunkptr.Buffer.droppedCount++;
                }

                if (streamStatus.State == CongestionControlState.SlowStart && envelope.Sequence >= minValidSequence)
                {
                    streamStatus.SlowStartThreshold = streamStatus.Cwnd / 2;
                    streamStatus.ResetCwnd();
                    streamStatus.State = CongestionControlState.SlowStart;

                    // invalidate any sequences in the send window, so they wont increment or reset cwnd
                    if (streamStatus.SendWindow.Count > 0)
                    {
                        minValidSequence = streamStatus.SendWindow.PeekLast().Sequence + 1;
                        AssertUtil.True(minValidSequence != 0, "minSequenceForSlowStartIncrement != 0");
                    }
                }
            }
            else
            {
                if (streamStatus.State == CongestionControlState.SlowStart &&
                    envelope.Sequence >= minValidSequence)
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

            if (lost)
            {
                SetChunkStates(envelope.Buffers, PendingChunkState.Waiting);
            }
            else
            {
                SetChunkStates(envelope.Buffers, PendingChunkState.Delivered);
            }

            if (lost)
            {
//                RiptideLogger.Log(LogType.Debug, $"Packet #{envelope.Sequence} was LOST.");
//                foreach (var chunkptr in envelope.Buffers)
//                {
//                    RiptideLogger.Log(LogType.Debug, $"Buffer: {chunkptr.Buffer.Handle}, dropped count: {chunkptr.Buffer.droppedCount}");
//                }
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

                // RiptideLogger.Log(LogType.Debug, $"Delivered chunk #{chunkIndex}, delivered chunks: {buffer.NumDeliveredChunks()} / {buffer.NumTotalChunks()}");

                if (buffer.IsDelivered())
                {
                    OnDelivered?.Invoke(buffer);
                }

                if (state == PendingChunkState.Waiting)
                    buffer.PushWaitingIndex((ushort)chunkIndex);
            }
        }


        public event Action<PendingBuffer> OnDelivered;
    }
}
