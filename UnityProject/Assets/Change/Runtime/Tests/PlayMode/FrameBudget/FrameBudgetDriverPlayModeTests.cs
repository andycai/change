using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.Tests.PlayMode.FrameBudget
{
    public sealed class FrameBudgetDriverPlayModeTests
    {
        private const string DriverName = "[Change.FrameBudget]";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var go = GameObject.Find(DriverName);
            if (go != null)
            {
                Object.Destroy(go);
            }

            FrameBudget.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Initialize_CreatesDontDestroyOnLoadDriver()
        {
            FrameBudget.Initialize(FrameBudgetPolicy.Default);
            yield return null;

            var go = GameObject.Find(DriverName);
            Assert.IsNotNull(go);
            Assert.AreEqual("DontDestroyOnLoad", go.scene.name);
        }

        [UnityTest]
        public IEnumerator ScheduleDeferred_WorkExecutesInLaterFrame()
        {
            FrameBudget.Initialize(FrameBudgetPolicy.Default);
            int value = 0;

            FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Deferred, "test", () => value = 7);

            int safetyFrames = 10;
            while (value != 7 && safetyFrames-- > 0)
            {
                yield return null;
            }

            Assert.AreEqual(7, value);
        }

        [UnityTest]
        public IEnumerator YieldIfBudgetExceeded_WhenExceeded_YieldsToNextFrame()
        {
            FrameBudget.Initialize(FrameBudgetPolicy.Default);

            bool continued = false;
            UniTask task = Run();

            yield return null;
            Assert.IsFalse(continued);

            yield return task.ToCoroutine();
            Assert.IsTrue(continued);

            async UniTask Run()
            {
                FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Critical, "heavy", () =>
                {
                    float end = Time.realtimeSinceStartup + 0.01f;
                    while (Time.realtimeSinceStartup < end)
                    {
                    }
                });

                await UniTask.Yield(PlayerLoopTiming.Update);
                await FrameBudgetUniTaskExtensions.YieldIfBudgetExceeded(FramePhase.Update);
                continued = true;
            }
        }
    }
}
