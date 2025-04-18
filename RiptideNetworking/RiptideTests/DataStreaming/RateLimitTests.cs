// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide.Collections;
using Riptide.DataStreaming;
using Riptide.Utils;
using System;

namespace RiptideTests.DataStreaming
{
    internal class RateLimitTests : IConnectionDSStatusProvider, IReceiverRTTProvider, IRateLimitSettings, ITimeProvider
    {
        private ConnectionDataStreamStatus status;
        private DataStreamer streamer;
        private DataReceiver receiver;
        private const int maxPayloadSize = 1200;
        private byte[] recvBytes;
        private DataStreamTestMessageSender receiver2streamer_sender;
        private DataStreamTestMessageSender streamer2receiver_sender;
        private DataStreamTestMessageCreator messageCreator;

        public TimeSpan DataLimitWindow { get; set; }

        public ByteAmount MaxAmountPerWindow { get; set; }

        private DateTime fake_time;

        [SetUp]
        public void Setup()
        {
        }

        private void init()
        {
            status = new ConnectionDataStreamStatus(10_000_000, 10_000_000);

            streamer2receiver_sender = new DataStreamTestMessageSender((message) =>
            {
                receiver.HandleChunkReceived(message);
                message.Release();
            });

            messageCreator = new DataStreamTestMessageCreator();

            streamer = new DataStreamer(this, messageCreator, streamer2receiver_sender, this, this, this, maxPayloadSize, 4);

            receiver2streamer_sender = new DataStreamTestMessageSender((message) =>
            {
                streamer.HandleChunkAck(message);
                message.Release();
            });
            receiver = new DataReceiver(maxPayloadSize, receiver2streamer_sender, messageCreator);

            recvBytes = null;
            receiver.OnReceived += (handle, bytes) =>
            {
                recvBytes = new byte[bytes.Length];

                Buffer.BlockCopy(bytes.Array, bytes.StartIndex, recvBytes, 0, bytes.Length);
            };
        }

        [Test]
        public void Test_CanStreamReturnsFalse_IfRateLimitExceeded()
        {
            fake_time = DateTime.UtcNow;
            init();
            // --- configure ---
            DataLimitWindow = TimeSpan.FromMinutes(1);
            MaxAmountPerWindow = ByteAmount.FromMegabytes(1);

            // --- test ---
            // should go through, send only 0.9 MB
            int buf_size = 900_000;
            PendingBuffer pendingBuffer = TestUtil.CreateBuffer(buf_size, maxPayloadSize, out byte[] buffer);

            Assert.AreEqual(true, streamer.CanStream(new ByteAmount(buf_size)), "expected to be able to stream since limit not hit");
            pendingBuffer.Handle = 1;
            streamer.Tick(0.1);
            receiver.Tick(0.1);
            streamer.QueueBufferForSend(pendingBuffer);

            fake_time += TimeSpan.FromSeconds(30);

            // should fail, over limit
            buf_size = 200_000;
            pendingBuffer = TestUtil.CreateBuffer(buf_size, maxPayloadSize, out buffer);
            Assert.AreEqual(false, streamer.CanStream(new ByteAmount(buf_size)), "expected to fail because over limit");
        }

        [Test]
        public void Test_CanStreamReturnsTrue_IfRateLimitExceededButTimeElapsedEnoughForTimeWindowToReset()
        {
            fake_time = DateTime.UtcNow;
            init();
            // --- configure ---
            DataLimitWindow = TimeSpan.FromMinutes(1);
            MaxAmountPerWindow = ByteAmount.FromMegabytes(1);

            // --- test ---
            // should go through, send only 0.9 MB
            int buf_size = 900_000;
            PendingBuffer pendingBuffer = TestUtil.CreateBuffer(buf_size, maxPayloadSize, out byte[] buffer);

            Assert.AreEqual(true, streamer.CanStream(new ByteAmount(buf_size)), "expected to be able to stream since limit not hit");
            pendingBuffer.Handle = 1;
            streamer.Tick(0.1);
            receiver.Tick(0.1);
            streamer.QueueBufferForSend(pendingBuffer);

            fake_time += TimeSpan.FromSeconds(61);

            // should fail, over limit
            buf_size = 900_000;
            pendingBuffer = TestUtil.CreateBuffer(buf_size, maxPayloadSize, out buffer);
            Assert.AreEqual(true, streamer.CanStream(new ByteAmount(buf_size)), "expected to be able to stream because time window should've reset");
        }

        public ConnectionDataStreamStatus GetConnectionDSStatus()
        {
            return status;
        }

        public int get_rtt_ms()
        {
            return 100;
        }

        public DateTime GetNowTime()
        {
            return fake_time;
        }
    }
}

