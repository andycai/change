using System.IO;
using UnityEditor;
using UnityEngine;

namespace Change.Editor.PSD2UI
{
    public class AssetImporter
    {
        private const string TargetDirectory = "Assets/GameRes/UIPanelArt";

        public string ImportSprites(string sourcePath, string targetFileName)
        {
            if (string.IsNullOrEmpty(targetFileName))
            {
                throw new System.ArgumentException("targetFileName must not be null or empty.", nameof(targetFileName));
            }

            if (targetFileName.Contains(".."))
            {
                throw new System.ArgumentException(
                    $"targetFileName must not contain path traversal: '{targetFileName}'",
                    nameof(targetFileName));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source sprite not found: {sourcePath}", sourcePath);
            }

            string dataPath = Application.dataPath;
            string fullTargetDir = Path.Combine(dataPath, "GameRes/UIPanelArt");

            if (!Directory.Exists(fullTargetDir))
            {
                Directory.CreateDirectory(fullTargetDir);
            }

            string destPath = Path.Combine(fullTargetDir, targetFileName);
            try
            {
                File.Copy(sourcePath, destPath, true);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    $"Failed to copy sprite from '{sourcePath}' to '{destPath}': {ex.Message}", ex);
            }

            AssetDatabase.Refresh();

            string assetPath = Path.Combine(TargetDirectory, targetFileName);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            return assetPath;
        }

        public void ApplySliceSettings(string spritePath, SliceData slice)
        {
            if (string.IsNullOrEmpty(spritePath))
            {
                Debug.LogWarning("[PSD2UI] ApplySliceSettings: spritePath is null or empty, skipping.");
                return;
            }

            if (slice == null)
            {
                Debug.LogWarning("[PSD2UI] ApplySliceSettings: slice is null, skipping.");
                return;
            }

            var importer = UnityEditor.AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[PSD2UI] ApplySliceSettings: no TextureImporter found for '{spritePath}'.");
                return;
            }

            importer.spriteBorder = new Vector4(slice.Left, slice.Bottom, slice.Right, slice.Top);
            importer.SaveAndReimport();
        }
    }
}
