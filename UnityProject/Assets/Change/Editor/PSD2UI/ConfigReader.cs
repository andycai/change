using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Root wrapper matching the JsonGenerator output:
    /// { "metadata": {...}, "layers": [...], "components": [...] }
    /// </summary>
    public class PsdJsonRoot
    {
        [JsonProperty("metadata")]
        public PsdJsonMetadata Metadata { get; set; }

        [JsonProperty("layers")]
        public List<UINodeData> Layers { get; set; } = new List<UINodeData>();

        [JsonProperty("components")]
        public List<LayerComponentMapping> Components { get; set; } = new List<LayerComponentMapping>();
    }

    public class PsdJsonMetadata
    {
        [JsonProperty("psdPath")]
        public string PsdPath { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; }

        [JsonProperty("generator")]
        public string Generator { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }

    /// <summary>
    /// One entry in the "components" array of the JsonGenerator output.
    /// </summary>
    public class LayerComponentMapping
    {
        [JsonProperty("layerId")]
        public string LayerId { get; set; }

        [JsonProperty("component")]
        public ComponentInfoData Component { get; set; }
    }

    public class ComponentInfoData
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("textBackend")]
        public string TextBackend { get; set; }

        [JsonProperty("confidence")]
        public float Confidence { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("needsReview")]
        public bool NeedsReview { get; set; }
    }

    public class ConfigReader
    {
        /// <summary>
        /// Load a JSON file produced by JsonGenerator and return the root
        /// UINodeData (first element of the "layers" array, which is the
        /// canvas root with all children nested inside).
        /// </summary>
        public UINodeData LoadFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"JSON config not found: {jsonPath}", jsonPath);
            }

            string jsonContent = File.ReadAllText(jsonPath);

            // Try the new format first: { metadata, layers[], components[] }
            PsdJsonRoot root = null;
            try
            {
                root = JsonConvert.DeserializeObject<PsdJsonRoot>(jsonContent);
            }
            catch { /* fall through to legacy format */ }

            if (root?.Layers != null && root.Layers.Count > 0)
            {
                // New format: build a synthetic root node whose children are
                // the top-level layers array entries.
                if (root.Layers.Count == 1)
                {
                    return root.Layers[0];
                }

                var syntheticRoot = new UINodeData
                {
                    Id = "root",
                    Name = "Root",
                    Type = "Container",
                    Rect = root.Metadata != null
                        ? new RectData { X = 0, Y = 0, Width = 0, Height = 0 }
                        : new RectData(),
                    Children = root.Layers,
                };
                return syntheticRoot;
            }

            // Legacy format: the whole JSON is a single UINodeData tree
            var nodeData = JsonConvert.DeserializeObject<UINodeData>(jsonContent);
            if (nodeData == null)
            {
                throw new InvalidDataException($"Failed to deserialize JSON: {jsonPath}");
            }
            return nodeData;
        }

        public bool ValidateConfig(string jsonPath, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                var nodeData = LoadFromJson(jsonPath);
                ValidateNode(nodeData, "", errors);
            }
            catch (System.Exception ex)
            {
                errors.Add($"JSON parsing error: {ex.Message}");
                return false;
            }

            return errors.Count == 0;
        }

        private void ValidateNode(UINodeData node, string path, List<string> errors)
        {
            string currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

            if (string.IsNullOrEmpty(node.Name))
                errors.Add($"{currentPath}: Missing required field 'Name'");

            if (string.IsNullOrEmpty(node.Type))
                errors.Add($"{currentPath}: Missing required field 'Type'");

            if (node.Rect == null)
                errors.Add($"{currentPath}: Missing required field 'Rect'");

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ValidateNode(child, currentPath, errors);
                }
            }
        }
    }
}
