using System.Numerics;
using MessagePack;

namespace Crossoverse.SignalStreaming.HighFreqSignal
{
    [MessagePackObject]
    public struct ItemPoseSignal : IHighFreqSignal
    {
        [Key(0)]
        public readonly int InstanceId;

        [Key(1)]
        public readonly Vector3 Position;

        [Key(2)]
        public readonly Quaternion Rotation;

        [Key(3)]
        public readonly System.Ulid GeneratedBy;

        public ItemPoseSignal(int instanceId, Vector3 position, Quaternion rotation, System.Ulid generatedBy)
        {
            InstanceId = instanceId;
            Position = position;
            Rotation = rotation;
            GeneratedBy = generatedBy;
        }
    }
}
