using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Crossoverse.SignalStreaming.BufferedSignal;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Toolkit.Transports;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class BufferedSignalStreamingChannel : IBufferedSignalStreamingChannel
    {
        public string Id => _signalStreamingChannel.Id;
        public bool IsConnected => _signalStreamingChannel.IsConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber => _signalStreamingChannel.ConnectionStateSubscriber;

        private static readonly Type TypeOfCreateObjectSignal = typeof(CreateObjectSignal);
        private readonly ConcurrentQueue<CreateObjectSignal> _incomingCreateObjectSignalBuffer = new();

        private readonly IMessageSerializer _messageSerializer;
        private readonly SignalStreamingChannel _signalStreamingChannel;

        private bool _isInitialized;
        private IDisposable _disposable;

        public BufferedSignalStreamingChannel
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

            DevelopmentOnlyLogger.Log($"<color=cyan>[{nameof(BufferedSignalStreamingChannel)}] Initialized.</color>");
        }

        public void Dispose()
        {            
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            await _signalStreamingChannel.DisposeAsync();
            _disposable?.Dispose();
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] Disposed.</color>");
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

        public void Send<T>(T signal) where T : IBufferedSignal
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] SendSignal</color>");

            var signalType = (int)SignalType.Unknown;

            if (typeof(T) == TypeOfCreateObjectSignal)
            {
                signalType = (int)SignalType.CreateObject;
            }

            if (signalType < 0) throw new ArgumentException($"Unknown signal type");

            var bufferingKey = new BufferingKey()
            {
                FirstKey = signalType,
                SecondKey = signal.GeneratedBy.ToString(),
                ThirdKey = signal.FilterKey,
            };

            var sendOptions = new SendOptions()
            {
                BroadcastingType = BroadcastingType.All,
                BufferingType = BufferingType.AddToBuffer,
                BufferingKey = bufferingKey,
                Reliability = true,
            };

            _signalStreamingChannel.Send<T>(signal, signalType, sendOptions);
        }

        public void RemoveBufferedSignal<T>(Ulid signalGeneratedBy, object filterKey) where T : IBufferedSignal
        {
            var signalType = (int)SignalType.Unknown;

            if (typeof(T) == TypeOfCreateObjectSignal)
            {
                signalType = (int)SignalType.CreateObject;
            }

            if (signalType < 0) throw new InvalidOperationException($"Unknown signal type");

            var bufferingKey = new BufferingKey()
            {
                FirstKey = signalType,
                SecondKey = signalGeneratedBy.ToString(),
                ThirdKey = filterKey,
            };

            var sendOptions = new SendOptions()
            {
                BroadcastingType = BroadcastingType.All,
                BufferingType = BufferingType.RemoveFromBuffer,
                BufferingKey = bufferingKey,
                Reliability = true,
            };

            _signalStreamingChannel.Send<T>(default, signalType, sendOptions);
        }

        public ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IBufferedSignal
        {
            // NOTE: This is a workaround to avoid boxing of value types.
            // References:
            //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
            //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
            //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
            //
            if (typeof(T) == TypeOfCreateObjectSignal)
            {
                if (_incomingCreateObjectSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingCreateObjectSignalBuffer.ToArray();
                var sequence = new ReadOnlySequence<CreateObjectSignal>(segment);

                var convertFunc = (Func<ReadOnlySequence<CreateObjectSignal>, ReadOnlySequence<T>>)(object)s_GetCreateObjectSignalSequence;
                return convertFunc.Invoke(sequence);
            }

            return ReadOnlySequence<T>.Empty;
        }

        public void DeleteIncomingSignals<T>(long count) where T : IBufferedSignal
        {
            if (typeof(T) == TypeOfCreateObjectSignal)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingCreateObjectSignalBuffer.TryDequeue(out _);
                }
            }
        }

        // NOTE: This is a workaround to avoid boxing of value types.
        // References:
        //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
        //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
        //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
        //
        private static Func<ReadOnlySequence<CreateObjectSignal>, ReadOnlySequence<CreateObjectSignal>> s_GetCreateObjectSignalSequence = (param) => param;

        private void HandleTransportedSignal((int SignalId, ReadOnlySequence<byte> Payload) data)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] HandleTransportedSignal</color>");

            if (data.SignalId == (int)SignalType.CreateObject)
            {
                var signal = _messageSerializer.Deserialize<CreateObjectSignal>(data.Payload);
                _incomingCreateObjectSignalBuffer.Enqueue(signal);
            }
        }
    }
}
