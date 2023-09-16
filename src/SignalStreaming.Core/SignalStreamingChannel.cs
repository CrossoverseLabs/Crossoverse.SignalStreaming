using System;
using System.Buffers;
using System.Threading;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Toolkit.Transports;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;

namespace Crossoverse.SignalStreaming
{
    public sealed class SignalStreamingChannel : ISignalStreamingChannel
    {
        public string Id => _id;

        public bool IsConnected => _isConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }
        public ISubscriber<(int SignalId, ReadOnlySequence<byte> Payload)> OnSignalReceived { get; }

        private readonly IDisposableBufferedPublisher<bool> _connectionStatePublisher;
        private readonly IDisposablePublisher<(int SignalType, ReadOnlySequence<byte> Payload)> _signalPublisher;

        private readonly string _id;
        private readonly ITransport _transport;
        private readonly IMessageSerializer _messageSerializer;

        private bool _isConnected;
        private bool _isInitialized;

        public SignalStreamingChannel
        (
            string id,
            ITransport transport,
            IMessageSerializer messageSerializer,
            EventFactory eventFactory
        )
        {
            _id = id;
            _transport = transport;
            _messageSerializer = messageSerializer;
            (_connectionStatePublisher, ConnectionStateSubscriber) = eventFactory.CreateBufferedEvent<bool>(_isConnected);
            (_signalPublisher, OnSignalReceived) = eventFactory.CreateEvent<(int SignalId, ReadOnlySequence<byte> Payload)>();
        }

        public void Initialize()
        {
            if (_isInitialized) return;
            _transport.OnReceiveMessage += HandleTransportedMessage;
            _isInitialized = true;
        }

        public void Dispose()
        {
            DisposeAsync().Forget();
        }

        public async UniTask DisposeAsync()
        {
            await DisconnectAsync();
            _connectionStatePublisher.Dispose();
            _signalPublisher.Dispose();
            _transport.OnReceiveMessage -= HandleTransportedMessage;
        }

        public async UniTask<bool> ConnectAsync(CancellationToken token = default)
        {
            if (_isConnected) return true;

            if (!_isInitialized) Initialize();

            _isConnected = await _transport.ConnectAsync(_id); // TODO: CancellationToken
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

        public void Send<TSignal>(TSignal signal, int signalType, SendOptions sendOptions)
        {
            if (signalType < 0) throw new ArgumentException($"Cannot send signal: {signal.GetType().Name}");

            using var buffer = ArrayPoolBufferWriter.RentThreadStaticWriter();

            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(3);
            writer.Write(signalType);
            writer.Write(_transport.ClientId);
            writer.Flush();

            _messageSerializer.Serialize(buffer, signal);

            _transport.Send(buffer.WrittenSpan.ToArray(), sendOptions);
        }

        private void HandleTransportedMessage(byte[] serializedMessage)
        {
            var messagePackReader = new MessagePackReader(serializedMessage);

            var arrayLength = messagePackReader.ReadArrayHeader();
            if (arrayLength != 3)
            {
                throw new InvalidOperationException($"The received message is unsupported format.");
            }

            var signalType = messagePackReader.ReadInt32();
            var transportClientId = messagePackReader.ReadInt32();

            var offset = (int) messagePackReader.Consumed;

            var payload = new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset);

            _signalPublisher.Publish((signalType, payload));
        }
    }
}
