using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProjectTools.MenuTool
{
    internal sealed class MenuToolData
    {
        public int version = 1;
        public string identifier = "Game";
        public string label = "Game";
        public string defaultFileName = "Default";
        public List<MenuToolNode> nodes = new List<MenuToolNode>();
    }

    internal sealed class MenuToolNode
    {
        public string identifier = "NewItem";
        public string label = "New Item";
        public string fileName = string.Empty;
        public List<MenuToolNode> children = new List<MenuToolNode>();
    }

    internal static class MenuToolDataUtility
    {
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        public static MenuToolData CreateDefault()
        {
            return new MenuToolData
            {
                version = 1,
                identifier = "Game",
                label = "Game",
                defaultFileName = "Default",
                nodes = new List<MenuToolNode>
                {
                    new MenuToolNode
                    {
                        identifier = "BasicSettings",
                        label = "Basic Settings",
                        fileName = string.Empty,
                        children = new List<MenuToolNode>()
                    },
                    new MenuToolNode
                    {
                        identifier = "Configs",
                        label = "Configs",
                        fileName = string.Empty,
                        children = new List<MenuToolNode>()
                    }
                }
            };
        }

        public static List<string> Validate(MenuToolData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("The Menu Tool definition is empty or invalid JSON.");
                return errors;
            }

            if (data.version != 1)
                errors.Add($"Unsupported Menu Tool format version '{data.version}'. Expected version 1.");

            ValidateIdentifier(data.identifier, "Root identifier", errors);
            if (string.IsNullOrWhiteSpace(data.label))
                errors.Add("Root label cannot be empty.");
            if (string.IsNullOrWhiteSpace(data.defaultFileName))
                errors.Add("Default file name cannot be empty.");

            ValidateNodes(data.nodes ?? new List<MenuToolNode>(), data.identifier ?? "Root", errors);
            return errors;
        }

        private static void ValidateNodes(List<MenuToolNode> nodes, string parent, List<string> errors)
        {
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    errors.Add($"'{parent}' contains a null menu item.");
                    continue;
                }

                ValidateIdentifier(node.identifier, $"Identifier under '{parent}'", errors);
                if (!string.IsNullOrWhiteSpace(node.identifier) && !identifiers.Add(node.identifier))
                    errors.Add($"Duplicate identifier '{node.identifier}' under '{parent}'.");

                if (string.Equals(node.identifier, parent, StringComparison.Ordinal))
                    errors.Add($"Identifier '{node.identifier}' cannot have the same name as its containing generated class.");

                if (node.identifier == "Label" || node.identifier == "Path" || node.identifier == "FileName" || node.identifier == "DefaultFileName")
                    errors.Add($"Identifier '{node.identifier}' is reserved by Menu Tool generated members.");

                if (string.IsNullOrWhiteSpace(node.label))
                    errors.Add($"Menu item '{node.identifier}' has an empty label.");
                else if (node.label.Contains("/"))
                    errors.Add($"Menu item '{node.identifier}' label contains '/'. Use child items to create hierarchy.");

                ValidateNodes(node.children ?? new List<MenuToolNode>(), node.identifier ?? parent, errors);
            }
        }

        private static void ValidateIdentifier(string value, string fieldName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{fieldName} cannot be empty.");
                return;
            }

            if (!IsValidIdentifier(value))
                errors.Add($"{fieldName} '{value}' is not a valid C# identifier.");
        }

        public static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
                    return false;
            }

            return !CSharpKeywords.Contains(value);
        }

        public static string SanitizeIdentifier(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var builder = new StringBuilder(value.Length + 1);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((i == 0 && (char.IsLetter(c) || c == '_')) ||
                    (i > 0 && (char.IsLetterOrDigit(c) || c == '_')))
                {
                    builder.Append(c);
                }
                else if (i == 0 && char.IsDigit(c))
                {
                    builder.Append('_').Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }

            var result = builder.ToString();
            if (CSharpKeywords.Contains(result))
                result = "_" + result;
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        public static string CombineMenuPath(string parent, string label)
        {
            return string.IsNullOrEmpty(parent) ? label : parent + "/" + label;
        }

        public static string EscapeCSharpString(string value)
        {
            if (value == null)
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        public static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim();
        }

        public static string AssetPathToAbsolute(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
        }

        public static string GetDirectoryAssetPath(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            var directory = Path.GetDirectoryName(normalized);
            return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
        }
    }
}
