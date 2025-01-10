// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiptideTests
{
    internal class PendingBufferTests
    {
        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void ConstructWorks_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 2, 4, 10);
            Assert.AreEqual(1, buffer.NumTotalChunks());

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
        }

        [Test]
        public void ConstructWorks_2()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0, 8, 4);
            Assert.AreEqual(2, buffer.NumTotalChunks());

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
        }

        [Test]
        public void ConstructWorks_3()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, 0, 9, 4);
            Assert.AreEqual(3, buffer.NumTotalChunks());

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));
        }

        [Test]
        public void SetChunkStateThrowsForInvalidIndex_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0, 8, 4);

            Assert.Throws<ArgumentOutOfRangeException>(() => { buffer.SetChunkState(2, PendingChunkState.OnFlight); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { buffer.SetChunkState(-1, PendingChunkState.OnFlight); });
        }

        [Test]
        public void SetChunkStateDoesNotAffectOtherChunkStates()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 0, 12, 4);

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));

            buffer.SetChunkState(0, PendingChunkState.OnFlight);
            Assert.AreEqual(PendingChunkState.OnFlight, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));

            buffer.SetChunkState(0, PendingChunkState.Waiting);
            buffer.SetChunkState(1, PendingChunkState.Waiting);
            buffer.SetChunkState(2, PendingChunkState.Delivered);
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Delivered, buffer.GetChunkState(2));

            buffer.SetChunkState(0, PendingChunkState.Waiting);
            buffer.SetChunkState(1, PendingChunkState.Waiting);
            buffer.SetChunkState(2, PendingChunkState.Waiting);
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));

            buffer.SetChunkState(1, PendingChunkState.Delivered);
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Delivered, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));
        }

        [Test]
        public void ConstructWorksThrowsWithInvalidArguments_1()
        {
            PendingBuffer buffer = NewBuffer();
            Assert.Throws<ArgumentNullException>(() => { buffer.Construct(null, 0, 8, 4); });
        }

        [Test]
        public void ConstructWorksThrowsWithInvalidArguments_2()
        {
            PendingBuffer buffer = NewBuffer();
            Assert.Throws<ArgumentException>(() => { buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0, 9, 4); });
        }

        [Test]
        public void ConstructWorksThrowsWithInvalidArguments_3()
        {
            PendingBuffer buffer = NewBuffer();
            Assert.Throws<ArgumentException>(() => { buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 1, 8, 4); });
        }

        [Test]
        public void ConstructWorksThrowsWithInvalidArguments_4()
        {
            PendingBuffer buffer = NewBuffer();
            Assert.Throws<ArgumentException>(() => { buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0, 0, 4); });
        }

        [Test]
        public void GetChunkStateThrowsArgumentOutOfRangeException_IfIndexIsOutOfRange()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 2, 4, 10);

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetChunkState(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetChunkState(1));
        }

        [Test]
        public void SeekNextWaitingIndexWorks_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 1, 10, 3);
            // buffer: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
            // chunk 1: [2, 3, 4]
            // chunk 2: [5, 6, 7]
            // chunk 3: [8, 9, 10]
            // chunk 4: [11]

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(3));

            Assert.AreEqual(0, buffer.SeekNextWaitingIndex(0));
            Assert.AreEqual(1, buffer.SeekNextWaitingIndex(1));
            Assert.AreEqual(2, buffer.SeekNextWaitingIndex(2));
            Assert.AreEqual(3, buffer.SeekNextWaitingIndex(3));

            buffer.SetChunkState(0, PendingChunkState.OnFlight);
            Assert.AreEqual(1, buffer.SeekNextWaitingIndex(0));
            buffer.SetChunkState(1, PendingChunkState.OnFlight);
            Assert.AreEqual(2, buffer.SeekNextWaitingIndex(0));

            buffer.SetChunkState(3, PendingChunkState.OnFlight);
            Assert.AreEqual(2, buffer.SeekNextWaitingIndex(2));

            buffer.SetChunkState(2, PendingChunkState.OnFlight);

            Assert.AreEqual(PendingChunkState.OnFlight, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.OnFlight, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.OnFlight, buffer.GetChunkState(2));
            Assert.AreEqual(PendingChunkState.OnFlight, buffer.GetChunkState(3));

            Assert.AreEqual(-1, buffer.SeekNextWaitingIndex(0));
            Assert.AreEqual(-1, buffer.SeekNextWaitingIndex(3));
        }

        [Test]
        public void SeekNextWaitingIndexThrowsOnInvalidChunkIndex()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4 }, 0, 4, 2);
            Assert.AreEqual(2, buffer.NumTotalChunks());
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SeekNextWaitingIndex(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SeekNextWaitingIndex(2));
        }

        [Test]
        public void GetArraySliceWorks_1()
        {
            PendingBuffer buffer = NewBuffer();
            // buffer: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
            // chunk 1: [2, 3, 4]
            // chunk 2: [5, 6, 7]
            // chunk 3: [8, 9, 10]
            // chunk 4: [11]
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 1, 10, 3);

            var arraySlice = buffer.GetBuffer(0);
            Assert.AreEqual(arraySlice.Length, 3);
            Assert.AreEqual(2, arraySlice[0]);
            Assert.AreEqual(3, arraySlice[1]);
            Assert.AreEqual(4, arraySlice[2]);

            arraySlice = buffer.GetBuffer(1);
            Assert.AreEqual(arraySlice.Length, 3);
            Assert.AreEqual(5, arraySlice[0]);
            Assert.AreEqual(6, arraySlice[1]);
            Assert.AreEqual(7, arraySlice[2]);

            arraySlice = buffer.GetBuffer(2);
            Assert.AreEqual(arraySlice.Length, 3);
            Assert.AreEqual(8, arraySlice[0]);
            Assert.AreEqual(9, arraySlice[1]);
            Assert.AreEqual(10, arraySlice[2]);

            arraySlice = buffer.GetBuffer(3);
            Assert.AreEqual(1, arraySlice.Length);
            Assert.AreEqual(11, arraySlice[0]);
        }

        [Test]
        public void IsDeliveredWorks_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4 }, 0, 4, 2);
            Assert.AreEqual(2, buffer.NumTotalChunks());
            Assert.AreEqual(false, buffer.IsDelivered());

            buffer.SetChunkState(0, PendingChunkState.OnFlight);
            buffer.SetChunkState(1, PendingChunkState.OnFlight);
            Assert.AreEqual(false, buffer.IsDelivered());

            buffer.SetChunkState(0, PendingChunkState.Delivered);
            buffer.SetChunkState(1, PendingChunkState.OnFlight);
            Assert.AreEqual(false, buffer.IsDelivered());

            buffer.SetChunkState(0, PendingChunkState.OnFlight);
            buffer.SetChunkState(1, PendingChunkState.Delivered);
            Assert.AreEqual(false, buffer.IsDelivered());

            buffer.SetChunkState(0, PendingChunkState.Delivered);
            buffer.SetChunkState(1, PendingChunkState.Delivered);
            Assert.AreEqual(true, buffer.IsDelivered());
        }

        private PendingBuffer NewBuffer() => new PendingBuffer();
    }
}
