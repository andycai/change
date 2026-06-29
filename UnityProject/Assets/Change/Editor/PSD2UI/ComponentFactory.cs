using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Factory for creating UI components on GameObjects.
    /// Supports Image, RawImage, Text (TextMeshProUGUI or UGUI), Button,
    /// ScrollRect, InputField (TMP_InputField), Dropdown, Toggle, Slider, Mask.
    /// </summary>
    public class ComponentFactory
    {
        private readonly TextStyleApplier textStyleApplier = new TextStyleApplier();

        /// <summary>
        /// Creates a UI component of the specified type on the target GameObject.
        /// </summary>
        /// <param name="type">
        /// Component type: "Image" | "RawImage" | "Text" | "Button" | "ScrollRect" |
        /// "InputField" | "Dropdown" | "Toggle" | "Slider" | "Mask" | "FillColor"
        /// </param>
        /// <param name="target">The GameObject to add the component to</param>
        /// <param name="info">
        /// Optional ComponentInfo with extended attributes (ImageType, TextBackend, Role).
        /// Pass null to use default behavior (backward compatible).
        /// </param>
        /// <param name="textStyles">Optional text style data from PSD parser; applied when type is "Text".</param>
        /// <returns>The created Component, or null if the type is unrecognized</returns>
        public Component CreateComponent(string type, GameObject target, ComponentInfo info = null, TextStylesData textStyles = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[PSD2UI] ComponentFactory.CreateComponent: target is null.");
                return null;
            }

            if (string.IsNullOrEmpty(type))
            {
                Debug.LogWarning("[PSD2UI] ComponentFactory.CreateComponent: type is null or empty.");
                return null;
            }

            switch (type)
            {
                case "Image":
                    var image = target.AddComponent<Image>();
                    if (info?.ImageType != null)
                        ConfigureImageType(image, info.ImageType);
                    return image;

                case "RawImage":
                    return target.AddComponent<RawImage>();

                case "Text":
                {
                    var tmp = CreateTextComponent(info?.TextBackend, target) as TextMeshProUGUI;
                    if (tmp != null && textStyles != null)
                    {
                        textStyleApplier.ApplyBasicProperties(tmp, textStyles, target.name);
                        if (textStyles.effects != null && textStyles.effects.Count > 0)
                        {
                            textStyleApplier.ApplyEffects(tmp, textStyles, target.name);
                        }
                    }
                    return tmp ?? CreateTextComponent(info?.TextBackend, target);
                }

                case "Button":
                    var button = target.AddComponent<Button>();
                    var buttonImage = target.AddComponent<Image>();
                    button.targetGraphic = buttonImage;
                    return button;

                case "ScrollRect":
                case "ScrollView":
                    return target.AddComponent<ScrollRect>();

                case "InputField":
                    return target.AddComponent<TMP_InputField>();

                case "Dropdown":
                    return target.AddComponent<Dropdown>();

                case "Toggle":
                    return target.AddComponent<Toggle>();

                case "Slider":
                    return target.AddComponent<Slider>();

                case "Mask":
                    return target.AddComponent<Mask>();

                case "FillColor":
                    Debug.LogWarning("[PSD2UI] ComponentFactory: FillColor has no direct Unity component. Using Image.");
                    return target.AddComponent<Image>();

                case "VerticalLayoutGroup":
                    return target.AddComponent<VerticalLayoutGroup>();

                case "HorizontalLayoutGroup":
                    return target.AddComponent<HorizontalLayoutGroup>();

                case "GridLayoutGroup":
                    return target.AddComponent<GridLayoutGroup>();

                default:
                    Debug.LogWarning($"[PSD2UI] ComponentFactory: unrecognized component type '{type}'.");
                    return null;
            }
        }

        /// <summary>
        /// Creates a UI component for the given node if its type is configured
        /// (non-null, non-empty, and not "Container"). Returns null otherwise.
        /// </summary>
        /// <param name="node">The UINodeData describing the UI element.</param>
        /// <param name="target">The GameObject to add the component to.</param>
        /// <returns>The created Component, or null if the node has no component type configured.</returns>
        public Component CreateComponentIfConfigured(UINodeData node, GameObject target)
        {
            if (node == null || string.IsNullOrEmpty(node.Type) || node.Type == "Container")
                return null;
            return CreateComponent(node.Type, target, null, node.TextStyles);
        }

        /// <summary>
        /// Configures the Image.type property based on the imageType string.
        /// </summary>
        private void ConfigureImageType(Image image, string imageType)
        {
            switch (imageType)
            {
                case "simple":
                    image.type = Image.Type.Simple;
                    break;
                case "sliced":
                    image.type = Image.Type.Sliced;
                    break;
                case "tiled":
                    image.type = Image.Type.Tiled;
                    break;
                case "filled":
                    image.type = Image.Type.Filled;
                    break;
                default:
                    Debug.LogWarning($"[PSD2UI] ComponentFactory: unknown imageType '{imageType}', using Simple.");
                    image.type = Image.Type.Simple;
                    break;
            }
        }

        /// <summary>
        /// Creates a text component based on the textBackend preference.
        /// "ugui" -> UGUI Text; anything else (or null) -> TextMeshProUGUI (default).
        /// </summary>
        private Component CreateTextComponent(string textBackend, GameObject target)
        {
            if (textBackend == "ugui")
                return target.AddComponent<UnityEngine.UI.Text>();
            else
                return target.AddComponent<TextMeshProUGUI>();
        }
    }
}
