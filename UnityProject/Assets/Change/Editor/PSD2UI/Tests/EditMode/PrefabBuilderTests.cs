using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// Integration tests for PrefabBuilder with AnchorEngine, ComponentFactory, and LayoutManager.
    /// Covers Prefab hierarchy creation, component type mounting, 6 anchor presets, and LayoutGroup attachment.
    /// </summary>
    public class PrefabBuilderTests
    {
        private PrefabBuilder _prefabBuilder;
        private AnchorEngine _anchorEngine;
        private ComponentFactory _componentFactory;
        private LayoutManager _layoutManager;

        private const string TestPrefabDir = "Assets/Temp/PSD2UITests";

        [SetUp]
        public void SetUp()
        {
            _prefabBuilder = new PrefabBuilder();
            _anchorEngine = new AnchorEngine();
            _componentFactory = new ComponentFactory();
            _layoutManager = new LayoutManager();

            // Ensure temp directory exists
            string fullDir = Path.Combine(Application.dataPath, "Temp/PSD2UITests");
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up all test prefabs and the temp directory
            string fullDir = Path.Combine(Application.dataPath, "Temp/PSD2UITests");
            if (Directory.Exists(fullDir))
            {
                var files = Directory.GetFiles(fullDir, "*.prefab");
                foreach (var file in files)
                {
                    string metaFile = file + ".meta";
                    if (File.Exists(metaFile)) File.Delete(metaFile);
                    File.Delete(file);
                }

                var remainingFiles = Directory.GetFiles(fullDir);
                var remainingDirs = Directory.GetDirectories(fullDir);
                if (remainingFiles.Length == 0 && remainingDirs.Length == 0)
                {
                    Directory.Delete(fullDir);
                    string metaDir = fullDir + ".meta";
                    if (File.Exists(metaDir)) File.Delete(metaDir);
                }
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Builds the filesystem path from an asset path.
        /// </summary>
        private static string AssetPathToFullPath(string assetPath)
        {
            return assetPath.Replace("Assets/", Application.dataPath + "/");
        }

        /// <summary>
        /// Generates a unique asset path for a test prefab.
        /// </summary>
        private static string UniquePrefabPath()
        {
            return $"{TestPrefabDir}/Test_{Guid.NewGuid():N}.prefab";
        }

        // ========================================================
        // Task 17-18: PrefabBuilder hierarchy tests
        // ========================================================

        [Test]
        public void BuildPrefab_WithSimpleNode_CreatesPrefabAsset()
        {
            var root = new UINodeData
            {
                Name = "Panel",
                Rect = new RectData { X = 0, Y = 0, Width = 800, Height = 600 }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            Assert.IsNotNull(prefab, "BuildPrefab should return a non-null GameObject");
            Assert.IsTrue(
                File.Exists(AssetPathToFullPath(prefabPath)),
                "Prefab file should exist on disk");

            // Verify the prefab has a RectTransform
            var rt = prefab.GetComponent<RectTransform>();
            Assert.IsNotNull(rt, "Root GameObject should have a RectTransform");
            Assert.AreEqual(new Vector2(800, 600), rt.sizeDelta);
        }

        [Test]
        public void BuildPrefab_WithNestedChildren_CreatesCorrectHierarchy()
        {
            var root = new UINodeData
            {
                Name = "MainWindow",
                Rect = new RectData { X = 0, Y = 0, Width = 1024, Height = 768 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData
                    {
                        Name = "Header",
                        Rect = new RectData { X = 0, Y = 668, Width = 1024, Height = 100 },
                        Children = new System.Collections.Generic.List<UINodeData>
                        {
                            new UINodeData
                            {
                                Name = "CloseButton",
                                Rect = new RectData { X = 974, Y = 40, Width = 40, Height = 40 }
                            }
                        }
                    },
                    new UINodeData
                    {
                        Name = "Content",
                        Rect = new RectData { X = 20, Y = 20, Width = 984, Height = 628 }
                    }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            Assert.AreEqual("MainWindow", prefab.name);

            // Check hierarchy
            var header = prefab.transform.Find("Header");
            Assert.IsNotNull(header, "Header should exist as child");
            Assert.AreEqual("Header", header.name);
            Assert.IsNotNull(header.GetComponent<RectTransform>());

            var closeButton = header.Find("CloseButton");
            Assert.IsNotNull(closeButton, "CloseButton should exist as child of Header");
            Assert.AreEqual("CloseButton", closeButton.name);

            var content = prefab.transform.Find("Content");
            Assert.IsNotNull(content, "Content should exist as child");
            Assert.AreEqual("Content", content.name);

            // Verify sibling count is correct (2 direct children)
            Assert.AreEqual(2, prefab.transform.childCount, "Root should have exactly 2 children");
        }

        [Test]
        public void BuildPrefab_ChildRectTransform_PositionSizeAreSet()
        {
            var root = new UINodeData
            {
                Name = "Root",
                Rect = new RectData { X = 0, Y = 0, Width = 500, Height = 500 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData
                    {
                        Name = "Child",
                        Rect = new RectData { X = 100, Y = 200, Width = 300, Height = 150 }
                    }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            var childRT = prefab.transform.Find("Child").GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(100, 200), childRT.anchoredPosition);
            Assert.AreEqual(new Vector2(300, 150), childRT.sizeDelta);
        }

        [Test]
        public void BuildPrefab_LeafNode_HasNoChildren()
        {
            var root = new UINodeData
            {
                Name = "Leaf",
                Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            Assert.AreEqual(0, prefab.transform.childCount);
        }

        [Test]
        public void BuildPrefab_NullRoot_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                _prefabBuilder.BuildPrefab(null, UniquePrefabPath());
            });

            Assert.That(ex.Message, Does.Contain("root"));
        }

        [Test]
        public void BuildPrefab_NullSavePath_ThrowsArgumentException()
        {
            var root = new UINodeData { Name = "Test" };

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                _prefabBuilder.BuildPrefab(root, null);
            });

            Assert.That(ex.Message, Does.Contain("savePath"));
        }

        [Test]
        public void BuildPrefab_EmptySavePath_ThrowsArgumentException()
        {
            var root = new UINodeData { Name = "Test" };

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                _prefabBuilder.BuildPrefab(root, "");
            });

            Assert.That(ex.Message, Does.Contain("savePath"));
        }

        [Test]
        public void BuildPrefab_DeeplyNestedHierarchy_AllLevelsExist()
        {
            var root = new UINodeData
            {
                Name = "L0",
                Rect = new RectData { X = 0, Y = 0, Width = 1000, Height = 1000 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData
                    {
                        Name = "L1",
                        Rect = new RectData { X = 0, Y = 0, Width = 1000, Height = 1000 },
                        Children = new System.Collections.Generic.List<UINodeData>
                        {
                            new UINodeData
                            {
                                Name = "L2",
                                Rect = new RectData { X = 10, Y = 10, Width = 100, Height = 100 },
                                Children = new System.Collections.Generic.List<UINodeData>
                                {
                                    new UINodeData
                                    {
                                        Name = "L3",
                                        Rect = new RectData { X = 5, Y = 5, Width = 50, Height = 50 }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            var l1 = prefab.transform.Find("L1");
            Assert.IsNotNull(l1);
            var l2 = l1.Find("L2");
            Assert.IsNotNull(l2);
            var l3 = l2.Find("L3");
            Assert.IsNotNull(l3);
            Assert.AreEqual("L3", l3.name);
        }

        // ========================================================
        // Task 17: 6 Anchor preset tests
        // ========================================================

        [Test]
        public void AnchorEngine_StretchAll_PresetApplied()
        {
            // parent: 1000x1000, child: nearly fills parent in both dimensions
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };
            var childRect = new RectData { X = 10f, Y = 10f, Width = 980f, Height = 980f };

            var go = new GameObject("Test_StretchAll");
            var rt = go.AddComponent<RectTransform>();

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            Assert.AreEqual(AnchorPreset.StretchAll, preset, "Should infer StretchAll");

            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            Assert.AreEqual(new Vector2(0, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(1, 1), rt.anchorMax);
            Assert.AreEqual(10f, rt.offsetMin.x, 0.001f); // left margin
            Assert.AreEqual(10f, rt.offsetMin.y, 0.001f); // bottom margin
            Assert.AreEqual(-10f, rt.offsetMax.x, 0.001f); // right margin (negative)
            Assert.AreEqual(-10f, rt.offsetMax.y, 0.001f); // top margin (negative)

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnchorEngine_HorizontalStretch_PresetApplied()
        {
            // parent: 1000x500, child: wide but short
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 500f };
            var childRect = new RectData { X = 10f, Y = 0f, Width = 980f, Height = 100f };

            var go = new GameObject("Test_HorizontalStretch");
            var rt = go.AddComponent<RectTransform>();

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            Assert.AreEqual(AnchorPreset.HorizontalStretch, preset, "Should infer HorizontalStretch");

            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            Assert.AreEqual(new Vector2(0, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(1, 0), rt.anchorMax);
            Assert.AreEqual(10f, rt.offsetMin.x, 0.001f); // left margin
            Assert.AreEqual(0f, rt.offsetMin.y, 0.001f);  // bottom edge
            Assert.AreEqual(-10f, rt.offsetMax.x, 0.001f); // right margin
            Assert.AreEqual(100f, rt.offsetMax.y, 0.001f); // top edge

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnchorEngine_VerticalStretch_PresetApplied()
        {
            // parent: 500x1000, child: tall but narrow
            var parentRect = new RectData { X = 0, Y = 0, Width = 500f, Height = 1000f };
            var childRect = new RectData { X = 0f, Y = 10f, Width = 100f, Height = 980f };

            var go = new GameObject("Test_VerticalStretch");
            var rt = go.AddComponent<RectTransform>();

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            Assert.AreEqual(AnchorPreset.VerticalStretch, preset, "Should infer VerticalStretch");

            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            Assert.AreEqual(new Vector2(0, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(0, 1), rt.anchorMax);
            Assert.AreEqual(0f, rt.offsetMin.x, 0.001f);  // left edge
            Assert.AreEqual(10f, rt.offsetMin.y, 0.001f); // bottom margin
            Assert.AreEqual(100f, rt.offsetMax.x, 0.001f); // right edge
            Assert.AreEqual(-10f, rt.offsetMax.y, 0.001f); // top margin

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnchorEngine_TopRight_PresetApplied()
        {
            // parent: 1000x1000, child: top-right corner
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };
            var childRect = new RectData { X = 850f, Y = 850f, Width = 100f, Height = 100f };

            var go = new GameObject("Test_TopRight");
            var rt = go.AddComponent<RectTransform>();

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            Assert.AreEqual(AnchorPreset.TopRight, preset, "Should infer TopRight");

            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            Assert.AreEqual(new Vector2(1, 1), rt.anchorMin);
            Assert.AreEqual(new Vector2(1, 1), rt.anchorMax);
            Assert.AreEqual(new Vector2(1, 1), rt.pivot);

            // rightGap = 1000 - 850 - 100 = 50
            // topGap = 1000 - 850 - 100 = 50
            Assert.AreEqual(-50f, rt.anchoredPosition.x, 0.001f);
            Assert.AreEqual(-50f, rt.anchoredPosition.y, 0.001f);
            Assert.AreEqual(new Vector2(100, 100), rt.sizeDelta);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnchorEngine_BottomCenter_PresetApplied()
        {
            // parent: 1000x1000, child: centered horizontally at the bottom
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };
            var childRect = new RectData { X = 460f, Y = 20f, Width = 80f, Height = 80f };

            var go = new GameObject("Test_BottomCenter");
            var rt = go.AddComponent<RectTransform>();

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            Assert.AreEqual(AnchorPreset.BottomCenter, preset, "Should infer BottomCenter");

            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            Assert.AreEqual(new Vector2(0.5f, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0), rt.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0), rt.pivot);

            // horizontalOffset = 460 + 40 - 500 = 0
            Assert.AreEqual(0f, rt.anchoredPosition.x, 0.001f);
            Assert.AreEqual(20f, rt.anchoredPosition.y, 0.001f); // distance from bottom
            Assert.AreEqual(new Vector2(80, 80), rt.sizeDelta);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnchorEngine_MiddleCenter_PresetApplied()
        {
            // parent: 1000x1000, child: centered in both dimensions
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };
            var childRect = new RectData { X = 460f, Y = 460f, Width = 80f, Height = 80f };

            var go = new GameObject("Test_MiddleCenter");
            var rt = go.AddComponent<RectTransform>();

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            Assert.AreEqual(AnchorPreset.MiddleCenter, preset, "Should infer MiddleCenter");

            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.pivot);

            // offsetX = 460 + 40 - 500 = 0
            Assert.AreEqual(0f, rt.anchoredPosition.x, 0.001f);
            Assert.AreEqual(0f, rt.anchoredPosition.y, 0.001f);
            Assert.AreEqual(new Vector2(80, 80), rt.sizeDelta);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnchorEngine_StretchAll_InferredFromRatioThreshold()
        {
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };

            // Edge case: exactly at the 95% threshold should trigger StretchAll (>95%)
            var childRect = new RectData { X = 24f, Y = 24f, Width = 952f, Height = 952f };

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            // 0.952 > 0.95, so both pass
            Assert.AreEqual(AnchorPreset.StretchAll, preset);
        }

        [Test]
        public void AnchorEngine_NullRect_FallsBackToMiddleCenter()
        {
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };

            var preset = _anchorEngine.InferAnchor(null, parentRect);
            Assert.AreEqual(AnchorPreset.MiddleCenter, preset, "Null childRect should fallback to MiddleCenter");
        }

        [Test]
        public void AnchorEngine_NullParentRect_FallsBackToMiddleCenter()
        {
            var childRect = new RectData { X = 0, Y = 0, Width = 100f, Height = 100f };

            var preset = _anchorEngine.InferAnchor(childRect, null);
            Assert.AreEqual(AnchorPreset.MiddleCenter, preset, "Null parentRect should fallback to MiddleCenter");
        }

        [Test]
        public void AnchorEngine_ApplyAnchor_NullRectTransform_NoException()
        {
            Assert.DoesNotThrow(() =>
            {
                _anchorEngine.ApplyAnchor(
                    null,
                    AnchorPreset.StretchAll,
                    new RectData { X = 0, Y = 0, Width = 100, Height = 100 },
                    new RectData { X = 0, Y = 0, Width = 200, Height = 200 });
            });
        }

        [Test]
        public void AnchorEngine_ApplyAnchor_NullChildRect_UsesFallback()
        {
            var go = new GameObject("Test_Fallback");
            var rt = go.AddComponent<RectTransform>();
            var parentRect = new RectData { X = 0, Y = 0, Width = 500, Height = 500 };

            Assert.DoesNotThrow(() =>
            {
                _anchorEngine.ApplyAnchor(rt, AnchorPreset.MiddleCenter, null, parentRect);
            });

            // Should still have been configured (with fallback rect)
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMax);

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ========================================================
        // Task 18: Component type mounting tests
        // ========================================================

        [Test]
        public void ComponentFactory_CreatesImage_ComponentExists()
        {
            var go = new GameObject("Test_Image");
            go.AddComponent<RectTransform>();

            var comp = _componentFactory.CreateComponent("Image", go);

            Assert.IsNotNull(comp);
            Assert.IsInstanceOf<Image>(comp);
            Assert.IsNotNull(go.GetComponent<Image>());

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ComponentFactory_CreatesText_ComponentExists()
        {
            var go = new GameObject("Test_Text");
            go.AddComponent<RectTransform>();

            var comp = _componentFactory.CreateComponent("Text", go);

            Assert.IsNotNull(comp);
            Assert.IsInstanceOf<TextMeshProUGUI>(comp);
            Assert.IsNotNull(go.GetComponent<TextMeshProUGUI>());

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ComponentFactory_CreatesButton_WithTargetGraphic()
        {
            var go = new GameObject("Test_Button");
            go.AddComponent<RectTransform>();

            var comp = _componentFactory.CreateComponent("Button", go);

            Assert.IsNotNull(comp);
            Assert.IsInstanceOf<Button>(comp);

            var button = (Button)comp;
            Assert.IsNotNull(button.targetGraphic, "Button targetGraphic should be set");

            var image = go.GetComponent<Image>();
            Assert.IsNotNull(image, "Button GameObject should also have an Image component");
            Assert.AreSame(image, button.targetGraphic, "targetGraphic should reference the Image on the same GO");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ComponentFactory_CreatesScrollRect_ComponentExists()
        {
            var go = new GameObject("Test_ScrollRect");
            go.AddComponent<RectTransform>();

            var comp = _componentFactory.CreateComponent("ScrollRect", go);

            Assert.IsNotNull(comp);
            Assert.IsInstanceOf<ScrollRect>(comp);
            Assert.IsNotNull(go.GetComponent<ScrollRect>());

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ComponentFactory_CreatesInputField_ComponentExists()
        {
            var go = new GameObject("Test_InputField");
            go.AddComponent<RectTransform>();

            var comp = _componentFactory.CreateComponent("InputField", go);

            Assert.IsNotNull(comp);
            Assert.IsInstanceOf<TMP_InputField>(comp);
            Assert.IsNotNull(go.GetComponent<TMP_InputField>());

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ComponentFactory_UnrecognizedType_ReturnsNull()
        {
            var go = new GameObject("Test_Unknown");
            go.AddComponent<RectTransform>();

            var comp = _componentFactory.CreateComponent("UnknownType", go);

            Assert.IsNull(comp, "Unknown component type should return null");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ComponentFactory_NullTarget_ReturnsNull()
        {
            var comp = _componentFactory.CreateComponent("Image", null);
            Assert.IsNull(comp);
        }

        [Test]
        public void ComponentFactory_NullOrEmptyType_ReturnsNull()
        {
            var go = new GameObject("Test_NullType");
            go.AddComponent<RectTransform>();

            Assert.IsNull(_componentFactory.CreateComponent(null, go));
            Assert.IsNull(_componentFactory.CreateComponent("", go));

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ========================================================
        // Task 18: LayoutGroup mounting tests
        // ========================================================

        [Test]
        public void LayoutManager_AttachesVerticalLayoutGroup_CorrectSettings()
        {
            var node = new UINodeData
            {
                Name = "VerticalList",
                Layout = new LayoutData
                {
                    Type = "Vertical",
                    Spacing = 10f,
                    Alignment = "UpperCenter"
                }
            };

            var go = new GameObject("Test_VerticalLayout");
            go.AddComponent<RectTransform>();

            _layoutManager.AttachLayoutGroup(node, go);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            Assert.IsNotNull(vlg, "Should have VerticalLayoutGroup component");
            Assert.AreEqual(10f, vlg.spacing);
            Assert.AreEqual(TextAnchor.UpperCenter, vlg.childAlignment);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LayoutManager_AttachesHorizontalLayoutGroup_CorrectSettings()
        {
            var node = new UINodeData
            {
                Name = "HorizontalList",
                Layout = new LayoutData
                {
                    Type = "Horizontal",
                    Spacing = 5f,
                    Alignment = "MiddleLeft"
                }
            };

            var go = new GameObject("Test_HorizontalLayout");
            go.AddComponent<RectTransform>();

            _layoutManager.AttachLayoutGroup(node, go);

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            Assert.IsNotNull(hlg, "Should have HorizontalLayoutGroup component");
            Assert.AreEqual(5f, hlg.spacing);
            Assert.AreEqual(TextAnchor.MiddleLeft, hlg.childAlignment);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LayoutManager_AttachesGridLayoutGroup_CorrectSettings()
        {
            var node = new UINodeData
            {
                Name = "GridView",
                Layout = new LayoutData
                {
                    Type = "Grid",
                    Spacing = 8f,
                    Alignment = "MiddleCenter"
                }
            };

            var go = new GameObject("Test_GridLayout");
            go.AddComponent<RectTransform>();

            _layoutManager.AttachLayoutGroup(node, go);

            var glg = go.GetComponent<GridLayoutGroup>();
            Assert.IsNotNull(glg, "Should have GridLayoutGroup component");
            Assert.AreEqual(new Vector2(8f, 8f), glg.spacing);
            Assert.AreEqual(TextAnchor.MiddleCenter, glg.childAlignment);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LayoutManager_MultipleCalls_OnlyOneLayoutGroupPerType()
        {
            var node = new UINodeData
            {
                Name = "List",
                Layout = new LayoutData { Type = "Vertical", Spacing = 5, Alignment = "UpperLeft" }
            };

            var go = new GameObject("Test_MultipleCalls");
            go.AddComponent<RectTransform>();

            _layoutManager.AttachLayoutGroup(node, go);
            _layoutManager.AttachLayoutGroup(node, go);

            var layouts = go.GetComponents<VerticalLayoutGroup>();
            Assert.AreEqual(2, layouts.Length,
                "Each call adds a new component (Unity allows multiple LayoutGroup components)");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LayoutManager_NoLayoutData_NoComponentAdded()
        {
            var node = new UINodeData { Name = "NoLayout" };

            var go = new GameObject("Test_NoLayout");
            go.AddComponent<RectTransform>();

            _layoutManager.AttachLayoutGroup(node, go);

            Assert.IsNull(go.GetComponent<VerticalLayoutGroup>());
            Assert.IsNull(go.GetComponent<HorizontalLayoutGroup>());
            Assert.IsNull(go.GetComponent<GridLayoutGroup>());

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LayoutManager_NullNode_NoException()
        {
            var go = new GameObject("Test_NullNode");
            go.AddComponent<RectTransform>();

            Assert.DoesNotThrow(() =>
            {
                _layoutManager.AttachLayoutGroup(null, go);
            });

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void LayoutManager_NullTarget_NoException()
        {
            var node = new UINodeData
            {
                Name = "Test",
                Layout = new LayoutData { Type = "Vertical" }
            };

            Assert.DoesNotThrow(() =>
            {
                _layoutManager.AttachLayoutGroup(node, null);
            });
        }

        [Test]
        public void LayoutManager_CollapseTemplate_KeepsOnlyFirstChild()
        {
            var node = new UINodeData
            {
                Name = "TemplateList",
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData { Name = "Template" },
                    new UINodeData { Name = "Duplicate1" },
                    new UINodeData { Name = "Duplicate2" },
                    new UINodeData { Name = "Duplicate3" }
                }
            };

            Assert.AreEqual(4, node.Children.Count, "Should start with 4 children");

            _layoutManager.CollapseTemplate(node);

            Assert.AreEqual(1, node.Children.Count, "Should have only 1 child after collapse");
            Assert.AreEqual("Template", node.Children[0].Name, "First child should be kept");
        }

        [Test]
        public void LayoutManager_CollapseTemplate_SingleChild_Unchanged()
        {
            var node = new UINodeData
            {
                Name = "SingleItem",
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData { Name = "Only" }
                }
            };

            _layoutManager.CollapseTemplate(node);

            Assert.AreEqual(1, node.Children.Count);
            Assert.AreEqual("Only", node.Children[0].Name);
        }

        [Test]
        public void LayoutManager_CollapseTemplate_NullNode_NoException()
        {
            Assert.DoesNotThrow(() =>
            {
                _layoutManager.CollapseTemplate(null);
            });
        }

        [Test]
        public void LayoutManager_ParseAlignment_UnrecognizedValue_DefaultsToUpperLeft()
        {
            var result = _layoutManager.ParseAlignment("UnknownAlignment");
            Assert.AreEqual(TextAnchor.UpperLeft, result);
        }

        [Test]
        public void LayoutManager_ParseAlignment_NullOrEmpty_DefaultsToUpperLeft()
        {
            Assert.AreEqual(TextAnchor.UpperLeft, _layoutManager.ParseAlignment(null));
            Assert.AreEqual(TextAnchor.UpperLeft, _layoutManager.ParseAlignment(""));
        }

        // ========================================================
        // Full integration tests: PrefabBuilder + AnchorEngine + ComponentFactory + LayoutManager
        // ========================================================

        [Test]
        public void FullIntegration_HeirarchyWithAllFeatures_ConstructedCorrectly()
        {
            // Build a UINodeData tree with components, anchors, and layouts
            var root = new UINodeData
            {
                Name = "ShopWindow",
                Type = "Container",
                Rect = new RectData { X = 0, Y = 0, Width = 800, Height = 600 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData
                    {
                        Name = "TitleBar",
                        Type = "Image",
                        Rect = new RectData { X = 0, Y = 550, Width = 800, Height = 50 }
                    },
                    new UINodeData
                    {
                        Name = "CloseBtn",
                        Type = "Button",
                        Rect = new RectData { X = 750, Y = 555, Width = 40, Height = 40 }
                    },
                    new UINodeData
                    {
                        Name = "ItemList",
                        Type = "Container",
                        Rect = new RectData { X = 10, Y = 10, Width = 780, Height = 530 },
                        Layout = new LayoutData
                        {
                            Type = "Vertical",
                            Spacing = 5f,
                            Alignment = "UpperLeft"
                        },
                        Children = new System.Collections.Generic.List<UINodeData>
                        {
                            new UINodeData
                            {
                                Name = "ItemTemplate",
                                Type = "Button",
                                Rect = new RectData { X = 0, Y = 0, Width = 780, Height = 60 },
                                Children = new System.Collections.Generic.List<UINodeData>
                                {
                                    new UINodeData
                                    {
                                        Name = "ItemIcon",
                                        Type = "Image",
                                        Rect = new RectData { X = 5, Y = 5, Width = 50, Height = 50 }
                                    },
                                    new UINodeData
                                    {
                                        Name = "ItemName",
                                        Type = "Text",
                                        Rect = new RectData { X = 60, Y = 5, Width = 200, Height = 50 }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            // --- Verify hierarchy ---
            Assert.AreEqual("ShopWindow", prefab.name);
            Assert.AreEqual(3, prefab.transform.childCount);

            var titleBar = prefab.transform.Find("TitleBar");
            Assert.IsNotNull(titleBar);
            Assert.IsNotNull(titleBar.GetComponent<RectTransform>());

            var closeBtn = prefab.transform.Find("CloseBtn");
            Assert.IsNotNull(closeBtn);

            var itemList = prefab.transform.Find("ItemList");
            Assert.IsNotNull(itemList);
            Assert.AreEqual(1, itemList.childCount);

            var itemTemplate = itemList.Find("ItemTemplate");
            Assert.IsNotNull(itemTemplate);
            Assert.AreEqual(2, itemTemplate.childCount);

            var itemIcon = itemTemplate.Find("ItemIcon");
            Assert.IsNotNull(itemIcon);

            var itemName = itemTemplate.Find("ItemName");
            Assert.IsNotNull(itemName);

            // --- Verify RectTransform positions ---
            var rootRT = prefab.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(800, 600), rootRT.sizeDelta);

            var titleBarRT = titleBar.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(0, 550), titleBarRT.anchoredPosition);
            Assert.AreEqual(new Vector2(800, 50), titleBarRT.sizeDelta);

            var closeBtnRT = closeBtn.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(750, 555), closeBtnRT.anchoredPosition);
            Assert.AreEqual(new Vector2(40, 40), closeBtnRT.sizeDelta);

            var itemListRT = itemList.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(10, 10), itemListRT.anchoredPosition);
            Assert.AreEqual(new Vector2(780, 530), itemListRT.sizeDelta);
        }

        [Test]
        public void FullIntegration_AnchorsAndComponentsAndLayout_AppliedTogether()
        {
            // Create a realistic UI scenario using all modules together on one GameObject
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 800f };
            var headerNode = new UINodeData
            {
                Name = "BottomBar",
                Type = "Image",
                Rect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 60f },
                Layout = new LayoutData
                {
                    Type = "Horizontal",
                    Spacing = 10f,
                    Alignment = "MiddleCenter"
                }
            };

            // Build basic hierarchy via PrefabBuilder
            var rootConfig = new UINodeData
            {
                Name = "Root",
                Rect = new RectData { X = 0, Y = 0, Width = 1000, Height = 800 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData
                    {
                        Name = "BottomBar",
                        Rect = new RectData { X = 0, Y = 0, Width = 1000, Height = 60 }
                    }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(rootConfig, prefabPath);
            var bottomBarGO = prefab.transform.Find("BottomBar").gameObject;

            // Apply anchor
            var childRect = headerNode.Rect;
            var preset = _anchorEngine.InferAnchor(childRect, parentRect);
            var rt = bottomBarGO.GetComponent<RectTransform>();
            _anchorEngine.ApplyAnchor(rt, preset, childRect, parentRect);

            // Apply component
            _componentFactory.CreateComponent(headerNode.Type, bottomBarGO);

            // Apply layout
            _layoutManager.AttachLayoutGroup(headerNode, bottomBarGO);

            // --- Verify anchor ---
            // HorizontalStretch (widthRatio=1.0 > 0.90, heightRatio=0.075 < 0.30)
            Assert.AreEqual(AnchorPreset.HorizontalStretch, preset);
            Assert.AreEqual(new Vector2(0, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(1, 0), rt.anchorMax);

            // --- Verify component ---
            var image = bottomBarGO.GetComponent<Image>();
            Assert.IsNotNull(image, "Should have Image component");

            // --- Verify layout ---
            var hlg = bottomBarGO.GetComponent<HorizontalLayoutGroup>();
            Assert.IsNotNull(hlg, "Should have HorizontalLayoutGroup");
            Assert.AreEqual(10f, hlg.spacing);
            Assert.AreEqual(TextAnchor.MiddleCenter, hlg.childAlignment);

            // Clean up the loaded prefab is handled by TearDown (file deletion)
        }

        [Test]
        public void FullIntegration_MultipleAnchorPresets_InSingleHierarchy()
        {
            // Build a hierarchy where different children get different anchor presets
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };

            // StretchAll child (fills parent)
            var bgChild = new RectData { X = 5, Y = 5, Width = 990, Height = 990 };

            // TopRight child
            var closeChild = new RectData { X = 920, Y = 920, Width = 60, Height = 60 };

            // BottomCenter child
            var barChild = new RectData { X = 400, Y = 10, Width = 200, Height = 50 };

            // MiddleCenter child
            var centerChild = new RectData { X = 450, Y = 450, Width = 100, Height = 100 };

            // Verify InferAnchor for each
            Assert.AreEqual(AnchorPreset.StretchAll, _anchorEngine.InferAnchor(bgChild, parentRect));
            Assert.AreEqual(AnchorPreset.TopRight, _anchorEngine.InferAnchor(closeChild, parentRect));
            Assert.AreEqual(AnchorPreset.BottomCenter, _anchorEngine.InferAnchor(barChild, parentRect));
            Assert.AreEqual(AnchorPreset.MiddleCenter, _anchorEngine.InferAnchor(centerChild, parentRect));

            // Apply anchors and verify each
            var go = new GameObject("Test_MultiAnchor");
            var rt = go.AddComponent<RectTransform>();

            // StretchAll
            _anchorEngine.ApplyAnchor(rt, AnchorPreset.StretchAll, bgChild, parentRect);
            Assert.AreEqual(new Vector2(0, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(1, 1), rt.anchorMax);

            // TopRight
            _anchorEngine.ApplyAnchor(rt, AnchorPreset.TopRight, closeChild, parentRect);
            Assert.AreEqual(new Vector2(1, 1), rt.anchorMin);
            Assert.AreEqual(new Vector2(1, 1), rt.anchorMax);
            Assert.AreEqual(new Vector2(1, 1), rt.pivot);

            // BottomCenter
            _anchorEngine.ApplyAnchor(rt, AnchorPreset.BottomCenter, barChild, parentRect);
            Assert.AreEqual(new Vector2(0.5f, 0), rt.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0), rt.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0), rt.pivot);

            // MiddleCenter
            _anchorEngine.ApplyAnchor(rt, AnchorPreset.MiddleCenter, centerChild, parentRect);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.pivot);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void FullIntegration_AllComponentTypes_OnHierarchyCreatedByBuilder()
        {
            // Create a hierarchy with all supported component types
            var root = new UINodeData
            {
                Name = "AllComponents",
                Rect = new RectData { X = 0, Y = 0, Width = 1000, Height = 800 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData { Name = "BgImage", Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 } },
                    new UINodeData { Name = "LabelText", Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 30 } },
                    new UINodeData { Name = "ActionButton", Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 40 } },
                    new UINodeData { Name = "ScrollView", Rect = new RectData { X = 0, Y = 0, Width = 300, Height = 200 } },
                    new UINodeData { Name = "NameInput", Rect = new RectData { X = 0, Y = 0, Width = 200, Height = 30 } }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            // Add components after building
            var bgImageGO = prefab.transform.Find("BgImage").gameObject;
            var labelGO = prefab.transform.Find("LabelText").gameObject;
            var buttonGO = prefab.transform.Find("ActionButton").gameObject;
            var scrollGO = prefab.transform.Find("ScrollView").gameObject;
            var inputGO = prefab.transform.Find("NameInput").gameObject;

            Assert.IsNotNull(_componentFactory.CreateComponent("Image", bgImageGO));
            Assert.IsNotNull(_componentFactory.CreateComponent("Text", labelGO));
            Assert.IsNotNull(_componentFactory.CreateComponent("Button", buttonGO));
            Assert.IsNotNull(_componentFactory.CreateComponent("ScrollRect", scrollGO));
            Assert.IsNotNull(_componentFactory.CreateComponent("InputField", inputGO));

            Assert.IsInstanceOf<Image>(bgImageGO.GetComponent<Image>());
            Assert.IsInstanceOf<TextMeshProUGUI>(labelGO.GetComponent<TextMeshProUGUI>());
            Assert.IsInstanceOf<Button>(buttonGO.GetComponent<Button>());
            Assert.IsInstanceOf<ScrollRect>(scrollGO.GetComponent<ScrollRect>());
            Assert.IsInstanceOf<TMP_InputField>(inputGO.GetComponent<TMP_InputField>());
        }

        [Test]
        public void FullIntegration_AllLayoutTypes_InSingleHierarchy()
        {
            var root = new UINodeData
            {
                Name = "LayoutContainer",
                Rect = new RectData { X = 0, Y = 0, Width = 500, Height = 500 },
                Children = new System.Collections.Generic.List<UINodeData>
                {
                    new UINodeData { Name = "VerticalGroup", Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 } },
                    new UINodeData { Name = "HorizontalGroup", Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 } },
                    new UINodeData { Name = "GridGroup", Rect = new RectData { X = 0, Y = 0, Width = 100, Height = 100 } }
                }
            };

            string prefabPath = UniquePrefabPath();
            var prefab = _prefabBuilder.BuildPrefab(root, prefabPath);

            // Attach layouts
            var vNode = new UINodeData { Layout = new LayoutData { Type = "Vertical", Spacing = 5, Alignment = "UpperLeft" } };
            var hNode = new UINodeData { Layout = new LayoutData { Type = "Horizontal", Spacing = 10, Alignment = "MiddleCenter" } };
            var gNode = new UINodeData { Layout = new LayoutData { Type = "Grid", Spacing = 8, Alignment = "LowerRight" } };

            _layoutManager.AttachLayoutGroup(vNode, prefab.transform.Find("VerticalGroup").gameObject);
            _layoutManager.AttachLayoutGroup(hNode, prefab.transform.Find("HorizontalGroup").gameObject);
            _layoutManager.AttachLayoutGroup(gNode, prefab.transform.Find("GridGroup").gameObject);

            var vlg = prefab.transform.Find("VerticalGroup").GetComponent<VerticalLayoutGroup>();
            Assert.IsNotNull(vlg);
            Assert.AreEqual(5f, vlg.spacing);

            var hlg = prefab.transform.Find("HorizontalGroup").GetComponent<HorizontalLayoutGroup>();
            Assert.IsNotNull(hlg);
            Assert.AreEqual(10f, hlg.spacing);

            var glg = prefab.transform.Find("GridGroup").GetComponent<GridLayoutGroup>();
            Assert.IsNotNull(glg);
            Assert.AreEqual(new Vector2(8f, 8f), glg.spacing);
        }

        [Test]
        public void FullIntegration_AnchorPresetDefaultFallback_IsMiddleCenter()
        {
            // A rect that doesn't match any specific rule should fallback to MiddleCenter
            var parentRect = new RectData { X = 0, Y = 0, Width = 1000f, Height = 1000f };

            // Not full enough for StretchAll, not wide enough for HorizontalStretch,
            // not tall enough for VerticalStretch, not in the right position for others
            var childRect = new RectData { X = 300, Y = 300, Width = 400, Height = 400 };

            var preset = _anchorEngine.InferAnchor(childRect, parentRect);

            // centerX = (300+200)/1000 = 0.5, centerY = (300+200)/1000 = 0.5
            // This is actually in MiddleCenter range (45-55%)
            Assert.AreEqual(AnchorPreset.MiddleCenter, preset);
        }
    }
}
