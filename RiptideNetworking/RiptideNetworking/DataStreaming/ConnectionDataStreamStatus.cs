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
        /// Total amount of bytes in flight. Aka byte count
        /// we have sent but we don't know whether they are dropped or received.
        /// </summary>
        public long BytesInFlight { get; set; }

        public long SlowStartThreshold { get; set; }

        /// <summary>
        /// In bytes. Maximum amount of bytes we may drop on the wire
        /// without acknowledgement.
        /// </summary>
        public long Cwnd { get; set; }

        private readonly long initialCwnd;

        public DataStreamCongestionControlState State { get; set; }

        public RingBuffer<PayloadInfo> SendWindow { get; set; }

        /// <summary>
        /// Buffers that have not yet been fully delivered.
        /// </summary>
        public List<PendingBuffer> PendingBuffers { get; set; }

        public void ResetCwnd()
        {
            Cwnd = initialCwnd;
        }

        public void IncrementCwnd()
        {
            Cwnd += initialCwnd;
        }

        public ConnectionDataStreamStatus(int initialCwnd, int initialSlowStartThreshold)
        {
            this.initialCwnd = initialCwnd;

            PendingBuffers = new List<PendingBuffer>();
            ResetCwnd();

            SendWindow = new RingBuffer<PayloadInfo>(DataStreamSettings.maxSendWindowElements);

            this.SlowStartThreshold = initialSlowStartThreshold;
        }
    }
}
