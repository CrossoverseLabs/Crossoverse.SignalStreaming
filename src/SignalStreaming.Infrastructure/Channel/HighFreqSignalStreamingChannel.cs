using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Crossoverse.SignalStreaming.HighFreqSignal;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Toolkit.Transports;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class HighFreqSignalStreamingChannel : IHighFreqSignalStreamingChannel
    {
        public string Id => _signalStreamingChannel.Id;
        public bool IsConnected => _signalStreamingChannel.IsConnected;
        public uint TransportClientId => _signalStreamingChannel.TransportClientId;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber => _signalStreamingChannel.ConnectionStateSubscriber;

        private static readonly Type TypeOfObjectPoseSignal = typeof(ObjectPoseSignal);
        private static readonly Type TypeOfItemPoseSignal = typeof(ItemPoseSignal);
        private readonly ConcurrentQueue<ObjectPoseSignal> _incomingObjectPoseSignalBuffer = new();
        private readonly ConcurrentQueue<ItemPoseSignal> _incomingItemPoseSignalBuffer = new();

        private readonly IMessageSerializer _messageSerializer;
        private readonly SignalStreamingChannel _signalStreamingChannel;

        private bool _isInitialized;
        private IDisposable _disposable;

        public HighFreqSignalStreamingChannel
        (
            string id,
            ITransport transport,
            IMessageSerializer messageSerializer,
            EventFactory eventFactory
        )
        {
            _messageSerializer = messageSerializer;
            _signalStreamingChannel = new SignalStreamingChannel
            (
                id,
                transport,
                messageSerializer,
                eventFactory
            );
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            var disposableBagBuilder = DisposableBag.CreateBuilder();

            _signalStreamingChannel.OnSignalReceived
                .Subscribe(HandleTransportedSignal)
                .AddTo(disposableBagBuilder);

            _disposable = disposableBagBuilder.Build();

            _signalStreamingChannel.Initialize();
            _isInitialized = true;

            DevelopmentOnlyLogger.Log($"<color=cyan>[{nameof(HighFreqSignalStreamingChannel)}] Initialized.</color>");
        }

        public void Dispose()
        {
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            await _signalStreamingChannel.DisposeAsync();
            _disposable?.Dispose();
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(HighFreqSignalStreamingChannel)}] Disposed.</color>");
        }

        public async UniTask<bool> ConnectAsync(CancellationToken token = default)
        {
            if (!_isInitialized) Initialize();
            return await _signalStreamingChannel.ConnectAsync(token);
        }

        public async UniTask DisconnectAsync()
        {
            await _signalStreamingChannel.DisconnectAsync();
        }

        public void Send<T>(T signal) where T : IHighFreqSignal
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(HighFreqSignalStreamingChannel)}] SendSignal</color>");

            var signalType = (int)SignalType.Unknown;

            if (typeof(T) == TypeOfObjectPoseSignal)
            {
                signalType = (int)SignalType.ObjectPose;
            }
            else if (typeof(T) == TypeOfItemPoseSignal)
            {
                signalType = (int)SignalType.ItemPose;
            }

            if (signalType < 0) throw new ArgumentException($"Unknown signal type");

            var sendOptions = new SendOptions()
            {
                BroadcastingType = BroadcastingType.All,
                BufferingType = BufferingType.DoNotBuffering,
                Reliability = false,
            };

            _signalStreamingChannel.Send<T>(signal, signalType, sendOptions);
        }

        public ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IHighFreqSignal
        {
            // NOTE: This is a workaround to avoid boxing of value types.
            // References:
            //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
            //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
            //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
            //
            if (typeof(T) == TypeOfObjectPoseSignal)
            {
                if (_incomingObjectPoseSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingObjectPoseSignalBuffer.ToArray(); // TODO: Optimization
                var sequence = new ReadOnlySequence<ObjectPoseSignal>(segment);

                var convertFunc = (Func<ReadOnlySequence<ObjectPoseSignal>, ReadOnlySequence<T>>)(object)s_GetObjectPoseSignalSequence;
                return convertFunc.Invoke(sequence);
            }
            else if (typeof(T) == TypeOfItemPoseSignal)
            {
                if (_incomingItemPoseSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingItemPoseSignalBuffer.ToArray(); // TODO: Optimization
                var sequence = new ReadOnlySequence<ItemPoseSignal>(segment);

                var convertFunc = (Func<ReadOnlySequence<ItemPoseSignal>, ReadOnlySequence<T>>)(object)s_GetItemPoseSignalSequence;
                return convertFunc.Invoke(sequence);
            }

            return ReadOnlySequence<T>.Empty;
        }

        public void DeleteIncomingSignals<T>(long count) where T : IHighFreqSignal
        {
            if (typeof(T) == TypeOfObjectPoseSignal)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingObjectPoseSignalBuffer.TryDequeue(out _);
                }
            }
            else if (typeof(T) == TypeOfItemPoseSignal)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingItemPoseSignalBuffer.TryDequeue(out _);
                }
            }
        }

        // NOTE: This is a workaround to avoid boxing of value types.
        // References:
        //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
        //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
        //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
        //
        private static Func<ReadOnlySequence<ObjectPoseSignal>, ReadOnlySequence<ObjectPoseSignal>> s_GetObjectPoseSignalSequence = (param) => param;
        private static Func<ReadOnlySequence<ItemPoseSignal>, ReadOnlySequence<ItemPoseSignal>> s_GetItemPoseSignalSequence = (param) => param;

        private void HandleTransportedSignal((int SignalId, ReadOnlySequence<byte> Payload) data)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(HighFreqSignalStreamingChannel)}] HandleTransportMessage</color>");

            if (data.SignalId == (int)SignalType.ObjectPose)
            {
                var signal = _messageSerializer.Deserialize<ObjectPoseSignal>(data.Payload);
                _incomingObjectPoseSignalBuffer.Enqueue(signal);
            }
            else if (data.SignalId == (int)SignalType.ItemPose)
            {
                var signal = _messageSerializer.Deserialize<ItemPoseSignal>(data.Payload);
                _incomingItemPoseSignalBuffer.Enqueue(signal);
            }
        }
    }
}
