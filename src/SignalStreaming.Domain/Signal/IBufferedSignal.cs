using System;

namespace Crossoverse.SignalStreaming
{
    public interface IBufferedSignal
    {
        object FilterKey { get; }
        Ulid GeneratedBy { get; }
    }
}
