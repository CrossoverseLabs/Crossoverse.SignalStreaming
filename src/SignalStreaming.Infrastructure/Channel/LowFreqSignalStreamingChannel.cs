using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Crossoverse.SignalStreaming.LowFreqSignal;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Toolkit.Transports;
using Cysharp.Threading.Tasks;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class LowFreqSignalStreamingChannel : ILowFreqSignalStreamingChannel
    {
        public string Id => _signalStreamingChannel.Id;
        public bool IsConnected => _signalStreamingChannel.IsConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber => _signalStreamingChannel.ConnectionStateSubscriber;

        private static readonly Type TypeOfDestroyObjectSignal = typeof(DestroyObjectSignal);
        private static readonly Type TypeOfTextMessageSignal = typeof(TextMessageSignal);
        private readonly ConcurrentQueue<DestroyObjectSignal> _incomingDestroyObjectSignalBuffer = new();
        private readonly ConcurrentQueue<TextMessageSignal> _incomingTextMessageSignalBuffer = new();

        private readonly IMessageSerializer _messageSerializer;
        private readonly SignalStreamingChannel _signalStreamingChannel;

        private bool _isInitialized;
        private IDisposable _disposable;

        public LowFreqSignalStreamingChannel
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

            DevelopmentOnlyLogger.Log($"<color=cyan>[{nameof(LowFreqSignalStreamingChannel)}] Initialized.</color>");
        }

        public void Dispose()
        {
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            await _signalStreamingChannel.DisposeAsync();
            _disposable?.Dispose();
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannel)}] Disposed.</color>");
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

        public void Send<T>(T signal) where T : ILowFreqSignal
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannel)}] SendSignal</color>");

            var signalType = (int)SignalType.Unknown;

            if (typeof(T) == TypeOfDestroyObjectSignal)
            {
                signalType = (int)SignalType.DestroyObject;
            }
            else if (typeof(T) == TypeOfTextMessageSignal)
            {
                signalType = (int)SignalType.TextMessage;
            }

            if (signalType < 0) throw new ArgumentException($"Unknown signal type");

            var sendOptions = new SendOptions()
            {
                BroadcastingType = BroadcastingType.All,
                BufferingType = BufferingType.DoNotBuffering,
                Reliability = true,
            };

            _signalStreamingChannel.Send<T>(signal, signalType, sendOptions);
        }

        public ReadOnlySequence<T> ReadIncomingSignals<T>() where T : ILowFreqSignal
        {
            // NOTE: This is a workaround to avoid boxing of value types.
            // References:
            //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
            //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
            //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
            //
            if (typeof(T) == TypeOfDestroyObjectSignal)
            {
                if (_incomingDestroyObjectSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingDestroyObjectSignalBuffer.ToArray();
                var sequence = new ReadOnlySequence<DestroyObjectSignal>(segment);

                var convertFunc = (Func<ReadOnlySequence<DestroyObjectSignal>, ReadOnlySequence<T>>)(object)s_GetDestroyObjectSignalSequence;
                return convertFunc.Invoke(sequence);
            }
            else if (typeof(T) == TypeOfTextMessageSignal)
            {
                if (_incomingTextMessageSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingTextMessageSignalBuffer.ToArray();
                var sequence = new ReadOnlySequence<TextMessageSignal>(segment);

                var convertFunc = (Func<ReadOnlySequence<TextMessageSignal>, ReadOnlySequence<T>>)(object)s_GetTextMessageSignalSequence;
                return convertFunc.Invoke(sequence);
            }

            return ReadOnlySequence<T>.Empty;
        }

        public void DeleteIncomingSignals<T>(long count) where T : ILowFreqSignal
        {
            if (typeof(T) == TypeOfDestroyObjectSignal)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingDestroyObjectSignalBuffer.TryDequeue(out _);
                }
            }
            else if (typeof(T) == TypeOfTextMessageSignal)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingTextMessageSignalBuffer.TryDequeue(out _);
                }
            }
        }

        // NOTE: This is a workaround to avoid boxing of value types.
        // References:
        //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
        //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
        //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
        //
        private static Func<ReadOnlySequence<DestroyObjectSignal>, ReadOnlySequence<DestroyObjectSignal>> s_GetDestroyObjectSignalSequence = (param) => param;
        private static Func<ReadOnlySequence<TextMessageSignal>, ReadOnlySequence<TextMessageSignal>> s_GetTextMessageSignalSequence = (param) => param;

        private void HandleTransportedSignal((int SignalId, ReadOnlySequence<byte> Payload) data)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannel)}] HandleTransportMessage</color>");

            if (data.SignalId == (int)SignalType.DestroyObject)
            {
                var signal = _messageSerializer.Deserialize<DestroyObjectSignal>(data.Payload);
                _incomingDestroyObjectSignalBuffer.Enqueue(signal);
            }
            else if (data.SignalId == (int)SignalType.TextMessage)
            {
                var signal = _messageSerializer.Deserialize<TextMessageSignal>(data.Payload);
                _incomingTextMessageSignalBuffer.Enqueue(signal);
            }
        }
    }
}
