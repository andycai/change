using NUnit.Framework;
using UnityEngine;
using TMPro;
using Change.Editor.PSD2UI;
using Change.Runtime.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// Testable subclass of TextStyleApplier that allows mocking the font lookup
    /// so tests can control which TMP_FontAsset is returned without requiring
    /// actual font assets on disk.
    /// </summary>
    internal class TestableTextStyleApplier : TextStyleApplier
    {
        /// <summary>
        /// The font asset to return from FindFont. Set to null to simulate
        /// a failed font lookup.
        /// </summary>
        public TMP_FontAsset MockFont { get; set; }

        /// <summary>
        /// Overrides the real font lookup with a configurable mock.
        /// </summary>
        protected internal override TMP_FontAsset FindFont(string fontName)
        {
            return MockFont;
        }
    }

    [TestFixture]
    public class TextStyleApplierTests
    {
        // ---- Constants ----

        private const string DefaultLayerName = "TestLayer";
        private const float DefaultTestFontSize = 14f;

        // ---- Fields ----

        private GameObject _testObject;
        private TextMeshProUGUI _tmpComponent;
        private TestableTextStyleApplier _applier;

        // ---- Lifecycle ----

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestTextObject");
            _tmpComponent = _testObject.AddComponent<TextMeshProUGUI>();
            _applier = new TestableTextStyleApplier();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
                _testObject = null;
                _tmpComponent = null;
            }

            _applier = null;
        }

        // ---- Helper Methods ----

        /// <summary>
        /// Creates a TextStylesData with the given alignment strings and the default
        /// fontSize. Other properties are left null so they are not applied.
        /// </summary>
        private static TextStylesData CreateAlignmentData(string horizontal, string vertical)
        {
            return new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                alignment = new AlignmentData
                {
                    horizontal = horizontal,
                    vertical = vertical
                }
            };
        }

        /// <summary>
        /// Creates a TextStylesData with the given font name and the default fontSize.
        /// </summary>
        private static TextStylesData CreateFontData(string fontName)
        {
            return new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontName = fontName
            };
        }

        // ================================================================
        // 1. Alignment mapping tests
        // ================================================================

        [Test]
        public void ApplyBasicProperties_Alignment_LeftTop_MapsToTopLeft()
        {
            var textStyles = CreateAlignmentData("left", "top");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.TopLeft));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_CenterMiddle_MapsToCenter()
        {
            var textStyles = CreateAlignmentData("center", "middle");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_RightBottom_MapsToBottomRight()
        {
            var textStyles = CreateAlignmentData("right", "bottom");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.BottomRight));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_JustifyTop_MapsToTopJustified()
        {
            var textStyles = CreateAlignmentData("justify", "top");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.TopJustified));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_NullValues_DefaultsToCenter()
        {
            var textStyles = CreateAlignmentData(null, null);
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_EmptyStrings_DefaultsToCenter()
        {
            var textStyles = CreateAlignmentData("", "");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_UnknownValues_DefaultsToCenter()
        {
            var textStyles = CreateAlignmentData("foo", "bar");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_LeftMiddle_MapsToLeft()
        {
            var textStyles = CreateAlignmentData("left", "middle");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Left));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_RightMiddle_MapsToRight()
        {
            var textStyles = CreateAlignmentData("right", "middle");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Right));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_CenterTop_MapsToTop()
        {
            var textStyles = CreateAlignmentData("center", "top");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Top));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_CenterBottom_MapsToBottom()
        {
            var textStyles = CreateAlignmentData("center", "bottom");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Bottom));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_JustifyMiddle_MapsToJustified()
        {
            var textStyles = CreateAlignmentData("justify", "middle");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Justified));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_JustifyBottom_MapsToBottomJustified()
        {
            var textStyles = CreateAlignmentData("justify", "bottom");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.BottomJustified));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_LeftBottom_MapsToBottomLeft()
        {
            var textStyles = CreateAlignmentData("left", "bottom");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.BottomLeft));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_RightTop_MapsToTopRight()
        {
            var textStyles = CreateAlignmentData("right", "top");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.TopRight));
        }

        [Test]
        public void ApplyBasicProperties_Alignment_NoAlignmentData_DoesNotChange()
        {
            // Pre-set alignment to something known, then apply TextStylesData
            // with no alignment data, and verify it stays unchanged.
            _tmpComponent.alignment = TextAlignmentOptions.BottomRight;

            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                alignment = null
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.BottomRight),
                "Alignment should not change when alignment data is null");
        }

        [Test]
        public void ApplyBasicProperties_Alignment_PartialData_VerticalNull_UsesDefault()
        {
            // AlignmentData with horizontal provided but vertical null should
            // handle gracefully by defaulting vertical to "" (→ middle fallback).
            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                alignment = new AlignmentData
                {
                    horizontal = "right",
                    vertical = null
                }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Right),
                "vertical=null should default to middle, giving Right for horizontal=right");
        }

        [Test]
        public void ApplyBasicProperties_Alignment_PartialData_HorizontalNull_UsesDefault()
        {
            // AlignmentData with vertical provided but horizontal null should
            // handle gracefully by defaulting horizontal to "" (→ center fallback).
            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                alignment = new AlignmentData
                {
                    horizontal = null,
                    vertical = "top"
                }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Top),
                "horizontal=null should default to center, giving Top for vertical=top");
        }

        // ---- Case-insensitive alignment (parameterized) ----

        [TestCase("LEFT", "TOP", TextAlignmentOptions.TopLeft)]
        [TestCase("Center", "Middle", TextAlignmentOptions.Center)]
        [TestCase("RIGHT", "BOTTOM", TextAlignmentOptions.BottomRight)]
        [TestCase("LeFt", "ToP", TextAlignmentOptions.TopLeft)]
        [TestCase("JUSTIFY", "middle", TextAlignmentOptions.Justified)]
        public void ApplyBasicProperties_Alignment_CaseInsensitive(
            string horizontal, string vertical, TextAlignmentOptions expected)
        {
            var textStyles = CreateAlignmentData(horizontal, vertical);
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);
            Assert.That(_tmpComponent.alignment, Is.EqualTo(expected));
        }

        // ================================================================
        // 2. Font matching tests
        // ================================================================

        [Test]
        public void ApplyBasicProperties_FontName_ValidMockFont_AppliesFont()
        {
            var mockFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            mockFont.name = "ImpactMock";
            _applier.MockFont = mockFont;

            var textStyles = CreateFontData("Impact");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.font, Is.SameAs(mockFont),
                "Font should be set to the mock font returned by FindFont");

            Object.DestroyImmediate(mockFont);
        }

        [Test]
        public void ApplyBasicProperties_FontName_FindFontReturnsNull_SetsFontToNull()
        {
            // Pre-set a font so we can observe it being cleared.
            var existingFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            existingFont.name = "ExistingFont";
            _tmpComponent.font = existingFont;

            _applier.MockFont = null;

            var textStyles = CreateFontData("MissingFont");
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.font, Is.Null,
                "Font should be set to null when FindFont returns null");

            Object.DestroyImmediate(existingFont);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void ApplyBasicProperties_FontName_EmptyOrWhitespaceOrNull_DoesNotChangeFont(string fontName)
        {
            var existingFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            existingFont.name = "PreservedFont";
            _tmpComponent.font = existingFont;

            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontName = fontName
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.font, Is.SameAs(existingFont),
                $"Font should not change when fontName is '{fontName?.Trim() ?? "(null)"}'");

            Object.DestroyImmediate(existingFont);
        }

        // ================================================================
        // 3. Lock detection tests
        // ================================================================

        [Test]
        public void ApplyBasicProperties_LockedGameObject_SkipsApplication()
        {
            // Set known initial properties
            _tmpComponent.fontSize = 24f;
            _tmpComponent.color = Color.white;
            _tmpComponent.fontStyle = FontStyles.Normal;
            _tmpComponent.alignment = TextAlignmentOptions.Center;

            // Add PSD2UILock to the GameObject
            _testObject.AddComponent<PSD2UILock>();

            var mockFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            mockFont.name = "ShouldNotApply";
            _applier.MockFont = mockFont;

            var textStyles = new TextStylesData
            {
                fontSize = 72f,
                color = new ColorData { r = 1f, g = 0f, b = 0f, a = 1f },
                fontStyle = new FontStyleData { bold = true, italic = true },
                alignment = new AlignmentData { horizontal = "right", vertical = "bottom" },
                fontName = "SomeFont"
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, "LockedLayer");

            // All properties should remain unchanged
            Assert.That(_tmpComponent.fontSize, Is.EqualTo(24f),
                "fontSize should not change on locked GameObject");
            Assert.That(_tmpComponent.color, Is.EqualTo(Color.white),
                "color should not change on locked GameObject");
            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Normal),
                "fontStyle should not change on locked GameObject");
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.Center),
                "alignment should not change on locked GameObject");
            Assert.That(_tmpComponent.font, Is.Null,
                "font should not change on locked GameObject");

            Object.DestroyImmediate(mockFont);
        }

        [Test]
        public void ApplyBasicProperties_LockChildren_AncestorLock_SkipsApplication()
        {
            // Create a parent with LockChildren enabled
            var parent = new GameObject("LockedParent");
            var parentLock = parent.AddComponent<PSD2UILock>();
            parentLock.LockChildren = true;

            try
            {
                // Reparent the test object under the locked parent
                _testObject.transform.SetParent(parent.transform);

                // Set known initial property
                _tmpComponent.fontSize = 24f;

                var textStyles = new TextStylesData
                {
                    fontSize = 72f
                };

                _applier.ApplyBasicProperties(_tmpComponent, textStyles, "ChildLayer");

                // fontSize should remain unchanged due to ancestor lock
                Assert.That(_tmpComponent.fontSize, Is.EqualTo(24f),
                    "fontSize should not change when ancestor has LockChildren enabled");
            }
            finally
            {
                // Ensure cleanup even if the test throws
                _testObject.transform.SetParent(null);
                Object.DestroyImmediate(parent);
            }
        }

        // ================================================================
        // 4. Null guard tests
        // ================================================================

        [Test]
        public void ApplyBasicProperties_NullTMPComponent_ReturnsEarlyWithoutThrowing()
        {
            var textStyles = new TextStylesData { fontSize = DefaultTestFontSize };

            Assert.DoesNotThrow(() =>
            {
                _applier.ApplyBasicProperties(null, textStyles, "NullTMPLayer");
            }, "Null tmpComponent should not cause an exception");
        }

        [Test]
        public void ApplyBasicProperties_NullTextStyles_ReturnsEarlyWithoutThrowing()
        {
            Assert.DoesNotThrow(() =>
            {
                _applier.ApplyBasicProperties(_tmpComponent, null, "NullStylesLayer");
            }, "Null textStyles should not cause an exception");
        }

        [Test]
        public void ApplyBasicProperties_NullTextStyles_DoesNotModifyTMPComponent()
        {
            _tmpComponent.fontSize = 36f;
            _tmpComponent.color = Color.green;

            _applier.ApplyBasicProperties(_tmpComponent, null, "NullStylesLayer");

            Assert.That(_tmpComponent.fontSize, Is.EqualTo(36f),
                "fontSize should not change when textStyles is null");
            Assert.That(_tmpComponent.color, Is.EqualTo(Color.green),
                "color should not change when textStyles is null");
        }

        [Test]
        public void ApplyBasicProperties_NullTMPComponentAndNullTextStyles_ReturnsEarlyWithoutThrowing()
        {
            Assert.DoesNotThrow(() =>
            {
                _applier.ApplyBasicProperties(null, null, "BothNullLayer");
            }, "Both null arguments should not cause an exception");
        }

        // ================================================================
        // 5. Basic property application tests
        // ================================================================

        [Test]
        public void ApplyBasicProperties_AppliesFontSize()
        {
            var textStyles = new TextStylesData { fontSize = 42f };
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontSize, Is.EqualTo(42f));
        }

        [Test]
        public void ApplyBasicProperties_AppliesFontSize_Zero()
        {
            var textStyles = new TextStylesData { fontSize = 0f };
            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontSize, Is.EqualTo(0f),
                "fontSize 0 should be applied (always set from textStyles)");
        }

        [Test]
        public void ApplyBasicProperties_AppliesColor()
        {
            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                color = new ColorData { r = 0.2f, g = 0.4f, b = 0.6f, a = 0.8f }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.color.r, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(_tmpComponent.color.g, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(_tmpComponent.color.b, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(_tmpComponent.color.a, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void ApplyBasicProperties_AppliesFontStyle_Bold()
        {
            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontStyle = new FontStyleData { bold = true, italic = false }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Bold));
        }

        [Test]
        public void ApplyBasicProperties_AppliesFontStyle_Italic()
        {
            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontStyle = new FontStyleData { bold = false, italic = true }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Italic));
        }

        [Test]
        public void ApplyBasicProperties_AppliesFontStyle_BoldItalic()
        {
            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontStyle = new FontStyleData { bold = true, italic = true }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Bold | FontStyles.Italic));
        }

        [Test]
        public void ApplyBasicProperties_AppliesFontStyle_Normal()
        {
            // Pre-set to a non-normal style so we can verify it changes
            _tmpComponent.fontStyle = FontStyles.Bold;

            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontStyle = new FontStyleData { bold = false, italic = false }
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Normal));
        }

        [Test]
        public void ApplyBasicProperties_NoFontStyleData_LeavesExistingStyleUnchanged()
        {
            _tmpComponent.fontStyle = FontStyles.Italic;

            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                fontStyle = null
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Italic),
                "fontStyle should not change when fontStyle data is null");
        }

        [Test]
        public void ApplyBasicProperties_NoColorData_LeavesExistingColorUnchanged()
        {
            _tmpComponent.color = Color.cyan;

            var textStyles = new TextStylesData
            {
                fontSize = DefaultTestFontSize,
                color = null
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, DefaultLayerName);

            Assert.That(_tmpComponent.color, Is.EqualTo(Color.cyan),
                "color should not change when color data is null");
        }

        [Test]
        public void ApplyBasicProperties_AllPropertiesApplied_WhenAllDataProvided()
        {
            var mockFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            mockFont.name = "TestFont";
            _applier.MockFont = mockFont;

            var textStyles = new TextStylesData
            {
                fontSize = 28f,
                color = new ColorData { r = 0.1f, g = 0.3f, b = 0.5f, a = 1f },
                fontStyle = new FontStyleData { bold = true, italic = false },
                alignment = new AlignmentData { horizontal = "right", vertical = "top" },
                fontName = "TestFont"
            };

            _applier.ApplyBasicProperties(_tmpComponent, textStyles, "AllPropsLayer");

            Assert.That(_tmpComponent.fontSize, Is.EqualTo(28f));
            Assert.That(_tmpComponent.color.r, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(_tmpComponent.color.g, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(_tmpComponent.color.b, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(_tmpComponent.fontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(_tmpComponent.alignment, Is.EqualTo(TextAlignmentOptions.TopRight));
            Assert.That(_tmpComponent.font, Is.SameAs(mockFont));

            Object.DestroyImmediate(mockFont);
        }
    }
}
