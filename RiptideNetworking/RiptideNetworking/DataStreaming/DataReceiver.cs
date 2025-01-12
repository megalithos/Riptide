// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        private readonly Dictionary<handle_t, FragmentAssembler> assemblerByHandle;


        private int maxPayloadSize;
        public DataReceiver(int maxPayloadSize)
        {
            this.maxPayloadSize = maxPayloadSize;
            this.assemblerByHandle = new Dictionary<handle_t, FragmentAssembler>();
        }

        public void Tick(double dt)
        {
        }

        private byte[] tmpbuf = new byte[2048];

        public unsafe void HandleChunkReceived(Message message)
        {
            uint sequence = (uint)message.GetVarULong();
            message.GetBits(DataStreamer.numChunksBits, out uint numChunksUInt);
            int numChunks = (int)numChunksUInt;

            for (int i = 0; i < numChunks; i++)
            {
                message.GetBits(DataStreamer.fragmentHandleBits, out uint fragmentHandleUInt);
                handle_t fragmentHandle = new handle_t((int)fragmentHandleUInt);

                message.GetBits(DataStreamer.numFragmentsBits, out uint numFragmentsUInt);
                int numFragments = (int)(numFragmentsUInt);

                message.GetBits(DataStreamer.fragmentIndexBits, out uint fragmentIndexUInt);
                int fragmentIndex = (int)(fragmentIndexUInt);

                int bufflen = (int)message.GetVarULong();

                UnsafeUtil.ZeroMemory(tmpbuf);

                int unreadBitsBefore = message.UnreadBits;
                message.GetBytes(bufflen, tmpbuf, 0);
                int unreadBitsAfter = message.UnreadBits;

                Assert.True((unreadBitsBefore - unreadBitsAfter) % 8 == 0, "(unreadBitsBefore - unreadBitsAfter) % 8 == 0");
                int readBytes = (unreadBitsBefore - unreadBitsAfter) / 8;

                FragmentAssembler fragmentAssembler = GetOrCreateAssembler(fragmentHandle, numFragments);
                ProcessReceivedFragment(fragmentIndex, readBytes, fragmentAssembler);
            }

            // ack
        }

        private void ProcessReceivedFragment(int fragmentIndex, int readBytes, FragmentAssembler fragmentAssembler)
        {
            if (!fragmentAssembler.IsFragmentReceived(fragmentIndex))
            {
                ArraySlice<byte> slice = new ArraySlice<byte>(tmpbuf, 0, maxPayloadSize);
                fragmentAssembler.AddFragment(fragmentIndex, slice);

                if (fragmentAssembler.IsFullyReceived())
                {
                    byte[] buf = fragmentAssembler.GetAssembledBuffer();

                    int bufflen = (buf[0] & 0xFF) |
                        ((buf[1] & 0xFF) << 8) |
                        ((buf[2] & 0xFF) << 16) |
                        ((buf[3] & 0xFF) << 24);

                    // +4 to include first 4 bytes = length
                    slice = new ArraySlice<byte>(buf, 0, bufflen + 4);

                    OnReceived?.Invoke(slice);
                }
            }
        }

        private FragmentAssembler GetOrCreateAssembler(handle_t fragmentHandle, int numFragments)
        {
            FragmentAssembler fragmentAssembler;
            if (!assemblerByHandle.TryGetValue(fragmentHandle, out fragmentAssembler))
            {
                fragmentAssembler = new FragmentAssembler(maxPayloadSize, numFragments);
                assemblerByHandle[fragmentHandle] = fragmentAssembler;
            }

            return fragmentAssembler;
        }

        public event Action<ArraySlice<byte>> OnReceived;
    }

}
