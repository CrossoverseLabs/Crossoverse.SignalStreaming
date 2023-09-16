using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface ISignalStreamingChannel : IDisposable
    {
        string Id { get; }

        bool IsConnected { get; }
        IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }

        void Initialize();

        UniTask<bool> ConnectAsync(CancellationToken token = default);
        UniTask DisconnectAsync();
    }
}
