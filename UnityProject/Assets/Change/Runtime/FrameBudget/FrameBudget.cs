using System;

namespace Change.Runtime
{
    public static class FrameBudget
    {
        private static FrameBudgetDriver _driver;

        public static void Initialize(FrameBudgetPolicy policy)
        {
            if (_driver != null)
            {
                _driver.ApplyPolicy(policy);
                return;
            }

            _driver = FrameBudgetBootstrap.EnsureDriver(policy);
        }

        public static void Schedule(FramePhase phase, FrameTaskPriority priority, string tag, Action callback)
        {
            if (_driver == null)
            {
                Initialize(FrameBudgetPolicy.Default);
            }

            _driver.Schedule(FrameWorkItem.Create(phase, priority, tag, callback));
        }

        public static bool IsPhaseBudgetExceeded(FramePhase phase)
        {
            return _driver != null && _driver.IsPhaseBudgetExceeded(phase);
        }

        internal static void ResetForTests()
        {
            _driver = null;
        }
    }
}
