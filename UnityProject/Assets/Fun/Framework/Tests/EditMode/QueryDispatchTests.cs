using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class QueryDispatchTests
    {
        private readonly struct GetScoreQuery : IQuery<int>
        {
        }

        private sealed class ScoreState
        {
            public int Value;
        }

        private sealed class GetScoreQueryHandler : IQueryHandler<GetScoreQuery, int>
        {
            private readonly ScoreState _state;

            public GetScoreQueryHandler(ScoreState state)
            {
                _state = state;
            }

            public int Handle(in GetScoreQuery query)
            {
                return _state.Value;
            }
        }

        [Test]
        public void Query_ReturnsResultFromRegisteredHandler()
        {
            var state = new ScoreState { Value = 27 };
            var bus = new CqrsBus();

            bus.RegisterQuery(new GetScoreQueryHandler(state));
            bus.Freeze();

            var result = bus.Query<GetScoreQuery, int>(new GetScoreQuery());

            Assert.AreEqual(27, result);
        }
    }
}
