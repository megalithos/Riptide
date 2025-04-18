// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riptide.DataStreaming
{
    public class DataRateLimitExceededException : Exception
    {
        public DataRateLimitExceededException()
        {
        }

        public DataRateLimitExceededException(string message)
            : base(message)
        {
        }

        public DataRateLimitExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
