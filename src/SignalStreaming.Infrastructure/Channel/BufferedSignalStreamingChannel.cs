using System;
using System.Buffers; 
using System.Threading;
using Crossoverse.Toolkit.Transports;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Core.Domain.SignalStreaming;
using Crossoverse.Core.Domain.SignalStreaming.BufferedSignal;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;

namespace Crossoverse.Core.Infrastructure.SignalStreaming
{
    public sealed class BufferedSignalStreamingChannel : IBufferedSignalStreamingChannel
    {
        public string Id => _id;
        public SignalType SignalType => SignalType.BufferedSignal;
        public StreamingType StreamingType => StreamingType.Bidirectional;

        public bool IsConnected => _isConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }
        public ISubscriber<CreateObjectSignal> OnCreateObjectSignalReceived { get; }

        private readonly IDisposableBufferedPublisher<bool> _connectionStatePublisher;
        private readonly IDisposablePublisher<CreateObjectSignal> _createObjectSignalPublisher;

        private readonly IMessageSerializer _messageSerializer = new MessagePackMessageSerializer();
        private readonly ITransport _transport;
        private readonly string _id;

        private bool _isConnected;
        private bool _initialized;

        public BufferedSignalStreamingChannel
        (
            string id,
            ITransport transport,
            EventFactory eventFactory
        )
        {
            _id = id;
            _transport = transport;
            (_connectionStatePublisher, ConnectionStateSubscriber) = eventFactory.CreateBufferedEvent<bool>(_isConnected);
            (_createObjectSignalPublisher, OnCreateObjectSignalReceived) = eventFactory.CreateEvent<CreateObjectSignal>();
        }

        public void Initialize()
        {
            _transport.OnReceiveMessage += OnTransportMessageReceived;
            _initialized = true;
        }

        public void Dispose()
        {
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            _transport.OnReceiveMessage -= OnTransportMessageReceived;
            await DisconnectAsync();
            _connectionStatePublisher.Dispose();
            _createObjectSignalPublisher.Dispose();
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] Disposed.</color>");
        }

        public async UniTask<bool> ConnectAsync(CancellationToken token = default)
        {
            if (!_initialized) Initialize();

            if (_isConnected)
            {
                DevelopmentOnlyLogger.Log($"<color=orange>[{nameof(BufferedSignalStreamingChannel)}] Already connected.</color>");
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

        public void Send<T>(T signal) where T : IBufferedSignal
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] SendSignal</color>");

            var signalId = signal switch
            {
                CreateObjectSignal _ => (int)SignalType.CreateObject,
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

            var bufferingKey = new BufferingKey()
            {
                FirstKey = signalId,
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

            _transport.Send(buffer.WrittenSpan.ToArray(), sendOptions);
        }

        public void RemoveBufferedSignal(SignalType signalType, Guid signalGeneratedBy, object filterKey)
        {
            var bufferingKey = new BufferingKey()
            {
                FirstKey = (int)signalType,
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

            _transport.Send(null, sendOptions);
        }

        private void OnTransportMessageReceived(byte[] serializedMessage)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] OnTransportMessageReceived</color>");

            var messagePackReader = new MessagePackReader(serializedMessage);

            var arrayLength = messagePackReader.ReadArrayHeader();
            if (arrayLength != 3)
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(BufferedSignalStreamingChannel)}] The received message is unsupported format.");
            }

            var signalId = messagePackReader.ReadInt32();
            var transportClientId = messagePackReader.ReadInt32();
            var offset = (int)messagePackReader.Consumed;

            if ((int)SignalType.CreateObject == signalId)
            {
                var signal = _messageSerializer.Deserialize<CreateObjectSignal>(new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset));
                _createObjectSignalPublisher.Publish(signal);
            }
        }
    }
}
