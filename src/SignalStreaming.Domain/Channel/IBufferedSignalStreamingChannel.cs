using System;
using System.Buffers;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface IBufferedSignalStreamingChannel<TSignalType>
        : ISignalStreamingChannel<TSignalType> where TSignalType : struct, Enum
    {
        void Send<T>(T signal) where T : IBufferedSignal;
        void RemoveBufferedSignal<T>(Guid signalGeneratedBy, object filterKey) where T : IBufferedSignal;
        ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IBufferedSignal;
        void DeleteIncomingSignals<T>(long count) where T : IBufferedSignal;
    }
}
