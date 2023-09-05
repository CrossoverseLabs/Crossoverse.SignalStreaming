using System;
using Crossoverse.Toolkit.Transports;

namespace Crossoverse.SignalStreaming
{
    public interface ITransportFactory<TSignalType> where TSignalType : struct, Enum
    {
        ITransport Create(string id, TSignalType signalType, StreamingType streamingType);
    }
}
