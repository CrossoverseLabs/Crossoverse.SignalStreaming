using System;

namespace Crossoverse.SignalStreaming
{
    public interface ISignalStreamingChannelFactory
    {
        ISignalStreamingChannel Create(string id, SignalType signalType, StreamingType streamingType);
    }

    public interface ISignalStreamingChannelFactoryV2<TSignalType> where TSignalType : struct, Enum
    {
        ISignalStreamingChannelV2<TSignalType> Create(string id, TSignalType signalType, StreamingType streamingType);
    }
}
