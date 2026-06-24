using UnityEngine;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Marker component that protects a GameObject from incremental updates.
    /// When attached to a GameObject in a PSD2UI-generated prefab, the
    /// IncrementalUpdater will skip this object during incremental
    /// regeneration, preserving any manual edits made to it.
    /// </summary>
    public class PSD2UILock : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Description of why this object is locked or what manual edits were made.")]
        private string _note = "";
    }
}
