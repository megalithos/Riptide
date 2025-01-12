// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide;
using Riptide.DataStreaming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiptideTests.DataStreaming
{
    internal class DataStreamTestMessageCreator : IMessageCreator
    {
        public Message Create()
        {
            return Message.Create();
        }
    }
}
