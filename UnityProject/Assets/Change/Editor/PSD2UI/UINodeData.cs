using System.Collections.Generic;
using Newtonsoft.Json;

namespace Change.Editor.PSD2UI
{
    public class UINodeData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Deserialized from JSON "bounds" field (JsonGenerator output).
        /// All C# consumers access this as .Rect for backward compatibility.
        /// </summary>
        [JsonProperty("bounds")]
        public RectData Rect { get; set; }

        /// <summary>
        /// Deserialized from JSON "assetPath" field (JsonGenerator output).
        /// All C# consumers access this as .SpritePath for backward compatibility.
        /// </summary>
        [JsonProperty("assetPath")]
        public string SpritePath { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; } = true;

        [JsonProperty("opacity")]
        public float Opacity { get; set; } = 1.0f;

        public SliceData Slice { get; set; }
        public LayoutData Layout { get; set; }

        [JsonProperty("textStyles")]
        public TextStylesData TextStyles { get; set; }

        [JsonProperty("children")]
        public List<UINodeData> Children { get; set; } = new List<UINodeData>();
    }

    public class RectData
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }
    }

    public class SliceData
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }

    public class LayoutData
    {
        public string Type { get; set; }  // "Vertical", "Horizontal", "Grid"
        public float Spacing { get; set; }
        public string Alignment { get; set; }
    }
}
