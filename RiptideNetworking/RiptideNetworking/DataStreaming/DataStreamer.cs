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
        private readonly int maxPayloadSize;
        private readonly int maxHeaderSize;

        public DataStreamer(IConnectionDSStatusProvider connectionDataStreamStatusProvider,
                            IMessageCreator messageCreator,
                            IMessageSender messageSender,
                            int maxPayloadSize,
                            int maxHeaderSize)
        {
            _connectionDataStreamStatus = connectionDataStreamStatusProvider;
            _messageCreator = messageCreator;
            _messageSender = messageSender;
            this.maxPayloadSize = maxPayloadSize;
            this.maxHeaderSize = maxHeaderSize;
        }

        public void Tick(double dt)
        {
            ConnectionDataStreamStatus dataStreamStatus = _connectionDataStreamStatus.GetConnectionDSStatus();

            // calculate how many bytes we may send
            int maxSendableBytes = int.MaxValue;// Math.Min(DataStreamSettings.c_maxPayloadSize, (int)(dataStreamStatus.Cwnd - dataStreamStatus.BytesInFlight));

            if (maxSendableBytes <= 0)
                return;

            long sendableBits = ((long)maxSendableBytes) * 8;
            Message message = null;
            int numChunksWriteBit = 0;

            // stages:
            // 1) try send each chunk in their own packet
            // 2) pack any last (smaller) chunks in the same packet and send, if possible
            for (int i = 0; i < dataStreamStatus.PendingBuffers.Count; i++)
            {
                if (message == null)
                {
                    message = InitNewMessage(out numChunksWriteBit);
                }

                PendingBuffer current = dataStreamStatus.PendingBuffers[i];

                int index = current.SeekNextWaitingIndex(0);

                // could not find a chunk that we could send
                if (index < 0)
                    continue;

                ArraySlice<byte> buffer = current.GetBuffer(index);
                bool canWrite = CanSerializeBufferInBits(buffer, message.UnwrittenBits);
                Assert.True(canWrite, "can write");

                // only 1 chunk in this message
                message.SetBits(1, numChunksBits, numChunksWriteBit);

                // write fragment header
                message.AddBits((uint)(int)current.Handle, fragmentHandleBits);
                message.AddBits((uint)current.NumTotalChunks(), numFragmentsBits);
                message.AddBits((uint)index, fragmentIndexBits);

                message.AddVarULong((ulong)buffer.Length);
                message.AddBytes(buffer.Array, buffer.StartIndex, buffer.Length, includeLength: false);
                sendableBits -= message.WrittenBits;

                _messageSender.Send(message);
                sequence++;
                message = null;

                if (sendableBits < maxHeaderSize * 8 + 8)
                {
                    // can not write even a single header + one byte
                    return;
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
                bool canWrite = CanSerializeBufferInBits(buffer, message.UnwrittenBits);

                if (!canWrite)
                    continue;

                // write fragment header
                message.AddBits((uint)(int)current.Handle, fragmentHandleBits);
                message.AddBits((uint)current.NumTotalChunks(), numFragmentsBits);
                message.AddBits((uint)lastChunkIndex, fragmentIndexBits);

                message.AddBytes(buffer.Array, buffer.StartIndex, buffer.Length);
                sendableBits -= message.WrittenBits;

                addedChunks++;

                int minExtraFragmentBits = fragmentIndexBits + numFragmentsBits + 8;
                if (message.UnwrittenBits < minExtraFragmentBits)
                {
                    // message is full
                    FinalizeSendMultipleChunkMessage(message, numChunksWriteBit, addedChunks);
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
                FinalizeSendMultipleChunkMessage(message, numChunksWriteBit, addedChunks);
                message = null;
            }
        }

        private void FinalizeSendMultipleChunkMessage(Message message, int numChunksWriteBit, int addedChunks)
        {
            message.SetBits((uint)addedChunks, numChunksBits, numChunksWriteBit);
            _messageSender.Send(message);
            sequence++;
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

        public void HandleChunkAck(Message message)
        {
        }

        public event Action<PendingBuffer> OnDelivered;
    }
}
