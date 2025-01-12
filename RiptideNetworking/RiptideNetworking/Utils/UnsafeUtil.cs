// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.Utils
{
    internal static class UnsafeUtil
    {
        public static unsafe void ZeroMemory(byte[] bytes)
        {
            int len = bytes.Length;
            Assert.True(len % 8 == 0, "len % 8 == 0");

            int ulen = (len / 8);

            fixed (byte* ptr = bytes)
            {
                ulong* uptr = (ulong*)ptr;
                for (int i = 0; i < ulen; i++)
                {
                    *uptr = 0;
                    uptr++;
                }
            }
        }
    }
}
