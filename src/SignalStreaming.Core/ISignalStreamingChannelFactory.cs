using System;

namespace Crossoverse.SignalStreaming
{
    public interface ISignalStreamingChannelFactory<TSignalType> where TSignalType : struct, Enum
    {
        ISignalStreamingChannel<TSignalType> Create(string id, TSignalType signalType, StreamingType streamingType);
    }
}
