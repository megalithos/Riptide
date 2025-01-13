// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.Utils
{
    internal static class AssertUtil
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new ApplicationException("ASSERT FAILED: " + message);
        }
    }
}
