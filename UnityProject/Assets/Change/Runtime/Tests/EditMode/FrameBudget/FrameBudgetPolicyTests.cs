using System;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.FrameBudget
{
    public sealed class FrameBudgetPolicyTests
    {
        [Test]
        public void DefaultPolicy_UsesExpectedValues()
        {
            var policy = FrameBudgetPolicy.Default;

            Assert.AreEqual(2.5f, policy.UpdateBudgetMs);
            Assert.AreEqual(1.0f, policy.LateUpdateBudgetMs);
            Assert.AreEqual(0.5f, policy.FixedUpdateBudgetMs);
            Assert.AreEqual(3, policy.MaxDeferredFrames);
            Assert.AreEqual(30, policy.OverBudgetWindowFrames);
            Assert.AreEqual(20, policy.OverBudgetPercentThreshold);
        }

        [Test]
        public void Constructor_WhenBudgetOrWindowInvalid_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(0f, 1f, 1f, 3, 30, 20));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 0f, 1f, 3, 30, 20));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 0f, 3, 30, 20));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 0, 30, 20));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 0, 20));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 30, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 30, 101));
        }

        [Test]
        public void GetPhaseBudgetMs_ReturnsPhaseSpecificValue()
        {
            var policy = new FrameBudgetPolicy(2f, 3f, 4f, 2, 30, 20);

            Assert.AreEqual(2f, policy.GetPhaseBudgetMs(FramePhase.Update));
            Assert.AreEqual(3f, policy.GetPhaseBudgetMs(FramePhase.LateUpdate));
            Assert.AreEqual(4f, policy.GetPhaseBudgetMs(FramePhase.FixedUpdate));
        }
    }
}
