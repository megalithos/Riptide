// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiptideTests
{
    internal class UnsafeUtilTests
    {
        [Test]
        public void ZeroMemoryWorks_1()
        {
            byte[] randomBytes2048 = TestUtil.GenerateRandomByteArray(2048);
            UnsafeUtil.ZeroMemory(randomBytes2048);
            byte[] expected = new byte[2048];
            TestUtil.AssertByteArraysEqual(expected, randomBytes2048);
        }
    }
}
