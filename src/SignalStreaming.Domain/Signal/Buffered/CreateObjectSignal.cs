using MessagePack;

namespace Crossoverse.SignalStreaming.BufferedSignal
{
    [MessagePackObject]
    public sealed class CreateObjectSignal : IBufferedSignal
    {
        [Key(0)]
        public System.Guid OriginalObjectId { get; set; }

        [Key(1)]
        public int InstanceId { get; set; }

        [Key(2)]
        public int OwnerClientId { get; set; }

        [Key(3)]
        public object FilterKey { get; set; }

        [Key(4)]
        public System.Ulid GeneratedBy { get; set; }
    }
}
