using System;
using MessagePack;

namespace Crossoverse.Core.Domain.SignalStreaming.BufferedSignal
{
    [MessagePackObject]
    public sealed class CreateObjectSignal : IBufferedSignal
    {
        [Key(0)]
        public Guid OriginalObjectId { get; set; }

        [Key(1)]
        public int InstanceId { get; set; }

        [Key(2)]
        public int OwnerClientId { get; set; }

        [Key(3)]
        public object FilterKey { get; set; }

        [Key(4)]
        public Guid GeneratedBy { get; set; }

        [Key(5)]
        public long OriginTimestampMilliseconds { get; set; }
    }
}
