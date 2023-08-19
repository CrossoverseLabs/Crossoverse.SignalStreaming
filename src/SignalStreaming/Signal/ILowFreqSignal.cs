using System;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface ILowFreqSignal
    {
        Guid GeneratedBy { get; }
        long OriginTimestampMilliseconds { get; } // UnixTimeMilliseconds
    }
}
