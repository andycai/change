using System;

namespace Change.Framework.Network
{
    public interface IMessageCodec
    {
        int Encode<TMessage>(TMessage message, byte[] buffer, int offset, int cmdId);

        TMessage Decode<TMessage>(byte[] buffer, int offset, int length, int cmdId);
    }
}
