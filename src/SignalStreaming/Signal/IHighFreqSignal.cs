using System;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface IHighFreqSignal
    {
        Guid GeneratedBy { get; }
        long OriginTimestampMilliseconds { get; } // UnixTimeMilliseconds
    }
}
