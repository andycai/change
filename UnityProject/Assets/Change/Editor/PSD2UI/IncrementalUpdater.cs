using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Change.Runtime.PSD2UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Coordinates incremental prefab updates by comparing old and new
    /// UINodeData configurations, then applying only the differences.
    ///
    /// GameObjects with a PSD2UILock component are preserved and skipped
    /// during updates, protecting manual edits from being overwritten.
    /// </summary>
    public class IncrementalUpdater
    {
        private readonly PrefabBuilder _prefabBuilder = new PrefabBuilder();
        private readonly ComponentFactory _componentFactory = new ComponentFactory();
        private readonly AnchorEngine _anchorEngine = new AnchorEngine();
        private readonly LayoutManager _layoutManager = new LayoutManager();
        private readonly ChangeDetector _changeDetector = new ChangeDetector();
        private readonly AssetImporter _assetImporter = new AssetImporter();

        /// <summary>
        /// Performs an incremental update of a prefab based on a new configuration.
        /// If the prefab does not exist yet, a full build is performed instead.
        /// </summary>
        /// <param name="prefabPath">
        /// Asset path to the existing prefab (e.g., "Assets/GameRes/UIPanelArt/Prefabs/MyWindow.prefab").
        /// </param>
        /// <param name="oldConfig">The previous UINodeData configuration.</param>
        /// <param name="newConfig">The new UINodeData configuration to apply.</param>
        /// <param name="spriteSourceDir">
        /// File system directory containing source sprite images.
        /// Sprite paths in the config are relative to this directory.
        /// </param>
        public void Update(string prefabPath, UINodeData oldConfig, UINodeData newConfig, string spriteSourceDir)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogError("[PSD2UI] IncrementalUpdater.Update: prefabPath is null or empty.");
                return;
            }

            if (newConfig == null)
            {
                Debug.LogError("[PSD2UI] IncrementalUpdater.Update: newConfig is null.");
                return;
            }

            // If the prefab doesn't exist, do a full build
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab == null)
            {
                Debug.Log($"[PSD2UI] Prefab not found at '{prefabPath}', performing full build.");
                _assetImporter.ImportSprites(newConfig, spriteSourceDir);
                _prefabBuilder.BuildPrefab(newConfig, prefabPath);
                return;
            }

            // Compare configurations
            var report = _changeDetector.CompareConfigs(oldConfig, newConfig);

            if (report.IsEmpty)
            {
                Debug.Log("[PSD2UI] No changes detected, skipping update.");
                return;
            }

            Debug.Log($"[PSD2UI] Incremental update: {report.Added.Count} added, " +
                      $"{report.Modified.Count} modified, {report.Removed.Count} removed.");

            // Import sprites for added and modified nodes
            ImportChangedSprites(report, spriteSourceDir);

            // Apply hierarchy changes to the prefab
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ApplyHierarchyChanges(prefabRoot.transform, report);
            }
            finally
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            Debug.Log("[PSD2UI] Incremental update complete.");
        }

        /// <summary>
        /// Imports sprite files for nodes that were added or whose sprite path changed.
        /// </summary>
        private void ImportChangedSprites(ChangeReport report, string spriteSourceDir)
        {
            // Import sprites for added nodes
            foreach (var change in report.Added)
            {
                ImportSpriteIfNeeded(change.NewNode, spriteSourceDir);
            }

            // Import sprites for modified nodes where SpritePath changed
            foreach (var change in report.Modified)
            {
                if (change.NewNode?.SpritePath != change.OldNode?.SpritePath)
                {
                    ImportSpriteIfNeeded(change.NewNode, spriteSourceDir);
                }
            }
        }

        /// <summary>
        /// Imports a sprite for a node and recursively imports sprites for all
        /// children in its subtree. Called when a subtree root is added.
        /// </summary>
        private void ImportSpriteIfNeeded(UINodeData node, string spriteSourceDir)
        {
            if (node == null) return;
            if (string.IsNullOrEmpty(spriteSourceDir)) return;

            if (!string.IsNullOrEmpty(node.SpritePath))
            {
                string sourcePath = Path.Combine(spriteSourceDir, node.SpritePath);
                string fileName = Path.GetFileName(node.SpritePath);

                if (!string.IsNullOrEmpty(fileName))
                {
                    if (File.Exists(sourcePath))
                    {
                        _assetImporter.ImportSprites(sourcePath, fileName);

                        string assetPath = Path.Combine("Assets/GameRes/UIPanelArt", fileName);
                        if (node.Slice != null)
                        {
                            _assetImporter.ApplySliceSettings(assetPath, node.Slice);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PSD2UI] Source sprite not found: '{sourcePath}', skipping.");
                    }
                }
            }

            // Recurse into children to import their sprites too
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ImportSpriteIfNeeded(child, spriteSourceDir);
                }
            }
        }

        /// <summary>
        /// Applies the hierarchy changes (removals, additions, modifications) to the
        /// prefab root transform. Locked GameObjects are skipped.
        /// </summary>
        private void ApplyHierarchyChanges(Transform root, ChangeReport report)
        {
            // Phase 1: Remove deleted nodes (bottom-up: reverse order to process children before parents)
            for (int i = report.Removed.Count - 1; i >= 0; i--)
            {
                var change = report.Removed[i];
                var target = FindByPath(root, change.Path);
                if (target != null && !PSD2UILock.HasLock(target.gameObject))
                {
                    Object.DestroyImmediate(target.gameObject);
                }
            }

            // Phase 2: Add new nodes and their subtrees
            foreach (var change in report.Added)
            {
                if (change.NewNode == null) continue;

                string parentPath = GetParentPath(change.Path);
                var parent = FindByPath(root, parentPath);
                if (parent == null)
                {
                    Debug.LogWarning($"[PSD2UI] IncrementalUpdater: parent not found for added node '{change.Path}', skipping.");
                    continue;
                }

                if (PSD2UILock.HasLock(parent.gameObject))
                {
                    Debug.Log($"[PSD2UI] IncrementalUpdater: parent '{parentPath}' is locked, skipping addition of '{change.Path}'.");
                    continue;
                }

                CreateNodeHierarchy(change.NewNode, parent);
            }

            // Phase 3: Modify existing nodes
            foreach (var change in report.Modified)
            {
                var target = FindByPath(root, change.Path);
                if (target == null)
                {
                    Debug.LogWarning($"[PSD2UI] IncrementalUpdater: target not found for modified node '{change.Path}', skipping.");
                    continue;
                }

                if (PSD2UILock.HasLock(target.gameObject))
                {
                    Debug.Log($"[PSD2UI] IncrementalUpdater: '{change.Path}' is locked, skipping modification.");
                    continue;
                }

                ApplyNodeModification(target, change.NewNode);
            }
        }

        /// <summary>
        /// Creates a GameObject hierarchy from a UINodeData tree under the given parent.
        /// </summary>
        private void CreateNodeHierarchy(UINodeData nodeData, Transform parent)
        {
            if (nodeData == null || parent == null) return;

            var go = new GameObject(nodeData.Name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            // Apply RectTransform properties
            if (nodeData.Rect != null)
            {
                rt.anchoredPosition = new Vector2(nodeData.Rect.X, nodeData.Rect.Y);
                rt.sizeDelta = new Vector2(nodeData.Rect.Width, nodeData.Rect.Height);
            }

            // Infer and apply anchor
            if (parent is RectTransform parentRT && nodeData.Rect != null)
            {
                var parentRect = new RectData
                {
                    X = 0,
                    Y = 0,
                    Width = parentRT.rect.width,
                    Height = parentRT.rect.height
                };
                var preset = _anchorEngine.InferAnchor(nodeData.Rect, parentRect);
                _anchorEngine.ApplyAnchor(rt, preset, nodeData.Rect, parentRect);
            }

            _componentFactory.CreateComponentIfConfigured(nodeData, go);

            // Attach layout group if configured
            _layoutManager.AttachLayoutGroup(nodeData, go);

            // Recurse into children
            if (nodeData.Children != null)
            {
                foreach (var child in nodeData.Children)
                {
                    CreateNodeHierarchy(child, rt);
                }
            }
        }

        /// <summary>
        /// Applies property-level modifications to an existing GameObject transform.
        /// Currently updates RectTransform position and size. Component type changes
        /// require removal and re-addition, which is handled conservatively (logged as warning).
        /// </summary>
        private void ApplyNodeModification(Transform target, UINodeData newNode)
        {
            if (target == null || newNode == null) return;

            var rt = target as RectTransform;
            if (rt != null && newNode.Rect != null)
            {
                rt.anchoredPosition = new Vector2(newNode.Rect.X, newNode.Rect.Y);
                rt.sizeDelta = new Vector2(newNode.Rect.Width, newNode.Rect.Height);
            }
        }

        /// <summary>
        /// Finds a transform by its hierarchical path relative to the root.
        /// The path format is "RootName/Child/Grandchild". If the first segment
        /// matches root.name, it is stripped before searching.
        /// </summary>
        /// <param name="root">The root transform to search from.</param>
        /// <param name="path">Hierarchical dot-slash path to the target node.</param>
        /// <returns>The found transform, or null if not found.</returns>
        private static Transform FindByPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return root;

            string[] segments = path.Split('/');

            // If the first segment matches the root name, skip it
            int startIndex = 0;
            if (segments.Length > 0 && segments[0] == root.name)
            {
                startIndex = 1;
            }

            if (startIndex >= segments.Length) return root;

            // Build relative path for Transform.Find (which supports '/' separators)
            string relativePath = string.Join("/", segments, startIndex, segments.Length - startIndex);
            return root.Find(relativePath);
        }

        /// <summary>
        /// Extracts the parent path from a full hierarchical path.
        /// "Root/Header/CloseButton" returns "Root/Header".
        /// "Root" returns "".
        /// </summary>
        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            int lastSlash = path.LastIndexOf('/');
            return lastSlash > 0 ? path.Substring(0, lastSlash) : "";
        }
    }
}

