using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Factory for creating UI components on GameObjects.
    /// Supports Image, Text, Button, ScrollRect, and InputField.
    /// TextMeshPro is detected via reflection and preferred over legacy Text.
    /// </summary>
    public class ComponentFactory
    {
        /// <summary>
        /// Creates a UI component of the specified type on the target GameObject.
        /// </summary>
        /// <param name="type">Component type: "Image", "Text", "Button", "ScrollRect", "InputField"</param>
        /// <param name="target">The GameObject to add the component to</param>
        /// <returns>The created Component, or null if the type is unrecognized</returns>
        public Component CreateComponent(string type, GameObject target)
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
                    return TryAddTextMeshPro(target) ?? target.AddComponent<Text>();

                case "Button":
                    var button = target.AddComponent<Button>();
                    // Ensure an Image exists for the Button's targetGraphic.
                    // If one was already added (e.g. node type was "Image" first),
                    // AddComponent<Image> would return the existing one — but since
                    // CreateComponent is called once per node, this is always a fresh add.
                    var buttonImage = target.AddComponent<Image>();
                    button.targetGraphic = buttonImage;
                    return button;

                case "ScrollRect":
                    return target.AddComponent<ScrollRect>();

                case "InputField":
                    return TryAddTMPInputField(target) ?? target.AddComponent<InputField>();

                default:
                    Debug.LogWarning($"[PSD2UI] ComponentFactory: unrecognized component type '{type}'.");
                    return null;
            }
        }

        /// <summary>
        /// Attempts to add a TextMeshProUGUI component via reflection.
        /// Returns null if TextMeshPro is not available in the project.
        /// </summary>
        private Component TryAddTextMeshPro(GameObject target)
        {
            var tmpType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                return target.AddComponent(tmpType) as Component;
            }

            return null;
        }

        /// <summary>
        /// Attempts to add a TMP_InputField component via reflection.
        /// Returns null if TextMeshPro is not available in the project.
        /// </summary>
        private Component TryAddTMPInputField(GameObject target)
        {
            var tmpInputType = System.Type.GetType("TMPro.TMP_InputField, Unity.TextMeshPro");
            if (tmpInputType != null)
            {
                return target.AddComponent(tmpInputType) as Component;
            }

            return null;
        }
    }
}
