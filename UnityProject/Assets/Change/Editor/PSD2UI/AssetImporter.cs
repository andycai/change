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

            if (Path.GetFileName(targetFileName) != targetFileName)
            {
                throw new System.ArgumentException(
                    $"Invalid filename: contains path separators or traversal: '{targetFileName}'",
                    nameof(targetFileName));
            }

            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new System.ArgumentException("sourcePath must not be null or empty.", nameof(sourcePath));
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

        /// <summary>
        /// Recursively imports all sprite files referenced in a UINodeData tree.
        /// Each node's SpritePath is resolved relative to the source directory,
        /// and the file name portion is used as the target asset name.
        /// Nodes without a SpritePath are silently skipped.
        /// </summary>
        /// <param name="nodeData">The root UINodeData tree to traverse.</param>
        /// <param name="sourceDirectory">
        /// File system directory containing the source sprite images.
        /// Each node's SpritePath is combined with this directory.
        /// </param>
        public void ImportSprites(UINodeData nodeData, string sourceDirectory)
        {
            if (nodeData == null)
            {
                Debug.LogWarning("[PSD2UI] AssetImporter.ImportSprites: nodeData is null.");
                return;
            }

            if (string.IsNullOrEmpty(sourceDirectory))
            {
                Debug.LogWarning("[PSD2UI] AssetImporter.ImportSprites: sourceDirectory is null or empty.");
                return;
            }

            ImportSpritesRecursive(nodeData, sourceDirectory);
        }

        private void ImportSpritesRecursive(UINodeData node, string sourceDirectory)
        {
            if (node == null) return;

            if (!string.IsNullOrEmpty(node.SpritePath))
            {
                string sourcePath = Path.Combine(sourceDirectory, node.SpritePath);
                string fileName = Path.GetFileName(node.SpritePath);

                if (!string.IsNullOrEmpty(fileName))
                {
                    if (File.Exists(sourcePath))
                    {
                        ImportSprites(sourcePath, fileName);

                        // Apply slice settings after import
                        if (node.Slice != null)
                        {
                            string assetPath = Path.Combine(TargetDirectory, fileName);
                            ApplySliceSettings(assetPath, node.Slice);
                        }
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[PSD2UI] AssetImporter: source sprite not found: '{sourcePath}', skipping.");
                    }
                }
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ImportSpritesRecursive(child, sourceDirectory);
                }
            }
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
