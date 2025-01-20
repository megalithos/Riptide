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
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },  10);
            Assert.AreEqual(1, buffer.NumTotalChunks());

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
        }

        [Test]
        public void ConstructWorks_2()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },  4);
            Assert.AreEqual(2, buffer.NumTotalChunks());

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
        }

        [Test]
        public void ConstructWorks_3()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },  4);
            Assert.AreEqual(3, buffer.NumTotalChunks());

            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(0));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(1));
            Assert.AreEqual(PendingChunkState.Waiting, buffer.GetChunkState(2));
        }

        [Test]
        public void SetChunkStateThrowsForInvalidIndex_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },  4);

            Assert.Throws<ArgumentOutOfRangeException>(() => { buffer.SetChunkState(2, PendingChunkState.OnFlight); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { buffer.SetChunkState(-1, PendingChunkState.OnFlight); });
        }

        [Test]
        public void SetChunkStateDoesNotAffectOtherChunkStates()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },  4);

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
            Assert.Throws<ArgumentNullException>(() => { buffer.Construct(null, 4); });
        }

        [Test]
        public void GetChunkStateThrowsArgumentOutOfRangeException_IfIndexIsOutOfRange()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },  10);

            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetChunkState(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetChunkState(1));
        }

        [Test]
        public void SeekNextWaitingIndexThrowsOnInvalidChunkIndex()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4 },  2);
            Assert.AreEqual(2, buffer.NumTotalChunks());
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SeekNextWaitingIndex(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.SeekNextWaitingIndex(2));
        }

        [Test]
        public void GetArraySliceWorks_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },  3);

            var arraySlice = buffer.GetBuffer(0);
            Assert.AreEqual(arraySlice.Length, 3);
            Assert.AreEqual(1, arraySlice[0]);
            Assert.AreEqual(2, arraySlice[1]);
            Assert.AreEqual(3, arraySlice[2]);

            arraySlice = buffer.GetBuffer(1);
            Assert.AreEqual(arraySlice.Length, 3);
            Assert.AreEqual(4, arraySlice[0]);
            Assert.AreEqual(5, arraySlice[1]);
            Assert.AreEqual(6, arraySlice[2]);

            arraySlice = buffer.GetBuffer(2);
            Assert.AreEqual(arraySlice.Length, 3);
            Assert.AreEqual(7, arraySlice[0]);
            Assert.AreEqual(8, arraySlice[1]);
            Assert.AreEqual(9, arraySlice[2]);

            arraySlice = buffer.GetBuffer(3);
            Assert.AreEqual(3, arraySlice.Length);
            Assert.AreEqual(10, arraySlice[0]);
            Assert.AreEqual(11, arraySlice[1]);
            Assert.AreEqual(12, arraySlice[2]);
        }

        [Test]
        public void IsDeliveredWorks_1()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4 },  2);
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

        [Test]
        public void Test_IsDeliveredWorks_2()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4 },  2);
            Assert.AreEqual(2, buffer.NumTotalChunks());
            Assert.AreEqual(false, buffer.IsDelivered());

            buffer.SetChunkState(0, PendingChunkState.Delivered);
            buffer.SetChunkState(1, PendingChunkState.Delivered);
            Assert.AreEqual(true, buffer.IsDelivered());

            buffer.SetChunkState(1, PendingChunkState.Waiting);
            Assert.AreEqual(false, buffer.IsDelivered());
        }

        [Test]
        public void GetLastChunkIndexWorks()
        {
            PendingBuffer buffer = NewBuffer();
            buffer.Construct(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },  3);
            Assert.AreEqual(4, buffer.NumTotalChunks());
            Assert.AreEqual(3, buffer.GetLastChunkIndex());
        }


        private PendingBuffer NewBuffer() => new PendingBuffer();
    }
}
