using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Change.Editor.PSD2UI
{
    public class MaterialCleanupTool
    {
        private const string MaterialDirectory = "Assets/GameRes/Materials/UI/PSD2UI/";

        [MenuItem("Tools/PSD2UI/Cleanup Unused Materials")]
        public static void CleanupUnusedMaterials()
        {
            if (!EditorUtility.DisplayDialog(
                "Material Cleanup",
                "This will scan all PSD2UI materials and delete unused ones. Continue?",
                "Yes", "Cancel"))
            {
                return;
            }

            var unusedMaterials = FindUnusedMaterials();

            if (unusedMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("Cleanup Complete", "No unused materials found.", "OK");
                return;
            }

            var message = $"Found {unusedMaterials.Count} unused materials:\n\n";
            message += string.Join("\n", unusedMaterials.Take(10).Select(m => Path.GetFileName(m)));
            if (unusedMaterials.Count > 10)
            {
                message += $"\n... and {unusedMaterials.Count - 10} more";
            }

            if (EditorUtility.DisplayDialog("Delete Unused Materials", message, "Delete", "Cancel"))
            {
                DeleteMaterials(unusedMaterials);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Cleanup Complete",
                    $"Deleted {unusedMaterials.Count} unused materials.", "OK");
            }
        }

        private static List<string> FindUnusedMaterials()
        {
            var unusedMaterials = new List<string>();

            if (!Directory.Exists(MaterialDirectory))
            {
                return unusedMaterials;
            }

            // Get all PSD2UI materials
            var allMaterials = Directory.GetFiles(MaterialDirectory, "*.mat", SearchOption.AllDirectories)
                .Select(NormalizePath)
                .ToArray();

            // Find all materials referenced by Prefabs
            var referencedMaterials = new HashSet<string>();
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                var tmpComponents = prefab.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var tmp in tmpComponents)
                {
                    if (tmp.fontSharedMaterial != null)
                    {
                        var matPath = AssetDatabase.GetAssetPath(tmp.fontSharedMaterial);
                        if (!string.IsNullOrEmpty(matPath))
                        {
                            referencedMaterials.Add(NormalizePath(Path.GetFullPath(matPath)));
                        }
                    }
                }
            }

            // Find unreferenced materials
            foreach (var materialPath in allMaterials)
            {
                var fullPath = Path.GetFullPath(materialPath);
                if (!referencedMaterials.Contains(NormalizePath(fullPath)))
                {
                    unusedMaterials.Add(materialPath);
                }
            }

            return unusedMaterials;
        }

        private static void DeleteMaterials(List<string> materialPaths)
        {
            foreach (var path in materialPaths)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [MenuItem("Tools/PSD2UI/Show Material Statistics")]
        public static void ShowMaterialStatistics()
        {
            if (!Directory.Exists(MaterialDirectory))
            {
                EditorUtility.DisplayDialog("Statistics", "Material directory does not exist.", "OK");
                return;
            }

            var allMaterials = Directory.GetFiles(MaterialDirectory, "*.mat", SearchOption.AllDirectories);
            var unusedMaterials = FindUnusedMaterials();
            var usedMaterials = allMaterials.Length - unusedMaterials.Count;

            var message = $"Total Materials: {allMaterials.Length}\n";
            message += $"Used Materials: {usedMaterials}\n";
            message += $"Unused Materials: {unusedMaterials.Count}\n\n";

            var totalSize = allMaterials.Sum(path => new FileInfo(path).Length);
            message += $"Total Size: {FormatBytes(totalSize)}";

            EditorUtility.DisplayDialog("Material Statistics", message, "OK");
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
