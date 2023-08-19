using System;
using MessagePack;

namespace Crossoverse.Core.Domain.SignalStreaming.LowFreqSignal
{
    [MessagePackObject]
    public sealed class DestroyObjectSignal : ILowFreqSignal
    {
        [Key(0)]
        public int InstanceId { get; set; }

        [Key(1)]
        public Guid GeneratedBy { get; set; }

        [Key(2)]
        public long OriginTimestampMilliseconds { get; set; }
    }
}
