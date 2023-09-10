using System.Numerics;
using MessagePack;

namespace Crossoverse.SignalStreaming.HighFreqSignal
{
    [MessagePackObject]
    public sealed class ObjectPoseSignal : IHighFreqSignal
    {
        [Key(0)]
        public int InstanceId { get; set; }

        [Key(1)]
        public Vector3 Position { get; set; }

        [Key(2)]
        public Quaternion Rotation { get; set; }

        [Key(3)]
        public System.Ulid GeneratedBy { get; set; }
    }
}
