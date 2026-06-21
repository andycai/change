using System;
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

        public float Query()
        {
            throw new NotSupportedException(
                "GetAttributeValueQry is dispatched through the GAS system, not the CQRS bus. " +
                "Use GasService.GetAttributeValue() instead.");
        }
    }
}
