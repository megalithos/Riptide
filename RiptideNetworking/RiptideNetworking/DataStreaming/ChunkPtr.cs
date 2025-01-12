// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.DataStreaming
{
    internal struct ChunkPtr
    {
        public PendingBuffer Buffer;
        public int ChunkIndex;

        public ChunkPtr(PendingBuffer buffer, int chunkIndex)
        {
            Buffer = buffer;
            ChunkIndex = chunkIndex;
        }
    }
}
