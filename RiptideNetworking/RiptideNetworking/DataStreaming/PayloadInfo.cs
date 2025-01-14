// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.DataStreaming
{
    internal struct PayloadInfo
    {
        public uint Sequence;

        /// <summary>
        /// In bytes.
        /// </summary>
        public int Size;

        /// <summary>
        /// Contains handles to chunks that were included
        /// in the payload.
        /// </summary>
        public List<ChunkPtr> Buffers;

        public float ExpirationTimer;

        public PayloadInfo(uint sequence, int size, List<ChunkPtr> buffers, float expirationTimer)
        {
            Sequence = sequence;
            Size = size;
            Buffers = buffers;
            this.ExpirationTimer = expirationTimer;
        }
    }
}
