using System.Collections.Generic;

namespace Change.Editor.PSD2UI
{
    public class UINodeData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public RectData Rect { get; set; }
        public string SpritePath { get; set; }
        public SliceData Slice { get; set; }
        public LayoutData Layout { get; set; }
        public List<UINodeData> Children { get; set; } = new List<UINodeData>();
    }

    public class RectData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
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
