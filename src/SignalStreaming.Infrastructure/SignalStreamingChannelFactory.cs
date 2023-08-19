using Crossoverse.Core.Domain.SignalStreaming;
using MessagePipe;

namespace Crossoverse.Core.Infrastructure.SignalStreaming
{
    public sealed class SignalStreamingChannelFactory : ISignalStreamingChannelFactory
    {
        private readonly ITransportFactory _transportFactory;
        private readonly EventFactory _eventFactory;

        public SignalStreamingChannelFactory
        (
            ITransportFactory transportFactory,
            EventFactory eventFactory
        )
        {
            _transportFactory = transportFactory;
            _eventFactory = eventFactory;
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
                return new BufferedSignalStreamingChannel(channelId, transport, _eventFactory);
            }
            else
            if (signalType == SignalType.LowFreqSignal)
            {
                return new LowFreqSignalStreamingChannel(channelId, transport, _eventFactory);
            }

            DevelopmentOnlyLogger.LogError($"[{nameof(SignalStreamingChannelFactory)}] Could not create channel. StreamingType: {streamingType}, SignalType: {signalType}.");
            return null;
        }
    }
}
