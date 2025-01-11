// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.DataStreaming
{
    // summary:
    //   - construct buffers out of fragments
    //   - invoke event on buffer received
    //   - acknowledge received packets
    internal class DataReceiver
    {
        public void Tick(double dt)
        {
        }

        public void HandleChunkReceived(Message message)
        {
        }

        public event Action<byte[]> OnReceived;
    }

}
