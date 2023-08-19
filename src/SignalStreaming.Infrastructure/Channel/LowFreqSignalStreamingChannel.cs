using System;
using System.Buffers; 
using System.Threading;
using Crossoverse.Toolkit.Transports;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Core.Domain.SignalStreaming;
using Crossoverse.Core.Domain.SignalStreaming.LowFreqSignal;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;

namespace Crossoverse.Core.Infrastructure.SignalStreaming
{
    public sealed class LowFreqSignalStreamingChannel : ILowFreqSignalStreamingChannel
    {
        public string Id => _id;
        public SignalType SignalType => SignalType.LowFreqSignal;
        public StreamingType StreamingType => StreamingType.Bidirectional;

        public bool IsConnected => _isConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }
        public ISubscriber<TextMessageSignal> OnTextMessageReceived { get; }
        public ISubscriber<DestroyObjectSignal> OnDestroyObjectSignalReceived { get; }

        private readonly IDisposableBufferedPublisher<bool> _connectionStatePublisher;
        private readonly IDisposablePublisher<TextMessageSignal> _textMessageSignalPublisher;
        private readonly IDisposablePublisher<DestroyObjectSignal> _destroyObjectSignalPublisher;

        private readonly IMessageSerializer _messageSerializer = new MessagePackMessageSerializer();
        private readonly ITransport _transport;
        private readonly string _id;

        private bool _isConnected;
        private bool _initialized;

        public LowFreqSignalStreamingChannel
        (
            string id,
            ITransport transport,
            EventFactory eventFactory
        )
        {
            _id = id;
            _transport = transport;
            (_connectionStatePublisher, ConnectionStateSubscriber) = eventFactory.CreateBufferedEvent<bool>(_isConnected);
            (_textMessageSignalPublisher, OnTextMessageReceived) = eventFactory.CreateEvent<TextMessageSignal>();
            (_destroyObjectSignalPublisher, OnDestroyObjectSignalReceived) = eventFactory.CreateEvent<DestroyObjectSignal>();
        }

        public void Initialize()
        {
            _transport.OnReceiveMessage += OnMessageReceived;
            _initialized = true;
        }

        public void Dispose()
        {
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            _transport.OnReceiveMessage -= OnMessageReceived;
            await DisconnectAsync();
            _connectionStatePublisher.Dispose();
            _textMessageSignalPublisher.Dispose();
            _destroyObjectSignalPublisher.Dispose();
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannel)}] Disposed.</color>");
        }

        public async UniTask<bool> ConnectAsync(CancellationToken token = default)
        {
            if (!_initialized) Initialize();

            if (_isConnected)
            {
                DevelopmentOnlyLogger.Log($"<color=orange>[{nameof(LowFreqSignalStreamingChannel)}] Already connected.</color>");
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
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannel)}] SendEvent</color>");

            var signalId = signal switch
            {
                TextMessageSignal _ => (int)SignalType.TextMessage,
                DestroyObjectSignal _ => (int)SignalType.DestroyObject,
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

        private void OnMessageReceived(byte[] serializedMessage)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(LowFreqSignalStreamingChannel)}] OnMessageReceived</color>");

            var messagePackReader = new MessagePackReader(serializedMessage);

            var arrayLength = messagePackReader.ReadArrayHeader();
            if (arrayLength != 3)
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(LowFreqSignalStreamingChannel)}] The received message is unsupported format.");
            }

            var signalId = messagePackReader.ReadInt32();
            var transportClientId = messagePackReader.ReadInt32();
            var offset = (int)messagePackReader.Consumed;

            if ((int)SignalType.TextMessage == signalId)
            {
                var signal = _messageSerializer.Deserialize<TextMessageSignal>(new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset));
                _textMessageSignalPublisher.Publish(signal);
            }
            else
            if ((int)SignalType.DestroyObject == signalId)
            {
                var signal = _messageSerializer.Deserialize<DestroyObjectSignal>(new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset));
                _destroyObjectSignalPublisher.Publish(signal);
            }
            else
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(LowFreqSignalStreamingChannel)}] The received message is unsupported format.");
            }
        }
    }
}
