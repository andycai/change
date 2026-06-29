using UnityEngine;

namespace Change.Runtime.PSD2UI
{
    /// <summary>
    /// 锁定标记组件，挂在 GameObject 上标记该节点不被 PSD2UI 增量更新覆盖。
    /// 当检测到目标节点挂有此组件时，IncrementalUpdater 将跳过该节点及其子节点的更新。
    /// </summary>
    [AddComponentMenu("PSD2UI/PSD2UI Lock")]
    public sealed class PSD2UILock : MonoBehaviour
    {
        /// <summary>
        /// 锁定 Transform（位置、大小、旋转、缩放）
        /// </summary>
        public bool LockTransform = true;

        /// <summary>
        /// 锁定组件（不移除或替换已有组件）
        /// </summary>
        public bool LockComponents = true;

        /// <summary>
        /// 同时锁定所有子节点
        /// </summary>
        public bool LockChildren = false;

        /// <summary>
        /// 锁定原因说明
        /// </summary>
        [TextArea(3, 5)]
        public string Notes = "锁定原因：";

        /// <summary>
        /// Checks whether a GameObject is protected by a PSD2UILock component,
        /// either on itself or on any ancestor with LockChildren enabled.
        /// This is the canonical lock-detection method shared by all PSD2UI modules.
        /// </summary>
        /// <param name="go">The GameObject to check.</param>
        /// <returns>True if the GameObject or any of its ancestors is locked.</returns>
        public static bool HasLock(GameObject go)
        {
            if (go == null) return false;

            // Check the GameObject itself for a direct lock
            if (go.GetComponent<PSD2UILock>() != null)
                return true;

            // Walk up ancestors to check for LockChildren cascading
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                var lockComp = parent.GetComponent<PSD2UILock>();
                if (lockComp != null && lockComp.LockChildren)
                    return true;
                parent = parent.parent;
            }

            return false;
        }
    }
}
