using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

/// <summary>Exercises the generator's actual validation and file guards without importing scripts or requesting reloads.</summary>
public static class MenuToolSafetyIntegrationChecks
{
    public static void CheckOwnership()
    {
        const string firstGuid = "11111111111111111111111111111111";
        const string otherGuid = "22222222222222222222222222222222";
        Type generator = Find("MenuToolCodeGenerator");
        string legacy = LegacySource("Assets/One/Game.menutool");
        string directory = Path.Combine(Path.GetTempPath(), "MenuToolSafety-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "Game.MenuTool.g.cs");
        string meta = output + ".meta";
        const string metadata = "fileFormatVersion: 2\nguid: 33333333333333333333333333333333\n";
        try
        {
            File.WriteAllText(output, "// Hand authored C# must be retained.\n");
            File.WriteAllText(meta, metadata);
            CheckRejectedModification(generator, output, firstGuid, legacy, meta, metadata);

            File.WriteAllText(output, (string)Call(generator, "AddOwnershipHeader", legacy, otherGuid));
            CheckRejectedModification(generator, output, firstGuid, legacy, meta, metadata);

            // The legacy basename header is not sufficient: even a one-byte content edit must refuse ownership.
            File.WriteAllText(output, legacy + "// Unrelated content.\n");
            CheckRejectedModification(generator, output, firstGuid, legacy, meta, metadata);

            File.WriteAllText(output, legacy.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            object[] write = { output, firstGuid, legacy, false, null };
            Require((bool)Call(generator, "TryWriteOwnedOutput", write) && (bool)write[3], "Exact legacy content must migrate through the real writer.");
            Require(File.ReadAllText(output).Contains("// MenuTool source GUID: " + firstGuid), "Migration must add the source GUID.");
            Require(File.ReadAllText(meta) == metadata, "Generation must preserve the existing output meta/GUID.");

            byte[] before = File.ReadAllBytes(output);
            write = new object[] { output, firstGuid, legacy, false, null };
            Require((bool)Call(generator, "TryWriteOwnedOutput", write) && !(bool)write[3], "Unchanged owned output must not be rewritten.");
            Require(Convert.ToBase64String(File.ReadAllBytes(output)) == Convert.ToBase64String(before), "No-op generation must preserve exact bytes.");

            string movedSource = LegacySource("Assets/Moved/Renamed.menutool");
            write = new object[] { output, firstGuid, movedSource, false, null };
            Require((bool)Call(generator, "TryWriteOwnedOutput", write) && (bool)write[3], "A moved source with unchanged GUID must retain ownership.");
            Require(File.ReadAllText(meta) == metadata, "An owned update must preserve output GUID.");

            bool deleteCalled = false;
            Func<string> unusableDefinition = () => throw new InvalidOperationException("A GUID-owned delete must not need a valid definition.");
            Func<bool> deleteFile = () =>
            {
                deleteCalled = true;
                File.Delete(output);
                return true;
            };
            object[] deletion = { output, firstGuid, unusableDefinition, deleteFile, null };
            Require((bool)Call(generator, "TryDeleteOwnedOutput", deletion) && deleteCalled && !File.Exists(output),
                "A GUID-owned output must reach the supplied deletion operation without reading the definition.");

            File.WriteAllText(output, legacy);
            deletion = new object[] { output, firstGuid, (Func<string>)(() => legacy), deleteFile, null };
            Require((bool)Call(generator, "TryDeleteOwnedOutput", deletion) && !File.Exists(output), "Exactly matching legacy output must be deletable.");

            write = new object[] { output, firstGuid, legacy, false, null };
            Require((bool)Call(generator, "TryWriteOwnedOutput", write) && (bool)write[3], "A new output must be generated with ownership.");
            CheckRejectedModification(generator, output, "", legacy, meta, metadata);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    public static void CheckPaths()
    {
        Type generator = Find("MenuToolCodeGenerator");
        foreach (string invalid in new[] { "../Outside.cs", "Assets/../Outside.cs", "Assets/Menu.txt", "Assets", "Packages/Menu.cs", "Assets/Bad\0.cs" })
        {
            object[] args = { "Assets/Definition/Game.menutool", invalid, null, null, null };
            Require(!(bool)Call(generator, "TryResolveValidatedOutput", args) && !string.IsNullOrEmpty((string)args[4]),
                "Shared generation/deletion path validation must reject invalid output without throwing: " + invalid);
        }
        object[] valid = { "Assets/Definition/Game.menutool", "Assets/Definition/Sub/../Game.g.cs", null, null, null };
        Require((bool)Call(generator, "TryResolveValidatedOutput", valid) && (string)valid[2] == "Assets/Definition/Game.g.cs",
            "A contained path must resolve to the canonical AssetDatabase path.");
        valid = new object[] { "Assets/Definition/Game.menutool", "", null, null, null };
        Require((bool)Call(generator, "TryResolveValidatedOutput", valid) && (string)valid[2] == "Assets/Definition/Game.MenuTool.g.cs",
            "Default output path must remain compatible.");
    }

    public static void CheckIdentifiers()
    {
        Type utility = Find("MenuToolDataUtility");
        foreach (string identifier in new[] { "Label", "Path", "DefaultFileName", " Label", "Path ", "DefaultFileName\t" })
        {
            object data = Call(utility, "CreateDefault");
            Set(data, "identifier", identifier);
            Require(((IList)Call(utility, "Validate", data)).Count > 0, "Root collision/whitespace must be rejected: " + identifier);
        }
        foreach (string identifier in new[] { "Game", "FileName" })
        {
            object data = Call(utility, "CreateDefault");
            Set(data, "identifier", identifier);
            Require(((IList)Call(utility, "Validate", data)).Count == 0, "A valid root without generated-member conflict must remain accepted.");
        }
        foreach (string identifier in new[] { "Game", "Label", "Path", "FileName", "DefaultFileName", " BasicSettings", "BasicSettings " })
        {
            object data = Call(utility, "CreateDefault");
            IList nodes = (IList)data.GetType().GetField("nodes").GetValue(data);
            Set(nodes[0], "identifier", identifier);
            Require(((IList)Call(utility, "Validate", data)).Count > 0, "Node conflicts and whitespace must be rejected.");
        }
        object duplicate = Call(utility, "CreateDefault");
        IList duplicateNodes = (IList)duplicate.GetType().GetField("nodes").GetValue(duplicate);
        Set(duplicateNodes[1], "identifier", "BasicSettings");
        Require(((IList)Call(utility, "Validate", duplicate)).Count > 0, "Duplicate sibling identifiers must remain rejected.");
    }

    public static void CheckProjectCompatibility()
    {
        const string sourcePath = "Assets/Scripts/Infra/MenuTool/Game.menutool";
        const string outputPath = "Assets/Scripts/Infra/MenuTool/Game.MenuTool.g.cs";
        object data = Call(Find("MenuToolJson"), "Deserialize", File.ReadAllText(sourcePath));
        Require(((IList)Call(Find("MenuToolDataUtility"), "Validate", data)).Count == 0, "Current project menu definition must remain valid.");
        string legacy = (string)Call(Find("MenuToolCodeGenerator"), "GenerateSource", data, "MenuTool", "", sourcePath);
        string guid = Regex.Match(File.ReadAllText(sourcePath + ".meta"), @"(?m)^guid: ([0-9a-f]{32})\r?$").Groups[1].Value;
        object[] ownership = { Path.GetFullPath(outputPath), guid, (Func<string>)(() => legacy), null, null };
        Require((bool)Call(Find("MenuToolCodeGenerator"), "TryReadOwnedOutput", ownership), "Existing project output must remain recognizable without editing it.");
        string actual = File.ReadAllText(outputPath).Replace("\r\n", "\n");
        actual = Regex.Replace(actual, @"(?m)^// MenuTool source GUID: [0-9a-fA-F]{32}\n", "");
        Require(actual == legacy.Replace("\r\n", "\n"), "Ownership migration must preserve all existing menu constants and structure.");
    }

    private static void CheckRejectedModification(Type generator, string output, string sourceGuid, string legacy, string meta, string metadata)
    {
        string before = File.ReadAllText(output);
        object[] write = { output, sourceGuid, legacy, false, null };
        Require(!(bool)Call(generator, "TryWriteOwnedOutput", write) && !(bool)write[3], "Unverified output must not be overwritten.");
        bool deleteCalled = false;
        Func<bool> delete = () => { deleteCalled = true; return true; };
        object[] deletion = { output, sourceGuid, (Func<string>)(() => legacy), delete, null };
        Require(!(bool)Call(generator, "TryDeleteOwnedOutput", deletion) && !deleteCalled, "Unverified output must not reach deletion.");
        Require(File.ReadAllText(output) == before && File.ReadAllText(meta) == metadata, "Refused operations must preserve output and metadata.");
    }

    private static string LegacySource(string sourcePath)
    {
        object data = Call(Find("MenuToolDataUtility"), "CreateDefault");
        return (string)Call(Find("MenuToolCodeGenerator"), "GenerateSource", data, "MenuTool", "", sourcePath);
    }

    private static Type Find(string shortName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType("ProjectTools.MenuTool." + shortName, false);
            if (type != null)
            {
                return type;
            }
        }
        throw new InvalidOperationException("MenuTool type unavailable: " + shortName);
    }

    private static object Call(Type type, string name, params object[] arguments)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
