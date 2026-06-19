using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class ModeConflictTests
    {
        private readonly struct SelfHandlingCommand : ISelfHandlingCommand
        {
            public void Execute() { }
        }

        private readonly struct PlainCommand : ICommand
        {
            public int Value { get; }
            public PlainCommand(int value) { Value = value; }
        }

        private sealed class PlainHandler : ICommandHandler<PlainCommand>
        {
            public void Handle(in PlainCommand command) { }
        }

        private sealed class SelfHandlingHandler : ICommandHandler<SelfHandlingCommand>
        {
            public void Handle(in SelfHandlingCommand command) { }
        }

        private sealed class AsyncSelfHandlingHandler : IAsyncCommandHandler<SelfHandlingCommand>
        {
            public System.Threading.Tasks.Task ExecuteAsync(SelfHandlingCommand command)
                => System.Threading.Tasks.Task.CompletedTask;
        }

        [Test]
        public void RegisterCommand_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterCommand(new SelfHandlingHandler()));
        }

        [Test]
        public void RegisterAsyncCommand_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterAsyncCommand(new AsyncSelfHandlingHandler()));
        }

        [Test]
        public void RegisterCommand_OnPlainType_Succeeds()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(() => bus.RegisterCommand(new PlainHandler()));
        }

        private readonly struct SelfHandlingQuery : ISelfHandlingQuery<int>
        {
            public int Execute() => 0;
        }

        private sealed class SelfHandlingQueryHandler : IQueryHandler<SelfHandlingQuery, int>
        {
            public int Handle(in SelfHandlingQuery query) => 0;
        }

        [Test]
        public void RegisterQuery_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterQuery(new SelfHandlingQueryHandler()));
        }

        private sealed class AsyncSelfHandlingQueryHandler : IAsyncQueryHandler<SelfHandlingQuery, int>
        {
            public System.Threading.Tasks.Task<int> ExecuteAsync(SelfHandlingQuery query)
                => System.Threading.Tasks.Task.FromResult(0);
        }

        [Test]
        public void RegisterAsyncQuery_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterAsyncQuery(new AsyncSelfHandlingQueryHandler()));
        }
    }
}
