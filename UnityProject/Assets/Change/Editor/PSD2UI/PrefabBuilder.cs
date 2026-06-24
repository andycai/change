using UnityEditor;
using UnityEngine;

namespace Change.Editor.PSD2UI
{
    public class PrefabBuilder
    {
        public GameObject BuildPrefab(UINodeData root, string savePath)
        {
            if (root == null)
            {
                throw new System.ArgumentNullException(nameof(root));
            }

            if (string.IsNullOrEmpty(savePath))
            {
                throw new System.ArgumentException("savePath must not be null or empty.", nameof(savePath));
            }

            var rootGO = new GameObject(root.Name);
            rootGO.AddComponent<RectTransform>();

            CreateNode(root, rootGO.transform);

            PrefabUtility.SaveAsPrefabAsset(rootGO, savePath);
            Object.DestroyImmediate(rootGO);

            return AssetDatabase.LoadAssetAtPath<GameObject>(savePath);
        }

        private void CreateNode(UINodeData node, Transform parent)
        {
            if (node == null)
            {
                Debug.LogWarning("[PSD2UI] CreateNode: node is null, skipping.");
                return;
            }

            if (parent == null)
            {
                Debug.LogWarning($"[PSD2UI] CreateNode: parent is null for node '{node.Name}', skipping.");
                return;
            }

            var go = new GameObject(node.Name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            // Set RectTransform properties
            if (node.Rect != null)
            {
                rt.anchoredPosition = new Vector2(node.Rect.X, node.Rect.Y);
                rt.sizeDelta = new Vector2(node.Rect.Width, node.Rect.Height);
            }

            // Recursively create child nodes
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CreateNode(child, rt);
                }
            }
        }
    }
}
