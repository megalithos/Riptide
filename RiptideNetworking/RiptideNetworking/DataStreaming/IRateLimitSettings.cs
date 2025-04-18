using Riptide.Utils;
using System;

namespace Riptide.DataStreaming
{
    internal interface IRateLimitSettings
    {
        TimeSpan DataLimitWindow { get; }
        ByteAmount MaxAmountPerWindow { get; }
    }
}
