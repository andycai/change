using System.Collections.Generic;
using UnityEngine;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Type of change detected between two configuration versions.
    /// </summary>
    public enum ChangeType
    {
        /// <summary>Node exists only in the new configuration.</summary>
        Added,

        /// <summary>Node exists in both but properties have changed.</summary>
        Modified,

        /// <summary>Node exists only in the old configuration.</summary>
        Removed
    }

    /// <summary>
    /// Describes a single node-level change between two config versions.
    /// </summary>
    public class NodeChange
    {
        /// <summary>Hierarchical path to the node (e.g., "Root/Header/CloseButton").</summary>
        public string Path { get; set; }

        /// <summary>What kind of change occurred.</summary>
        public ChangeType ChangeType { get; set; }

        /// <summary>The node data from the old configuration (null for Added).</summary>
        public UINodeData OldNode { get; set; }

        /// <summary>The node data from the new configuration (null for Removed).</summary>
        public UINodeData NewNode { get; set; }
    }

    /// <summary>
    /// Complete report of all changes between two UINodeData trees.
    /// </summary>
    public class ChangeReport
    {
        /// <summary>Nodes present only in the new configuration.</summary>
        public List<NodeChange> Added { get; set; } = new List<NodeChange>();

        /// <summary>Nodes present in both but with modified properties.</summary>
        public List<NodeChange> Modified { get; set; } = new List<NodeChange>();

        /// <summary>Nodes present only in the old configuration.</summary>
        public List<NodeChange> Removed { get; set; } = new List<NodeChange>();

        /// <summary>
        /// Returns true if there are no changes of any kind.
        /// </summary>
        public bool IsEmpty => Added.Count == 0 && Modified.Count == 0 && Removed.Count == 0;
    }

    /// <summary>
    /// Compares two UINodeData configuration trees and produces a ChangeReport
    /// listing all added, modified, and removed nodes. Nodes are matched by name
    /// at each hierarchy level.
    /// </summary>
    public class ChangeDetector
    {
        /// <summary>
        /// Compares an old and new UINodeData tree and returns a detailed change report.
        /// </summary>
        /// <param name="oldConfig">The previous configuration (may be null).</param>
        /// <param name="newConfig">The new configuration (may be null).</param>
        /// <returns>A ChangeReport containing all detected differences.</returns>
        public ChangeReport CompareConfigs(UINodeData oldConfig, UINodeData newConfig)
        {
            var report = new ChangeReport();

            if (oldConfig == null && newConfig == null)
            {
                Debug.LogWarning("[PSD2UI] ChangeDetector.CompareConfigs: both configs are null.");
                return report;
            }

            if (oldConfig == null)
            {
                // Entire tree is new
                AddTreeAsAdded(newConfig, "", report);
                return report;
            }

            if (newConfig == null)
            {
                // Entire tree was removed
                AddTreeAsRemoved(oldConfig, "", report);
                return report;
            }

            CompareNodes(oldConfig, newConfig, "", report);
            return report;
        }

        /// <summary>
        /// Recursively compares two nodes and their children, populating the report.
        /// </summary>
        /// <param name="oldNode">Node from the old configuration.</param>
        /// <param name="newNode">Node from the new configuration.</param>
        /// <param name="parentPath">Hierarchical path of the parent ("" for root).</param>
        /// <param name="report">The change report to populate.</param>
        private void CompareNodes(UINodeData oldNode, UINodeData newNode, string parentPath, ChangeReport report)
        {
            string nodeName = oldNode?.Name ?? newNode?.Name ?? "Unknown";
            string path = string.IsNullOrEmpty(parentPath) ? nodeName : $"{parentPath}/{nodeName}";

            // Check for property-level modifications on the node itself
            if (IsModified(oldNode, newNode))
            {
                report.Modified.Add(new NodeChange
                {
                    Path = path,
                    ChangeType = ChangeType.Modified,
                    OldNode = oldNode,
                    NewNode = newNode
                });
            }

            // Compare children
            var oldChildren = oldNode?.Children ?? new List<UINodeData>();
            var newChildren = newNode?.Children ?? new List<UINodeData>();

            var oldByName = BuildNameLookup(oldChildren);
            var newByName = BuildNameLookup(newChildren);

            // Detect removed children (in old but not in new)
            foreach (var oldChild in oldChildren)
            {
                if (string.IsNullOrEmpty(oldChild.Name)) continue;

                if (newByName.TryGetValue(oldChild.Name, out var newChild))
                {
                    // Child exists in both — recurse
                    CompareNodes(oldChild, newChild, path, report);
                }
                else
                {
                    // Child was removed (and its entire subtree)
                    string childPath = $"{path}/{oldChild.Name}";
                    AddTreeAsRemoved(oldChild, childPath, report);
                }
            }

            // Detect added children (in new but not in old)
            foreach (var newChild in newChildren)
            {
                if (string.IsNullOrEmpty(newChild.Name)) continue;

                if (!oldByName.ContainsKey(newChild.Name))
                {
                    // Child was added (and its entire subtree)
                    string childPath = $"{path}/{newChild.Name}";
                    AddTreeAsAdded(newChild, childPath, report);
                }
            }
        }

        /// <summary>
        /// Recursively adds a node and its entire subtree as "Added" changes.
        /// </summary>
        private void AddTreeAsAdded(UINodeData node, string path, ChangeReport report)
        {
            if (node == null) return;

            report.Added.Add(new NodeChange
            {
                Path = path,
                ChangeType = ChangeType.Added,
                NewNode = node
            });

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    string childPath = string.IsNullOrEmpty(path)
                        ? child.Name
                        : $"{path}/{child.Name}";
                    AddTreeAsAdded(child, childPath, report);
                }
            }
        }

        /// <summary>
        /// Recursively adds a node and its entire subtree as "Removed" changes.
        /// </summary>
        private void AddTreeAsRemoved(UINodeData node, string path, ChangeReport report)
        {
            if (node == null) return;

            report.Removed.Add(new NodeChange
            {
                Path = path,
                ChangeType = ChangeType.Removed,
                OldNode = node
            });

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    string childPath = string.IsNullOrEmpty(path)
                        ? child.Name
                        : $"{path}/{child.Name}";
                    AddTreeAsRemoved(child, childPath, report);
                }
            }
        }

        /// <summary>
        /// Builds a case-sensitive name lookup from a list of UINodeData children.
        /// Nodes with null or empty names are skipped with a warning.
        /// Duplicate names at the same level emit a warning and the last one wins.
        /// </summary>
        private static Dictionary<string, UINodeData> BuildNameLookup(List<UINodeData> children)
        {
            var lookup = new Dictionary<string, UINodeData>();
            if (children == null) return lookup;

            foreach (var child in children)
            {
                if (string.IsNullOrEmpty(child.Name))
                {
                    Debug.LogWarning("[PSD2UI] ChangeDetector: child node with null or empty name, skipping.");
                    continue;
                }

                if (lookup.ContainsKey(child.Name))
                {
                    Debug.LogWarning(
                        $"[PSD2UI] ChangeDetector: duplicate child name '{child.Name}' at same level; last occurrence wins.");
                }

                lookup[child.Name] = child;
            }

            return lookup;
        }

        /// <summary>
        /// Determines whether two UINodeData nodes differ in any meaningful property.
        /// </summary>
        private static bool IsModified(UINodeData oldNode, UINodeData newNode)
        {
            if (oldNode == null || newNode == null) return false;

            if (oldNode.Type != newNode.Type) return true;
            if (oldNode.SpritePath != newNode.SpritePath) return true;
            if (!RectsEqual(oldNode.Rect, newNode.Rect)) return true;
            if (!SlicesEqual(oldNode.Slice, newNode.Slice)) return true;
            if (!LayoutsEqual(oldNode.Layout, newNode.Layout)) return true;

            return false;
        }

        private static bool RectsEqual(RectData a, RectData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        }

        private static bool SlicesEqual(SliceData a, SliceData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        private static bool LayoutsEqual(LayoutData a, LayoutData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.Type == b.Type && a.Spacing == b.Spacing && a.Alignment == b.Alignment;
        }
    }
}
