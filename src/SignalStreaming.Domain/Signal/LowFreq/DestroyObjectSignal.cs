using MessagePack;

namespace Crossoverse.SignalStreaming.LowFreqSignal
{
    [MessagePackObject]
    public sealed class DestroyObjectSignal : ILowFreqSignal
    {
        [Key(0)]
        public int InstanceId { get; set; }

        [Key(1)]
        public System.Ulid GeneratedBy { get; set; }
    }
}
