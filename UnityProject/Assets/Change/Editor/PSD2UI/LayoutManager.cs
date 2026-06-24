using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Manages Layout Group component attachment on GameObjects.
    /// Supports Vertical, Horizontal, and Grid layout types.
    /// Also provides template collapse for repeated child nodes.
    /// </summary>
    public class LayoutManager
    {
        /// <summary>
        /// Attaches the appropriate LayoutGroup component to the target GameObject
        /// based on the node's Layout data.
        /// </summary>
        /// <param name="node">The UINodeData containing layout configuration</param>
        /// <param name="target">The GameObject to attach the LayoutGroup to</param>
        public void AttachLayoutGroup(UINodeData node, GameObject target)
        {
            if (node == null)
            {
                Debug.LogWarning("[PSD2UI] LayoutManager.AttachLayoutGroup: node is null.");
                return;
            }

            if (target == null)
            {
                Debug.LogWarning("[PSD2UI] LayoutManager.AttachLayoutGroup: target is null.");
                return;
            }

            if (node.Layout == null) return;

            switch (node.Layout.Type)
            {
                case "Vertical":
                    var vg = target.AddComponent<VerticalLayoutGroup>();
                    vg.spacing = node.Layout.Spacing;
                    vg.childAlignment = ParseAlignment(node.Layout.Alignment);
                    break;

                case "Horizontal":
                    var hg = target.AddComponent<HorizontalLayoutGroup>();
                    hg.spacing = node.Layout.Spacing;
                    hg.childAlignment = ParseAlignment(node.Layout.Alignment);
                    break;

                case "Grid":
                    var gg = target.AddComponent<GridLayoutGroup>();
                    gg.spacing = new Vector2(node.Layout.Spacing, node.Layout.Spacing);
                    gg.childAlignment = ParseAlignment(node.Layout.Alignment);
                    break;

                default:
                    Debug.LogWarning(
                        $"[PSD2UI] LayoutManager: unrecognized layout type '{node.Layout.Type}' for node '{node.Name}'.");
                    break;
            }
        }

        /// <summary>
        /// Collapses template children: keeps only the first child and removes duplicates.
        /// This is used when PSD exports repeated list-item templates — we only need one
        /// child as the template for code generation.
        /// </summary>
        /// <param name="node">The UINodeData whose children should be collapsed</param>
        public void CollapseTemplate(UINodeData node)
        {
            if (node == null)
            {
                Debug.LogWarning("[PSD2UI] LayoutManager.CollapseTemplate: node is null.");
                return;
            }

            if (node.Children == null || node.Children.Count <= 1) return;

            // Keep only the first child as the template
            var template = node.Children[0];
            node.Children.Clear();
            node.Children.Add(template);

            // Recursively collapse children of the template
            CollapseTemplate(template);
        }

        /// <summary>
        /// Parses an alignment string into a TextAnchor enum value.
        /// Supports standard Unity alignment names.
        /// </summary>
        /// <param name="alignment">Alignment string (e.g., "UpperLeft", "MiddleCenter")</param>
        /// <returns>The corresponding TextAnchor value, defaults to UpperLeft</returns>
        public TextAnchor ParseAlignment(string alignment)
        {
            if (string.IsNullOrEmpty(alignment))
                return TextAnchor.UpperLeft;

            switch (alignment)
            {
                case "UpperLeft":   return TextAnchor.UpperLeft;
                case "UpperCenter": return TextAnchor.UpperCenter;
                case "UpperRight":  return TextAnchor.UpperRight;
                case "MiddleLeft":  return TextAnchor.MiddleLeft;
                case "MiddleCenter": return TextAnchor.MiddleCenter;
                case "MiddleRight": return TextAnchor.MiddleRight;
                case "LowerLeft":   return TextAnchor.LowerLeft;
                case "LowerCenter": return TextAnchor.LowerCenter;
                case "LowerRight":  return TextAnchor.LowerRight;
                default:
                    Debug.LogWarning(
                        $"[PSD2UI] LayoutManager.ParseAlignment: unrecognized alignment '{alignment}', defaulting to UpperLeft.");
                    return TextAnchor.UpperLeft;
            }
        }
    }
}
