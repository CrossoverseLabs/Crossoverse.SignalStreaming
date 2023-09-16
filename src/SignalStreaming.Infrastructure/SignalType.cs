namespace Crossoverse.SignalStreaming.Infrastructure
{
    public enum SignalType
    {
        Unknown = -1,

        BufferedSignal = 0,
        LowFreqSignal = 64,
        HighFreqSignal = 128,

        // Buffered
        CreateObject = BufferedSignal + 1,

        // Low Frequency
        TextMessage = LowFreqSignal + 1,
        DestroyObject = LowFreqSignal + 2,

        // High Frequency
        ObjectPose = HighFreqSignal + 1,
    }
}
