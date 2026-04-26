namespace Change.Framework.Network
{
    public interface IMessageCodec
    {
        byte[] Encode<TMessage>(TMessage message);

        TMessage Decode<TMessage>(byte[] payload);
    }
}
