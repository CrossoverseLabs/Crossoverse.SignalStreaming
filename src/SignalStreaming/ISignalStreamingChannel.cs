using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Crossoverse.Core.Domain.SignalStreaming
{
    public interface ISignalStreamingChannel : IDisposable
    {
        string Id { get; }
        SignalType SignalType { get; }
        StreamingType StreamingType { get; }

        bool IsConnected { get; }
        IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }

        void Initialize();

        UniTask<bool> ConnectAsync(CancellationToken token = default);
        UniTask DisconnectAsync();
    }
}
