using TMPro;
using UnityEngine;
using Change.Runtime.PSD2UI;

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
                    $"[PSD2UI] TextStyleApplier.ApplyBasicProperties: " +
                    $"TextMeshProUGUI component is null for layer '{layerName ?? "(null)"}'.");
                return;
            }

            if (textStyles == null)
            {
                Debug.LogWarning(
                    $"[PSD2UI] TextStyleApplier.ApplyBasicProperties: " +
                    $"textStyles data is null for layer '{layerName ?? "(null)"}'.");
                return;
            }

            if (HasLock(tmpComponent.gameObject))
            {
                Debug.Log(
                    $"[PSD2UI] TextStyleApplier.ApplyBasicProperties: " +
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
            if (!string.IsNullOrEmpty(textStyles.fontName))
            {
                tmpComponent.font = FindFont(textStyles.fontName);
            }
        }

        // ---- Private Helpers ----

        /// <summary>
        /// Checks whether a GameObject is protected by a PSD2UILock component,
        /// either on itself or on any ancestor with LockChildren enabled.
        /// Follows the same convention as IncrementalUpdater.HasLock().
        /// </summary>
        /// <param name="go">The GameObject to check.</param>
        /// <returns>True if the GameObject or any of its ancestors is locked.</returns>
        private static bool HasLock(GameObject go)
        {
            if (go == null) return false;

            // Check the GameObject itself for a direct lock
            if (go.GetComponent<PSD2UILock>() != null)
                return true;

            // Walk up ancestors to check for LockChildren cascading
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                var lockComp = parent.GetComponent<PSD2UILock>();
                if (lockComp != null && lockComp.LockChildren)
                    return true;
                parent = parent.parent;
            }

            return false;
        }

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
                case "top":
                    switch (h)
                    {
                        case "left":    return TextAlignmentOptions.TopLeft;
                        case "right":   return TextAlignmentOptions.TopRight;
                        case "justify": return TextAlignmentOptions.TopJustified;
                        case "center":
                        default:        return TextAlignmentOptions.Top;
                    }

                case "bottom":
                    switch (h)
                    {
                        case "left":    return TextAlignmentOptions.BottomLeft;
                        case "right":   return TextAlignmentOptions.BottomRight;
                        case "justify": return TextAlignmentOptions.BottomJustified;
                        case "center":
                        default:        return TextAlignmentOptions.Bottom;
                    }

                case "middle":
                default:
                    switch (h)
                    {
                        case "left":    return TextAlignmentOptions.Left;
                        case "right":   return TextAlignmentOptions.Right;
                        case "justify": return TextAlignmentOptions.Justified;
                        case "center":
                        default:        return TextAlignmentOptions.Center;
                    }
            }
        }

        /// <summary>
        /// Finds a TMP_FontAsset by font name.
        ///
        /// Current implementation (placeholder for Task 10):
        /// 1. Tries Resources.Load&lt;TMP_FontAsset&gt; by name.
        /// 2. Checks if TMP_Settings.defaultFontAsset name matches.
        /// 3. Falls back to TMP_Settings.instance.defaultFontAsset.
        ///
        /// Full fuzzy-matching (case-insensitive, partial name, AssetDatabase scan)
        /// will be added in Task 10.
        /// </summary>
        /// <param name="fontName">The PSD-reported font family name.</param>
        /// <returns>
        /// The matching TMP_FontAsset, or the default font asset as fallback.
        /// May return null if no default font asset is configured in TMP Settings.
        /// </returns>
        private static TMP_FontAsset FindFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
                return TMP_Settings.instance != null
                    ? TMP_Settings.instance.defaultFontAsset
                    : null;

            // Attempt direct Resources load by name
            var font = Resources.Load<TMP_FontAsset>(fontName);
            if (font != null)
                return font;

            // Check if the default font itself matches the requested name
            var defaultFont = TMP_Settings.instance != null
                ? TMP_Settings.instance.defaultFontAsset
                : null;

            if (defaultFont != null && defaultFont.name == fontName)
                return defaultFont;

            // Fallback: use default font asset with a warning
            if (defaultFont != null)
            {
                Debug.LogWarning(
                    $"[PSD2UI] TextStyleApplier.FindFont: " +
                    $"font '{fontName}' not found, falling back to default '{defaultFont.name}'.");
            }
            else
            {
                Debug.LogWarning(
                    $"[PSD2UI] TextStyleApplier.FindFont: " +
                    $"font '{fontName}' not found and no TMP default font asset is configured.");
            }

            return defaultFont;
        }
    }
}
