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

        [Test]
        public void Query_WithoutRegistration_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

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
        public void RegisterQuery_AfterFreeze_ThrowsRegistryFrozenException()
        {
            var state = new ScoreState();
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.RegisterQuery(new GetScoreQueryHandler(state)));
        }

        [Test]
        public void RegisterQuery_AfterFreeze_WithNullHandler_ThrowsRegistryFrozenException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.RegisterQuery<GetScoreQuery, int>(null));
        }

        [Test]
        public void Query_BeforeFreeze_ThrowsInvalidOperationException()
        {
            var state = new ScoreState();
            var bus = new CqrsBus();
            bus.RegisterQuery(new GetScoreQueryHandler(state));

            Assert.Throws<InvalidOperationException>(() => bus.Query<GetScoreQuery, int>(new GetScoreQuery()));
        }

        [Test]
        public void RegisterQuery_NullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(() => bus.RegisterQuery<GetScoreQuery, int>(null));
        }
    }
}
