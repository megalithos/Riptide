// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.DataStreaming
{
    internal static class DataStreamSettings
    {
        public const int c_maxPayloadSize = 1231;

        // 1231 so it matches riptide message's payload size.
        // I dont know why it's 1231 and not 1232
        public const int initialCwndSize = c_maxPayloadSize;
        public const int slowStartThreshold = 1_073_741_824;
        public const int maxSendWindowElements = 1024;
    }
}
