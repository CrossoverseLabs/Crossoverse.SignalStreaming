using System;
using System.Buffers;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface ILowFreqSignalStreamingChannel : ISignalStreamingChannel
    {
        void Send<T>(T signal) where T : ILowFreqSignal;
        ReadOnlySequence<T> ReadIncomingSignals<T>() where T : ILowFreqSignal;
        void DeleteIncomingSignals<T>(long count) where T : ILowFreqSignal;
    }
}
