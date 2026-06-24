using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    public class AssetImporterTests
    {
        private const string TestTargetDir = "Assets/GameRes/UIPanelArt";
        private string testImagePath;
        private AssetImporter assetImporter;

        [SetUp]
        public void SetUp()
        {
            assetImporter = new AssetImporter();

            // Create a small test PNG texture
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            byte[] pngData = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            testImagePath = Path.Combine(Application.temporaryCachePath, "test_import_source.png");
            File.WriteAllBytes(testImagePath, pngData);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up temp source image
            if (File.Exists(testImagePath))
            {
                File.Delete(testImagePath);
            }

            // Clean up imported test assets by glob pattern
            string fullTargetDir = Path.Combine(Application.dataPath, "GameRes/UIPanelArt");
            if (Directory.Exists(fullTargetDir))
            {
                var files = Directory.GetFiles(fullTargetDir, "test_*.png");
                foreach (var file in files)
                {
                    string metaFile = file + ".meta";
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                    }
                    File.Delete(file);
                }
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void ImportSprites_ValidSource_ImportsToUIPanelArt()
        {
            string spritePath = assetImporter.ImportSprites(testImagePath, "test_sprite.png");

            Assert.IsNotNull(spritePath);
            Assert.That(spritePath, Does.Contain(TestTargetDir));
            Assert.That(spritePath, Does.EndWith("test_sprite.png"));

            // Verify the file exists on disk
            string fullPath = Path.Combine(Application.dataPath, "GameRes/UIPanelArt/test_sprite.png");
            Assert.IsTrue(File.Exists(fullPath), $"Expected file at: {fullPath}");

            // Verify the asset is recognized by Unity
            var loadedAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
            Assert.IsNotNull(loadedAsset, "Imported asset should be loadable as Texture2D");
        }

        [Test]
        public void ImportSprites_NullFileName_ThrowsArgumentException()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
            {
                assetImporter.ImportSprites(testImagePath, null);
            });

            Assert.That(ex.Message, Does.Contain("targetFileName"));
        }

        [Test]
        public void ImportSprites_PathTraversal_ThrowsArgumentException()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
            {
                assetImporter.ImportSprites(testImagePath, "../escape.png");
            });

            Assert.That(ex.Message, Does.Contain("Invalid filename"));
        }

        [Test]
        public void ImportSprites_MissingSource_ThrowsFileNotFoundException()
        {
            string badPath = Path.Combine(Application.temporaryCachePath, "nonexistent.png");

            var ex = Assert.Throws<FileNotFoundException>(() =>
            {
                assetImporter.ImportSprites(badPath, "should_fail.png");
            });

            Assert.That(ex.Message, Does.Contain("nonexistent.png"));
        }

        [Test]
        public void ApplySliceSettings_ValidSprite_SetsSpriteBorder()
        {
            string spritePath = assetImporter.ImportSprites(testImagePath, "test_slice_sprite.png");

            var slice = new SliceData { Left = 10, Top = 5, Right = 20, Bottom = 8 };
            assetImporter.ApplySliceSettings(spritePath, slice);

            var importer = UnityEditor.AssetImporter.GetAtPath(spritePath) as TextureImporter;
            Assert.IsNotNull(importer, "Should have a TextureImporter");

            Assert.AreEqual(
                new Vector4(10, 8, 20, 5), // left, bottom, right, top
                importer.spriteBorder,
                "Sprite border should match the slice data");
        }

        [Test]
        public void ApplySliceSettings_NullSpritePath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                assetImporter.ApplySliceSettings(null, new SliceData());
            });
        }

        [Test]
        public void ApplySliceSettings_NullSlice_DoesNotThrow()
        {
            string spritePath = assetImporter.ImportSprites(testImagePath, "test_null_slice.png");

            Assert.DoesNotThrow(() =>
            {
                assetImporter.ApplySliceSettings(spritePath, null);
            });

            // Border should remain at default (zero)
            var importer = UnityEditor.AssetImporter.GetAtPath(spritePath) as TextureImporter;
            Assert.IsNotNull(importer);
            Assert.AreEqual(Vector4.zero, importer.spriteBorder);
        }

        [Test]
        public void ApplySliceSettings_InvalidPath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                assetImporter.ApplySliceSettings("Assets/Nonexistent/sprite.png", new SliceData());
            });
        }
    }
}
