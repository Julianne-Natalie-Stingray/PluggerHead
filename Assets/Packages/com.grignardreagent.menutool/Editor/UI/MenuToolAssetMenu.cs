using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.MenuTool
{
    internal static class MenuToolAssetMenu
    {
        [MenuItem("Assets/Create/Menu Tool Definition", priority = 210)]
        private static void CreateMenuToolDefinition()
        {
            var folder = GetSelectedFolder();
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Game.menutool");
            var data = MenuToolDataUtility.CreateDefault();
            File.WriteAllText(assetPath, MenuToolJson.Serialize(data, true) + System.Environment.NewLine);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            MenuToolEditorWindow.Open(assetPath);
        }

        private static string GetSelectedFolder()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return "Assets";

            var selectedPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(selectedPath))
                return "Assets";

            if (!selectedPath.StartsWith("Assets", System.StringComparison.Ordinal))
                return "Assets";

            if (AssetDatabase.IsValidFolder(selectedPath))
                return selectedPath;

            var directory = Path.GetDirectoryName(selectedPath);
            return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
        }
    }
}
