using System;

namespace Crossoverse.SignalStreaming
{
    public interface IHighFreqSignal
    {
        Ulid GeneratedBy { get; }
    }
}
