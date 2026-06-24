using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Represents a detected interactive UI component for code generation.
    /// </summary>
    public class ComponentInfo
    {
        /// <summary>The GameObject name (used as field name in generated code).</summary>
        public string Name { get; set; }

        /// <summary>The Unity component type: Button, Toggle, Slider, InputField, ScrollRect.</summary>
        public string Type { get; set; }

        /// <summary>The UnityEvent property name: onClick, onValueChanged.</summary>
        public string EventType { get; set; }
    }

    /// <summary>
    /// Scans prefab GameObjects for interactive UI components and orchestrates
    /// the generation of the corresponding view code via ViewCodeTemplate.
    /// </summary>
    public class ViewCodeGenerator
    {
        private readonly ViewCodeTemplate _template = new ViewCodeTemplate();

        /// <summary>
        /// Recursively scans a prefab for interactive components (Button, Toggle,
        /// Slider, InputField, ScrollRect) and returns them as a list of ComponentInfo.
        /// </summary>
        /// <param name="prefab">The root GameObject of the prefab to scan.</param>
        /// <returns>List of detected interactive components.</returns>
        public List<ComponentInfo> ScanComponents(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[PSD2UI] ViewCodeGenerator.ScanComponents: prefab is null.");
                return new List<ComponentInfo>();
            }

            var components = new List<ComponentInfo>();
            ScanRecursive(prefab.transform, components);
            return components;
        }

        /// <summary>
        /// Recursively walks the transform hierarchy and collects interactive components.
        /// </summary>
        private void ScanRecursive(Transform transform, List<ComponentInfo> components)
        {
            if (transform == null) return;

            var button = transform.GetComponent<Button>();
            if (button != null)
            {
                components.Add(new ComponentInfo
                {
                    Name = transform.name,
                    Type = "Button",
                    EventType = "onClick"
                });
            }

            var toggle = transform.GetComponent<Toggle>();
            if (toggle != null)
            {
                components.Add(new ComponentInfo
                {
                    Name = transform.name,
                    Type = "Toggle",
                    EventType = "onValueChanged"
                });
            }

            var slider = transform.GetComponent<Slider>();
            if (slider != null)
            {
                components.Add(new ComponentInfo
                {
                    Name = transform.name,
                    Type = "Slider",
                    EventType = "onValueChanged"
                });
            }

            var inputField = transform.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                components.Add(new ComponentInfo
                {
                    Name = transform.name,
                    Type = "InputField",
                    EventType = "onValueChanged"
                });
            }

            var scrollRect = transform.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                components.Add(new ComponentInfo
                {
                    Name = transform.name,
                    Type = "ScrollRect",
                    EventType = "onValueChanged"
                });
            }

            foreach (Transform child in transform)
            {
                ScanRecursive(child, components);
            }
        }

        /// <summary>
        /// Extracts the module name from a prefab name by stripping
        /// "Window" or "Panel" suffixes.
        /// </summary>
        /// <param name="prefabName">The prefab name (e.g., "QuestWindow", "InventoryPanel").</param>
        /// <returns>The module name, or "Common" if nothing remains after stripping.</returns>
        public string ExtractModuleName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return "Common";

            string moduleName = prefabName.Replace("Window", "").Replace("Panel", "");
            return string.IsNullOrEmpty(moduleName) ? "Common" : moduleName;
        }

        /// <summary>
        /// Generates the view code file for the given prefab.
        /// Scans components, generates code via ViewCodeTemplate, and writes to
        /// <c>Assets/HotUpdate/GameLogic/{module}/Views/Generated/{name}View.Generated.cs</c>.
        /// </summary>
        /// <param name="prefab">The root GameObject of the prefab.</param>
        /// <param name="prefabName">The prefab file name (without extension).</param>
        public void GenerateViewCode(GameObject prefab, string prefabName)
        {
            if (prefab == null)
            {
                Debug.LogError("[PSD2UI] ViewCodeGenerator.GenerateViewCode: prefab is null.");
                return;
            }

            if (string.IsNullOrEmpty(prefabName))
            {
                Debug.LogError("[PSD2UI] ViewCodeGenerator.GenerateViewCode: prefabName is null or empty.");
                return;
            }

            var components = ScanComponents(prefab);
            string moduleName = ExtractModuleName(prefabName);
            string generatedCode = _template.Generate(prefabName, moduleName, components);

            string directory = Path.Combine("Assets", "HotUpdate", "GameLogic", moduleName, "Views", "Generated");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string filePath = Path.Combine(directory, $"{prefabName}View.Generated.cs");
            File.WriteAllText(filePath, generatedCode);
            AssetDatabase.Refresh();

            Debug.Log($"[PSD2UI] Generated view code: {filePath} ({components.Count} components scanned)");
        }
    }
}
