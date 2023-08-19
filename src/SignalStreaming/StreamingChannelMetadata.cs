namespace Crossoverse.Core.Domain.SignalStreaming
{
    public sealed class StreamingChannelMetadata
    {
        public string StreamingContentId; // Key1
        public SignalType SignalType; // Key2
        public StreamingType StreamingType; // Key3
        public string ChannelId; // Value
    }
}
