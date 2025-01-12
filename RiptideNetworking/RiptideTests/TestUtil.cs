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
    }
}
