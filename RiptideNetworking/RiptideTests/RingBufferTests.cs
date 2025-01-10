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
    internal class RingBufferTests
    {
        [SetUp]
        public void SetUp() 
        {
        
        }

        [Test]
        public void ResizeOpWorks()
        {
            RingBuffer<int> buff = new RingBuffer<int>(4);
            buff.Push(2);
            buff.Push(5);
            buff.Push(1);
            buff.Push(13);

            buff.Resize(8);
            Assert.AreEqual(8, buff.Capacity);
            Assert.AreEqual(4, buff.Count);

            Assert.AreEqual(2, buff.Pop());
            Assert.AreEqual(5, buff.Pop());
            Assert.AreEqual(1, buff.Pop());
            Assert.AreEqual(13, buff.Pop());
            Assert.AreEqual(0, buff.Count);
        }

        [Test]
        public void TestWhenArrayRestartsFromTheBeginningInMiddle()
        {
            RingBuffer<int> buff = new RingBuffer<int>(4);
            buff.Push(1);
            buff.Push(2);
            buff.Push(3);
            buff.Push(4);

            buff.Pop();
            buff.Pop();

            // here it should look like [?, ?, 3, 4]

            buff.Push(5);
            buff.Push(6);

            // here it should look like [5, 6, 3, 4]

            Assert.AreEqual(4, buff.Count);
            buff.Resize(8);
            Assert.AreEqual(4, buff.Count);

            Assert.AreEqual(3, buff.Pop());
            Assert.AreEqual(4, buff.Pop());
            Assert.AreEqual(5, buff.Pop());
            Assert.AreEqual(6, buff.Pop());

            Assert.AreEqual(0, buff.Count);
        }
    }
}
