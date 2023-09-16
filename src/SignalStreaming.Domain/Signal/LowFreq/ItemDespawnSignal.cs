using MessagePack;

namespace Crossoverse.SignalStreaming.LowFreqSignal
{
    [MessagePackObject]
    public struct ItemDespawnSignal : ILowFreqSignal
    {
        [Key(0)]
        public readonly int InstanceId;

        [Key(1)]
        public readonly System.Ulid GeneratedBy;

        public ItemDespawnSignal(int instanceId, System.Ulid generatedBy)
        {
            InstanceId = instanceId;
            GeneratedBy = generatedBy;
        }
    }
}
