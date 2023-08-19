using System;
using Crossoverse.Core.Configuration;
using Crossoverse.Core.Domain.SignalStreaming;
using Crossoverse.Toolkit.Transports;
#if CROSSOVERSE_PHOTON_TRANSPORT
using Crossoverse.Toolkit.Transports.PhotonRealtime;
#endif

namespace Crossoverse.Core.Infrastructure.SignalStreaming
{
    public sealed class TransportFactory : ITransportFactory
    {
        private readonly IConfigurationRepository<string> _configurationRepository;

        public TransportFactory
        (
            IConfigurationRepository<string> configurationRepository
        )
        {
            _configurationRepository = configurationRepository;
        }

        public ITransport Create(string channelId, SignalType signalType, StreamingType streamingType)
        {
            var transportTypeName = _configurationRepository.Find($"{Constants.StreamingTransportSettingsKeyPrefix}:{signalType}:{streamingType}");

            if (!Enum.TryParse(transportTypeName, out TransportType transportType))
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(SignalStreamingChannelFactory)}] Cannot create transport. TransportType: '{transportTypeName}', SignalType: '{signalType}', StreamingType: '{streamingType}'.");
                return null;
            }

#if CROSSOVERSE_PHOTON_TRANSPORT
            var updateRatePerSecond = signalType switch
            {
                SignalType.BufferedSignal => 5,
                SignalType.LowFreqSignal => 5,
                SignalType.HighFreqSignal => 30,
                _ => 20,
            };
#endif

            return transportType switch
            {
#if CROSSOVERSE_PHOTON_TRANSPORT
                TransportType.PhotonRealtime => CreatePhotonRealtimeTransport(channelId, updateRatePerSecond),
#else
                TransportType.PhotonRealtime => throw new NotImplementedException("Package [dev.crossoverse.toolkit.transports.photonrealtime] cannot be found."),
#endif
                _ => null,
            };
        }

#if CROSSOVERSE_PHOTON_TRANSPORT
        private PhotonRealtimeTransport CreatePhotonRealtimeTransport(string channelId, int updateRatePerSecond)
        {
            var punVersion = _configurationRepository.Find(Constants.PhotonRealtimeSettingsKey_PunVersion);
            var appVersion = _configurationRepository.Find(Constants.PhotonRealtimeSettingsKey_AppVersion);
            var region = _configurationRepository.Find(Constants.PhotonRealtimeSettingsKey_Region);
            var appId = _configurationRepository.Find(Constants.PhotonRealtimeSettingsKey_AppId);

            var appSettings = new Photon.Realtime.AppSettings()
            {
                AppVersion = appVersion,
                AppIdRealtime = appId,
                FixedRegion = region,
            };

            return new PhotonRealtimeTransport
            (
                punVersion: punVersion,
                connectParameters: new PhotonRealtimeConnectParameters(){ AppSettings = appSettings },
                joinParameters: new PhotonRealtimeJoinParameters(){ RoomName = channelId },
                targetFrameRate: updateRatePerSecond,
                isBackgroundThread: true,
                protocol: ExitGames.Client.Photon.ConnectionProtocol.Udp
            );
        }
#endif
    }
}
