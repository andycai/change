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
                throw new FileNotFoundException($"JSON config not found: {jsonPath}", jsonPath);
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
