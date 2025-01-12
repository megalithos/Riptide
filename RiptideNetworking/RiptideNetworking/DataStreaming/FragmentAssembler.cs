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
    internal class FragmentAssembler
    {
        private readonly byte[] buffer;
        private readonly byte[] recvMask;
        private readonly int fragmentSize;

        /// <summary>
        /// How many fragments we have received.
        /// </summary>
        private int numReceived;

        /// <summary>
        /// Total number of fragments in the buffer.
        /// </summary>
        public readonly int numFragments;

        public FragmentAssembler(int fragmentSize, int numFragments)
        {
            this.fragmentSize = fragmentSize;
            this.numFragments = numFragments;

            buffer = new byte[fragmentSize * numFragments];

            int requiredRecvMaskBytes = MyMath.IntCeilDiv(numFragments, 8);
            recvMask = new byte[requiredRecvMaskBytes];
        }

        /// <summary>
        /// Length of the returned byte array will always be multiple of 
        /// <see cref="fragmentSize"/>
        /// </summary>
        public byte[] GetAssembledBuffer()
        {
            if (!IsFullyReceived())
                throw new InvalidOperationException();

            return buffer;
        }

        public bool IsFullyReceived()
        {
            return numReceived == numFragments;
        }

        public bool IsFragmentReceived(int fragmentIndex)
        {
            ValidateIndex(fragmentIndex);

            int byteIndex = fragmentIndex / 8;
            int bitIndex = fragmentIndex % 8;
            byte mask = recvMask[byteIndex];
            return ((mask >> bitIndex) & 1) != 0;
        }

        public void AddFragment(int fragmentIndex, ArraySlice<byte> srcbuf)
        {
            ValidateIndex(fragmentIndex);
            if (srcbuf.Length != fragmentSize)
                throw new InvalidOperationException();

            if (IsFragmentReceived(fragmentIndex))
                throw new InvalidOperationException();

            int writeByteIndex = fragmentIndex * fragmentSize;
            Buffer.BlockCopy(srcbuf.Array, srcbuf.StartIndex, buffer, writeByteIndex, fragmentSize);

            int maskByteIndex = fragmentIndex / 8;
            int maskBitIndex = fragmentIndex % 8;
            // mark received
            int bvalue = recvMask[maskByteIndex];
            bvalue |= (1 << maskBitIndex);
            recvMask[maskByteIndex] = (byte)bvalue;

            numReceived++;
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= numFragments)
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
