using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Factory for creating UI components on GameObjects.
    /// Supports Image, Text (TextMeshProUGUI), Button, ScrollRect, and InputField (TMP_InputField).
    /// TextMeshPro is a required dependency (declared in asmdef references).
    /// </summary>
    public class ComponentFactory
    {
        private readonly TextStyleApplier textStyleApplier = new TextStyleApplier();

        /// <summary>
        /// Creates a UI component of the specified type on the target GameObject.
        /// </summary>
        /// <param name="type">Component type: "Image", "Text", "Button", "ScrollRect", "InputField"</param>
        /// <param name="target">The GameObject to add the component to</param>
        /// <param name="textStyles">Optional text style data from PSD parser; applied when type is "Text".</param>
        /// <returns>The created Component, or null if the type is unrecognized</returns>
        public Component CreateComponent(string type, GameObject target, TextStylesData textStyles = null)
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
                    return target.AddComponent<Image>();

                case "Text":
                {
                    var tmp = target.AddComponent<TextMeshProUGUI>();
                    if (textStyles != null)
                    {
                        textStyleApplier.ApplyBasicProperties(tmp, textStyles, target.name);
                    }
                    return tmp;
                }

                case "Button":
                    var button = target.AddComponent<Button>();
                    var buttonImage = target.AddComponent<Image>();
                    button.targetGraphic = buttonImage;
                    return button;

                case "ScrollRect":
                    return target.AddComponent<ScrollRect>();

                case "InputField":
                    return target.AddComponent<TMP_InputField>();

                default:
                    Debug.LogWarning($"[PSD2UI] ComponentFactory: unrecognized component type '{type}'.");
                    return null;
            }
        }
    }
}
