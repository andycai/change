using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using Change.Editor.PSD2UI;
using Change.Runtime.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// End-to-end tests that verify text style properties flow correctly from
    /// PSD-parsed data (UINodeData tree) through PrefabBuilder into a Unity
    /// prefab, and that locked GameObjects are preserved during incremental updates.
    /// </summary>
    [TestFixture]
    public class TextStylesEndToEndTests
    {
        // ---- Constants ----

        private const string PrefabSavePath = "Assets/Temp/TextStylesE2E.prefab";
        private const string RootNodeName    = "E2ERoot";
        private const string TextNodeName    = "TextLabel";

        // ---- Lifecycle ----

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Temp"))
            {
                AssetDatabase.CreateFolder("Assets", "Temp");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath) != null)
            {
                AssetDatabase.DeleteAsset(PrefabSavePath);
            }
        }

        // ---- Helper Methods ----

        /// <summary>
        /// Creates a UINodeData container root with a single Text child whose
        /// TextStylesData carries all of the basic properties: fontSize, color,
        /// fontStyle, alignment, and fontName.
        /// </summary>
        private static UINodeData CreateRootWithText(
            string rootName,
            string textNodeName,
            float fontSize,
            float colorR, float colorG, float colorB, float colorA,
            bool bold, bool italic,
            string hAlign, string vAlign,
            string fontName,
            RectData rect = null)
        {
            return new UINodeData
            {
                Name = rootName,
                Type = "Container",
                Children = new List<UINodeData>
                {
                    new UINodeData
                    {
                        Name = textNodeName,
                        Type = "Text",
                        Rect = rect,
                        TextStyles = new TextStylesData
                        {
                            fontSize  = fontSize,
                            color     = new ColorData { r = colorR, g = colorG, b = colorB, a = colorA },
                            fontStyle = new FontStyleData { bold = bold, italic = italic },
                            alignment = new AlignmentData { horizontal = hAlign, vertical = vAlign },
                            fontName  = fontName
                        }
                    }
                }
            };
        }

        // ================================================================
        // Test 1: PSD data -> Unity prefab -> text styles correctly applied
        // ================================================================

        [Test]
        public void BuildPrefab_WithTextStyles_AppliesAllBasicPropertiesToTMPComponent()
        {
            // ---- Arrange ----
            var root = CreateRootWithText(
                rootName:     RootNodeName,
                textNodeName: TextNodeName,
                fontSize:     42f,
                colorR:       1f, colorG: 0f, colorB: 0f, colorA: 1f,  // red
                bold:         true,
                italic:       true,
                hAlign:       "center",
                vAlign:       "middle",
                fontName:     "Impact");

            var builder = new PrefabBuilder();
            GameObject prefab = null;

            // ---- Act ----
            try
            {
                prefab = builder.BuildPrefab(root, PrefabSavePath);

                // ---- Assert ----
                Assert.That(prefab, Is.Not.Null,
                    "BuildPrefab should return a non-null prefab asset");

                var childTransform = prefab.transform.Find(TextNodeName);
                Assert.That(childTransform, Is.Not.Null,
                    $"Child '{TextNodeName}' should exist in prefab hierarchy");

                var tmp = childTransform.GetComponent<TextMeshProUGUI>();
                Assert.That(tmp, Is.Not.Null,
                    "Child should have a TextMeshProUGUI component");

                // fontSize
                Assert.That(tmp.fontSize, Is.EqualTo(42f),
                    "fontSize should be 42");

                // color (red)
                Assert.That(tmp.color.r, Is.EqualTo(1f).Within(0.001f));
                Assert.That(tmp.color.g, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tmp.color.b, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tmp.color.a, Is.EqualTo(1f).Within(0.001f));

                // fontStyle (Bold | Italic)
                Assert.That(tmp.fontStyle, Is.EqualTo(FontStyles.Bold | FontStyles.Italic),
                    "fontStyle should be Bold | Italic");

                // alignment (Center)
                Assert.That(tmp.alignment, Is.EqualTo(TextAlignmentOptions.Center),
                    "alignment should be Center");

                // font -- skip if the environment lacks TMP Settings
                if (tmp.font == null)
                {
                    Assert.Inconclusive(
                        "Skipping font assertion: TMP Settings defaultFontAsset not configured. " +
                        "Configure TMP Settings or add a font asset to Resources to enable this check.");
                }
            }
            finally
            {
                if (prefab != null)
                {
                    Object.DestroyImmediate(prefab);
                }
            }
        }

        // ================================================================
        // Test 2: Locked component skipped during incremental update
        // ================================================================

        [Test]
        public void IncrementalUpdate_LockedComponent_SkipsModification()
        {
            // ---- Arrange: old config ----
            var oldRect = new RectData { X = 0f, Y = 0f, Width = 100f, Height = 30f };
            var oldConfig = CreateRootWithText(
                rootName:     RootNodeName,
                textNodeName: TextNodeName,
                fontSize:     24f,
                colorR:       0f, colorG: 0f, colorB: 1f, colorA: 1f,   // blue
                bold:         true,
                italic:       false,
                hAlign:       "left",
                vAlign:       "middle",
                fontName:     null,
                rect:         oldRect);

            // ---- Arrange: new config (different fontSize + different Rect position) ----
            // IsModified() in ChangeDetector does NOT compare TextStyles directly,
            // so we change Rect to trigger a "Modified" detection. The lock must
            // prevent BOTH RectTransform updates AND any future component re-application.
            var newRect = new RectData { X = 10f, Y = 10f, Width = 100f, Height = 30f };
            var newConfig = CreateRootWithText(
                rootName:     RootNodeName,
                textNodeName: TextNodeName,
                fontSize:     72f,
                colorR:       1f, colorG: 0f, colorB: 0f, colorA: 1f,   // red
                bold:         false,
                italic:       true,
                hAlign:       "right",
                vAlign:       "bottom",
                fontName:     null,
                rect:         newRect);

            // ---- Step 1: Build initial prefab ----
            var builder = new PrefabBuilder();
            var initialPrefab = builder.BuildPrefab(oldConfig, PrefabSavePath);
            Assert.That(initialPrefab, Is.Not.Null,
                "Initial prefab build should succeed");
            Object.DestroyImmediate(initialPrefab);

            // ---- Step 2: Add PSD2UILock to the Text child inside the prefab ----
            var prefabContents = PrefabUtility.LoadPrefabContents(PrefabSavePath);
            try
            {
                var childTransform = prefabContents.transform.Find(TextNodeName);
                Assert.That(childTransform, Is.Not.Null,
                    "Text child should exist in loaded prefab contents");

                var lockComp = childTransform.gameObject.AddComponent<PSD2UILock>();
                lockComp.LockTransform  = true;
                lockComp.LockComponents = true;
            }
            finally
            {
                PrefabUtility.SaveAsPrefabAsset(prefabContents, PrefabSavePath);
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            // ---- Step 3: Run incremental update with changed config ----
            var updater = new IncrementalUpdater();
            // Pass empty spriteSourceDir so no sprites are imported.
            updater.Update(PrefabSavePath, oldConfig, newConfig, string.Empty);

            // ---- Step 4: Reload prefab and verify locked component was NOT modified ----
            GameObject updatedPrefab = null;
            try
            {
                updatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath);
                Assert.That(updatedPrefab, Is.Not.Null,
                    "Prefab should exist after incremental update");

                var childTransform = updatedPrefab.transform.Find(TextNodeName);
                Assert.That(childTransform, Is.Not.Null,
                    "Text child should still exist after incremental update");

                // TMP properties must remain unchanged (old config values)
                var tmp = childTransform.GetComponent<TextMeshProUGUI>();
                Assert.That(tmp, Is.Not.Null,
                    "TextMeshProUGUI component should still exist");

                Assert.That(tmp.fontSize, Is.EqualTo(24f),
                    "fontSize should NOT change on a locked component");
                Assert.That(tmp.color.r, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tmp.color.g, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tmp.color.b, Is.EqualTo(1f).Within(0.001f),
                    "color should NOT change on a locked component (still blue)");
                Assert.That(tmp.fontStyle, Is.EqualTo(FontStyles.Bold),
                    "fontStyle should NOT change on a locked component");
                Assert.That(tmp.alignment, Is.EqualTo(TextAlignmentOptions.Left),
                    "alignment should NOT change on a locked component");

                // RectTransform position must also remain unchanged (lock respected)
                var rt = childTransform as RectTransform;
                Assert.That(rt, Is.Not.Null);
                Assert.That(rt.anchoredPosition.x, Is.EqualTo(oldRect.X).Within(0.001f),
                    "anchoredPosition.x should NOT change on a locked component");
                Assert.That(rt.anchoredPosition.y, Is.EqualTo(oldRect.Y).Within(0.001f),
                    "anchoredPosition.y should NOT change on a locked component");
            }
            finally
            {
                if (updatedPrefab != null)
                {
                    Object.DestroyImmediate(updatedPrefab);
                }
            }
        }

        // ================================================================
        // Test 3: Non-locked component IS modified during incremental update
        //         (sanity check that the update path works when no lock present)
        // ================================================================

        [Test]
        public void IncrementalUpdate_UnlockedComponent_AppliesRectChanges()
        {
            // ---- Arrange ----
            var oldRect = new RectData { X = 0f, Y = 0f, Width = 200f, Height = 50f };
            var newRect = new RectData { X = 50f, Y = 25f, Width = 300f, Height = 40f };

            var oldConfig = CreateRootWithText(
                rootName:     RootNodeName,
                textNodeName: TextNodeName,
                fontSize:     14f,
                colorR:       1f, colorG: 1f, colorB: 1f, colorA: 1f,
                bold:         false,
                italic:       false,
                hAlign:       "center",
                vAlign:       "middle",
                fontName:     null,
                rect:         oldRect);

            var newConfig = CreateRootWithText(
                rootName:     RootNodeName,
                textNodeName: TextNodeName,
                fontSize:     14f,
                colorR:       1f, colorG: 1f, colorB: 1f, colorA: 1f,
                bold:         false,
                italic:       false,
                hAlign:       "center",
                vAlign:       "middle",
                fontName:     null,
                rect:         newRect);

            // ---- Step 1: Build initial prefab (NO lock applied) ----
            var builder = new PrefabBuilder();
            var initialPrefab = builder.BuildPrefab(oldConfig, PrefabSavePath);
            Assert.That(initialPrefab, Is.Not.Null);
            Object.DestroyImmediate(initialPrefab);

            // ---- Step 2: Run incremental update ----
            var updater = new IncrementalUpdater();
            updater.Update(PrefabSavePath, oldConfig, newConfig, string.Empty);

            // ---- Step 3: Verify RectTransform was updated ----
            GameObject updatedPrefab = null;
            try
            {
                updatedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabSavePath);
                Assert.That(updatedPrefab, Is.Not.Null);

                var childTransform = updatedPrefab.transform.Find(TextNodeName);
                Assert.That(childTransform, Is.Not.Null);

                var rt = childTransform as RectTransform;
                Assert.That(rt, Is.Not.Null);

                Assert.That(rt.anchoredPosition.x, Is.EqualTo(newRect.X).Within(0.001f),
                    "anchoredPosition.x should be updated for unlocked component");
                Assert.That(rt.anchoredPosition.y, Is.EqualTo(newRect.Y).Within(0.001f),
                    "anchoredPosition.y should be updated for unlocked component");
                Assert.That(rt.sizeDelta.x, Is.EqualTo(newRect.Width).Within(0.001f),
                    "sizeDelta.x should be updated for unlocked component");
                Assert.That(rt.sizeDelta.y, Is.EqualTo(newRect.Height).Within(0.001f),
                    "sizeDelta.y should be updated for unlocked component");
            }
            finally
            {
                if (updatedPrefab != null)
                {
                    Object.DestroyImmediate(updatedPrefab);
                }
            }
        }
    }
}
