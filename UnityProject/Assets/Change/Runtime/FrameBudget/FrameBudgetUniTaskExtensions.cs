using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime
{
    public static class FrameBudgetUniTaskExtensions
    {
        public static UniTask YieldIfBudgetExceeded(FramePhase phase, CancellationToken cancellationToken = default)
        {
            if (!FrameBudget.IsPhaseBudgetExceeded(phase))
            {
                return UniTask.CompletedTask;
            }

            PlayerLoopTiming timing = phase switch
            {
                FramePhase.Update => PlayerLoopTiming.Update,
                FramePhase.LateUpdate => PlayerLoopTiming.LastPostLateUpdate,
                FramePhase.FixedUpdate => PlayerLoopTiming.FixedUpdate,
                _ => PlayerLoopTiming.Update,
            };

            return UniTask.Yield(timing, cancellationToken);
        }
    }
}
