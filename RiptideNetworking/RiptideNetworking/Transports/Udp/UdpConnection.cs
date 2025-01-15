// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using Riptide.DataStreaming;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;

namespace Riptide.Transports.Udp
{
    /// <summary>Represents a connection to a <see cref="UdpServer"/> or <see cref="UdpClient"/>.</summary>
    public class UdpConnection : Connection, IEquatable<UdpConnection>, IMessageSender, IConnectionDSStatusProvider, IReceiverRTTProvider
    {
        /// <summary>The endpoint representing the other end of the connection.</summary>
        public readonly IPEndPoint RemoteEndPoint;

        /// <summary>The local peer this connection is associated with.</summary>
        private readonly UdpPeer peer;

        private readonly ConnectionDataStreamStatus dataStreamStatus;

        private readonly DataStreamer dataStreamer;
        private readonly DataReceiver dataReceiver;

        private handle_t nextHandle = new handle_t(1);

        private readonly int maxPendingBufferSize =
                DataStreamSettings.c_maxPayloadSize - MyMath.IntCeilDiv(DataStreamer.totalFragmentHeaderBits + DataStreamer.totalSubheaderBits + 4, 8);

        /// <summary>Initializes the connection.</summary>
        /// <param name="remoteEndPoint">The endpoint representing the other end of the connection.</param>
        /// <param name="peer">The local peer this connection is associated with.</param>
        internal UdpConnection(IPEndPoint remoteEndPoint, UdpPeer peer)
        {
            RemoteEndPoint = remoteEndPoint;
            this.peer = peer;
            dataStreamStatus = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, DataStreamSettings.maxCwnd);
            dataStreamer = new DataStreamer(this, new StreamerMessageCreator(), this, this, maxPendingBufferSize, 4, 32);
            dataReceiver = new DataReceiver(maxPendingBufferSize, this, new ReceiverMessageCreator());

            dataStreamer.OnDelivered += (PendingBuffer pb) =>
            {
                this.OnStreamDelivered?.Invoke(pb.Handle);
            };

            dataReceiver.OnReceived += (ArraySlice<byte> slice) =>
            {
                byte[] buff = new byte[slice.Length - 4];
                Buffer.BlockCopy(slice.Array, slice.StartIndex + 4, buff, 0, slice.Length - 4);
                OnStreamReceived?.Invoke(buff);
            };
        }

        public handle_t BeginStream(int channel, byte[] buffer, int startIndex, int numBytes)
        {
            ValidateArgs(buffer, startIndex, numBytes);

            PendingBuffer pendingBuffer = new PendingBuffer();
            int len = buffer.Length;
            byte[] buffWithLength = new byte[len + 4];
            buffWithLength[0] = (byte)(len & 0xFF);
            buffWithLength[1] = (byte)(len >> 8 & 0xFF);
            buffWithLength[2] = (byte)(len >> 16 & 0xFF);
            buffWithLength[3] = (byte)(len >> 24 & 0xFF);
            Buffer.BlockCopy(buffer, 0, buffWithLength, 4, len);

            pendingBuffer.Construct(buffWithLength, maxPendingBufferSize);
            pendingBuffer.Handle = nextHandle;
            nextHandle++;

            dataStreamStatus.PendingBuffers.Add(pendingBuffer);

            return pendingBuffer.Handle;
        }

        private static void ValidateArgs(byte[] buffer, int startIndex, int numBytes)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (startIndex < 0 || startIndex >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index is out of range.");
            if (numBytes < 0 || startIndex + numBytes > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(numBytes), "Number of bytes exceeds buffer length.");
        }

        public void Tick(double dt)
        {
            dataStreamer.Tick(dt);
            dataReceiver.Tick(dt);
        }

        public void HandleDataStreamChunkReceived(Message message)
        {
            dataReceiver.HandleChunkReceived(message);
        }

        public void HandleDataStreamChunkAckReceived(Message message)
        {
            dataStreamer.HandleChunkAck(message);
        }

        public event Action<handle_t> OnStreamDelivered;

        public event Action<byte[]> OnStreamReceived;

        /// <inheritdoc/>
        protected internal override void Send(byte[] dataBuffer, int amount)
        {
            peer.Send(dataBuffer, amount, RemoteEndPoint);
        }

        /// <inheritdoc/>
        public override string ToString() => RemoteEndPoint.ToStringBasedOnIPFormat();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as UdpConnection);
        /// <inheritdoc/>
        public bool Equals(UdpConnection other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return RemoteEndPoint.Equals(other.RemoteEndPoint);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return -288961498 + EqualityComparer<IPEndPoint>.Default.GetHashCode(RemoteEndPoint);
        }

        public void Send(Message message)
        {
            base.Send(message);
        }

        ConnectionDataStreamStatus IConnectionDSStatusProvider.GetConnectionDSStatus()
        {
            return dataStreamStatus;
        }

        public int get_rtt_ms()
        {
            return SmoothRTT;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator ==(UdpConnection left, UdpConnection right)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (left is null)
            {
                if (right is null)
                    return true;

                return false; // Only the left side is null
            }

            // Equals handles case of null on right side
            return left.Equals(right);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator !=(UdpConnection left, UdpConnection right) => !(left == right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
