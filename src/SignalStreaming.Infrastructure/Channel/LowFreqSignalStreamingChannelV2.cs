using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Crossoverse.Toolkit.Transports;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.SignalStreaming;
using Crossoverse.SignalStreaming.LowFreqSignal;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class LowFreqSignalStreamingChannelV2 : ILowFreqSignalStreamingChannelV2
    {
        public string Id => _id;
        public SignalType SignalType => SignalType.LowFreqSignal;
        public StreamingType StreamingType => StreamingType.Bidirectional;

        public bool IsConnected => _isConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }

        private static readonly Type TYPE_OF_TEXT_MESSAGE_SIGNAL = typeof(TextMessageSignal);
        private static readonly Type TYPE_OF_DESTROY_OBJECT_SIGNAL = typeof(DestroyObjectSignal);
        private readonly ConcurrentQueue<TextMessageSignal> _incomingTextMessageSignalBuffer = new();
        private readonly ConcurrentQueue<DestroyObjectSignal> _incomingDestroyObjectSignalBuffer = new();

        private readonly IDisposableBufferedPublisher<bool> _connectionStatePublisher;

        private readonly IMessageSerializer _messageSerializer = new MessagePackMessageSerializer();
        private readonly ITransport _transport;
        private readonly string _id;

        private bool _isConnected;
        private bool _initialized;

        public LowFreqSignalStreamingChannelV2
        (
            string id,
            ITransport transport,
            EventFactory eventFactory
        )
        {
            _id = id;
            _transport = transport;
            (_connectionStatePublisher, ConnectionStateSubscriber) = eventFactory.CreateBufferedEvent<bool>(_isConnected);
        }

        public void Initialize()
        {
            _transport.OnReceiveMessage += HandleTransportMessage;
            _initialized = true;
        }

        public void Dispose()
        {
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            _transport.OnReceiveMessage -= HandleTransportMessage;
            await DisconnectAsync();
            _connectionStatePublisher.Dispose();
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannelV2)}] Disposed.</color>");
        }

        public async UniTask<bool> ConnectAsync(CancellationToken token = default)
        {
            if (!_initialized) Initialize();

            if (_isConnected)
            {
                DevelopmentOnlyLogger.Log($"<color=orange>[{nameof(LowFreqSignalStreamingChannelV2)}] Already connected.</color>");
                return true;
            }

            _isConnected = await _transport.ConnectAsync(_id);
            _connectionStatePublisher.Publish(_isConnected);

            return _isConnected;
        }

        public async UniTask DisconnectAsync()
        {
            if (!_isConnected) return;

            await _transport.DisconnectAsync();
            _isConnected = false;

            _connectionStatePublisher.Publish(_isConnected);
        }

        public void Send<T>(T signal) where T : ILowFreqSignal
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannelV2)}] SendEvent</color>");

            var signalId = signal switch
            {
                TextMessageSignal _ => (int) SignalType.TextMessage,
                DestroyObjectSignal _ => (int) SignalType.DestroyObject,
                _ => -1,
            };

            if (signalId < 0) throw new ArgumentException($"Cannot send signal: {signal.GetType().Name}");

            using var buffer = ArrayPoolBufferWriter.RentThreadStaticWriter();

            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(3);
            writer.Write(signalId);
            writer.Write(_transport.ClientId);
            writer.Flush();

            _messageSerializer.Serialize(buffer, signal);

            var sendOptions = new SendOptions()
            {
                BroadcastingType = BroadcastingType.All,
                BufferingType = BufferingType.DoNotBuffering,
                Reliability = true,
            };

            _transport.Send(buffer.WrittenSpan.ToArray(), sendOptions);
        }

        public ReadOnlySequence<T> ReadIncomingSignals<T>() where T : ILowFreqSignal
        {
            // References:
            //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
            //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
            //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
            //
            if (typeof(T) == TYPE_OF_DESTROY_OBJECT_SIGNAL)
            {
                if (_incomingDestroyObjectSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingDestroyObjectSignalBuffer.ToArray();
                var sequence = new ReadOnlySequence<DestroyObjectSignal>(segment);
                var convertFunc = (Func<ReadOnlySequence<DestroyObjectSignal>, ReadOnlySequence<T>>)(object)s_GetDestroyObjectSignalSequence;
                return convertFunc.Invoke(sequence);
            }
            else if (typeof(T) == TYPE_OF_TEXT_MESSAGE_SIGNAL)
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
            if (typeof(T) == TYPE_OF_DESTROY_OBJECT_SIGNAL)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingDestroyObjectSignalBuffer.TryDequeue(out _);
                }
            }
            else if (typeof(T) == TYPE_OF_TEXT_MESSAGE_SIGNAL)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingTextMessageSignalBuffer.TryDequeue(out _);
                }
            }
        }

        // References:
        //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
        //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
        //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
        //
        private static Func<ReadOnlySequence<DestroyObjectSignal>, ReadOnlySequence<DestroyObjectSignal>> s_GetDestroyObjectSignalSequence = (param) => param;
        private static Func<ReadOnlySequence<TextMessageSignal>, ReadOnlySequence<TextMessageSignal>> s_GetTextMessageSignalSequence = (param) => param;

        private void HandleTransportMessage(byte[] serializedMessage)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannelV2)}] HandleTransportMessage</color>");

            var messagePackReader = new MessagePackReader(serializedMessage);

            var arrayLength = messagePackReader.ReadArrayHeader();
            if (arrayLength != 3)
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(LowFreqSignalStreamingChannelV2)}] The received message is unsupported format.");
            }

            var signalId = messagePackReader.ReadInt32();
            var transportClientId = messagePackReader.ReadInt32();
            var offset = (int)messagePackReader.Consumed;

            if ((int)SignalType.TextMessage == signalId)
            {
                var signal = _messageSerializer.Deserialize<TextMessageSignal>(new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset));
                _incomingTextMessageSignalBuffer.Enqueue(signal);
            }
            else
            if ((int)SignalType.DestroyObject == signalId)
            {
                var signal = _messageSerializer.Deserialize<DestroyObjectSignal>(new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset));
                _incomingDestroyObjectSignalBuffer.Enqueue(signal);
            }
            else
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(LowFreqSignalStreamingChannelV2)}] The received message is unsupported format.");
            }
        }
    }
}
