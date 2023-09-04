using Crossoverse.SignalStreaming;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class SignalStreamingChannelFactory : ISignalStreamingChannelFactoryV2<SignalType>
    {
        private readonly ITransportFactoryV2<SignalType> _transportFactory;
        private readonly EventFactory _eventFactory;

        public SignalStreamingChannelFactory
        (
            ITransportFactoryV2<SignalType> transportFactory,
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
                // return new BufferedSignalStreamingChannelV2(channelId, transport, _eventFactory);
            }
            else
            if (signalType == SignalType.LowFreqSignal)
            {
                return new LowFreqSignalStreamingChannel(channelId, transport, _eventFactory);
                // return new LowFreqSignalStreamingChannelV2(channelId, transport, _eventFactory);
            }
            else
            if (signalType == SignalType.HighFreqSignal)
            {
                return new HighFreqSignalStreamingChannel(channelId, transport, _eventFactory);
                // return new HighFreqSignalStreamingChannelV2(channelId, transport, _eventFactory);
            }

            DevelopmentOnlyLogger.LogError($"[{nameof(SignalStreamingChannelFactory)}] Could not create channel. StreamingType: {streamingType}, SignalType: {signalType}.");
            return null;
        }
    }
}
