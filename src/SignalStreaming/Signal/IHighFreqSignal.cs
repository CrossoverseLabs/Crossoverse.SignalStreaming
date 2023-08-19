using System;

namespace Crossoverse.SignalStreaming
{
    public interface IHighFreqSignal
    {
        Guid GeneratedBy { get; }
        long OriginTimestampMilliseconds { get; } // UnixTimeMilliseconds
    }
}
