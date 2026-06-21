using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class QueryDispatchTests
    {
        private readonly struct GetScoreQuery : IQuery<int>
        {
            private readonly ScoreState _state;

            public GetScoreQuery(ScoreState state) { _state = state; }

            public int Query() => _state.Value;
        }

        private sealed class ScoreState
        {
            public int Value;
        }

        [Test]
        public void Ask_ReturnsFromSelfHandlingQuery()
        {
            var state = new ScoreState { Value = 27 };
            var bus = new CqrsBus();

            var result = bus.Ask<GetScoreQuery, int>(new GetScoreQuery(state));

            Assert.AreEqual(27, result);
        }

        [Test]
        public void SelfHandling_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new ScoreState { Value = 3 };
            var bus = new CqrsBus();
            var query = new GetScoreQuery(state);

            for (var i = 0; i < 1000; i++) bus.Ask<GetScoreQuery, int>(query);

            ForceFullGc();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = 0;
            for (var i = 0; i < 100000; i++) sum += bus.Ask<GetScoreQuery, int>(query);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(3 * 100000, sum);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
