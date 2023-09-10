using MessagePack;

namespace Crossoverse.SignalStreaming.LowFreqSignal
{
    [MessagePackObject]
    public sealed class TextMessageSignal : ILowFreqSignal
    {
        [Key(0)]
        public string Message { get; set; }

        [Key(1)]
        public System.Ulid GeneratedBy { get; set; }
    }
}
