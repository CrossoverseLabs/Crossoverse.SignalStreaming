namespace Crossoverse.SignalStreaming
{
    public interface ISignalStreamingChannelFactory
    {
        ISignalStreamingChannel Create(string id, SignalType signalType, StreamingType streamingType);
    }
}
