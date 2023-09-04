namespace Crossoverse.SignalStreaming.Infrastructure.Unity
{
    [System.Serializable]
    public sealed class StreamingTransportType
    {
        public SignalType SignalType; // Key1
        public StreamingType StreamingType; // Key2
        public TransportType TransportType; // Value
    }
}
