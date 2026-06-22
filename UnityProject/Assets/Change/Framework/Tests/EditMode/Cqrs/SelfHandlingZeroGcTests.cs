using System.Collections.Generic;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    /// <summary>
    /// Zero-GC allocation tests for self-handling struct commands/queries.
    /// Note: The dual-mode tests (Class handler + Struct self-handling) have been
    /// consolidated into pure self-handling struct tests as part of the CQRS
    /// simplification (FRD #2 → self-handling only).
    /// </summary>
    public class SelfHandlingZeroGcTests : ZeroGcTestBase
    {
        // 负向测试需要分配"累积存活"——追加到 List 使引用不被回收，
        // 这样 GC.GetTotalMemory 才能量出堆增长（单次覆写的引用会被 GC 回收，读作 0 增长）。
        private static readonly List<string> _accumulated = new List<string>();
        private static int _counter;

        [Test]
        public void AssertZeroGc_RejectsAllocatingAction()
        {
            Assert.Throws<AssertionException>(() =>
                AssertZeroGc(() => _accumulated.Add((_counter++).ToString())));
        }

        [Test]
        public void AssertZeroGc_PassesNoOpAction()
        {
            AssertZeroGc(() => { });
        }

        // —— 自处理 Struct 路径 0GC ——

        private sealed class SelfHandlingSink
        {
            public int Value;
        }

        private readonly struct SelfHandlingStructCommand : ICommand
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
