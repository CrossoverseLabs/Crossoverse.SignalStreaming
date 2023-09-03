using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using Crossoverse.Toolkit.Transports;
using Crossoverse.Toolkit.Serialization;
using Crossoverse.SignalStreaming;
using Crossoverse.SignalStreaming.HighFreqSignal;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;

namespace Crossoverse.SignalStreaming.Infrastructure
{
    public sealed class HighFreqSignalStreamingChannelV2 : IHighFreqSignalStreamingChannelV2
    {
        public string Id => _id;
        public SignalType SignalType => SignalType.HighFreqSignal;
        public StreamingType StreamingType => StreamingType.Bidirectional;

        public bool IsConnected => _isConnected;

        public IBufferedSubscriber<bool> ConnectionStateSubscriber { get; }

        private static readonly Type TYPE_OF_OBJECT_POSE_SIGNAL = typeof(ObjectPoseSignal);
        private readonly ConcurrentQueue<ObjectPoseSignal> _incomingObjectPoseSignalBuffer = new(); // TODO: Optimization

        private readonly IDisposableBufferedPublisher<bool> _connectionStatePublisher;

        private readonly IMessageSerializer _messageSerializer = new MessagePackMessageSerializer();
        private readonly ITransport _transport;
        private readonly string _id;

        private bool _isConnected;
        private bool _initialized;

        public HighFreqSignalStreamingChannelV2
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
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(HighFreqSignalStreamingChannelV2)}] Disposed.</color>");
        }

        public async UniTask<bool> ConnectAsync(CancellationToken token = default)
        {
            if (!_initialized) Initialize();

            if (_isConnected)
            {
                DevelopmentOnlyLogger.Log($"<color=orange>[{nameof(HighFreqSignalStreamingChannelV2)}] Already connected.</color>");
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

        public void Send<T>(T signal) where T : IHighFreqSignal
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(HighFreqSignalStreamingChannelV2)}] SendEvent</color>");

            var signalId = signal switch
            {
                ObjectPoseSignal => (int) SignalType.ObjectPose,
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
                Reliability = false,
            };

            _transport.Send(buffer.WrittenSpan.ToArray(), sendOptions);
        }

        public ReadOnlySequence<T> ReadIncomingSignals<T>() where T : IHighFreqSignal
        {
            // References:
            //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
            //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
            //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
            //
            if (typeof(T) == TYPE_OF_OBJECT_POSE_SIGNAL)
            {
                if (_incomingObjectPoseSignalBuffer.IsEmpty) return ReadOnlySequence<T>.Empty;

                var segment = _incomingObjectPoseSignalBuffer.ToArray(); // TODO: Optimization
                var sequence = new ReadOnlySequence<ObjectPoseSignal>(segment);
                var convertFunc = (Func<ReadOnlySequence<ObjectPoseSignal>, ReadOnlySequence<T>>)(object)s_GetObjectPoseSignalSequence;
                return convertFunc.Invoke(sequence);
            }

            return ReadOnlySequence<T>.Empty;
        }

        public void DeleteIncomingSignals<T>(long count) where T : IHighFreqSignal
        {
            if (typeof(T) == TYPE_OF_OBJECT_POSE_SIGNAL)
            {
                for (var i = 0; i < count; i++)
                {
                    _incomingObjectPoseSignalBuffer.TryDequeue(out _);
                }
            }
        }

        // References:
        //  - https://cactuaroid.hatenablog.com/entry/2021/07/31/234125
        //  - https://stackoverflow.com/questions/29997500/how-to-avoid-boxing-of-value-types
        //  - https://stackoverflow.com/questions/45507393/primitive-type-conversion-in-generic-method-without-boxing/45508419#45508419
        //
        private static Func<ReadOnlySequence<ObjectPoseSignal>, ReadOnlySequence<ObjectPoseSignal>> s_GetObjectPoseSignalSequence = (param) => param;

        private void HandleTransportMessage(byte[] serializedMessage)
        {
            DevelopmentOnlyLogger.Log($"<color=lime>[{nameof(HighFreqSignalStreamingChannelV2)}] HandleTransportMessage</color>");

            var messagePackReader = new MessagePackReader(serializedMessage);

            var arrayLength = messagePackReader.ReadArrayHeader();
            if (arrayLength != 3)
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(HighFreqSignalStreamingChannelV2)}] The received message is unsupported format.");
            }

            var signalId = messagePackReader.ReadInt32();
            var transportClientId = messagePackReader.ReadInt32();
            var offset = (int)messagePackReader.Consumed;

            if ((int)SignalType.ObjectPose == signalId)
            {
                var signal = _messageSerializer.Deserialize<ObjectPoseSignal>(new ReadOnlySequence<byte>(serializedMessage, offset, serializedMessage.Length - offset));
                _incomingObjectPoseSignalBuffer.Enqueue(signal);
            }
            else
            {
                DevelopmentOnlyLogger.LogError($"[{nameof(HighFreqSignalStreamingChannelV2)}] The received message is unsupported format.");
            }
        }
    }
}
