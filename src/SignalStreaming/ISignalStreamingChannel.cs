using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public interface ISignalStreamingChannel<TSignalType> : IDisposable where TSignalType : struct, Enum
    {
        string Id { get; }
        TSignalType SignalType { get; }
        StreamingType StreamingType { get; }

        bool IsConnected { get; }
        IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }

        void Initialize();

        UniTask<bool> ConnectAsync(CancellationToken token = default);
        UniTask DisconnectAsync();
    }
}
