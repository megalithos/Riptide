// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.Utils
{
    internal static class MyMath
    {
        /// <summary>
        /// Perform integer division and add 1
        /// to the result, if modulo between the two values
        /// is nonzero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IntCeilDiv(int a, int b)
        {
            int result = a / b;
            if (a % b != 0)
            {
                result++;
            }
            return result;
        }

        public static bool equal_approx(double a, double b, double epsilon)
        {
            return Math.Abs(a - b) < epsilon;
        }
    }
}
