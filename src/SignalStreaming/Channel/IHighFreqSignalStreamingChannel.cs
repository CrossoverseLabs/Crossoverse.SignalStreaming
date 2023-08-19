using MessagePipe;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface IHighFreqSignalStreamingChannel : ISignalStreamingChannel
    {
        ISubscriber<HighFreqSignal.Pose> OnObjectPoseReceived { get; }
        void Send<T>(T signal) where T : IHighFreqSignal;
    }
}
