using System.Buffers;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface IHighFreqSignalStreamingChannelV2 : ISignalStreamingChannel
    {
        void Send<T>(T signal) where T : IHighFreqSignal;
        ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IHighFreqSignal;
        void DeleteIncomingSignals<T>(long count) where T : IHighFreqSignal;
    }
}