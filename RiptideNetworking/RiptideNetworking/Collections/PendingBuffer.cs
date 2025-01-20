// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.DataStreaming;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.Collections
{
    internal class PendingBuffer
    {
        private byte[] buffer;
        private byte[] chunkStates;
        private int totalChunks;
        private int numDeliveredChunks;
        private int numOnFlightChunks;
        private int numWaitingChunks;

        private const int chunkStateBits = 2;
        private int chunkStatesPerByte;
        private int maxChunkSize;

        public handle_t Handle;

        public int droppedCount;

        public RingBuffer<ushort> waitingIndices;

        public void Construct(byte[] buffer, int maxChunkSize)
        {
            ValidateArgs(buffer, maxChunkSize);

            int numRequiredChunks = MyMath.IntCeilDiv(buffer.Length, maxChunkSize);

            this.buffer = buffer;

            int chunkBitsRequired = chunkStateBits * numRequiredChunks;
            int chunkStatesArraySize = MyMath.IntCeilDiv(chunkBitsRequired, 8);

            AssertUtil.True(chunkStateBits <= 8, "chunkStateBits <= 8");
            AssertUtil.True(chunkStateBits % 2 == 0, "chunkStateBits % 2 == 0");
            this.chunkStates = new byte[chunkStatesArraySize];
            totalChunks = numRequiredChunks;
            chunkStatesPerByte = 8 / chunkStateBits;

            this.maxChunkSize = maxChunkSize;

            waitingIndices = new RingBuffer<ushort>(totalChunks);
            for (int i = 0; i < totalChunks; i++)
            {
                waitingIndices.Push((ushort)i);
            }
        }

        private static void ValidateArgs(byte[] buffer, int maxChunkSize)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (maxChunkSize <= 0)
                throw new ArgumentException(nameof(maxChunkSize));
            if (chunkStateBits < 0 || chunkStateBits > 8)
                throw new ArgumentOutOfRangeException(nameof(chunkStateBits));
        }

        public PendingChunkState GetChunkState(int chunkIndex)
        {
            if (chunkIndex < 0 || chunkIndex >= totalChunks)
                throw new ArgumentOutOfRangeException(nameof(chunkIndex));

            GetIndexes(chunkIndex, out int byteIndex, out int chunkStateIndexWithinByte);
            byte bvalue = chunkStates[byteIndex];
            byte mask = GetChunkStateMask();

            bvalue = (byte)(bvalue >> (chunkStateIndexWithinByte * chunkStateBits));
            byte chunkState = (byte)(bvalue & mask);
            return (PendingChunkState)(chunkState);
        }

        public void SetChunkState(int chunkIndex, PendingChunkState state)
        {
            if (chunkIndex < 0 || chunkIndex >= totalChunks)
                throw new ArgumentOutOfRangeException(nameof(chunkIndex));

            var prevState = GetChunkState(chunkIndex);

            if (prevState != state)
            {
                if (state == PendingChunkState.Delivered)
                    numDeliveredChunks++;
                else if (prevState == PendingChunkState.Delivered)
                    numDeliveredChunks--;
            }

            int byteIndex, chunkStateIndexWithinByte;
            GetIndexes(chunkIndex, out byteIndex, out chunkStateIndexWithinByte);

            byte bvalue = (byte)state;
            byte mask = GetChunkStateMask();

            byte originalByte = chunkStates[byteIndex];
            originalByte &= (byte)(~(mask << (chunkStateIndexWithinByte * chunkStateBits))); // reset state we are about to set
            chunkStates[byteIndex] = (byte)(originalByte | (bvalue << (chunkStateIndexWithinByte * chunkStateBits)));
        }

        public ArraySlice<byte> GetBuffer(int chunkIndex)
        {
            int byteOffset = maxChunkSize * chunkIndex;

            int sliceLength = Math.Min(buffer.Length - byteOffset, maxChunkSize);
            var slice = new ArraySlice<byte>(buffer, byteOffset, sliceLength);
            return slice;
        }

        public int SeekNextWaitingIndex(int seekStartChunkIndex)
        {
            if (seekStartChunkIndex < 0 || seekStartChunkIndex >= totalChunks)
                throw new ArgumentOutOfRangeException(nameof(seekStartChunkIndex));

            if (waitingIndices.Count > 0)
                return waitingIndices.Peek();

            return -1;
        }

        public void PopWaitingIndex()
        {
            waitingIndices.Pop();
        }

        public void PushWaitingIndex(ushort index)
        {
            waitingIndices.Push(index);
        }

        public int GetLastChunkIndex()
        {
            return totalChunks - 1;
        }

        public bool IsDelivered()
        {
            return NumTotalChunks() == numDeliveredChunks;
        }

        public int NumDeliveredChunks() => numDeliveredChunks;

        public int NumTotalChunks() { return totalChunks; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetChunkStateMask()
        {
            return (1 << chunkStateBits) - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetIndexes(int chunkIndex, out int byteIndex, out int chunkStateIndexWithinByte)
        {
            byteIndex = chunkIndex / chunkStatesPerByte;
            chunkStateIndexWithinByte = chunkIndex % chunkStatesPerByte;
        }
    }
}
