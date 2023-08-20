using System;
using System.Numerics;
using MessagePack;

namespace Crossoverse.SignalStreaming.HighFreqSignal
{
    [MessagePackObject]
    public sealed class ObjectPoseSignal : IHighFreqSignal
    {
        [Key(0)]
        public int InstanceId { get; }

        [Key(1)]
        public Vector3 Position { get; }

        [Key(2)]
        public Quaternion Rotation { get; }

        [Key(3)]
        public Guid GeneratedBy { get; set; }

        [Key(4)]
        public long OriginTimestampMilliseconds { get; set; }
    }
}
