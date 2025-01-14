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
        private IMessageSender messageSender;
        private IMessageCreator messageCreator;

        private uint recvSequence;
        private ulong ackMask;

        public DataReceiver(int maxPayloadSize, IMessageSender messageSender, IMessageCreator messageCreator)
        {
            this.maxPayloadSize = maxPayloadSize;
            this.assemblerByHandle = new Dictionary<handle_t, FragmentAssembler>();
            this.messageSender = messageSender;
            this.messageCreator = messageCreator;
        }

        public void Tick(double dt)
        {
        }

        private byte[] tmpbuf = new byte[2048];

        public unsafe void HandleChunkReceived(Message message)
        {
            uint sequence = (uint)message.GetVarULong();

            // discard duplicate and out of order
            if (sequence <= recvSequence)
                return;

            message.GetBits(DataStreamer.payloadFragmentCountBits, out uint numChunksUInt);
            int numChunks = (int)numChunksUInt;

            for (int i = 0; i < numChunks; i++)
            {
                message.GetBits(DataStreamer.fragmentHandleBits, out uint fragmentHandleUInt);
                handle_t fragmentHandle = new handle_t((int)fragmentHandleUInt);

                message.GetBits(DataStreamer.totalFragmentsBits, out uint numFragmentsUInt);
                int numFragments = (int)(numFragmentsUInt);

                message.GetBits(DataStreamer.fragmentIndexBits, out uint fragmentIndexUInt);
                int fragmentIndex = (int)(fragmentIndexUInt);

                message.GetBits(DataStreamer.arraySizeBits, out uint bufflenUInt);
                int bufflen = (int)bufflenUInt;

                UnsafeUtil.ZeroMemory(tmpbuf);

                int unreadBitsBefore = message.UnreadBits;
                message.GetBytes(bufflen, tmpbuf, 0);
                int unreadBitsAfter = message.UnreadBits;

                AssertUtil.True((unreadBitsBefore - unreadBitsAfter) % 8 == 0, "(unreadBitsBefore - unreadBitsAfter) % 8 == 0");
                int readBytes = (unreadBitsBefore - unreadBitsAfter) / 8;

                FragmentAssembler fragmentAssembler = GetOrCreateAssembler(fragmentHandle, numFragments);
                ProcessReceivedFragment(fragmentIndex, readBytes, fragmentAssembler, fragmentHandle);
            }

            // ack
            uint distance = sequence - recvSequence;
            if (distance > DataStreamSettings.ackMaskBitCount)
            {
                ackMask = 1;
            }
            else
            {
                ackMask = (ackMask << (int)distance) | 1;
            }

            recvSequence = sequence;

            Message ackMessage = messageCreator.Create();
            ackMessage.AddUInt(sequence);
            ackMessage.AddULong(ackMask);
            messageSender.Send(ackMessage);
        }

        private void ProcessReceivedFragment(int fragmentIndex, int readBytes, FragmentAssembler fragmentAssembler, handle_t fragmentHandle)
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
                    assemblerByHandle.Remove(fragmentHandle);
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
