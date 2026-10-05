using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ProjectTools.MenuTool
{
    [ScriptedImporter(1, Extension)]
    internal sealed class MenuToolImporter : ScriptedImporter
    {
        public const string Extension = "menutool";

        [SerializeField] private bool generateCSharp = true;
        [SerializeField] private string className = "MenuTool";
        [SerializeField] private string namespaceName = string.Empty;
        [SerializeField] private string outputPath = string.Empty;

        public bool GenerateCSharp => generateCSharp;
        public string ClassName => className;
        public string NamespaceName => namespaceName;
        public string OutputPath => outputPath;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            MenuToolData data;
            try
            {
                var json = File.ReadAllText(ctx.assetPath);
                data = MenuToolJson.Deserialize(json);
            }
            catch (Exception exception)
            {
                ctx.LogImportError($"Could not parse '{ctx.assetPath}': {exception.Message}", null);
                data = MenuToolDataUtility.CreateDefault();
            }

            var errors = MenuToolDataUtility.Validate(data);
            foreach (var error in errors)
                ctx.LogImportError(error, null);

            var asset = ScriptableObject.CreateInstance<MenuToolDefinition>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            asset.Initialize(data);

            ctx.AddObjectToAsset("definition", asset);
            ctx.SetMainObject(asset);
        }
    }
}
