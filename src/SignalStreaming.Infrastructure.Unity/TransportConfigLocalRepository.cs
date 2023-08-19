using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crossoverse.Core.Configuration;
using Crossoverse.Core.Domain.SignalStreaming;
using Crossoverse.Core.Infrastructure.SignalStreaming;
using UnityEngine;

namespace Crossoverse.Core.Unity.Infrastructure.SignalStreaming
{
    [CreateAssetMenu(
        menuName = "Crossoverse/LocalRepository/" + nameof(TransportConfigLocalRepository),
        fileName = nameof(TransportConfigLocalRepository))]
    public class TransportConfigLocalRepository : ScriptableObject, IConfigurationRepository<string>
    {
        [SerializeField] List<StreamingTransportType> _streamingTransportTypes = new();
        [SerializeField] string _photonRealtime_PunVersion = "2.4.1";
        [SerializeField] string _photonRealtime_AppVersion = "";
        [SerializeField] string _photonRealtime_Region = "jp";
        [SerializeField] string _photonRealtime_AppId = "********** DO_NOT_COMMIT_APP_ID **********";

        public string Find(string key)
        {
            if (key.StartsWith(Constants.StreamingTransportSettingsKeyPrefix))
            {
                var splits = key.Split(':');
                if (splits.Length != 4)
                {
                    return string.Empty;
                }

                var signalType = splits[2];
                var streamingType = splits[3];

                foreach (var streamingTransportType in _streamingTransportTypes)
                {
                    if (streamingTransportType.SignalType.ToString() == signalType
                    && streamingTransportType.StreamingType.ToString() == streamingType)
                    {
                        return streamingTransportType.TransportType.ToString();
                    }
                }
            }

            if (key.StartsWith(Constants.PhotonRealtimeSettingsKeyPrefix))
            {
                return key switch
                {
                    Constants.PhotonRealtimeSettingsKey_PunVersion => _photonRealtime_PunVersion,
                    Constants.PhotonRealtimeSettingsKey_AppVersion => _photonRealtime_AppVersion,
                    Constants.PhotonRealtimeSettingsKey_Region => _photonRealtime_Region,
                    Constants.PhotonRealtimeSettingsKey_AppId => _photonRealtime_AppId,
                    _ => string.Empty,
                };
            }

            return string.Empty;
        }

        public async Task<string> FindAsync(string key)
        {
            throw new NotImplementedException();
        }

        public void Save(string key, string value)
        {
            throw new NotImplementedException();
        }

        public async Task<string> SaveAsync(string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}
