using System;
using System.Buffers;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface IBufferedSignalStreamingChannel : ISignalStreamingChannel
    {
        void Send<T>(T signal) where T : IBufferedSignal;
        void RemoveBufferedSignal<T>(Ulid signalGeneratedBy, object filterKey) where T : IBufferedSignal;
        ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IBufferedSignal;
        void DeleteIncomingSignals<T>(long count) where T : IBufferedSignal;
    }
}
