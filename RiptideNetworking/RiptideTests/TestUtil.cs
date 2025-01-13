// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Riptide.Collections;
using Riptide.Utils;
using Assert = NUnit.Framework.Assert;

namespace RiptideTests
{
    internal static class TestUtil
    {
        public static byte[] GenerateRandomByteArray(int length)
        {
            if (length <= 0)
                throw new ArgumentException("Length must be greater than zero.", nameof(length));

            byte[] byteArray = new byte[length];
            Random random = new Random();

            random.NextBytes(byteArray);

            return byteArray;
        }

        public static void AssertByteArraysEqual(byte[] expected, byte[] b)
        {
            Assert.IsTrue(expected != null, "expected was null");
            Assert.IsTrue(b != null, "b was null");
            Assert.AreEqual(expected.Length, b.Length, $"array length mismatch, expected array of length {expected.Length} but received array of length {b.Length}");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.IsTrue(expected[i] == b[i], $"expected value {expected[i]} but received value {b[i]} at index #{i}");
            }
        }

        public static void AssertDoublesEqualApprox(double expected, double actual, double epsilon)
        {
            bool equal = MyMath.equal_approx(expected, actual, epsilon);
            if (!equal)
                Assert.Fail($"Double equality test failed. Expected: {expected}, actual: {actual}, epsilon: {epsilon}");
        }

        public static void AssertIntsEqualApprox(int expected, int actual, int maxDiff)
        {
            bool equal = MyMath.equal_approx(expected, actual, maxDiff);
            if (!equal)
                Assert.Fail($"Int equality test failed. Expected: {expected}, actual: {actual}, maxDiff: {maxDiff}");
        }

        public static PendingBuffer CreateBuffer(int size, long maxPayloadSize, out byte[] buffer)
        {
            int len = size - 4;
            buffer = TestUtil.GenerateRandomByteArray(size);

            // write len of payload
            buffer[0] = (byte)(len & 0xFF);
            buffer[1] = (byte)(len >> 8 & 0xFF);
            buffer[2] = (byte)(len >> 16 & 0xFF);
            buffer[3] = (byte)(len >> 24 & 0xFF);

            PendingBuffer pb = new PendingBuffer();
            pb.Construct(buffer, (int)maxPayloadSize);

            return pb;
        }
    }
}
