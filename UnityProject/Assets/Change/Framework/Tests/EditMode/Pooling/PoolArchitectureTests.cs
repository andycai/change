using System;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class PoolArchitectureTests
    {
        [Test]
        public void Collections_ObjectPoolType_MustNotExist()
        {
            var objectPoolType = Type.GetType("Change.Framework.Collections.Object" + "Pool`1, Change.Framework");
            Assert.IsNull(objectPoolType, "Collections layer must not expose the legacy pool type.");
        }

        [Test]
        public void Collections_IResettableType_MustNotExist()
        {
            var resettableType = Type.GetType("Change.Framework.Collections.IReset" + "table, Change.Framework");
            Assert.IsNull(resettableType, "Collections layer must not expose the legacy reset contract.");
        }
    }
}
