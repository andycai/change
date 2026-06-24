using System.Text.RegularExpressions;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Converts raw layer names into standardized C# field and method names
    /// based on component type and event type mappings.
    /// </summary>
    public static class NamingUtility
    {
        /// <summary>
        /// Generates a field name from a layer name and component type.
        /// Uses a type-based prefix (btn/txt/tgl/sld/inp/scr) followed by PascalCase.
        /// </summary>
        /// <param name="layerName">The raw layer name from the PSD hierarchy.</param>
        /// <param name="componentType">The Unity component type (Button, Text, Toggle, etc.).</param>
        /// <returns>A prefixed PascalCase field name suitable for C# code generation.</returns>
        public static string ToFieldName(string layerName, string componentType)
        {
            string prefix = componentType switch
            {
                "Button" => "btn",
                "Text" => "txt",
                "Toggle" => "tgl",
                "Slider" => "sld",
                "InputField" => "inp",
                "ScrollRect" => "scr",
                _ => ""
            };

            string cleaned = CleanName(layerName);
            return prefix + ToPascalCase(cleaned);
        }

        /// <summary>
        /// Generates a method name from a layer name and event type.
        /// Produces "On" + PascalCase + event-specific suffix.
        /// </summary>
        /// <param name="layerName">The raw layer name from the PSD hierarchy.</param>
        /// <param name="eventType">The UnityEvent property name (onClick, onValueChanged, onEndEdit).</param>
        /// <returns>A method name suitable for an event handler in generated code.</returns>
        public static string ToMethodName(string layerName, string eventType)
        {
            string cleaned = CleanName(layerName);
            string eventSuffix = eventType switch
            {
                "onClick" => "Clicked",
                "onValueChanged" => "ValueChanged",
                "onEndEdit" => "EndEdit",
                _ => ""
            };

            return "On" + ToPascalCase(cleaned) + eventSuffix;
        }

        /// <summary>
        /// Strips all non-alphanumeric characters from a name string.
        /// </summary>
        /// <param name="name">The raw name to clean.</param>
        /// <returns>A string containing only letters and digits.</returns>
        private static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            return Regex.Replace(name, @"[^a-zA-Z0-9]", "");
        }

        /// <summary>
        /// Converts the first character of a string to uppercase (PascalCase).
        /// </summary>
        /// <param name="name">The string to convert.</param>
        /// <returns>The string with its first character uppercased.</returns>
        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
