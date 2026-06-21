using System.Collections.Generic;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class DualModeZeroGcTests : ZeroGcTestBase
    {
        // 负向测试需要分配"累积存活"——追加到 List 使引用不被回收，
        // 这样 GC.GetTotalMemory 才能量出堆增长（单次覆写的引用会被 GC 回收，读作 0 增长）。
        private static readonly List<string> _accumulated = new List<string>();
        private static int _counter;

        [Test]
        public void AssertZeroGc_RejectsAllocatingAction()
        {
            // 负向测试：每次分配一个新字符串并累积，断言应失败——证明基类能检出堆增长。
            Assert.Throws<AssertionException>(() =>
                AssertZeroGc(() => _accumulated.Add((_counter++).ToString())));
        }

        [Test]
        public void AssertZeroGc_PassesNoOpAction()
        {
            AssertZeroGc(() => { });
        }

        // —— 任务 2: Class 池化路径 0GC ——
        private readonly struct PoolableClassCommand : ICommand { }

        private sealed class PoolableClassCommandHandler
            : ICommandHandler<PoolableClassCommand>, IPoolable
        {
            public int Count;
            public void Handle(in PoolableClassCommand command) { Count++; }
            public void Reset() { }
        }

        [Test]
        public void Send_ClassPooledHandler_HotPath_AllocatesZeroBytes()
        {
            var handler = new PoolableClassCommandHandler();
            var bus = new CqrsBus();
            bus.RegisterCommand(handler);
            var command = new PoolableClassCommand();

            AssertZeroGc(() => bus.Send(in command));

            Assert.AreEqual(WarmupIterations + MeasuredIterations, handler.Count);
        }

        // —— 任务 3: Struct 自处理路径 0GC ——
        private sealed class SelfHandlingSink
        {
            public int Value;
        }

        private readonly struct SelfHandlingStructCommand : ISelfHandlingCommand
        {
            private readonly SelfHandlingSink _sink;
            public SelfHandlingStructCommand(SelfHandlingSink sink) { _sink = sink; }
            public void Execute() { _sink.Value++; }
        }

        [Test]
        public void Send_SelfHandlingStruct_HotPath_AllocatesZeroBytes()
        {
            var sink = new SelfHandlingSink();
            var bus = new CqrsBus();
            var command = new SelfHandlingStructCommand(sink);

            AssertZeroGc(() => bus.Send(in command));

            Assert.AreEqual(WarmupIterations + MeasuredIterations, sink.Value);
        }
    }
}
