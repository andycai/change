using TMPro;
using UnityEngine;
using Change.Runtime.PSD2UI;
using System.IO;
using UnityEditor;

namespace Change.Editor.PSD2UI
{
    // ---- Serializable Data Classes ----
    // These mirror the JSON shape produced by the Node.js PSD parser.
    // They serve as shared data contracts used by TextStyleApplier and future modules.

    /// <summary>
    /// Container for all text style properties extracted from a PSD text layer.
    /// </summary>
    [System.Serializable]
    public class TextStylesData
    {
        /// <summary>Font size in Unity units (points).</summary>
        public float fontSize;

        /// <summary>Text color with r,g,b,a in Unity 0-1 range.</summary>
        public ColorData color;

        /// <summary>Font family name as specified in PSD (e.g., "Impact", "Arial").</summary>
        public string fontName;

        /// <summary>Bold and italic style flags.</summary>
        public FontStyleData fontStyle;

        /// <summary>Horizontal and vertical alignment values.</summary>
        public AlignmentData alignment;

        /// <summary>
        /// Optional array of text effects (shadow, outline, glow, etc.).
        /// May be null or empty for plain text layers. Effect-specific subclasses
        /// are added in a future task (Task 14).
        /// </summary>
        public TextEffectData[] effects;
    }

    /// <summary>
    /// RGBA color with each channel in the 0-1 range (Unity convention).
    /// </summary>
    [System.Serializable]
    public class ColorData
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    /// <summary>
    /// Boolean flags for bold and italic font styles.
    /// </summary>
    [System.Serializable]
    public class FontStyleData
    {
        public bool bold;
        public bool italic;
    }

    /// <summary>
    /// Horizontal and vertical text alignment as string values
    /// matching the PSD parser output (e.g., "left", "center", "middle", "bottom").
    /// </summary>
    [System.Serializable]
    public class AlignmentData
    {
        public string horizontal;
        public string vertical;
    }

    /// <summary>
    /// Base class for text effect entries. Each effect has a type identifier
    /// and an enabled flag. Effect-specific subclasses (with additional properties
    /// like color, offset, size) are added in a future task (Task 14).
    /// </summary>
    [System.Serializable]
    public class TextEffectData
    {
        /// <summary>Effect type identifier (e.g., "shadow", "outline", "glow").</summary>
        public string type;

        /// <summary>Whether this effect is enabled.</summary>
        public bool enabled;
    }

    [System.Serializable]
    public class StrokeEffectData : TextEffectData
    {
        public ColorData color;
        public float width;
        public string position;
    }

    [System.Serializable]
    public class ShadowEffectData : TextEffectData
    {
        public ColorData color;
        public float offsetX;
        public float offsetY;
        public float blur;
    }

    [System.Serializable]
    public class GradientEffectData : TextEffectData
    {
        public string gradientType;
        public float angle;
        public GradientColorStop[] colors;
        public bool degraded;
    }

    [System.Serializable]
    public class GradientColorStop
    {
        public float r, g, b, a;
        public float position;
    }

    [System.Serializable]
    public class GlowEffectData : TextEffectData
    {
        public ColorData color;
        public float size;
        public float spread;
    }

    [System.Serializable]
    public class BevelEffectData : TextEffectData
    {
        public string style;
        public float depth;
        public float size;
        public float angle;
        public ColorData highlightColor;
        public ColorData shadowColor;
    }

    // ---- Main Applier Class ----

    /// <summary>
    /// Applies text style properties (from PSD-extracted JSON data) to a
    /// TextMeshProUGUI component in the Unity Editor.
    ///
    /// Respects the PSD2UILock mechanism: locked GameObjects are skipped
    /// to preserve manual edits during incremental updates.
    /// </summary>
    public class TextStyleApplier
    {
        // ---- Constants ----

        private const string LogPrefix = "[PSD2UI]";

        // Alignment string values from the PSD parser
        private const string AlignLeft    = "left";
        private const string AlignCenter  = "center";
        private const string AlignRight   = "right";
        private const string AlignJustify = "justify";
        private const string AlignTop     = "top";
        private const string AlignMiddle  = "middle";
        private const string AlignBottom  = "bottom";

        // ---- Helpers ----

        /// <summary>
        /// The TMP default font asset, or null if TMP Settings are not configured.
        /// Caches the lookup for repeated calls within a single ApplyBasicProperties run.
        /// </summary>
        private static TMP_FontAsset DefaultFontAsset =>
            TMP_Settings.instance != null
                ? TMP_Settings.defaultFontAsset
                : null;

