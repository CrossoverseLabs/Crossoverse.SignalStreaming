using Crossoverse.Toolkit.Serialization;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class SignalStreamingChannelFactory : ISignalStreamingChannelFactory<SignalType>
    {
        private readonly ITransportFactory<SignalType> _transportFactory;
        private readonly EventFactory _eventFactory;
        private readonly IMessageSerializer _messageSerializer;

        public SignalStreamingChannelFactory
        (
            ITransportFactory<SignalType> transportFactory,
            EventFactory eventFactory
        )
        {
            _transportFactory = transportFactory;
            _eventFactory = eventFactory;
            _messageSerializer = new MessagePackMessageSerializer();
        }

        public ISignalStreamingChannel Create(string channelId, SignalType signalType, StreamingType streamingType)
        {
            var transport = _transportFactory.Create(channelId, signalType, streamingType);

            if (transport is null)
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(SignalStreamingChannelFactory)}] Could not create transport. StreamingType: {streamingType}, SignalType: {signalType}.");
                return null;
            }

            if (signalType == SignalType.BufferedSignal)
            {
                return new BufferedSignalStreamingChannel(channelId, transport, _messageSerializer, _eventFactory);
            }
            else
            if (signalType == SignalType.LowFreqSignal)
            {
                return new LowFreqSignalStreamingChannel(channelId, transport, _eventFactory);
            }
            else
            if (signalType == SignalType.HighFreqSignal)
            {
                return new HighFreqSignalStreamingChannel(channelId, transport, _eventFactory);
            }

            DevelopmentOnlyLogger.LogError($"[{nameof(SignalStreamingChannelFactory)}] Could not create channel. StreamingType: {streamingType}, SignalType: {signalType}.");
            return null;
        }
    }
}
