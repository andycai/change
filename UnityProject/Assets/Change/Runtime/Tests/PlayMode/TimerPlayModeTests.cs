using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.Tests.PlayMode
{
    public sealed class TimerPlayModeTests
    {
        private const string DriverGameObjectName = "[Change.Timer]";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = 0;

            var driver = GameObject.Find(DriverGameObjectName);
            if (driver != null)
            {
                UnityEngine.Object.Destroy(driver);
            }

            yield return null;
        }

        [Test]
        public void DelayAsync_WhenCanceledBeforeAwait_GetResultThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            TimerAwaiter awaiter = global::Change.Runtime.Timer.DelayAsync(1f, cts.Token, true).GetAwaiter();

            Assert.IsTrue(awaiter.IsCompleted);
            var ex = CaptureGetResultException(awaiter);
            Assert.IsInstanceOf<OperationCanceledException>(ex);
        }

        [Test]
        public void DelayAsync_WhenCanceledAfterOnCompleted_ContinuationRunsWithoutFrameTick()
        {
            var cts = new CancellationTokenSource();
            TimerAwaiter awaiter = global::Change.Runtime.Timer.DelayAsync(10f, cts.Token, true).GetAwaiter();
            bool resumed = false;

            awaiter.OnCompleted(() => resumed = true);
            cts.Cancel();

            Assert.IsTrue(SpinWait.SpinUntil(() => resumed, 200), "Continuation should resume immediately when cancellation is requested.");
            var ex = CaptureGetResultException(awaiter);
            Assert.IsInstanceOf<OperationCanceledException>(ex);
        }

        [Test]
        public void Delay_WhenSecondsInvalid_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.Delay(float.NaN, () => { }, CancellationToken.None, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.Delay(float.NegativeInfinity, () => { }, CancellationToken.None, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.Delay(-0.01f, () => { }, CancellationToken.None, true));
        }

        [Test]
        public void Repeat_WhenIntervalInvalid_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.Repeat(float.NaN, () => { }, CancellationToken.None, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.Repeat(float.PositiveInfinity, () => { }, CancellationToken.None, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.Repeat(-0.01f, () => { }, CancellationToken.None, true));
        }

        [Test]
        public void DelayAsync_WhenSecondsInvalid_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.DelayAsync(float.NaN, CancellationToken.None, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.DelayAsync(float.PositiveInfinity, CancellationToken.None, true));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                global::Change.Runtime.Timer.DelayAsync(-0.01f, CancellationToken.None, true));
        }

        [Test]
        public void Delay_WhenCalledFromBackgroundThread_ThrowsInvalidOperationException()
        {
            Exception captured = null;
            using var done = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                try
                {
                    global::Change.Runtime.Timer.Delay(0.1f, () => { }, CancellationToken.None, true);
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    done.Set();
                }
            });

            thread.Start();
            Assert.IsTrue(done.Wait(1000), "Background thread did not complete in time.");
            thread.Join();

            Assert.IsNotNull(captured);
            Assert.IsInstanceOf<InvalidOperationException>(captured);
            StringAssert.Contains("main thread", captured.Message);
        }

        [UnityTest]
        public IEnumerator DelayAsync_WhenCanceledWhileWaiting_ContinuationResumesAndGetResultThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            TimerAwaiter awaiter = global::Change.Runtime.Timer.DelayAsync(10f, cts.Token, true).GetAwaiter();
            bool resumed = false;

            awaiter.OnCompleted(() => resumed = true);

            yield return new WaitForSecondsRealtime(0.05f);
            cts.Cancel();

            yield return WaitUntil(() => resumed, 1f, "DelayAsync continuation did not resume after cancellation.");

            var ex = CaptureGetResultException(awaiter);
            Assert.IsInstanceOf<OperationCanceledException>(ex);
        }

        [UnityTest]
        public IEnumerator Delay_WhenCallbackSchedulesAnotherDelay_DoesNotBreakDispatchLoop()
        {
            bool firstTriggered = false;
            bool secondTriggered = false;
            bool hadCollectionModified = false;

            Application.LogCallback callback = (condition, _, type) =>
            {
                if (type != LogType.Exception && type != LogType.Error)
                {
                    return;
                }

                if (condition != null && condition.Contains("Collection was modified"))
                {
                    hadCollectionModified = true;
                }
            };

            Application.logMessageReceived += callback;

            try
            {
                global::Change.Runtime.Timer.Delay(0f, () =>
                {
                    firstTriggered = true;
                    global::Change.Runtime.Timer.Delay(0f, () => secondTriggered = true, CancellationToken.None, true);
                }, CancellationToken.None, true);

                yield return WaitUntil(() => secondTriggered, 1f, "Nested delay callback was not executed.");

                Assert.IsTrue(firstTriggered);
                Assert.IsFalse(hadCollectionModified);
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }
        }

        [UnityTest]
        public IEnumerator Delay_WhenDriverCreated_IsMovedToDontDestroyOnLoadScene()
        {
            var handle = global::Change.Runtime.Timer.Delay(0.5f, () => { }, CancellationToken.None, true);
            try
            {
                yield return null;

                var driver = GameObject.Find(DriverGameObjectName);
                Assert.IsNotNull(driver);
                Assert.AreEqual("DontDestroyOnLoad", driver.scene.name);
            }
            finally
            {
                handle.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Repeat_WhenFrameOvershootsInterval_PreservesElapsedRemainder()
        {
            Time.captureFramerate = 10;

            int invokeCount = 0;
            var handle = global::Change.Runtime.Timer.Repeat(0.15f, () => invokeCount++, CancellationToken.None, true);
            try
            {
                yield return null;
                Assert.AreEqual(0, invokeCount, "First frame should only accumulate elapsed time.");

                yield return null;
                Assert.AreEqual(1, invokeCount, "Second frame should trigger the first callback.");

                yield return null;
                Assert.AreEqual(2, invokeCount, "Third frame should trigger again when overshoot remainder is preserved.");
            }
            finally
            {
                handle.Dispose();
                Time.captureFramerate = 0;
            }
        }

        [UnityTest]
        public IEnumerator Repeat_WhenDisposed_StopsFurtherCallbacks()
        {
            int invokeCount = 0;
            var handle = global::Change.Runtime.Timer.Repeat(0f, () => invokeCount++, CancellationToken.None, true);

            yield return WaitUntil(() => invokeCount > 0, 1f, "Repeat callback never invoked before dispose.");

            int countAtDispose = invokeCount;
            handle.Dispose();

            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(countAtDispose, invokeCount, "Repeat callback should stop after dispose.");
        }

        [UnityTest]
        public IEnumerator Repeat_WhenCallbackThrows_StopsFurtherCallbacks()
        {
            int invokeCount = 0;
            var handle = global::Change.Runtime.Timer.Repeat(0f, () =>
            {
                invokeCount++;
                throw new InvalidOperationException("repeat boom");
            }, CancellationToken.None, true);
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("repeat boom"));

                yield return null;
                int countAfterThrow = invokeCount;
                Assert.AreEqual(1, countAfterThrow, "Repeat should invoke once before being stopped.");

                yield return null;
                yield return null;
                Assert.AreEqual(countAfterThrow, invokeCount, "Repeat should stop after callback exception.");
            }
            finally
            {
                handle.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator EveryFrame_WhenCallbackThrows_StopsFurtherCallbacks()
        {
            int invokeCount = 0;
            var handle = global::Change.Runtime.Timer.EveryFrame((_, _) =>
            {
                invokeCount++;
                throw new InvalidOperationException("frame boom");
            }, CancellationToken.None, true);
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("frame boom"));

                yield return null;
                int countAfterThrow = invokeCount;
                Assert.AreEqual(1, countAfterThrow, "EveryFrame should invoke once before being stopped.");

                yield return null;
                yield return null;
                Assert.AreEqual(countAfterThrow, invokeCount, "EveryFrame should stop after callback exception.");
            }
            finally
            {
                handle.Dispose();
            }
        }

        private static Exception CaptureGetResultException(TimerAwaiter awaiter)
        {
            try
            {
                awaiter.GetResult();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string timeoutMessage)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), timeoutMessage);
        }
    }
}
