using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.MenuTool
{
    internal static class MenuToolCodeGenerator
    {
        private const string OwnershipPrefix = "// MenuTool source GUID: ";

        public static bool GenerateForAssetPath(string assetPath, bool logResult = false)
        {
            assetPath = MenuToolDataUtility.NormalizeAssetPath(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as MenuToolImporter;
            if (importer == null || !importer.GenerateCSharp)
                return false;

            if (!TryBuildLegacySource(assetPath, importer, out var legacySource, out var sourceError))
            {
                Debug.LogError($"Menu Tool: {sourceError}");
                return false;
            }

            if (!TryResolveValidatedOutput(assetPath, importer.OutputPath, out var outputPath, out var absoluteOutput, out var pathError))
            {
                Debug.LogError($"Menu Tool: {pathError}");
                return false;
            }

            var sourceGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!TryWriteOwnedOutput(absoluteOutput, sourceGuid, legacySource, out var changed, out var writeError))
            {
                Debug.LogError($"Menu Tool: {writeError}");
                return false;
            }
            if (!changed)
            {
                if (logResult)
                    Debug.Log($"Menu Tool: generated C# is already up to date: {outputPath}");
                return false;
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            if (logResult)
                Debug.Log($"Menu Tool: generated {outputPath}");
            return true;
        }

        private static bool TryBuildLegacySource(string assetPath, MenuToolImporter importer, out string source, out string error)
        {
            source = null;
            error = null;
            MenuToolData data;
            try
            {
                data = MenuToolJson.Deserialize(File.ReadAllText(assetPath));
            }
            catch (Exception exception)
            {
                error = $"could not read '{assetPath}': {exception.Message}";
                return false;
            }

            var errors = MenuToolDataUtility.Validate(data);
            if (errors.Count > 0)
            {
                error = "C# generation skipped because the definition is invalid:\n- " + string.Join("\n- ", errors);
                return false;
            }

            var className = string.IsNullOrWhiteSpace(importer.ClassName)
                ? "MenuTool"
                : importer.ClassName.Trim();

            if (!MenuToolDataUtility.IsValidIdentifier(className))
            {
                error = $"'{className}' is not a valid generated C# class name.";
                return false;
            }

            if (string.Equals(className, data.identifier, StringComparison.Ordinal))
            {
                error = $"root identifier '{data.identifier}' cannot be the same as outer generated class '{className}'.";
                return false;
            }

            var namespaceName = (importer.NamespaceName ?? string.Empty).Trim();
            if (!IsValidNamespace(namespaceName))
            {
                error = $"'{namespaceName}' is not a valid C# namespace.";
                return false;
            }

            data.nodes ??= new List<MenuToolNode>();
            source = GenerateSource(data, className, namespaceName, assetPath);
            return true;
        }

        public static string ResolveOutputPath(string assetPath, string configuredPath)
        {
            var configured = MenuToolDataUtility.NormalizeAssetPath(configuredPath);
            if (!string.IsNullOrEmpty(configured))
                return configured;

            var directory = MenuToolDataUtility.GetDirectoryAssetPath(assetPath);
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            return $"{directory}/{fileName}.MenuTool.g.cs";
        }

        public static bool TryDeleteGeneratedFile(string assetPath)
        {
            assetPath = MenuToolDataUtility.NormalizeAssetPath(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as MenuToolImporter;
            if (importer == null)
                return false;

            if (!TryResolveValidatedOutput(assetPath, importer.OutputPath, out var outputPath, out var absoluteOutput, out var pathError))
            {
                Debug.LogError($"Menu Tool: {pathError}");
                return false;
            }

            // GUID-tagged files remain removable even if the definition is now invalid. Legacy files
            // require rebuilding the complete previous format; a basename is never proof of ownership.
            Func<string> legacySource = () =>
            {
                if (!TryBuildLegacySource(assetPath, importer, out var source, out var error))
                    throw new InvalidOperationException(error);
                return source;
            };
            bool deleted = TryDeleteOwnedOutput(absoluteOutput, AssetDatabase.AssetPathToGUID(assetPath), legacySource,
                () => AssetDatabase.DeleteAsset(outputPath), out var deleteError);
            if (!string.IsNullOrEmpty(deleteError))
                Debug.LogError($"Menu Tool: {deleteError}");
            return deleted;
        }

        private static bool TryWriteOwnedOutput(string absoluteOutput, string sourceGuid, string legacySource, out bool changed, out string error)
        {
            changed = false;
            if (!TryReadOwnedOutput(absoluteOutput, sourceGuid, () => legacySource, out var existing, out error))
                return false;
            string source = AddOwnershipHeader(legacySource, sourceGuid);
            if (string.Equals(NormalizeNewlines(existing), NormalizeNewlines(source), StringComparison.Ordinal))
                return true;
            try
            {
                var directory = Path.GetDirectoryName(absoluteOutput);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(absoluteOutput, source, new UTF8Encoding(false));
                changed = true;
                return true;
            }
            catch (Exception exception)
            {
                error = $"could not write '{absoluteOutput}': {exception.Message}";
                return false;
            }
        }

        private static bool TryDeleteOwnedOutput(string absoluteOutput, string sourceGuid, Func<string> legacySource,
            Func<bool> deleteAsset, out string error)
        {
            if (!TryReadOwnedOutput(absoluteOutput, sourceGuid, legacySource, out var existing, out error) || existing == null)
                return false;
            try
            {
                if (deleteAsset())
                    return true;
                error = $"could not delete '{absoluteOutput}'.";
            }
            catch (Exception exception)
            {
                error = $"could not delete '{absoluteOutput}': {exception.Message}";
            }
            return false;
        }

        private static bool TryReadOwnedOutput(string absoluteOutput, string sourceGuid, Func<string> legacySource,
            out string existing, out string error)
        {
            existing = null;
            error = null;
            if (!Guid.TryParseExact(sourceGuid, "N", out _))
            {
                error = "A valid source asset GUID is required before modifying generated C#.";
                return false;
            }
            try
            {
                if (!File.Exists(absoluteOutput))
                    return true;
                existing = File.ReadAllText(absoluteOutput);
                string normalized = NormalizeNewlines(existing);
                string[] lines = normalized.Split('\n');
                if (lines.Length > 1 && lines[0] == "// <auto-generated />" && lines[1].StartsWith(OwnershipPrefix, StringComparison.Ordinal))
                {
                    if (string.Equals(lines[1], OwnershipPrefix + sourceGuid, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (string.Equals(normalized, NormalizeNewlines(legacySource()), StringComparison.Ordinal))
                {
                    return true;
                }
                error = $"Refusing to modify '{absoluteOutput}': its source ownership cannot be verified.";
                return false;
            }
            catch (Exception exception)
            {
                error = $"could not verify ownership of '{absoluteOutput}': {exception.Message}";
                return false;
            }
        }

        private static string AddOwnershipHeader(string legacySource, string sourceGuid)
        {
            string normalized = NormalizeNewlines(legacySource);
            int firstLineEnd = normalized.IndexOf('\n');
            return normalized.Insert(firstLineEnd + 1, OwnershipPrefix + sourceGuid + "\n");
        }

        private static bool TryResolveValidatedOutput(string assetPath, string configuredPath,
            out string outputPath, out string absoluteOutput, out string error)
        {
            outputPath = null;
            absoluteOutput = null;
            error = null;
            try
            {
                outputPath = ResolveOutputPath(assetPath, configuredPath);
                if (!IsValidOutputPath(outputPath, out error))
                    return false;
                absoluteOutput = MenuToolDataUtility.AssetPathToAbsolute(outputPath);
                string assetsRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets"));
                outputPath = "Assets/" + absoluteOutput.Substring(assetsRoot.Length + 1).Replace('\\', '/');
                return true;
            }
            catch (Exception exception)
            {
                error = $"Invalid generated C# output path: {exception.Message}";
                return false;
            }
        }

        private static string GenerateSource(MenuToolData data, string className, string namespaceName, string sourceAssetPath)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine($"// Generated from {Path.GetFileName(sourceAssetPath)}. Edit the .menutool asset, not this file.");
            builder.AppendLine();

            var indent = 0;
            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.Append("namespace ").Append(namespaceName).AppendLine();
                builder.AppendLine("{");
                indent++;
            }

            AppendIndent(builder, indent).Append("public static partial class ").Append(className).AppendLine();
            AppendIndent(builder, indent).AppendLine("{");
            indent++;

            var rootPath = data.label.Trim();
            AppendIndent(builder, indent).Append("public static partial class ").Append(data.identifier.Trim()).AppendLine();
            AppendIndent(builder, indent).AppendLine("{");
            indent++;
            AppendConstant(builder, indent, "Label", data.label.Trim());
            AppendConstant(builder, indent, "Path", rootPath);
            AppendConstant(builder, indent, "DefaultFileName", data.defaultFileName.Trim());

            if (data.nodes != null && data.nodes.Count > 0)
                builder.AppendLine();

            for (var i = 0; i < data.nodes.Count; i++)
            {
                AppendNode(builder, indent, data.nodes[i], rootPath, data.defaultFileName.Trim());
                if (i < data.nodes.Count - 1)
                    builder.AppendLine();
            }

            indent--;
            AppendIndent(builder, indent).AppendLine("}");
            indent--;
            AppendIndent(builder, indent).AppendLine("}");

            if (!string.IsNullOrEmpty(namespaceName))
            {
                indent--;
                AppendIndent(builder, indent).AppendLine("}");
            }

            return builder.ToString();
        }

        private static void AppendNode(StringBuilder builder, int indent, MenuToolNode node, string parentPath, string defaultFileName)
        {
            var path = MenuToolDataUtility.CombineMenuPath(parentPath, node.label.Trim());
            var fileName = string.IsNullOrWhiteSpace(node.fileName) ? defaultFileName : node.fileName.Trim();

            AppendIndent(builder, indent).Append("public static partial class ").Append(node.identifier.Trim()).AppendLine();
            AppendIndent(builder, indent).AppendLine("{");
            indent++;
            AppendConstant(builder, indent, "Label", node.label.Trim());
            AppendConstant(builder, indent, "FileName", fileName);
            AppendConstant(builder, indent, "Path", path);

            var children = node.children ?? new List<MenuToolNode>();
            if (children.Count > 0)
                builder.AppendLine();

            for (var i = 0; i < children.Count; i++)
            {
                AppendNode(builder, indent, children[i], path, defaultFileName);
                if (i < children.Count - 1)
                    builder.AppendLine();
            }

            indent--;
            AppendIndent(builder, indent).AppendLine("}");
        }

        private static void AppendConstant(StringBuilder builder, int indent, string name, string value)
        {
            AppendIndent(builder, indent)
                .Append("public const string ")
                .Append(name)
                .Append(" = \"")
                .Append(MenuToolDataUtility.EscapeCSharpString(value))
                .AppendLine("\";");
        }

        private static StringBuilder AppendIndent(StringBuilder builder, int indent)
        {
            return builder.Append(' ', indent * 4);
        }

        private static string NormalizeNewlines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static bool IsValidNamespace(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return true;

            var parts = namespaceName.Split('.');
            if (parts.Length == 0)
                return false;

            foreach (var part in parts)
            {
                if (!MenuToolDataUtility.IsValidIdentifier(part))
                    return false;
            }

            return true;
        }

        private static bool IsValidOutputPath(string outputPath, out string error)
        {
            outputPath = MenuToolDataUtility.NormalizeAssetPath(outputPath);
            if (!outputPath.StartsWith("Assets/", StringComparison.Ordinal) && outputPath != "Assets")
            {
                error = "Generated C# path must be inside the project's Assets folder.";
                return false;
            }

            if (!outputPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                error = "Generated C# path must end with '.cs'.";
                return false;
            }

            var absoluteOutput = MenuToolDataUtility.AssetPathToAbsolute(outputPath);
            var assetsRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets"));
            var assetsPrefix = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absoluteOutput.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "Generated C# path cannot escape the project's Assets folder.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
