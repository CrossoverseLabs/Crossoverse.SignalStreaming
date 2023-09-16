using MessagePack;

namespace Crossoverse.SignalStreaming.BufferedSignal
{
    [MessagePackObject]
    public class ItemSpawnSignal : IBufferedSignal
    {
        [Key(0)]
        public System.Guid OriginalObjectId { get; }

        [Key(1)]
        public int InstanceId { get; }

        [Key(2)]
        public object FilterKey { get; }

        [Key(3)]
        public System.Ulid GeneratedBy { get; }

        public ItemSpawnSignal(System.Guid originalObjectId, int instanceId, object filterKey, System.Ulid generatedBy)
        {
            OriginalObjectId = originalObjectId;
            InstanceId = instanceId;
            FilterKey = filterKey;
            GeneratedBy = generatedBy;
        }
    }
}
