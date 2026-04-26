using System;
using System.Collections.Generic;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class DelegateProtobufCodec : IMessageCodec
    {
        private readonly Dictionary<Type, Delegate> _encoders = new Dictionary<Type, Delegate>();
        private readonly Dictionary<Type, Delegate> _decoders = new Dictionary<Type, Delegate>();

        public void Register<T>(Func<T, byte[]> encode, Func<byte[], T> decode)
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

        public byte[] Encode<TMessage>(TMessage message)
        {
            if (!_encoders.TryGetValue(typeof(TMessage), out var encoder))
            {
                throw new CodecOperationException(
                    "encode",
                    -1,
                    -1,
                    new InvalidOperationException($"No encoder registered for message type '{typeof(TMessage).FullName}'."));
            }

            try
            {
                var payload = ((Func<TMessage, byte[]>)encoder).Invoke(message);
                if (payload == null)
                {
                    throw new InvalidOperationException("Encoder returned null payload.");
                }

                return payload;
            }
            catch (Exception ex) when (ex is not CodecOperationException)
            {
                throw new CodecOperationException("encode", -1, -1, ex);
            }
        }

        public TMessage Decode<TMessage>(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!_decoders.TryGetValue(typeof(TMessage), out var decoder))
            {
                throw new CodecOperationException(
                    "decode",
                    -1,
                    -1,
                    new InvalidOperationException($"No decoder registered for message type '{typeof(TMessage).FullName}'."));
            }

            try
            {
                return ((Func<byte[], TMessage>)decoder).Invoke(payload);
            }
            catch (Exception ex) when (ex is not CodecOperationException)
            {
                throw new CodecOperationException("decode", -1, -1, ex);
            }
        }
    }
}
