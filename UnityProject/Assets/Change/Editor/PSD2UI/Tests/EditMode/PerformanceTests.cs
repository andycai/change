using NUnit.Framework;
using UnityEngine;
using TMPro;
using System.Diagnostics;
using System.IO;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    [TestFixture]
    public class PerformanceTests
    {
        [Test]
        public void ApplyTextStyles_50Layers_UnderFiveSeconds()
        {
            var jsonPath = Path.Combine(
                Application.dataPath,
                "Change/Editor/PSD2UI/Tests/Fixtures/large-text-config.json"
            );

            if (!File.Exists(jsonPath))
            {
                Assert.Ignore("Large test JSON not found");
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var config = JsonUtility.FromJson<PSDConfig>(json);

            var stopwatch = Stopwatch.StartNew();

            var builder = new PrefabBuilder();
            var rootObject = builder.Build(config);

            stopwatch.Stop();

            var textComponents = rootObject.GetComponentsInChildren<TextMeshProUGUI>(true);

            UnityEngine.Debug.Log($"Performance Test Results:");
            UnityEngine.Debug.Log($"- Text components: {textComponents.Length}");
            UnityEngine.Debug.Log($"- Generation time: {stopwatch.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"- Avg per component: {stopwatch.ElapsedMilliseconds / (float)textComponents.Length:F2}ms");

            Assert.GreaterOrEqual(textComponents.Length, 50, "Should have at least 50 text components");
            Assert.Less(stopwatch.ElapsedMilliseconds, 5000, "Should complete in under 5 seconds");

            Object.DestroyImmediate(rootObject);
        }

        [Test]
        public void ApplyTextStyles_ReasonablePerLayerOverhead()
        {
            // 测试单个文本组件的应用时间
            var textStyles = new TextStylesData
            {
                fontSize = 24,
                color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = false, italic = false },
                alignment = new AlignmentData { horizontal = "center", vertical = "middle" },
                effects = new TextEffectData[]
                {
                    new StrokeEffectData
                    {
                        type = "stroke",
                        enabled = true,
                        color = new ColorData { r = 0, g = 0, b = 0, a = 1 },
                        width = 2,
                        position = "outside"
                    },
                    new ShadowEffectData
                    {
                        type = "dropShadow",
                        enabled = true,
                        color = new ColorData { r = 0, g = 0, b = 0, a = 0.5f },
                        offsetX = 2,
                        offsetY = -2,
                        blur = 4
                    }
                }
            };

            var gameObject = new GameObject("TestText");
            var tmpComponent = gameObject.AddComponent<TextMeshProUGUI>();
            var applier = new TextStyleApplier();

            var stopwatch = Stopwatch.StartNew();

            applier.ApplyBasicProperties(tmpComponent, textStyles, "test");
            applier.ApplyEffects(tmpComponent, textStyles.effects, "test", "001");

            stopwatch.Stop();

            // 期望：单个组件应用时间 < 100ms
            Assert.Less(stopwatch.ElapsedMilliseconds, 100,
                $"Single component should apply in under 100ms, took {stopwatch.ElapsedMilliseconds}ms");

            Object.DestroyImmediate(gameObject);
        }
    }
}
