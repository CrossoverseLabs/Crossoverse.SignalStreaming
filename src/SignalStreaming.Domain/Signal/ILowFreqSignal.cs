using System;

namespace Crossoverse.SignalStreaming
{
    public interface ILowFreqSignal
    {
        Ulid GeneratedBy { get; }
    }
}
