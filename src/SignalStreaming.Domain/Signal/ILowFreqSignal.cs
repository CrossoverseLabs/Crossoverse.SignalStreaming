using System;

namespace Crossoverse.SignalStreaming
{
    public interface ILowFreqSignal
    {
        Guid GeneratedBy { get; }
        long OriginTimestampMilliseconds { get; } // UnixTimeMilliseconds
    }
}
