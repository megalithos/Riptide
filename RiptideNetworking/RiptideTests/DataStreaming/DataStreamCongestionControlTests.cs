// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide.Collections;
using Riptide.DataStreaming;
using Riptide.Utils;
using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Configuration;

namespace RiptideTests.DataStreaming
{
    internal class DataStreamCongestionControlTests : IReceiverRTTProvider, IConnectionDSStatusProvider
    {
        private ConnectionDataStreamStatus status;
        private DataStreamer streamer;
        private DataReceiver receiver;
        private const int maxPayloadSize = 1200;
        private byte[] recvBytes;
        private DataStreamTestMessageSender receiver2streamer_sender;
        private DataStreamTestMessageSender streamer2receiver_sender;
        private DataStreamTestMessageCreator messageCreator;

        private List<OnFlightTestMessage> receiverList;
        private List<OnFlightTestMessage> streamerList;
        private double simulationTime;

        private const int initialSlowStartThreshold = 200_000;

        [SetUp]
        public void Setup()
        {
            simulationTime = 0.0;
            receiverList = new List<OnFlightTestMessage>();
            streamerList = new List<OnFlightTestMessage>();

            status = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, initialSlowStartThreshold);

            streamer2receiver_sender = new DataStreamTestMessageSender((Message message) =>
            {
                receiverList.Add(
                    new OnFlightTestMessage
                    {
                        message = message,
                        arrivalTime = simulationTime + get_rtt_ms() / 2 / 1000f
                    });
            });

            messageCreator = new DataStreamTestMessageCreator();

            streamer = new DataStreamer(this, messageCreator, streamer2receiver_sender, this, maxPayloadSize, Message.MaxHeaderSize + MyMath.IntCeilDiv(DataStreamer.numHeaderBits, 8));

            receiver2streamer_sender = new DataStreamTestMessageSender((Message message) =>
            {
                streamerList.Add(new OnFlightTestMessage
                {
                    message = message,
                    arrivalTime = simulationTime + get_rtt_ms() / 2 / 1000f
                });
            });
            receiver = new DataReceiver(maxPayloadSize, receiver2streamer_sender, messageCreator);

            recvBytes = null;
            receiver.OnReceived += (ArraySlice<byte> bytes) =>
            {
                recvBytes = new byte[bytes.Length];

                Buffer.BlockCopy(bytes.Array, bytes.StartIndex, recvBytes, 0, bytes.Length);
            };
        }

        private float[] frame_dts = new float[] { 0.013f, 0.004f, 0.050f, 0.020f, 0.010f, 0.002f };
        [Test]
        public void Test_SlowStartGrowsExponentially_AndEventuallyFullyReceived()
        {
            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = status.Cwnd;

            PendingBuffer pb = TestUtil.CreateBuffer((int)(initialSlowStartThreshold * 1.5f), maxPayloadSize, out byte[] testbuf);

            status.PendingBuffers.Add(pb);

            while (simulationTime <= testSimulationDuration)
            {
                // advance time
                float dt = frame_dts[dtIndex];
                simulationTime += dt;
                dtIndex = (dtIndex + 1) % frame_dts.Length;

                streamer.Tick(dt);
                receiver.Tick(dt);
                process_messages(receiverList, (Message m) => {
                    receiver.HandleChunkReceived(m);
                });
                process_messages(streamerList, (Message m) => {
                    streamer.HandleChunkAck(m);
                });

                if (status.Cwnd != prevCwnd && status.Cwnd <= initialSlowStartThreshold)
                {
                    Console.WriteLine("cwnd: " + status.Cwnd.ToString());
                    double change = (double)status.Cwnd / prevCwnd;
                    TestUtil.AssertDoublesEqualApprox(2.0, change, 0.01);

                    prevCwnd = status.Cwnd;
                }
            }

            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
        }

        private List<Message> removeList = new List<Message>();
        private void process_messages(List<OnFlightTestMessage> list, Action<Message> cb)
        {
            removeList.Clear();

            foreach (var msg in list)
            {
                if (simulationTime >= msg.arrivalTime)
                {
                    cb(msg.message);
                    removeList.Add(msg.message);
                }
            }

            foreach (var msg in removeList)
            {
                list.RemoveAll((OnFlightTestMessage m) => removeList.Contains(m.message));
            }
        }

        public int get_rtt_ms()
        {
            return 200;
        }

        public ConnectionDataStreamStatus GetConnectionDSStatus()
        {
            return status;
        }
    }
}
