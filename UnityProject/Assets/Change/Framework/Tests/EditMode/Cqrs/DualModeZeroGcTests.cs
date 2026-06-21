using System.Collections.Generic;
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
    }
}
