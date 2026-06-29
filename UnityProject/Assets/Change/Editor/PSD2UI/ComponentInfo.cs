using System;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// 组件识别结果数据类，对应 TypeScript 端导出的 ComponentInfo JSON 结构。
    /// 由 Newtonsoft.Json 自动反序列化，所有可选字段为 null 时不影响默认行为。
    /// </summary>
    [Serializable]
    public class ComponentInfo
    {
        public string Type { get; set; }
        public string TextBackend { get; set; }
        public string ImageType { get; set; }
        public string Role { get; set; }
        public float Confidence { get; set; }
        public string Source { get; set; }
        public bool NeedsReview { get; set; }
    }
}
