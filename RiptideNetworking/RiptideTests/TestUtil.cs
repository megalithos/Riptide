// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        public class TestPRNG
        {
            public TestPRNG(int seed)
            {
                m_randomValue = (uint)seed;
            }

            private static uint m_randomValue;

            /// <summary>
            /// Generate random number in range [0, maxExclusive[
            /// </summary>
            public uint Next(int maxExclusive)
            {
                // my own alg for testing...
                m_randomValue *= 7;
                m_randomValue >>= 2;
                return m_randomValue % (uint)maxExclusive;
            }
        }

        public static byte[] HashBytes(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(bytes);
            }
        }

        public static void AssertByteArraysEqual(byte[] expectd, byte[] b)
        {
            Assert.IsTrue(expectd != null, "a was null");
            Assert.IsTrue(b != null, "b was null");
            Assert.IsTrue(expectd.Length == b.Length, "length mismatch");
            for (int i = 0; i < expectd.Length; i++)
            {
                if (expectd[i] != b[i])
                    Assert.Fail($"Mismatch at index #{i}. Expected: {expectd[i]}, actual: {b[i]}");
            }
        }
    }
}
