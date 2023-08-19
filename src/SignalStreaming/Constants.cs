namespace Crossoverse.Core.Domain.SignalStreaming
{
    public static class Constants
    {
        public const string StreamingTransportSettingsKeyFormat = "SignalStreamingChannel:TransportType:<SignalType>:<StreamingType>";
        public const string StreamingTransportSettingsKeyPrefix = "SignalStreamingChannel:TransportType";

        public const string PhotonRealtimeSettingsKeyFormat = "Transport:PhotonRealtime:<Key>";
        public const string PhotonRealtimeSettingsKeyPrefix = "Transport:PhotonRealtime";

        public const string PhotonRealtimeSettingsKey_PunVersion = "Transport:PhotonRealtime:PunVersion";
        public const string PhotonRealtimeSettingsKey_AppVersion = "Transport:PhotonRealtime:AppVersion";
        public const string PhotonRealtimeSettingsKey_Region = "Transport:PhotonRealtime:Region";
        public const string PhotonRealtimeSettingsKey_AppId = "Transport:PhotonRealtime:AppId";
    }
}
