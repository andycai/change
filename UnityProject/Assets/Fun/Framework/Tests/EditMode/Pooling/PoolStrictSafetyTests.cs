using System;
using Fun.Framework.Pooling;
using NUnit.Framework;

namespace Fun.Framework.Tests.Pooling
{
    public class PoolStrictSafetyTests
    {
        private sealed class StrictPayload : IPoolable
        {
            public int ResetCount;

            public void Reset()
            {
                ResetCount++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<StrictPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<StrictPayload>.Clear();
        }

        [Test]
        public void Release_Null_FollowsBuildPolicy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<ArgumentNullException>(() => Pool<StrictPayload>.Release(null));
#else
            Assert.DoesNotThrow(() => Pool<StrictPayload>.Release(null));
#endif
        }

        [Test]
        public void Release_DoubleRelease_FollowsBuildPolicy()
        {
            var item = Pool<StrictPayload>.Get();
            Pool<StrictPayload>.Release(item);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => Pool<StrictPayload>.Release(item));
#else
            Assert.DoesNotThrow(() => Pool<StrictPayload>.Release(item));
#endif
        }

        [Test]
        public void Release_ForeignInstance_FollowsBuildPolicy()
        {
            var foreign = new StrictPayload();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => Pool<StrictPayload>.Release(foreign));
#else
            Assert.DoesNotThrow(() => Pool<StrictPayload>.Release(foreign));
#endif
        }
    }
}
