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
            Assert.AreEqual(16, policy.ImportantMaxPerFrame);
            Assert.AreEqual(256, policy.QueueCapacityPerPhase);
        }

        [Test]
        public void Constructor_WhenBudgetOrWindowInvalid_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(0f, 1f, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 0f, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 0f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(float.NaN, 1f, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, float.NaN, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, float.NaN, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(float.PositiveInfinity, 1f, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, float.PositiveInfinity, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, float.PositiveInfinity, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(float.NegativeInfinity, 1f, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, float.NegativeInfinity, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, float.NegativeInfinity, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 0, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 0, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 30, 0, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 30, 101, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 30, 20, 0, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 30, 20, 16, 0));
        }

        [Test]
        public void GetPhaseBudgetMs_ReturnsPhaseSpecificValue()
        {
            var policy = new FrameBudgetPolicy(2f, 3f, 4f, 2, 30, 20, 8, 128);

            Assert.AreEqual(2f, policy.GetPhaseBudgetMs(FramePhase.Update));
            Assert.AreEqual(3f, policy.GetPhaseBudgetMs(FramePhase.LateUpdate));
            Assert.AreEqual(4f, policy.GetPhaseBudgetMs(FramePhase.FixedUpdate));
        }

        [Test]
        public void GetPhaseBudgetMs_WhenPhaseIsUnknown_ThrowsArgumentOutOfRangeException()
        {
            var policy = FrameBudgetPolicy.Default;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = policy.GetPhaseBudgetMs((FramePhase)255));
        }
    }
}
