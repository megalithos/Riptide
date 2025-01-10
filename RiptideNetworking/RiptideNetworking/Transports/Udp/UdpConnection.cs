// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Collections;
using Riptide.DataStreaming;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Riptide.Transports.Udp
{
    /// <summary>Represents a connection to a <see cref="UdpServer"/> or <see cref="UdpClient"/>.</summary>
    public class UdpConnection : Connection, IEquatable<UdpConnection>
    {
        /// <summary>The endpoint representing the other end of the connection.</summary>
        public readonly IPEndPoint RemoteEndPoint;

        /// <summary>The local peer this connection is associated with.</summary>
        private readonly UdpPeer peer;

        // data stream fields / props
        private readonly ConnectionDataStreamStatus dataStreamStatus;
        private byte[] tmpWriteBuf;

        private const int maxPayloadSize = 1225;

        /// <summary>Initializes the connection.</summary>
        /// <param name="remoteEndPoint">The endpoint representing the other end of the connection.</param>
        /// <param name="peer">The local peer this connection is associated with.</param>
        internal UdpConnection(IPEndPoint remoteEndPoint, UdpPeer peer)
        {
            RemoteEndPoint = remoteEndPoint;
            this.peer = peer;
            dataStreamStatus = new ConnectionDataStreamStatus();

            tmpWriteBuf = new byte[maxPayloadSize];
        }

        private static SimpleArrayPool<byte> s_pool = new SimpleArrayPool<byte>(maxPayloadSize, 2000);

        private int acquiredArrayCount;

        private byte[] SafeAcquireArray()
        {
            acquiredArrayCount++;

            if (acquiredArrayCount > 10000)
            {
                RiptideLogger.Log(LogType.Error, "acquiredArrayCount > 10000");
            }

            return s_pool.Acquire();
        }

        private void SafeReleaseArray(byte[] array)
        {
            s_pool.Release(array);
            acquiredArrayCount--;
        }

        public handle_t BeginStream(int channel, byte[] buffer, int startIndex, int numBytes)
        {
            Stopwatch sw_second = new Stopwatch();
            Stopwatch sw_third = new Stopwatch();
            Stopwatch sw = Stopwatch.StartNew();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (startIndex < 0 || startIndex >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "Start index is out of range.");
            if (numBytes < 0 || startIndex + numBytes > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(numBytes), "Number of bytes exceeds buffer length.");

            int totalBytesRequired = numBytes;
            int numRequiredBuffers = MyMath.IntCeilDiv(totalBytesRequired, maxPayloadSize);

            RiptideLogger.Log(LogType.Debug, $"totalBytesRequired: {totalBytesRequired}");

            // struct BufferChunkState
            //   1 = waiting        waiting to be sent        
            //   2 = onFlight       sent but not acked
            //   3 = delivered      acked by client

            // interface PendingBuffer
            //     byte[] buffer;
            //     byte[] chunkStates;
            //     int totalChunks;
            //     int numDeliveredChunks;
            //     int numOnFlightChunks;
            //     int numWaitingChunks
            //
            //      void Construct(buffer, startIndex, numBytes, maxSize)
            //      BufferState GetStateFrom(int index) throw on invalid index, 
            //      void SetState(int index, BufferState state) throw on invalid index, throw if set again same value
            //
            //      ArraySlice<byte> GetBuffer(int index)
            //
            //      summary:
            //        return the index or -1 if no waiting index found
            //      int GetNextWaitingIndex(int seekStartIndex) 
            // 
            //      summary:
            //        true when the buffer is fully delivered
            //      bool IsDelivered();

            // waiting -> onFlight
            // onFlight -> waiting
            // onFlight -> delivered

            // struct envelope
            //   sequence
            //   size
            //   List<handle_t, int> bufferIndexByHandle

            // on send:
            //   envelope = (sequence, 0)
            //   for each handle, pendingBuffer in pendingBuffersByHandles
            //     if no space: continue
            //     index = pendingBuffer.GetNextWaitingIndex()
            //     if index < 0 continue
            //     len = Write(msg, pendingBuffer.GetBuffer(index))
            //     pendingBuffer.SetState(index, onFlight)
            //     envelope.size += len
            //     
            //   add envelope to sent envelopes dict

            // on envelope deliver:
            //   for each (handle_t, bufferIndex) in envelope:
            //     buffer = get buffer by handle_t
            //     buffer.SetState(bufferIndex, delivered)
            //     
            // 

            sw.Stop();

            RiptideLogger.Log(LogType.Debug, $"numRequiredBuffers: {numRequiredBuffers}. took: {((float)sw.Elapsed.TotalMilliseconds):F1} ms");
            RiptideLogger.Log(LogType.Debug, $"sw_second ms: {sw_second.Elapsed.TotalMilliseconds:F1}");
            RiptideLogger.Log(LogType.Debug, $"buffer resize op ms: {sw_third.Elapsed.TotalMilliseconds:F1}");

            return default(handle_t);
        }

        public void Tick(double dt)
        {
            // create message
            // Message.Create(MessageHeader.DataStreamChunk)

            // send message
            // increment bytes in flight
            // increment SequenceId

            // finally if time to increase cwnd, then increase it and reset timer
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
