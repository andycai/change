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

            global::Change.Runtime.FrameBudget.ResetForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Initialize_CreatesDontDestroyOnLoadDriver()
        {
            global::Change.Runtime.FrameBudget.Initialize(FrameBudgetPolicy.Default);
            yield return null;

            var go = GameObject.Find(DriverName);
            Assert.IsNotNull(go);
            Assert.AreEqual("DontDestroyOnLoad", go.scene.name);
        }

        [UnityTest]
        public IEnumerator ScheduleDeferred_WorkExecutesInLaterFrame()
        {
            global::Change.Runtime.FrameBudget.Initialize(FrameBudgetPolicy.Default);
            int value = 0;

            global::Change.Runtime.FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Deferred, "test", () => value = 7);

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
            global::Change.Runtime.FrameBudget.Initialize(FrameBudgetPolicy.Default);

            bool yielded = false;
            UniTask task = Run();

            yield return task.ToCoroutine();
            Assert.IsTrue(yielded);

            async UniTask Run()
            {
                int safety = 5;
                while (!global::Change.Runtime.FrameBudget.IsPhaseBudgetExceeded(FramePhase.Update) && safety-- > 0)
                {
                    global::Change.Runtime.FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Critical, "heavy", () =>
                    {
                        float end = Time.realtimeSinceStartup + 0.01f;
                        while (Time.realtimeSinceStartup < end)
                        {
                        }
                    });

                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                }

                Assert.IsTrue(global::Change.Runtime.FrameBudget.IsPhaseBudgetExceeded(FramePhase.Update));

                int frameBefore = Time.frameCount;
                await FrameBudgetUniTaskExtensions.YieldIfBudgetExceeded(FramePhase.Update);
                yielded = Time.frameCount > frameBefore;
            }
        }
    }
}
