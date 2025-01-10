// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.DataStreaming
{
    internal class ConnectionDataStreamStatus
    {
        public float CwndIncreaseTimer { get; set; }
        public uint SequenceId { get; set; } = 0;
        public uint RecvSequence { get; set; } = 0;
        public ulong RecvAckMask { get; set; } = 0;

        /// <summary>
        /// Total amount of bytes in flight.
        /// </summary>
        public ulong BytesInFlight { get; set; }

        /// <summary>
        /// Congestion window in bytes.
        /// </summary>
        public long Cwnd { get; set; }

        private readonly long initialCwnd;

        public DataStreamCongestionControlState State { get; set; }

        public RingBuffer<SendEnvelope> SendWindow { get; set; }

        /// <summary>
        /// Buffers that have been sent but we do not know if the recipient has
        /// received or dropped them.
        /// </summary>
        public Dictionary<long, ArraySlice<byte>> BuffersOnFlight = new Dictionary<long, ArraySlice<byte>>();

        /// <summary>
        /// Buffers waiting to be sent.
        /// </summary>
        public RingBuffer<ArraySlice<byte>> WaitingBuffers = new RingBuffer<ArraySlice<byte>>(1024);

        public void ResetCwnd()
        {
            Cwnd = initialCwnd;
        }

        /// <summary>
        /// e.g. if initial cwnd is 1, will increase cwnd
        /// by 1, if it is let's say MSS and we are using byte count,
        /// will incrase by that. like if INITIAL_CWND is 1200, after first call to this
        /// it will be 2400, after third call it'll be 3600 and so on
        /// </summary>
        public void IncreaseCwndByInitialCwnd()
        {
            Cwnd += initialCwnd;
        }

        public ConnectionDataStreamStatus()
        {
            this.initialCwnd = DataStreamSettings.initialCwndSize;
            ResetCwnd();

            SendWindow = new RingBuffer<SendEnvelope>(DataStreamSettings.maxSendWindowElements);
        }
    }
}
