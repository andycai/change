using System.Collections.Generic;
using System.Text;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Generates the C# source code string for a view from scanned component info.
    /// </summary>
    public class ViewCodeTemplate
    {
        /// <summary>
        /// Generates the view code string for the given prefab and its interactive components.
        /// </summary>
        /// <param name="className">The class name (e.g., "QuestWindow").</param>
        /// <param name="moduleName">The extracted module name (e.g., "Quest").</param>
        /// <param name="components">The scanned interactive components.</param>
        /// <returns>The generated C# source code as a string.</returns>
        public string GenerateClass(string className, string moduleName, List<ComponentInfo> components)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED CODE - DO NOT EDIT MANUALLY");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using TMPro;");
            sb.AppendLine();
            sb.AppendLine("namespace Change.Runtime.UI");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {className}View : MonoBehaviour");
            sb.AppendLine("    {");

            // Field declarations
            foreach (var comp in components)
            {
                string fieldName = NamingUtility.ToFieldName(comp.Name, comp.Type);
                sb.AppendLine($"        [SerializeField] private {comp.Type} {fieldName};");
            }

            sb.AppendLine();
            sb.AppendLine("        private void Start()");
            sb.AppendLine("        {");

            // Event bindings
            foreach (var comp in components)
            {
                string fieldName = NamingUtility.ToFieldName(comp.Name, comp.Type);
                string methodName = NamingUtility.ToMethodName(comp.Name, comp.EventType);
                sb.AppendLine($"            {fieldName}.{comp.EventType}.AddListener({methodName});");
            }

            sb.AppendLine("        }");
            sb.AppendLine();

            // Partial method declarations
            foreach (var comp in components)
            {
                string methodName = NamingUtility.ToMethodName(comp.Name, comp.EventType);
                sb.AppendLine($"        partial void {methodName}();");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
