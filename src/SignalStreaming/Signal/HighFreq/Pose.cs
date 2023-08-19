using System;
using MessagePack;
using UnityEngine;

namespace Crossoverse.Core.Domain.SignalStreaming.HighFreqSignal
{
    [MessagePackObject]
    public sealed class Pose : IHighFreqSignal
    {
        [Key(0)]
        public int InstanceId { get; }

        [Key(1)]
        public Vector3 Position { get; }

        [Key(2)]
        public Quaternion Rotation { get; }

        [Key(4)]
        public Guid GeneratedBy { get; set; }

        [Key(5)]
        public long OriginTimestampMilliseconds { get; set; }
    }
}
