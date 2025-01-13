// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiptideTests.DataStreaming
{
    internal struct OnFlightTestMessage
    {
        internal enum Dir
        {
            streamer2receiver = 1,
            receiver2streamer
        }
        public Message message;
        public double arrivalTime;
        public int size;
        public Dir dir;
        public bool remove;
    }
}
