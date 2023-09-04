using System;
using Crossoverse.Toolkit.Transports;

namespace Crossoverse.SignalStreaming
{
    public interface ITransportFactory
    {
        ITransport Create(string id, SignalType signalType, StreamingType streamingType);
    }

    public interface ITransportFactoryV2<TSignalType> where TSignalType : struct, Enum
    {
        ITransport Create(string id, TSignalType signalType, StreamingType streamingType);
    }
}
