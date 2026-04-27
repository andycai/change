using UnityEngine;

namespace Change.Runtime
{
    internal sealed class FrameBudgetDriver : MonoBehaviour
    {
        private FrameBudgetPolicy _policy;
        private FrameWorkScheduler _scheduler;
        private FramePerfRecorder _updateRecorder;
        private FramePerfRecorder _lateRecorder;
        private FramePerfRecorder _fixedRecorder;

        private bool _updateExceeded;
        private bool _lateExceeded;
        private bool _fixedExceeded;

        public void ApplyPolicy(in FrameBudgetPolicy policy)
        {
            _policy = policy;
            _scheduler = new FrameWorkScheduler(policy);
            _updateRecorder = new FramePerfRecorder(policy.OverBudgetWindowFrames, policy.OverBudgetPercentThreshold);
            _lateRecorder = new FramePerfRecorder(policy.OverBudgetWindowFrames, policy.OverBudgetPercentThreshold);
            _fixedRecorder = new FramePerfRecorder(policy.OverBudgetWindowFrames, policy.OverBudgetPercentThreshold);
        }

        public void Schedule(in FrameWorkItem item)
        {
            _scheduler.Enqueue(item);
        }

        public bool IsPhaseBudgetExceeded(FramePhase phase)
        {
            return phase switch
            {
                FramePhase.Update => _updateExceeded,
                FramePhase.LateUpdate => _lateExceeded,
                FramePhase.FixedUpdate => _fixedExceeded,
                _ => false,
            };
        }

        private void Update()
        {
            RunPhase(FramePhase.Update, _updateRecorder, ref _updateExceeded);
        }

        private void LateUpdate()
        {
            RunPhase(FramePhase.LateUpdate, _lateRecorder, ref _lateExceeded);
        }

        private void FixedUpdate()
        {
            RunPhase(FramePhase.FixedUpdate, _fixedRecorder, ref _fixedExceeded);
        }

        private void RunPhase(FramePhase phase, FramePerfRecorder recorder, ref bool exceededFlag)
        {
            float start = Time.realtimeSinceStartup;
            float budgetMs = _policy.GetPhaseBudgetMs(phase);

            _scheduler.RunPhase(phase, Time.frameCount, budgetMs);

            float costMs = (Time.realtimeSinceStartup - start) * 1000f;
            exceededFlag = costMs > budgetMs;

            recorder.RecordFrame(costMs, exceededFlag);
            FramePerfSnapshot snapshot = recorder.CreateSnapshot();
            if (snapshot.ShouldThrottle)
            {
                // MVP: throttling remains scheduler-policy driven, so this is informational for now.
            }
        }
    }
}
