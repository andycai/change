using Change.Runtime.UI;
using FairyGUI;
using UnityEngine;

namespace GameScript.UI.Quest
{
    /// <summary>
    /// Attach to the quest window prefab root so <see cref="FairyGuiWindowFactory"/> can obtain a
    /// FairyGUI <see cref="GComponent"/> without an exported <see cref="UIPackage"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestFairyGuiRootSource : MonoBehaviour, IFairyGuiWindowRootSource
    {
        private GComponent _root;

        private void Awake()
        {
            if (_root == null)
            {
                _root = QuestUiRootBuilder.BuildRoot();
            }
        }

        public GComponent GetWindowRoot()
        {
            if (_root == null)
            {
                _root = QuestUiRootBuilder.BuildRoot();
            }

            return _root;
        }
    }
}
