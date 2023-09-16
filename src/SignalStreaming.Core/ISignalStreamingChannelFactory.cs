using System;

namespace Crossoverse.SignalStreaming
{
    public interface ISignalStreamingChannelFactory<TSignalType> where TSignalType : struct, Enum
    {
        ISignalStreamingChannel Create(string id, TSignalType signalType, StreamingType streamingType);
    }
}
