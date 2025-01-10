// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide.Collections;

namespace RiptideTests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GrantsArray()
        {
            SimpleArrayPool<byte> pool = new SimpleArrayPool<byte>(1024, 1);
            byte[] arr = pool.Acquire();
            Assert.IsTrue(arr != null);
            Assert.IsTrue(arr.Length == 1024);
        }

        [Test]
        public void IfCapacityIsOne_ArrayAcquired_ThenReleased_ThenAcquired_ItIsSameArrayReference()
        {
            SimpleArrayPool<byte> pool = new SimpleArrayPool<byte>(1024, 1);
            byte[] arr = pool.Acquire();
            pool.Release(arr);

            byte[] arr2 = pool.Acquire();
            Assert.AreEqual(arr2, arr);
        }

        [Test]
        public void CapacityIsCorrect()
        {
            SimpleArrayPool<byte> pool = new SimpleArrayPool<byte>(1024, 10);
            Assert.IsTrue(pool.Capacity() == 10);
        }

        [Test]
        public void CapacityDoublesIfAcquiredEnough_1()
        {
            SimpleArrayPool<byte> pool = new SimpleArrayPool<byte>(1024, 1);
            byte[] arr = pool.Acquire();
            Assert.AreEqual(1, pool.Capacity());
            arr = pool.Acquire();
            Assert.AreEqual(2, pool.Capacity());
            arr = pool.Acquire();
            Assert.AreEqual(4, pool.Capacity());
            arr = pool.Acquire();
            Assert.AreEqual(4, pool.Capacity());
            arr = pool.Acquire();
            Assert.AreEqual(8, pool.Capacity());
        }


        [Test]
        public void DoesNotUnnecessarilyIncreaseCapacity()
        {
            SimpleArrayPool<byte> pool = new SimpleArrayPool<byte>(1024, 4);
            var arr = pool.Acquire();
            var arr2 = pool.Acquire();
            var arr3 = pool.Acquire();
            var arr4 = pool.Acquire();
            Assert.AreEqual(4, pool.Capacity());
            pool.Release(arr);
            pool.Release(arr2);
            Assert.AreEqual(4, pool.Capacity());
            pool.Acquire();
            pool.Acquire();
            Assert.AreEqual(4, pool.Capacity());
        }

        [Test]
        public void DoesNotUnnecessarilyIncreaseCapacity2_AndRefsStaySame()
        {
            SimpleArrayPool<byte> pool = new SimpleArrayPool<byte>(1024, 4);
            var arr1 = pool.Acquire();
            var arr2 = pool.Acquire();
            var arr3 = pool.Acquire();
            var arr4 = pool.Acquire();
            Assert.AreEqual(4, pool.Capacity());
            pool.Release(arr1);
            pool.Release(arr2);
            pool.Release(arr3);
            pool.Release(arr4);
            Assert.AreEqual(4, pool.Capacity());
            var arr11 = pool.Acquire();
            var arr21 = pool.Acquire();
            var arr31 = pool.Acquire();
            var arr41 = pool.Acquire();
            Assert.AreEqual(arr1, arr11);
            Assert.AreEqual(arr2, arr21);
            Assert.AreEqual(arr3, arr31);
            Assert.AreEqual(arr4, arr41);
            Assert.AreEqual(4, pool.Capacity());
        }
    }
}
