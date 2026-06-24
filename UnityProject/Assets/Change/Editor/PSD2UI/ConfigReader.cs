using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Change.Editor.PSD2UI
{
    public class ConfigReader
    {
        public UINodeData LoadFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"JSON config not found: {jsonPath}");
            }

            string jsonContent = File.ReadAllText(jsonPath);
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
            return true;
        }
    }
}
