// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using Riptide.Utils;
using System;
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

        /// <summary>
        /// Minimum amount of bits for the header, excluding any riptide message headers.
        /// </summary>
        public const int numHeaderBits = sequenceBits + numChunksBits + numFragmentsBits + fragmentIndexBits;
        public const int numChunkHeaderBits = numFragmentsBits + fragmentIndexBits;

        private uint sequence = 1;

        private readonly IConnectionDSStatusProvider _connectionDataStreamStatus;
        private readonly IMessageCreator _messageCreator;
        private readonly IMessageSender _messageSender;
        private readonly int maxPayloadSize;

        public DataStreamer(IConnectionDSStatusProvider connectionDataStreamStatusProvider,
                            IMessageCreator messageCreator,
                            IMessageSender messageSender,
                            int maxPayloadSize)
        {
            _connectionDataStreamStatus = connectionDataStreamStatusProvider;
            _messageCreator = messageCreator;
            _messageSender = messageSender;
            this.maxPayloadSize = maxPayloadSize;
        }

        public void Tick(double dt)
        {
            ConnectionDataStreamStatus dataStreamStatus = _connectionDataStreamStatus.GetConnectionDSStatus();

            int maxSendableBytes = Math.Min(maxPayloadSize, (int)(dataStreamStatus.Cwnd - dataStreamStatus.BytesInFlight));

            if (maxSendableBytes <= 0)
                return;


        }

        public void HandleChunkAck(Message message)
        {
        }

        public event Action<PendingBuffer> OnDelivered;
    }
}
