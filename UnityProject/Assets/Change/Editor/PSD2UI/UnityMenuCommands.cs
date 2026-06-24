using System.IO;
using UnityEditor;
using UnityEngine;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Unity Editor menu commands for the PSD2UI generator workflow.
    /// Provides menu items under Assets/PSD2UGUI that integrate all
    /// modules: ConfigReader, AssetImporter, PrefabBuilder, ViewCodeGenerator,
    /// ChangeDetector, and IncrementalUpdater.
    /// </summary>
    public static class UnityMenuCommands
    {
        private const string MenuRoot = "Assets/PSD2UGUI/";
        private const string PrefabOutputDir = "Assets/GameRes/UIPanelArt/Prefabs";

        #region Full Generation

        /// <summary>
        /// Generates a complete prefab with view code from a JSON config file.
        /// Prompts the user to select a JSON config, then imports all sprites,
        /// builds the prefab, and generates the C# view code.
        /// </summary>
        [MenuItem(MenuRoot + "Generate Prefab from Config")]
        public static void GeneratePrefabFromConfig()
        {
            string configPath = EditorUtility.OpenFilePanel(
                "Select JSON Config",
                "Assets",
                "json");

            if (string.IsNullOrEmpty(configPath))
            {
                // User cancelled
                return;
            }

            try
            {
                // 1. Read and validate config
                var reader = new ConfigReader();
                UINodeData nodeData = reader.LoadFromJson(configPath);

                if (nodeData == null)
                {
                    EditorUtility.DisplayDialog(
                        "PSD2UGUI Error",
                        "Failed to deserialize the JSON config. Check the file format.",
                        "OK");
                    return;
                }

                if (string.IsNullOrEmpty(nodeData.Name))
                {
                    EditorUtility.DisplayDialog(
                        "PSD2UGUI Error",
                        "The root node must have a valid Name. Please check the config.",
                        "OK");
                    return;
                }

                // 2. Determine sprite source directory (config directory by default)
                string spriteSourceDir = Path.GetDirectoryName(configPath);

                // 3. Import all sprites
                var importer = new AssetImporter();
                importer.ImportSprites(nodeData, spriteSourceDir);
                AssetDatabase.Refresh();

                // 4. Build prefab
                string prefabPath = EnsurePrefabOutputDirectory(nodeData.Name);
                var builder = new PrefabBuilder();
                GameObject prefab = builder.BuildPrefab(nodeData, prefabPath);

                if (prefab == null)
                {
                    EditorUtility.DisplayDialog(
                        "PSD2UGUI Error",
                        "Failed to build the prefab. Check the Console for details.",
                        "OK");
                    return;
                }

                // 5. Generate view code
                var codeGen = new ViewCodeGenerator();
                codeGen.GenerateViewCode(prefab, nodeData.Name);

                // Save a copy of the config alongside the prefab for future incremental updates
                SaveConfigCopy(configPath, nodeData.Name);

                // Success
                EditorUtility.DisplayDialog(
                    "PSD2UGUI - Complete",
                    $"Prefab generated successfully!\n\n" +
                    $"Prefab: {prefabPath}\n" +
                    $"View code: Assets/HotUpdate/GameLogic/{codeGen.ExtractModuleName(nodeData.Name)}/Views/Generated/{nodeData.Name}View.Generated.cs\n\n" +
                    $"A config copy was saved alongside the prefab for future incremental updates.",
                    "OK");

                Debug.Log($"[PSD2UI] Full generation complete for '{nodeData.Name}'.");
            }
            catch (FileNotFoundException ex)
            {
                Debug.LogError($"[PSD2UI] Config file error: {ex.Message}");
                EditorUtility.DisplayDialog("PSD2UGUI Error", $"Config file error:\n{ex.Message}", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PSD2UI] Generation failed: {ex}");
                EditorUtility.DisplayDialog(
                    "PSD2UGUI Error",
                    $"An unexpected error occurred:\n{ex.Message}\n\nCheck the Console for details.",
                    "OK");
            }
        }

        #endregion

        #region Incremental Update

        /// <summary>
        /// Performs an incremental update of an existing prefab from a new JSON config.
        /// Prompts for the old (baseline) config, the new config, and the target prefab.
        /// Only changed nodes are updated; locked nodes are preserved.
        /// </summary>
        [MenuItem(MenuRoot + "Incremental Update from Config")]
        public static void IncrementalUpdateFromConfig()
        {
            // Select old (baseline) config
            string oldConfigPath = EditorUtility.OpenFilePanel(
                "Select OLD (baseline) JSON Config",
                "Assets",
                "json");

            if (string.IsNullOrEmpty(oldConfigPath)) return;

            // Select new config
            string newConfigPath = EditorUtility.OpenFilePanel(
                "Select NEW JSON Config",
                Path.GetDirectoryName(oldConfigPath) ?? "Assets",
                "json");

            if (string.IsNullOrEmpty(newConfigPath)) return;

            // Select target prefab
            string prefabPath = EditorUtility.OpenFilePanel(
                "Select Target Prefab to Update",
                PrefabOutputDir,
                "prefab");

            if (string.IsNullOrEmpty(prefabPath)) return;

            // Convert to asset-relative path
            string dataPath = Application.dataPath;
            if (!prefabPath.StartsWith(dataPath))
            {
                EditorUtility.DisplayDialog(
                    "PSD2UGUI Error",
                    "The selected prefab must be inside the Unity project Assets directory.",
                    "OK");
                return;
            }

            string assetPrefabPath = "Assets" + prefabPath.Substring(dataPath.Length);

            try
            {
                // 1. Load both configs
                var reader = new ConfigReader();
                UINodeData oldConfig = reader.LoadFromJson(oldConfigPath);
                UINodeData newConfig = reader.LoadFromJson(newConfigPath);

                if (oldConfig == null || newConfig == null)
                {
                    EditorUtility.DisplayDialog(
                        "PSD2UGUI Error",
                        "Failed to load one or both config files.",
                        "OK");
                    return;
                }

                // 2. Determine sprite source directory (from new config location)
                string spriteSourceDir = Path.GetDirectoryName(newConfigPath);

                // 3. Perform incremental update
                var updater = new IncrementalUpdater();
                updater.Update(assetPrefabPath, oldConfig, newConfig, spriteSourceDir);

                AssetDatabase.Refresh();

                // 4. Regenerate view code if prefab structure changed
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPrefabPath);
                if (prefab != null)
                {
                    var codeGen = new ViewCodeGenerator();
                    codeGen.GenerateViewCode(prefab, newConfig.Name);
                }

                // 5. Save the new config as baseline for next incremental update
                SaveConfigCopy(newConfigPath, newConfig.Name);

                EditorUtility.DisplayDialog(
                    "PSD2UGUI - Incremental Update Complete",
                    $"Prefab updated successfully!\n\n" +
                    $"Prefab: {assetPrefabPath}\n" +
                    $"Locked GameObjects were preserved.\n" +
                    $"View code was regenerated.\n" +
                    $"The new config was saved as the updated baseline.",
                    "OK");

                Debug.Log($"[PSD2UI] Incremental update complete for '{newConfig.Name}'.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PSD2UI] Incremental update failed: {ex}");
                EditorUtility.DisplayDialog(
                    "PSD2UGUI Error",
                    $"An unexpected error occurred during incremental update:\n{ex.Message}\n\nCheck the Console for details.",
                    "OK");
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a JSON config file and displays the results in a dialog.
        /// </summary>
        [MenuItem(MenuRoot + "Validate Config")]
        public static void ValidateConfig()
        {
            string configPath = EditorUtility.OpenFilePanel(
                "Select JSON Config to Validate",
                "Assets",
                "json");

            if (string.IsNullOrEmpty(configPath)) return;

            try
            {
                var reader = new ConfigReader();
                bool isValid = reader.ValidateConfig(configPath, out var errors);

                if (isValid)
                {
                    EditorUtility.DisplayDialog(
                        "PSD2UGUI - Validation Passed",
                        "The config file is valid.",
                        "OK");
                }
                else
                {
                    string errorList = string.Join("\n", errors);
                    EditorUtility.DisplayDialog(
                        "PSD2UGUI - Validation Failed",
                        $"The config file has {errors.Count} issue(s):\n\n{errorList}",
                        "OK");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "PSD2UGUI Error",
                    $"Validation error:\n{ex.Message}",
                    "OK");
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Ensures the prefab output directory exists and returns the full
        /// asset path for the prefab file.
        /// </summary>
        private static string EnsurePrefabOutputDirectory(string prefabName)
        {
            string fullDir = Path.Combine(Application.dataPath, "GameRes/UIPanelArt/Prefabs");
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
            }

            return Path.Combine(PrefabOutputDir, prefabName + ".prefab");
        }

        /// <summary>
        /// Saves a copy of the JSON config alongside the prefab so it can serve
        /// as the baseline for future incremental updates.
        /// The copy is saved as Assets/GameRes/UIPanelArt/Prefabs/{name}_config.json.
        /// </summary>
        private static void SaveConfigCopy(string sourceConfigPath, string prefabName)
        {
            string fullDir = Path.Combine(Application.dataPath, "GameRes/UIPanelArt/Prefabs");
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
            }

            string destPath = Path.Combine(fullDir, prefabName + "_config.json");
            try
            {
                File.Copy(sourceConfigPath, destPath, true);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[PSD2UI] Failed to save config copy to '{destPath}': {ex.Message}");
            }
        }

        #endregion
    }
}
