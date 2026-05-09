namespace Change.Runtime.Net
{
    public readonly struct NetworkCommandEnvelope
    {
        public NetworkCommandEnvelope(string operationKey, int payload)
        {
            OperationKey = operationKey;
            Payload = payload;
        }

        public string OperationKey { get; }
        public int Payload { get; }
    }
}
