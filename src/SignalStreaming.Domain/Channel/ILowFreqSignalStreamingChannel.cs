using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface ILowFreqSignalStreamingChannel : ISignalStreamingChannel
    {
        ISubscriber<LowFreqSignal.TextMessageSignal> OnTextMessageReceived { get; }
        ISubscriber<LowFreqSignal.DestroyObjectSignal> OnDestroyObjectSignalReceived { get; }
        void Send<T>(T signal) where T : ILowFreqSignal;
    }
}
