using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct GetAttributeValueQry : IQuery<float>
    {
        public GetAttributeValueQry(string targetId, string attributeName)
        {
            TargetId = targetId; AttributeName = attributeName;
        }
        public string TargetId { get; }
        public string AttributeName { get; }
    }
}
