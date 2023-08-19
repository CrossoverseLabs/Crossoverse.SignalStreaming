using Crossoverse.Toolkit.Transports;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface ITransportFactory
    {
        ITransport Create(string id, SignalType signalType, StreamingType streamingType);
    }
}
