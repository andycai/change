using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Represents a detected interactive UI component for code generation.
    /// </summary>
    public class ViewComponentInfo
    {
        /// <summary>The sanitized, unique name used as the field name in generated code.</summary>
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

        // ── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Recursively scans a prefab for interactive components (Button, Toggle,
        /// Slider, InputField, ScrollRect) and returns them as a list of ComponentInfo.
        /// Duplicate names are resolved by appending a numeric suffix (_1, _2, ...).
        /// </summary>
        /// <param name="prefab">The root GameObject of the prefab to scan.</param>
        /// <returns>List of detected interactive components with unique, sanitized names.</returns>
        public List<ViewComponentInfo> ScanComponents(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[PSD2UI] ViewCodeGenerator.ScanComponents: prefab is null.");
                return new List<ViewComponentInfo>();
            }

            var components = new List<ViewComponentInfo>();
            // Use "" as initial prefix so root's children start with just their own name.
            ScanRecursive(prefab.transform, components, "");
            ResolveDuplicateNames(components);
            return components;
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
        /// File I/O is wrapped in try-catch so exceptions are logged rather than thrown.
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
            string generatedCode = _template.GenerateClass(prefabName, moduleName, components);

            string directory = Path.Combine("Assets", "HotUpdate", "GameLogic", moduleName, "Views", "Generated");
            string filePath = Path.Combine(directory, $"{prefabName}View.Generated.cs");

            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, generatedCode);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PSD2UI] Failed to write generated view code to '{filePath}': {ex.Message}");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[PSD2UI] Generated view code: {filePath} ({components.Count} components scanned)");
        }

        /// <summary>
        /// Converts a GameObject name into a valid C# identifier by replacing
        /// illegal characters with underscores and handling leading digits.
        /// </summary>
        /// <param name="name">The raw GameObject name (may contain spaces, parens, etc.).</param>
        /// <returns>A valid C# identifier suitable for use as a field or method name.</returns>
        public static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Component";

            var sb = new StringBuilder(name.Length + 2);

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (i == 0 && char.IsDigit(c))
                {
                    sb.Append('_');
                    sb.Append(c);
                }
                else if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
                else
                {
                    // Replace any illegal character with underscore
                    sb.Append('_');
                }
            }

            // Collapse consecutive underscores
            string result = sb.ToString();
            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            // Trim leading/trailing underscores
            result = result.Trim('_');

            // If the result is empty or just an underscore, use fallback
            if (string.IsNullOrEmpty(result) || result == "_")
                return "Component";

            return result;
        }

        // ── Private helpers ──────────────────────────────────────────

        /// <summary>
        /// Recursively walks the transform hierarchy, building a hierarchy-based
        /// name for each component to avoid name collisions between different
        /// branches (e.g., Header/CloseButton vs Footer/CloseButton).
        /// </summary>
        /// <param name="transform">The current transform to inspect.</param>
        /// <param name="components">The list to append detected components to.</param>
        /// <param name="parentPath">
        /// The sanitized path from the root to the parent, or "" for the root level.
        /// Each level is separated by "_".
        /// </param>
        private void ScanRecursive(Transform transform, List<ViewComponentInfo> components, string parentPath)
        {
            if (transform == null) return;

            // Build the full hierarchical name for this node
            string rawName = transform.name;
            string sanitized = SanitizeIdentifier(rawName);
            string fullName = string.IsNullOrEmpty(parentPath)
                ? sanitized
                : $"{parentPath}_{sanitized}";

            // Detect interactive components using the hierarchy-based name
            AddComponentIfPresent<Button>(transform, components, fullName, "Button", "onClick");
            AddComponentIfPresent<Toggle>(transform, components, fullName, "Toggle", "onValueChanged");
            AddComponentIfPresent<Slider>(transform, components, fullName, "Slider", "onValueChanged");
            AddComponentIfPresent<TMP_InputField>(transform, components, fullName, "InputField", "onValueChanged");
            AddComponentIfPresent<ScrollRect>(transform, components, fullName, "ScrollRect", "onValueChanged");

            // Recurse into children, passing this node's sanitized name as the next parent prefix
            string childPrefix = string.IsNullOrEmpty(parentPath)
                ? sanitized
                : $"{parentPath}_{sanitized}";

            foreach (Transform child in transform)
            {
                ScanRecursive(child, components, childPrefix);
            }
        }

        /// <summary>
        /// Helper that checks for a component on the transform and, if present,
        /// adds a ComponentInfo entry with the given type and eventType.
        /// </summary>
        private static void AddComponentIfPresent<T>(Transform transform, List<ViewComponentInfo> components,
            string name, string type, string eventType)
            where T : Component
        {
            if (transform.GetComponent<T>() != null)
            {
                components.Add(new ViewComponentInfo
                {
                    Name = name,
                    Type = type,
                    EventType = eventType
                });
            }
        }

        /// <summary>
        /// Detects duplicate component names in the list and resolves them by
        /// appending a numeric suffix (_1, _2, ...) starting from the second occurrence.
        /// The first occurrence keeps the original name.
        /// </summary>
        private static void ResolveDuplicateNames(List<ViewComponentInfo> components)
        {
            var nameCounts = new Dictionary<string, int>();

            // First pass: count occurrences of each name
            foreach (var comp in components)
            {
                if (nameCounts.ContainsKey(comp.Name))
                    nameCounts[comp.Name]++;
                else
                    nameCounts[comp.Name] = 1;
            }

            // Second pass: rename duplicates
            var renameCounters = new Dictionary<string, int>();
            for (int i = 0; i < components.Count; i++)
            {
                string name = components[i].Name;
                if (nameCounts[name] <= 1) continue;

                if (!renameCounters.ContainsKey(name))
                    renameCounters[name] = 0;  // first occurrence keeps original name
                else
                    renameCounters[name]++;

                if (renameCounters[name] > 0)
                    components[i].Name = $"{name}_{renameCounters[name]}";
            }
        }
    }
}
