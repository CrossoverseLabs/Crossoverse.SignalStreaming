using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface IHighFreqSignalStreamingChannel : ISignalStreamingChannel
    {
        ISubscriber<HighFreqSignal.Pose> OnObjectPoseReceived { get; }
        void Send<T>(T signal) where T : IHighFreqSignal;
    }
}
