using System;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface IBufferedSignal
    {
        object FilterKey { get; }
        Guid GeneratedBy { get; }
        long OriginTimestampMilliseconds { get; } // UnixTimeMilliseconds
    }
}
