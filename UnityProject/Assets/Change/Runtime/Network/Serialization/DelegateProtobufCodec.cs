using System;
using System.Collections.Generic;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class DelegateProtobufCodec : IMessageCodec
    {
        private delegate int EncodeDelegate<T>(T message, byte[] buffer, int offset);
        private delegate T DecodeDelegate<T>(byte[] buffer, int offset, int length);

        private readonly Dictionary<Type, Delegate> _encoders = new Dictionary<Type, Delegate>();
        private readonly Dictionary<Type, Delegate> _decoders = new Dictionary<Type, Delegate>();

        public void Register<T>(Func<T, byte[], int, int> encode, Func<byte[], int, int, T> decode)
        {
            if (encode == null)
            {
                throw new ArgumentNullException(nameof(encode));
            }

            if (decode == null)
            {
                throw new ArgumentNullException(nameof(decode));
            }

            var messageType = typeof(T);
            if (_encoders.ContainsKey(messageType) || _decoders.ContainsKey(messageType))
            {
                throw new InvalidOperationException($"Codec delegates already registered for message type '{messageType.FullName}'.");
            }

            _encoders[messageType] = encode;
            _decoders[messageType] = decode;
        }

        public int Encode<TMessage>(TMessage message, byte[] buffer, int offset, int cmdId)
        {
            if (!_encoders.TryGetValue(typeof(TMessage), out var encoder))
            {
                throw new CodecOperationException(
                    "encode",
                    cmdId,
                    -1,
                    new InvalidOperationException($"No encoder registered for message type '{typeof(TMessage).FullName}'."));
            }

            try
            {
                var func = (Func<TMessage, byte[], int, int>)encoder;
                return func(message, buffer, offset);
            }
            catch (Exception ex) when (ex is not CodecOperationException)
            {
                throw new CodecOperationException("encode", cmdId, -1, ex);
            }
        }

        public TMessage Decode<TMessage>(byte[] buffer, int offset, int length, int cmdId)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!_decoders.TryGetValue(typeof(TMessage), out var decoder))
            {
                throw new CodecOperationException(
                    "decode",
                    cmdId,
                    -1,
                    new InvalidOperationException($"No decoder registered for message type '{typeof(TMessage).FullName}'."));
            }

            try
            {
                var func = (Func<byte[], int, int, TMessage>)decoder;
                return func(buffer, offset, length);
            }
            catch (Exception ex) when (ex is not CodecOperationException)
            {
                throw new CodecOperationException("decode", cmdId, -1, ex);
            }
        }
    }
}
