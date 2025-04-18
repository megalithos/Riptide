// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.DataStreaming
{
    internal class DefaultRateLimitSettings : IRateLimitSettings
    {
        public TimeSpan DataLimitWindow => TimeSpan.FromMinutes(1);

        public ByteAmount MaxAmountPerWindow => ByteAmount.FromMegabytes(60);
    }
}
