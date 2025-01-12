// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide;
using Riptide.DataStreaming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit;
using NUnit.Framework;
using Riptide.Utils;
using System.Security.Cryptography;
using Assert = NUnit.Framework.Assert;
using Riptide.Collections;

namespace RiptideTests
{
    internal class SimpleDataStreamerTests : IConnectionDSStatusProvider, IMessageCreator, IMessageSender
    {
        private ConnectionDataStreamStatus status;
        private DataStreamer streamer;
        private DataReceiver receiver;
        private const int maxPayloadSize = 1200;
        private byte[] recvBytes;

        [SetUp]
        public void Setup()
        {
            status = new ConnectionDataStreamStatus();
            streamer = new DataStreamer(this, this, this, maxPayloadSize, Message.MaxHeaderSize + MyMath.IntCeilDiv(DataStreamer.numHeaderBits, 8));
            receiver = new DataReceiver(maxPayloadSize);

            recvBytes = null;
            receiver.OnReceived += (byte[] bytes) =>
            {
                recvBytes = new byte[bytes.Length];
                Buffer.BlockCopy(bytes, 0, recvBytes, 0, bytes.Length);
            };
        }

        [Test]
        public void Simple_FragmentedTestBufferIsReceived_TwoFullFragments()
        {
            int len = maxPayloadSize - 4;
            byte[] buffer = TestUtil.GenerateRandomByteArray(maxPayloadSize);

            // write len of payload
            buffer[0] = (byte)(len & 0xFF);
            buffer[1] = (byte)((len >> 8) & 0xFF);
            buffer[2] = (byte)((len >> 16) & 0xFF);
            buffer[3] = (byte)((len >> 24) & 0xFF);

            buffer[0 + 4] = 0xDE;
            buffer[1 + 4] = 0xAD;
            buffer[2 + 4] = 0xBE;
            buffer[3 + 4] = 0xEF;
            buffer[buffer.Length - 4] = 0xDE;
            buffer[buffer.Length - 3] = 0xAD;
            buffer[buffer.Length - 2] = 0xBE;
            buffer[buffer.Length - 1] = 0xEF;

            PendingBuffer pb = new PendingBuffer();
            pb.Construct(buffer, maxPayloadSize);
            status.PendingBuffers.Add(pb);

            streamer.Tick(0.1);
            receiver.Tick(0.1);

            TestUtil.AssertByteArraysEqual(buffer, recvBytes);
        }

        [Test]
        public void Simple_TestBufferIsReceived_1()
        {
            int len = maxPayloadSize * 2 - 4;
            byte[] buffer = TestUtil.GenerateRandomByteArray(maxPayloadSize * 2);

            // write len of payload
            buffer[0] = (byte)(len & 0xFF);
            buffer[1] = (byte)((len >> 8) & 0xFF);
            buffer[2] = (byte)((len >> 16) & 0xFF);
            buffer[3] = (byte)((len >> 24) & 0xFF);

            buffer[0 + 4] = 0xDE;
            buffer[1 + 4] = 0xAD;
            buffer[2 + 4] = 0xBE;
            buffer[3 + 4] = 0xEF;
            buffer[buffer.Length - 4] = 0xDE;
            buffer[buffer.Length - 3] = 0xAD;
            buffer[buffer.Length - 2] = 0xBE;
            buffer[buffer.Length - 1] = 0xEF;

            PendingBuffer pb = new PendingBuffer();
            pb.Construct(buffer, maxPayloadSize);
            status.PendingBuffers.Add(pb);

            streamer.Tick(0.1);
            receiver.Tick(0.1);

            TestUtil.AssertByteArraysEqual(buffer, recvBytes);
        }

        public Message Create()
        {
            return Message.Create();
        }

        public ConnectionDataStreamStatus GetConnectionDSStatus()
        {
            return status;
        }

        public void Send(Message message)
        {
            receiver.HandleChunkReceived(message);
            message.Release();
        }
    }
}