        // ---- Public Methods ----

        /// <summary>
        /// Applies basic text properties (font size, color, font style, alignment, font)
        /// from the parsed text styles data onto the target TextMeshProUGUI component.
        ///
        /// This is the entry point called by ComponentFactory or IncrementalUpdater
        /// after a Text component has been created or matched on a GameObject.
        /// </summary>
        /// <param name="tmpComponent">
        /// The TextMeshProUGUI component to configure. Must not be null.
        /// </param>
        /// <param name="textStyles">
        /// Parsed text style data from the PSD parser. Must not be null.
        /// </param>
        /// <param name="layerName">
        /// The PSD layer name, used for log context only.
        /// </param>
        public void ApplyBasicProperties(TextMeshProUGUI tmpComponent, TextStylesData textStyles, string layerName)
        {
            if (tmpComponent == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} TextStyleApplier.ApplyBasicProperties: " +
                    $"TextMeshProUGUI component is null for layer '{layerName ?? "(null)"}'.");
                return;
            }

            if (textStyles == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} TextStyleApplier.ApplyBasicProperties: " +
                    $"textStyles data is null for layer '{layerName ?? "(null)"}'.");
                return;
            }

            if (PSD2UILock.HasLock(tmpComponent.gameObject))
            {
                Debug.Log(
                    $"{LogPrefix} TextStyleApplier.ApplyBasicProperties: " +
                    $"'{layerName}' is locked (PSD2UILock detected), skipping text style application.");
                return;
            }

            // 1. Font size
            tmpComponent.fontSize = textStyles.fontSize;

            // 2. Color (r,g,b,a are already in Unity's 0-1 range from the parser)
            if (textStyles.color != null)
            {
                tmpComponent.color = new Color(
                    textStyles.color.r,
                    textStyles.color.g,
                    textStyles.color.b,
                    textStyles.color.a);
            }

            // 3. Font style (bold / italic)
            if (textStyles.fontStyle != null)
            {
                tmpComponent.fontStyle = MapFontStyle(textStyles.fontStyle);
            }

            // 4. Text alignment
            if (textStyles.alignment != null)
            {
                tmpComponent.alignment = MapAlignment(
                    textStyles.alignment.horizontal,
                    textStyles.alignment.vertical);
            }

            // 5. Font asset lookup
            if (!string.IsNullOrWhiteSpace(textStyles.fontName))
            {
                tmpComponent.font = FindFont(textStyles.fontName);
            }
        }

        public void ApplyEffects(
            TextMeshProUGUI tmpComponent,
            TextEffectData[] effects,
            string layerName,
            string layerId)
        {
            if (effects == null || effects.Length == 0)
            {
                return;
            }

            // Create independent Material
            var material = CreateMaterial(layerName, layerId);
            if (material == null) return;
            tmpComponent.fontSharedMaterial = material;

            // Apply each effect
            foreach (var effect in effects)
            {
                if (!effect.enabled) continue;

                switch (effect.type)
                {
                    case "stroke":
                        ApplyStroke(material, effect as StrokeEffectData);
                        break;
                    case "dropShadow":
                        ApplyDropShadow(material, effect as ShadowEffectData);
                        break;
                    case "innerShadow":
                        ApplyInnerShadow(material, effect as ShadowEffectData);
                        break;
                    case "gradient":
                        ApplyGradient(material, tmpComponent, effect as GradientEffectData);
                        break;
                    case "outerGlow":
                        ApplyOuterGlow(material, effect as GlowEffectData);
                        break;
                    case "bevel":
                        ApplyBevel(material, effect as BevelEffectData);
                        break;
                }
            }
        }

        // ---- Protected / Internal Methods (overridable for tests) ----

        private Material CreateMaterial(string layerName, string layerId)
        {
            var shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader == null)
            {
                Debug.LogError("[TextStyleApplier] TMP_SDF shader not found");
                return null;
            }

            var material = new Material(shader);
            material.name = $"TMP_{layerName}_{layerId}_Material";

            var materialPath = $"Assets/GameRes/Materials/UI/PSD2UI/{material.name}.mat";
            var directory = Path.GetDirectoryName(materialPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            return material;
        }

        private void ApplyStroke(Material mat, StrokeEffectData stroke)
        {
            if (mat == null || stroke == null) return;

            mat.SetFloat("_OutlineWidth", stroke.width);
            mat.SetColor("_OutlineColor", new Color(
                stroke.color.r,
                stroke.color.g,
                stroke.color.b,
                stroke.color.a
            ));
            mat.SetFloat("_OutlineSoftness", 0);
        }

        private void ApplyDropShadow(Material mat, ShadowEffectData shadow)
        {
            if (mat == null || shadow == null) return;

            mat.SetColor("_UnderlayColor", new Color(
                shadow.color.r,
                shadow.color.g,
                shadow.color.b,
                shadow.color.a
            ));
            mat.SetFloat("_UnderlayOffsetX", shadow.offsetX / 100f);
            mat.SetFloat("_UnderlayOffsetY", shadow.offsetY / 100f);
            mat.SetFloat("_UnderlaySoftness", shadow.blur / 10f);
        }

        private void ApplyInnerShadow(Material mat, ShadowEffectData innerShadow)
        {
            if (mat == null || innerShadow == null) return;

            mat.SetFloat("_UnderlayDilate", -0.5f);
            mat.SetColor("_UnderlayColor", new Color(
                innerShadow.color.r,
                innerShadow.color.g,
                innerShadow.color.b,
                innerShadow.color.a
            ));
            mat.SetFloat("_UnderlayOffsetX", innerShadow.offsetX / 100f);
            mat.SetFloat("_UnderlayOffsetY", innerShadow.offsetY / 100f);
            // blur maps to underlay softness (same as drop shadow)
            mat.SetFloat("_UnderlaySoftness", innerShadow.blur / 10f);
        }

        private void ApplyOuterGlow(Material mat, GlowEffectData glow)
        {
            if (mat == null || glow == null) return;

            mat.SetColor("_GlowColor", new Color(
                glow.color.r,
                glow.color.g,
                glow.color.b,
                glow.color.a
            ));
            mat.SetFloat("_GlowOffset", glow.size / 10f);
            mat.SetFloat("_GlowOuter", 1.0f);
            mat.SetFloat("_GlowPower", 0.75f);
            mat.SetFloat("_GlowInner", glow.spread / 10f);
        }

        private void ApplyGradient(Material mat, TextMeshProUGUI tmp, GradientEffectData gradient)
        {
            if (mat == null || gradient == null || gradient.colors == null || gradient.colors.Length < 2)
                return;

            mat.SetFloat("_GradientScale", 1.0f);

            var color1 = new Color(
                gradient.colors[0].r,
                gradient.colors[0].g,
                gradient.colors[0].b,
                gradient.colors[0].a
            );
            var color2 = new Color(
                gradient.colors[1].r,
                gradient.colors[1].g,
                gradient.colors[1].b,
                gradient.colors[1].a
            );

            if (gradient.angle == 90 || gradient.angle == 270)
            {
                tmp.enableVertexGradient = true;
                tmp.colorGradient = new VertexGradient(color1, color1, color2, color2);
            }
            else
            {
                tmp.enableVertexGradient = true;
                tmp.colorGradient = new VertexGradient(color1, color2, color1, color2);
            }
        }

        private void ApplyBevel(Material mat, BevelEffectData bevel)
        {
            if (mat == null || bevel == null) return;

            mat.SetFloat("_Bevel", 1.0f);
            mat.SetFloat("_BevelWidth", bevel.size / 10f);
            mat.SetFloat("_LightAngle", bevel.angle);
            mat.SetColor("_SpecularColor", new Color(
                bevel.highlightColor.r,
                bevel.highlightColor.g,
                bevel.highlightColor.b,
                bevel.highlightColor.a
            ));
            mat.SetFloat("_BevelClamp", bevel.depth / 100f);
            if (bevel.shadowColor != null)
                mat.SetColor("_ReflectColor", new Color(
                    bevel.shadowColor.r,
                    bevel.shadowColor.g,
                    bevel.shadowColor.b,
                    bevel.shadowColor.a
                ));
        }

        /// <summary>
        /// Finds a TMP_FontAsset by font name.
        ///
        /// Lookup order:
        /// 1. Exact name match via Resources.Load&lt;TMP_FontAsset&gt;.
        /// 2. Extension-stripped name match (handles .ttf/.otf suffixes).
        /// 3. Case-insensitive match against TMP_Settings.defaultFontAsset name.
        /// 4. Fallback to TMP_Settings.defaultFontAsset with a warning.
        /// 5. Returns null with an error if no default font is configured.
        ///
        /// Marked internal virtual so test assemblies can override it with a custom
        /// font resolver without requiring a full IFontResolver interface (Task 9).
        /// </summary>
        /// <param name="fontName">The PSD-reported font family name.</param>
        /// <returns>
        /// The matching TMP_FontAsset, or the default font asset as fallback.
        /// May return null if no default font asset is configured in TMP Settings.
        /// </returns>
        protected internal virtual TMP_FontAsset FindFont(string fontName)
        {
            // 1. If fontName is empty or whitespace, return null (caller keeps existing font)
            if (string.IsNullOrWhiteSpace(fontName))
                return null;

            // 2. Exact name match (Resources.Load)
            var exact = Resources.Load<TMP_FontAsset>(fontName);
            if (exact != null) return exact;

            // 3. Strip extension and retry exact match (fontName may contain .ttf etc.)
            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fontName);
            if (nameWithoutExt != fontName)
            {
                var exactNoExt = Resources.Load<TMP_FontAsset>(nameWithoutExt);
                if (exactNoExt != null) return exactNoExt;
            }

            // 4. Default font name match — case-insensitive against both original and extension-stripped name
            var defaultFont = DefaultFontAsset;
            if (defaultFont != null &&
                (string.Equals(defaultFont.name, fontName, System.StringComparison.OrdinalIgnoreCase) ||
                 (nameWithoutExt != fontName &&
                  string.Equals(defaultFont.name, nameWithoutExt, System.StringComparison.OrdinalIgnoreCase))))
                return defaultFont;

            // 5. Fallback to default font with warning
            if (defaultFont != null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} TextStyleApplier.FindFont: " +
                    $"Font '{fontName}' not found in Resources, falling back to default font '{defaultFont.name}'.");
                return defaultFont;
            }

            // 6. No default font; log error and return null
            Debug.LogError(
                $"{LogPrefix} TextStyleApplier.FindFont: " +
                $"Font '{fontName}' not found and no TMP default font asset is configured.");
            return null;
        }

        // ---- Public Static Helpers ----

        /// <summary>
        /// Checks that TMP Settings configuration is complete.
        /// Call during editor menu or PSD2UI tool initialization to catch
        /// configuration problems early.
        /// </summary>
        /// <returns>true if configuration is complete; false if issues exist.</returns>
        public static bool ValidateTMPSettings()
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogError(
                    $"{LogPrefix} TextStyleApplier.ValidateTMPSettings: " +
                    "TMP Settings asset not found. Please create it via Window > TextMeshPro > Settings.");
                return false;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} TextStyleApplier.ValidateTMPSettings: " +
                    "TMP Settings has no default font asset configured. Font matching will return null when no match is found.");
                return false;
            }

            return true;
        }

        // ---- Private Helpers ----

        /// <summary>
        /// Maps bold and italic boolean flags to the TMPro FontStyles flags enum.
        /// </summary>
        private static FontStyles MapFontStyle(FontStyleData style)
        {
            FontStyles result = FontStyles.Normal;
            if (style.bold) result |= FontStyles.Bold;
            if (style.italic) result |= FontStyles.Italic;
            return result;
        }

        /// <summary>
        /// Maps horizontal (left/center/right/justify) and vertical (top/middle/bottom)
        /// alignment strings to the corresponding TMPro TextAlignmentOptions value.
        ///
        /// Covers all 12 common combinations. Unknown or empty values default to
        /// center/middle. This mapping will be refined/extended in Task 9.
        /// </summary>
        private static TextAlignmentOptions MapAlignment(string horizontal, string vertical)
        {
            string h = (horizontal ?? "").ToLowerInvariant();
            string v = (vertical ?? "").ToLowerInvariant();

            switch (v)
            {
                case AlignTop:
                    switch (h)
                    {
                        case AlignLeft:    return TextAlignmentOptions.TopLeft;
                        case AlignRight:   return TextAlignmentOptions.TopRight;
                        case AlignJustify: return TextAlignmentOptions.TopJustified;
                        case AlignCenter:
                        default:           return TextAlignmentOptions.Top;
                    }

                case AlignBottom:
                    switch (h)
                    {
                        case AlignLeft:    return TextAlignmentOptions.BottomLeft;
                        case AlignRight:   return TextAlignmentOptions.BottomRight;
                        case AlignJustify: return TextAlignmentOptions.BottomJustified;
                        case AlignCenter:
                        default:           return TextAlignmentOptions.Bottom;
                    }

                case AlignMiddle:
                default:
                    switch (h)
                    {
                        case AlignLeft:    return TextAlignmentOptions.Left;
                        case AlignRight:   return TextAlignmentOptions.Right;
                        case AlignJustify: return TextAlignmentOptions.Justified;
                        case AlignCenter:
                        default:           return TextAlignmentOptions.Center;
                    }
            }
        }
    }
}
