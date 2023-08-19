using Crossoverse.Toolkit.Transports;

namespace Crossoverse.SignalStreaming
{
    public interface ITransportFactory
    {
        ITransport Create(string id, SignalType signalType, StreamingType streamingType);
    }
}
