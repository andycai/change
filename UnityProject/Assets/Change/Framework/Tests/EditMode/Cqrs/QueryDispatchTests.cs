using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class QueryDispatchTests
    {
        private readonly struct GetScoreQuery : IQuery<int>
        {
        }

        private readonly struct GetMultiResultQuery : IQuery<int>, IQuery<string>
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

        private readonly struct StructGetScoreQueryHandler : IQueryHandler<GetScoreQuery, int>
        {
            public int Handle(in GetScoreQuery query)
            {
                return 0;
            }
        }

        private sealed class GetMultiResultIntHandler : IQueryHandler<GetMultiResultQuery, int>
        {
            public int Handle(in GetMultiResultQuery query) => 1;
        }

        private sealed class GetMultiResultStringHandler : IQueryHandler<GetMultiResultQuery, string>
        {
            public string Handle(in GetMultiResultQuery query) => "x";
        }

        [Test]
        public void Query_ReturnsResultFromRegisteredHandler()
        {
            var state = new ScoreState { Value = 27 };
            var bus = new CqrsBus();

            bus.RegisterQuery(new GetScoreQueryHandler(state));

            var result = bus.Query<GetScoreQuery, int>(new GetScoreQuery());

            Assert.AreEqual(27, result);
        }

        [Test]
        public void Ask_ReturnsResultFromRegisteredHandler()
        {
            var state = new ScoreState { Value = 27 };
            var bus = new CqrsBus();

            bus.RegisterQuery(new GetScoreQueryHandler(state));

            var result = bus.Ask<GetScoreQuery, int>(new GetScoreQuery());

            Assert.AreEqual(27, result);
        }

        [Test]
        public void Query_WithoutRegistration_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();

            Assert.Throws<HandlerNotRegisteredException>(() => bus.Query<GetScoreQuery, int>(new GetScoreQuery()));
        }

        [Test]
        public void RegisterQuery_DuplicateRegistration_ThrowsDuplicateRegistrationException()
        {
            var state = new ScoreState();
            var bus = new CqrsBus();

            bus.RegisterQuery(new GetScoreQueryHandler(state));

            Assert.Throws<DuplicateRegistrationException>(() => bus.RegisterQuery(new GetScoreQueryHandler(state)));
        }

        [Test]
        public void RegisterQuery_NullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(() => bus.RegisterQuery<GetScoreQuery, int>(null));
        }

        [Test]
        public void RegisterQuery_StructHandler_ThrowsInvalidOperationException()
        {
            var bus = new CqrsBus();

            Assert.Throws<InvalidOperationException>(() => bus.RegisterQuery(new StructGetScoreQueryHandler()));
        }

        [Test]
        public void RegisterQuery_SameQueryType_DifferentResultType_ThrowsDuplicateRegistrationException()
        {
            var bus = new CqrsBus();
            bus.RegisterQuery(new GetMultiResultIntHandler());

            Assert.Throws<DuplicateRegistrationException>(
                () => bus.RegisterQuery(new GetMultiResultStringHandler()));
        }
    }
}
