using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface IHighFreqSignalStreamingChannel : ISignalStreamingChannel
    {
        ISubscriber<HighFreqSignal.ObjectPoseSignal> OnObjectPoseReceived { get; }
        void Send<T>(T signal) where T : IHighFreqSignal;
    }
}
