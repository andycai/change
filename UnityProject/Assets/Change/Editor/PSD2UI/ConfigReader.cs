using System.Collections.Generic;

namespace Change.Editor.PSD2UI
{
    public class ConfigReader
    {
        public UINodeData LoadFromJson(string jsonPath)
        {
            // TODO: Implement
            return null;
        }

        public bool ValidateConfig(string jsonPath, out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
