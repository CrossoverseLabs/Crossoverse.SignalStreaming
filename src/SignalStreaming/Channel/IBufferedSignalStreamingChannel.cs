using System;
using MessagePipe;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface IBufferedSignalStreamingChannel : ISignalStreamingChannel
    {
        ISubscriber<BufferedSignal.CreateObjectSignal> OnCreateObjectSignalReceived { get; }
        void Send<T>(T signal) where T : IBufferedSignal;
        void RemoveBufferedSignal(SignalType signalType, Guid signalGeneratedBy, object filterKey);
    }
}
