// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using Riptide.DataStreaming;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Udp
{
    /// <summary>Represents a connection to a <see cref="UdpServer"/> or <see cref="UdpClient"/>.</summary>
    public class UdpConnection : Connection, IEquatable<UdpConnection>, IMessageCreator, IMessageSender, IConnectionDSStatusProvider, IReceiverRTTProvider
    {
        /// <summary>The endpoint representing the other end of the connection.</summary>
        public readonly IPEndPoint RemoteEndPoint;

        /// <summary>The local peer this connection is associated with.</summary>
        private readonly UdpPeer peer;

        private readonly ConnectionDataStreamStatus dataStreamStatus;

        private readonly DataStreamer dataStreamer;

        private handle_t nextHandle = new handle_t(1);

        // TODO: wastes couple bytes I think, since riptide message's max payload
        // size includes notify header bits but we are using our custom header.
        // At time of writing, this is faster to get working by just using riptide message's
        // write operations (instead of using custom bit stream). Since we use 
        // riptide message's write ops we must conform to it's limitations.
        /// <summary>
        /// Maximum amount of bytes we can add to a message.
        /// </summary>
        private readonly int maxPayloadBits = DataStreamSettings.c_maxPayloadSize * 8 - DataStreamer.numHeaderBits - 4; // -4 for unreliable header
        private readonly int maxHeaderSize = MyMath.IntCeilDiv(4 + DataStreamer.numHeaderBits, 8); // 4 for unreliable header

        /// <summary>Initializes the connection.</summary>
        /// <param name="remoteEndPoint">The endpoint representing the other end of the connection.</param>
        /// <param name="peer">The local peer this connection is associated with.</param>
        internal UdpConnection(IPEndPoint remoteEndPoint, UdpPeer peer)
        {
            RemoteEndPoint = remoteEndPoint;
            this.peer = peer;
            dataStreamStatus = new ConnectionDataStreamStatus(DataStreamSettings.initialCwndSize, DataStreamSettings.maxCwnd);
            dataStreamer = new DataStreamer(this, this, this, this, maxPayloadBits / 8, maxHeaderSize);
        }

        public handle_t BeginStream(int channel, byte[] buffer, int startIndex, int numBytes)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (startIndex < 0 || startIndex >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index is out of range.");
            if (numBytes < 0 || startIndex + numBytes > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(numBytes), "Number of bytes exceeds buffer length.");

            PendingBuffer pendingBuffer = new PendingBuffer();
            pendingBuffer.Construct(buffer, maxPayloadBits);
            pendingBuffer.Handle = nextHandle;
            nextHandle++;

            dataStreamStatus.PendingBuffers.Add(pendingBuffer);

            return pendingBuffer.Handle;
        }

        public void Tick(double dt)
        {
            dataStreamer.Tick(dt);
        }

        public void HandleDataStreamChunkReceived(Message message)
        {
        }

        public void HandleDataStreamChunkAckReceived(Message message)
        {

        }

        public event Action<handle_t> OnStreamDelivered;

        public event Action<handle_t> OnStreamReceived;

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

        public Message Create()
        {
            return Message.Create(MessageSendMode.DataStream);
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
