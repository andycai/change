using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using TMPro;
using Change.Editor.PSD2UI;
using Change.Runtime.PSD2UI;
using UnityEditor;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// Performance tests for TextStyleApplier when processing large sets of
    /// text layers (50+ components).
    /// </summary>
    [TestFixture]
    public class PerformanceTests
    {
        private const string TestPrefabDir = "Assets/Temp/PSD2UIPerfTests";
        private const int LargeTextLayerCount = 60;

        // ---- Lifecycle ----

        [SetUp]
        public void SetUp()
        {
            string fullDir = System.IO.Path.Combine(Application.dataPath, "Temp/PSD2UIPerfTests");
            if (!System.IO.Directory.Exists(fullDir))
            {
                System.IO.Directory.CreateDirectory(fullDir);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up all test prefabs and the temp directory
            string fullDir = System.IO.Path.Combine(Application.dataPath, "Temp/PSD2UIPerfTests");
            if (System.IO.Directory.Exists(fullDir))
            {
                var files = System.IO.Directory.GetFiles(fullDir, "*.prefab");
                foreach (var file in files)
                {
                    string metaFile = file + ".meta";
                    if (System.IO.File.Exists(metaFile)) System.IO.File.Delete(metaFile);
                    System.IO.File.Delete(file);
                }

                var remainingFiles = System.IO.Directory.GetFiles(fullDir);
                var remainingDirs = System.IO.Directory.GetDirectories(fullDir);
                if (remainingFiles.Length == 0 && remainingDirs.Length == 0)
                {
                    System.IO.Directory.Delete(fullDir);
                    string metaDir = fullDir + ".meta";
                    if (System.IO.File.Exists(metaDir)) System.IO.File.Delete(metaDir);
                }
            }

            AssetDatabase.Refresh();
        }

        // ---- Helpers ----

        private static string UniquePrefabPath()
        {
            return $"{TestPrefabDir}/PerfTest_{Guid.NewGuid():N}.prefab";
        }

        /// <summary>
        /// Creates a UINodeData tree with the specified number of text children,
        /// each configured with a stroke effect for realistic application load.
        /// </summary>
        private static UINodeData CreateLargeTextTree(int textCount)
        {
            var children = new System.Collections.Generic.List<UINodeData>();
            for (int i = 0; i < textCount; i++)
            {
                children.Add(new UINodeData
                {
                    Name = $"Text_{i:D3}",
                    Type = "Text",
                    Rect = new RectData
                    {
                        X = 10f,
                        Y = 10f + i * 20f,
                        Width = 200f,
                        Height = 20f
                    },
                    TextStyles = new TextStylesData
                    {
                        fontSize = 14f,
                        color = new ColorData { r = 1f, g = 1f, b = 1f, a = 1f },
                        fontName = "Arial",
                        fontStyle = new FontStyleData { bold = false, italic = false },
                        alignment = new AlignmentData { horizontal = "left", vertical = "top" },
                        effects = new TextEffectData[]
                        {
                            new StrokeEffectData
                            {
                                type = "stroke",
                                enabled = true,
                                color = new ColorData { r = 0f, g = 0f, b = 0f, a = 1f },
                                width = 2f,
                                position = "outside"
                            }
                        }
                    }
                });
            }

            return new UINodeData
            {
                Name = "PerfTestRoot",
                Rect = new RectData { X = 0f, Y = 0f, Width = 300f, Height = 1500f },
                Children = children
            };
        }

        // ================================================================
        // 1. Large-scale application performance
        // ================================================================

        [Test]
        public void ApplyTextStyles_50Layers_UnderFiveSeconds()
        {
            var builder = new PrefabBuilder();
            var applier = new TextStyleApplier();
            string prefabPath = UniquePrefabPath();

            var root = CreateLargeTextTree(LargeTextLayerCount);

            var sw = Stopwatch.StartNew();

            // Build the prefab hierarchy with TextMeshProUGUI components
            var prefab = builder.BuildPrefab(root, prefabPath);
            Assert.IsNotNull(prefab, "Prefab should be created");

            // Apply text styles to each text child
            for (int i = 0; i < LargeTextLayerCount; i++)
            {
                string childName = $"Text_{i:D3}";
                var childTransform = prefab.transform.Find(childName);
                Assert.IsNotNull(childTransform,
                    $"Child '{childName}' should exist in the prefab hierarchy");

                var tmp = childTransform.GetComponent<TextMeshProUGUI>();
                Assert.IsNotNull(tmp,
                    $"Child '{childName}' should have a TextMeshProUGUI component");

                var styles = root.Children[i].TextStyles;
                applier.ApplyBasicProperties(tmp, styles, childName);

                if (styles.effects != null && styles.effects.Length > 0)
                {
                    applier.ApplyEffects(tmp, styles.effects, childName, $"perf_{i:D3}");
                }
            }

            sw.Stop();

            // Clean up material assets created during the test to avoid
            // polluting the project.
            for (int i = 0; i < LargeTextLayerCount; i++)
            {
                string matPath =
                    $"Assets/GameRes/Materials/UI/PSD2UI/TMP_Text_{i:D3}_perf_{i:D3}_Material.mat";
                AssetDatabase.DeleteAsset(matPath);
            }

            UnityEngine.Debug.Log(
                $"[PerformanceTests] Applied styles to {LargeTextLayerCount} text layers " +
                $"in {sw.ElapsedMilliseconds}ms");

            Assert.That(LargeTextLayerCount, Is.GreaterThanOrEqualTo(50),
                "Should process at least 50 text layers");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000),
                $"Applying styles to {LargeTextLayerCount} layers should complete in under 5 seconds");
        }

        // ================================================================
        // 2. Single-component per-layer overhead benchmark
        // ================================================================

        [Test]
        public void ApplyTextStyles_ReasonablePerLayerOverhead()
        {
            var go = new GameObject("PerfSingleTest");
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var applier = new TextStyleApplier();

            // Construct a realistic effect set with stroke + dropShadow,
            // matching the real data structure field names exactly.
            var styles = new TextStylesData
            {
                fontSize = 24f,
                color = new ColorData { r = 1f, g = 1f, b = 1f, a = 1f },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = true, italic = false },
                alignment = new AlignmentData { horizontal = "center", vertical = "middle" },
                effects = new TextEffectData[]
                {
                    new StrokeEffectData
                    {
                        type = "stroke",
                        enabled = true,
                        color = new ColorData { r = 0f, g = 0f, b = 0f, a = 1f },
                        width = 2f,
                        position = "outside"
                    },
                    new ShadowEffectData
                    {
                        type = "dropShadow",
                        enabled = true,
                        color = new ColorData { r = 0f, g = 0f, b = 0f, a = 0.5f },
                        offsetX = 2f,
                        offsetY = -2f,
                        blur = 4f
                    }
                }
            };

            var sw = Stopwatch.StartNew();

            // Use a unique layerId to avoid Material path collisions with
            // other tests that may run in the same session.
            applier.ApplyBasicProperties(tmp, styles, "perf_single");
            applier.ApplyEffects(tmp, styles.effects, "perf_single", "perf_overhead_001");

            sw.Stop();

            UnityEngine.Debug.Log(
                $"[PerformanceTests] Single text layer style application: " +
                $"{sw.ElapsedMilliseconds}ms");

            // --- Cleanup: Material must be deleted BEFORE destroying the GameObject,
            //     so the fontSharedMaterial reference is still valid when we ask
            //     AssetDatabase for its path (same pattern as TextStyleApplierTests.TearDown).
            if (tmp != null && tmp.fontSharedMaterial != null)
            {
                var matPath = AssetDatabase.GetAssetPath(tmp.fontSharedMaterial);
                if (!string.IsNullOrEmpty(matPath))
                {
                    AssetDatabase.DeleteAsset(matPath);
                }
            }

            Object.DestroyImmediate(go);

            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(100),
                "Single text layer style application should complete in under 100ms");
        }
    }
}
