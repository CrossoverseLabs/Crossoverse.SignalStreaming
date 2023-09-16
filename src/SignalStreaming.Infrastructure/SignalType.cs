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
        ItemSpawn = BufferedSignal + 2,
        PlayerSpawn = BufferedSignal + 3,

        // Low Frequency
        TextMessage = LowFreqSignal + 1,
        DestroyObject = LowFreqSignal + 2,
        ItemDespawn = BufferedSignal + 3,
        PlayerDespawn = BufferedSignal + 4,

        // High Frequency
        ObjectPose = HighFreqSignal + 1,
        ItemPose = BufferedSignal + 2,
    }
}
