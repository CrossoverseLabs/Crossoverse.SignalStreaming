using System;

namespace Crossoverse.SignalStreaming
{
    public interface IBufferedSignal
    {
        object FilterKey { get; }
        Guid GeneratedBy { get; }
        long OriginTimestampMilliseconds { get; } // UnixTimeMilliseconds
    }
}
