using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Crossoverse.SignalStreaming;
using Crossoverse.SignalStreaming.BufferedSignal;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.Toolkit.Transports;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class BufferedSignalStreamingChannel : IBufferedSignalStreamingChannel
    {
        public string Id => _id;
        public SignalType SignalType => SignalType.BufferedSignal;
        public StreamingType StreamingType => StreamingType.Bidirectional;

        public bool IsConnected => _isConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }

        private static readonly Type TYPE_OF_CREATE_OBJECT_SIGNAL = typeof(CreateObjectSignal);
        private readonly ConcurrentQueue<CreateObjectSignal> _incomingCreateObjectSignalBuffer = new();

        private readonly IDisposableBufferedPublisher<bool> _connectionStatePublisher;

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
                CreateObjectSignal _ => (int) SignalType.CreateObject,
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

        public void RemoveBufferedSignal<T>(Ulid signalGeneratedBy, object filterKey) where T : IBufferedSignal
        {
            var signalId = -1;

            if (typeof(T) == TYPE_OF_CREATE_OBJECT_SIGNAL)
            {
                signalId = (int) SignalType.CreateObject;
            }

            if (signalId < 0) throw new ArgumentException($"Unknown signal type");

            var bufferingKey = new BufferingKey()
            {
                FirstKey = signalId,
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

        public ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IBufferedSignal
        {
            // References:
            //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
            //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
            //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
            //
            if (typeof(T) == TYPE_OF_CREATE_OBJECT_SIGNAL)
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
            if (typeof(T) == TYPE_OF_CREATE_OBJECT_SIGNAL)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingCreateObjectSignalBuffer.TryDequeue(out _);
                }
            }
        }

        // References:
        //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
        //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
        //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
        //
        private static Func<ReadOnlySequence<CreateObjectSignal>, ReadOnlySequence<CreateObjectSignal>> s_GetCreateObjectSignalSequence = (param) => param;

        private void HandleTransportMessage(byte[] serializedMessage)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(BufferedSignalStreamingChannel)}] HandleTransportMessage</color>");

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
                _incomingCreateObjectSignalBuffer.Enqueue(signal);
            }
        }
    }
}
