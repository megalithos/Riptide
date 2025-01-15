// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide;
using Riptide.Collections;
using Riptide.DataStreaming;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace RiptideTests.DataStreaming
{
    // todo:
    //   - test param ssthresh
    internal class DataStreamCongestionControlTests : IReceiverRTTProvider, IConnectionDSStatusProvider
    {
        private ConnectionDataStreamStatus status;
        private DataStreamer streamer;
        private DataReceiver receiver;
        private readonly int maxPendingBufferBufferSize =
            DataStreamSettings.c_maxPayloadSize - MyMath.IntCeilDiv(DataStreamer.totalFragmentHeaderBits + DataStreamer.totalSubheaderBits + 4, 8);
        private byte[] recvBytes;
        private DataStreamTestMessageSender receiver2streamer_sender;
        private DataStreamTestMessageSender streamer2receiver_sender;
        private DataStreamTestMessageCreator messageCreator;

        private List<OnFlightTestMessage> receiverList;
        private List<OnFlightTestMessage> streamerList;
        private double simulationTime;

        private const int initialSlowStartThreshold = 500_000;
        private int measured_cwnd;
        private PendingBuffer deliveredPb;
        private bool dropSinglePacket = false;

        // -1: no drop
        // 0: 10%
        // 1: 20%
        // 2: 30%
        // 3: 40%
        // 4: 50%
        // 9: 100%
        private int dropChance = -1;

        [SetUp]
        public void Setup()
        {
            rtt = 200;
            measured_cwnd = 0;
            simulationTime = 0.0;
            receiverList = new List<OnFlightTestMessage>();
            streamerList = new List<OnFlightTestMessage>();

            status = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, initialSlowStartThreshold);

            streamer2receiver_sender = new DataStreamTestMessageSender((Message message) =>
            {
                var msg = new OnFlightTestMessage
                {
                    message = message,
                    arrivalTime = simulationTime + get_rtt_ms() / 2 / 1000f,
                    size = message.BytesInUse,
                };
                if (dropSinglePacket)
                {
                    dropSinglePacket = false;
                }
                else if (dropChance >= 0 && prng.Next(10) <= dropChance)
                {
                }
                else
                {
                    receiverList.Add(
                        msg
                    );
                }

                measured_cwnd += msg.size;
            });

            messageCreator = new DataStreamTestMessageCreator();

            streamer = new DataStreamer(this, messageCreator, streamer2receiver_sender, this, maxPendingBufferBufferSize, 4);

            receiver2streamer_sender = new DataStreamTestMessageSender((Message message) =>
            {
                streamerList.Add(new OnFlightTestMessage
                {
                    message = message,
                    arrivalTime = simulationTime + get_rtt_ms() / 2 / 1000f,
                    size = MyMath.IntCeilDiv(message.WrittenBits, 8)
                });
            });
            receiver = new DataReceiver(maxPendingBufferBufferSize, receiver2streamer_sender, messageCreator);

            recvBytes = null;
            receiver.OnReceived += (ArraySlice<byte> bytes) =>
            {
                recvBytes = new byte[bytes.Length];

                Buffer.BlockCopy(bytes.Array, bytes.StartIndex, recvBytes, 0, bytes.Length);
            };

            deliveredPb = null;
            streamer.OnDelivered += (PendingBuffer pb) =>
            {
                deliveredPb = pb;
            };

            dropChance = -1;
            prng = null;
        }

        private float[] frame_dts = new float[] { 0.025f };
        [Test]
        public void Test_SlowStartGrowsExponentially_AndEventuallyFullyReceived()
        {
            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = 1229;

            PendingBuffer pb = TestUtil.CreateBuffer((int)(initialSlowStartThreshold * 1.5f), maxPendingBufferBufferSize, out byte[] testbuf);

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

                if (measured_cwnd > prevCwnd && measured_cwnd <= initialSlowStartThreshold)
                {
                    double change = (double)measured_cwnd / prevCwnd;
                    Console.WriteLine("cwnd: " + measured_cwnd.ToString() + $" change: {change:F3}x");
                    TestUtil.AssertDoublesEqualApprox(2.0, change, 0.1);

                    prevCwnd = measured_cwnd;
                }

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
        }

        [Test]
        public void Test_HugeBufferStreamDoesNotExceedMaxCwnd_AndCwndOnlyGrows()
        {
            streamer = new DataStreamer(this, messageCreator, streamer2receiver_sender, this, maxPendingBufferBufferSize, 4, 32);
            streamer.OnDelivered += (PendingBuffer pendingBuffer) =>
            {
                deliveredPb = pendingBuffer;
            };

            double testSimulationDuration = 30.0;

            int dtIndex = 0;

            int ssthresh = 10_000_000;
            status = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, ssthresh);

            PendingBuffer pb = TestUtil.CreateBuffer((int)(ssthresh * 1.5f), maxPendingBufferBufferSize, out byte[] testbuf);

            status.PendingBuffers.Add(pb);
            long prevCwnd = status.Cwnd;
            int total = 0;

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

                if (measured_cwnd > DataStreamSettings.maxCwnd)
                    Assert.Fail($"should not exceed maxCwnd. measured_cwnd: {new ByteAmount(measured_cwnd)}, max cwnd: {new ByteAmount(DataStreamSettings.maxCwnd)}");

                var tickstat = streamer.GetLastTickStat();
                if (tickstat.countSentFullBuffers > 0)
                {
                    total += tickstat.countSentFullBuffers;
                    Console.WriteLine($"simulationTime: {simulationTime:F3}, sent buffers: {tickstat.countSentFullBuffers}, cwnd: {status.Cwnd}, total sent: {total}");
                }

                if (status.Cwnd != prevCwnd)
                {
                    Assert.IsTrue(status.Cwnd > prevCwnd, "cwnd should only increase in size");

                    prevCwnd = status.Cwnd;
                }

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
        }


        private TestUtil.TestPRNG prng;
        [Test]
        public void Test_DeliveredWithLoss()
        {
            prng = new TestUtil.TestPRNG(1337);
            dropChance = 4; // 50%
            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = 1229;

            PendingBuffer pb = TestUtil.CreateBuffer(2_371_224, maxPendingBufferBufferSize, out byte[] testbuf);
            byte[] originalBufferHash = TestUtil.HashBytes(testbuf);

            status.PendingBuffers.Add(pb);
            int iteration = -1;

            while (simulationTime <= testSimulationDuration)
            {
                iteration++;
                // advance time
                float dt = frame_dts[dtIndex];
                simulationTime += dt;
                dtIndex = (dtIndex + 1) % frame_dts.Length;

                streamer.Tick(dt);

                var tickstat = streamer.GetLastTickStat();
                if (tickstat.countSentFullBuffers > 0)
                {
                    Console.WriteLine($"simulationTime: {simulationTime:F3}, sentFullBuffers: {tickstat.countSentFullBuffers}, iteration: #{iteration}");
                }

                receiver.Tick(dt);
                process_messages(receiverList, (Message m) => {
                    receiver.HandleChunkReceived(m);
                });
                process_messages(streamerList, (Message m) => {
                    streamer.HandleChunkAck(m);
                });

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
            byte[] actualHash = TestUtil.HashBytes(recvBytes);
            TestUtil.AssertByteArraysEqual(originalBufferHash, actualHash);
        }

        [Test]
        public void Test_DeliveredWithLowPing()
        {
            rtt = (int)Math.Round(frame_dts[0] * 1000f);
            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = 1229;

            PendingBuffer pb = TestUtil.CreateBuffer(2_371_224, maxPendingBufferBufferSize, out byte[] testbuf);
            byte[] originalBufferHash = TestUtil.HashBytes(testbuf);

            status.PendingBuffers.Add(pb);
            int iteration = -1;

            while (simulationTime <= testSimulationDuration)
            {
                iteration++;
                // advance time
                float dt = frame_dts[dtIndex];
                simulationTime += dt;
                dtIndex = (dtIndex + 1) % frame_dts.Length;

                streamer.Tick(dt);

                var tickstat = streamer.GetLastTickStat();
                if (tickstat.countSentFullBuffers > 0)
                {
                    Console.WriteLine($"simulationTime: {simulationTime:F3}, sentFullBuffers: {tickstat.countSentFullBuffers}, iteration: #{iteration}");
                }

                receiver.Tick(dt);
                process_messages(receiverList, (Message m) => {
                    receiver.HandleChunkReceived(m);
                });
                process_messages(streamerList, (Message m) => {
                    streamer.HandleChunkAck(m);
                });

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
            byte[] actualHash = TestUtil.HashBytes(recvBytes);
            TestUtil.AssertByteArraysEqual(originalBufferHash, actualHash);
        }

        [Test]
        public void Test_SlowStartToCongestionAvoidanceTransition_AndEventuallyDelivered()
        {
            int ssthresh = 50_000;
            status = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, 50_000);

            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = DataStreamSettings.initialCwndSize;

            PendingBuffer pb = TestUtil.CreateBuffer((int)(initialSlowStartThreshold * 3f), maxPendingBufferBufferSize, out byte[] testbuf);

            status.PendingBuffers.Add(pb);

            double prevTime = 0;

            int congestionAvoidanceCount = 0;
            int iteration = -1;

            while (simulationTime <= testSimulationDuration)
            {
                iteration++;
                // advance time
                float dt = frame_dts[dtIndex];
                simulationTime += dt;
                dtIndex = (dtIndex + 1) % frame_dts.Length;

                streamer.Tick(dt);
                receiver.Tick(dt);
                process_messages(receiverList, (Message m) => {
                    receiver.HandleChunkReceived(m);
                });

                streamer.ResetLastAckStat();
                process_messages(streamerList, (Message m) => {
                    streamer.HandleChunkAck(m);
                });
                DataStreamer.ackstat_t ackstat = streamer.GetLastAckStat();
                if (ackstat.acksReceived > 0)
                {
                    // Console.WriteLine($"ackstat.acksReceived: {ackstat.acksReceived}");
                }

                // slow start
                long cwnd = status.Cwnd;

                Assert.That(measured_cwnd <= cwnd, "no more bytes on wire than cwnd");

                if (cwnd > prevCwnd)
                {
                    Console.WriteLine("====================================");
                    Console.WriteLine("cwnd: " + cwnd.ToString());
                    if (cwnd <= ssthresh)
                    {
                        double change = (double)cwnd / prevCwnd;
                        Console.WriteLine("(slow start) change: {0}x", change);
                        TestUtil.AssertDoublesEqualApprox(2.0, change, 0.1);
                    }
                    else
                    {
                        // past SSThresh = we should be in congestion avoidance 

                        if (congestionAvoidanceCount > 0) // skip first
                        {
                            long change = cwnd - prevCwnd;
                            double timeChange = simulationTime - prevTime;

                            DataStreamer.tickstat_t tickstat = streamer.GetLastTickStat();

//                            Console.WriteLine("time: {0:F3} s, timeChange: {1} ms, change: {2}, iteration: #{3}, connectionCwnd: {4}",
//                                simulationTime, (int)Math.Round(timeChange * 1000), change, iteration, status.Cwnd);

                            TestUtil.AssertIntsEqualApprox(DataStreamSettings.c_maxPayloadSize, (int)change, 2);
                            TestUtil.AssertDoublesEqualApprox(get_rtt_ms(), timeChange * 1000f, 0.1);
                        }

                        prevTime = simulationTime;
                        congestionAvoidanceCount++;
                    }

                    prevCwnd = cwnd;
                }

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
            Assert.AreEqual(pb, deliveredPb, "event should be invoked");
        }

        [Test]
        public void Test_SS_To_CA_To_SS_Works()
        {
            int ssthresh = 50_000;
            status = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, 50_000);

            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = DataStreamSettings.initialCwndSize;

            PendingBuffer pb = TestUtil.CreateBuffer((int)(initialSlowStartThreshold * 3f), maxPendingBufferBufferSize, out byte[] testbuf);

            status.PendingBuffers.Add(pb);

            double prevTime = 0;

            int congestionAvoidanceCount = 0;
            int iteration = -1;

            bool didDrop = false;

            const int dropThreshold = 75_000;

            double dropTime = 0.0;

            bool cantest = true;
            while (simulationTime <= testSimulationDuration)
            {
                iteration++;
                // advance time
                float dt = frame_dts[dtIndex];
                simulationTime += dt;
                dtIndex = (dtIndex + 1) % frame_dts.Length;

                var tickstat = streamer.GetLastTickStat();
                streamer.Tick(dt);
                if (tickstat.countSentFullBuffers > 0 || tickstat.countSentPartialMessages > 0)
                {
                    Console.WriteLine($"time: {simulationTime:F3}, sent full buffers: {tickstat.countSentFullBuffers}, sent partial messages: {tickstat.countSentPartialMessages}");
                }
                receiver.Tick(dt);
                process_messages(receiverList, (Message m) => {
                    receiver.HandleChunkReceived(m);
                });

                streamer.ResetLastAckStat();
                process_messages(streamerList, (Message m) => {
                    streamer.HandleChunkAck(m);
                });

                // slow start
                long cwnd = status.Cwnd;

                if (cwnd > dropThreshold && !didDrop)
                {
                    dropSinglePacket = true;
                    Console.WriteLine($"time: {simulationTime:F3} WILL DROP single packet...");
                    didDrop = true;
                    dropTime = simulationTime;
                    cantest = false;
                }

                if (cwnd != prevCwnd)
                {
                    if (cwnd <= 1300 && didDrop)
                    {
                        double elapsed = simulationTime - dropTime;
                        TestUtil.AssertDoublesEqualApprox(get_rtt_ms() / 1000f, elapsed, 0.1);

                        ssthresh = (int)dropThreshold / 2;
                        congestionAvoidanceCount = 0;
                        cantest = true;
                        prevCwnd = cwnd / 2; // hacky
                    }

                    Console.WriteLine("====================================");
                    Console.WriteLine($"time: {simulationTime:F3} s , cwnd: " + cwnd.ToString() + ", measured cwnd: " + measured_cwnd.ToString());

                    if (cantest)
                    {
                        if (cwnd <= ssthresh)
                        {
                            double change = (double)cwnd / prevCwnd;
                            Console.WriteLine("(slow start) change: {0}x", change);
                            TestUtil.AssertDoublesEqualApprox(2.0, change, 0.1);
                        }
                        else
                        {
                            // past SSThresh = we should be in congestion avoidance 

                            if (congestionAvoidanceCount > 0) // skip first
                            {
                                long change = cwnd - prevCwnd;
                                double timeChange = simulationTime - prevTime;

                                TestUtil.AssertIntsEqualApprox(DataStreamSettings.c_maxPayloadSize, (int)change, 2);
                                TestUtil.AssertDoublesEqualApprox(get_rtt_ms(), timeChange * 1000f, 0.1);
                                Console.WriteLine("congestion avoidance test OK");
                            }

                            prevTime = simulationTime;
                            congestionAvoidanceCount++;
                        }
                    }

                    prevCwnd = cwnd;
                }

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            Assert.IsTrue(didDrop, "didDrop");
            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
            Assert.AreEqual(pb, deliveredPb, "event should be invoked");
        }

        [Test]
        public void Test_SS_To_SS_Works()
        {
            int ssthresh = 100_000;
            status = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, 100_000);

            double testSimulationDuration = 30.0;

            int dtIndex = 0;
            long prevCwnd = DataStreamSettings.initialCwndSize;

            PendingBuffer pb = TestUtil.CreateBuffer((int)(initialSlowStartThreshold * 3f), maxPendingBufferBufferSize, out byte[] testbuf);

            status.PendingBuffers.Add(pb);

            double prevTime = 0;

            int congestionAvoidanceCount = 0;
            int iteration = -1;

            bool didDrop = false;

            const int dropThreshold = 75_000;

            double dropTime = 0.0;

            bool cantest = true;
            bool dropDetected = false;
            while (simulationTime <= testSimulationDuration)
            {
                iteration++;
                // advance time
                float dt = frame_dts[dtIndex];
                simulationTime += dt;
                dtIndex = (dtIndex + 1) % frame_dts.Length;

                var tickstat = streamer.GetLastTickStat();
                streamer.Tick(dt);
                if (tickstat.countSentFullBuffers > 0 || tickstat.countSentPartialMessages > 0)
                {
                    Console.WriteLine($"time: {simulationTime:F3}, sent full buffers: {tickstat.countSentFullBuffers}, sent partial messages: {tickstat.countSentPartialMessages}");
                }
                receiver.Tick(dt);
                process_messages(receiverList, (Message m) => {
                    receiver.HandleChunkReceived(m);
                });

                streamer.ResetLastAckStat();
                process_messages(streamerList, (Message m) => {
                    streamer.HandleChunkAck(m);
                });

                // slow start
                long cwnd = status.Cwnd;

                if (cwnd > dropThreshold && !didDrop)
                {
                    dropSinglePacket = true;
                    Console.WriteLine($"time: {simulationTime:F3} WILL DROP single packet... cwnd: {cwnd}");
                    didDrop = true;
                    dropTime = simulationTime;
                    cantest = false;
                }

                if (cwnd != prevCwnd)
                {
                    if (cwnd <= 1300 && didDrop)
                    {
                        double elapsed = simulationTime - dropTime;
                        TestUtil.AssertDoublesEqualApprox(get_rtt_ms() / 1000f, elapsed, 0.1);

                        ssthresh = (int)dropThreshold / 2;
                        congestionAvoidanceCount = 0;
                        cantest = true;
                        prevCwnd = cwnd / 2; // hacky
                        dropDetected = true;
                    }

                    Console.WriteLine("====================================");
                    Console.WriteLine($"time: {simulationTime:F3} s , cwnd: " + cwnd.ToString() + ", measured cwnd: " + measured_cwnd.ToString());

                    if (cantest)
                    {
                        if (cwnd <= ssthresh)
                        {
                            double change = (double)cwnd / prevCwnd;
                            Console.WriteLine("(slow start) change: {0}x", change);
                            TestUtil.AssertDoublesEqualApprox(2.0, change, 0.1);
                        }
                    }

                    prevCwnd = cwnd;
                }

                foreach (var m in receiverList)
                {
                    if (!m.remove) continue;

                    measured_cwnd -= m.size;
                }

                receiverList.RemoveAll(m => m.remove);
                streamerList.RemoveAll(m => m.remove);
            }

            Assert.True(dropDetected, "dropDetected");
            Assert.IsTrue(didDrop, "didDrop");
            TestUtil.AssertByteArraysEqual(testbuf, recvBytes);
            Assert.AreEqual(pb, deliveredPb, "event should be invoked");
        }

        private void process_messages(List<OnFlightTestMessage> list, Action<Message> cb)
        {
            DataStreamer.debugValue = 0;
            for (int i = 0; i < list.Count; i++)
            {
                OnFlightTestMessage msg = list[i];
                if (simulationTime >= msg.arrivalTime)
                {
                    cb(msg.message);
                    msg.remove = true;
                    list[i] = msg;
                }
            }
        }

        private int rtt;
        public int get_rtt_ms()
        {
            return rtt;
        }

        public ConnectionDataStreamStatus GetConnectionDSStatus()
        {
            return status;
        }
    }
}
