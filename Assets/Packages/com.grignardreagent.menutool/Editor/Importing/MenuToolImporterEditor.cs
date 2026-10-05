using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ProjectTools.MenuTool
{
    [CustomEditor(typeof(MenuToolImporter))]
    internal sealed class MenuToolImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty generateCSharp;
        private SerializedProperty className;
        private SerializedProperty namespaceName;
        private SerializedProperty outputPath;

        public override void OnEnable()
        {
            base.OnEnable();
            generateCSharp = serializedObject.FindProperty("generateCSharp");
            className = serializedObject.FindProperty("className");
            namespaceName = serializedObject.FindProperty("namespaceName");
            outputPath = serializedObject.FindProperty("outputPath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var importer = (MenuToolImporter)target;
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Menu Tool Definition", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Edit the menu hierarchy in the Menu Tool editor. Import settings control the generated compile-time C# API.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Edit Definition", GUILayout.Height(24f)))
                    MenuToolEditorWindow.Open(importer.assetPath);

                if (GUILayout.Button("Show in Project", GUILayout.Height(24f)))
                {
                    var source = AssetDatabase.LoadMainAssetAtPath(importer.assetPath);
                    Selection.activeObject = source;
                    EditorGUIUtility.PingObject(source);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("C# Generation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(generateCSharp, new GUIContent("Generate C# Class"));

            using (new EditorGUI.DisabledScope(!generateCSharp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(className, new GUIContent("C# Class Name"));
                EditorGUILayout.PropertyField(namespaceName, new GUIContent("C# Namespace"));
                EditorGUILayout.PropertyField(
                    outputPath,
                    new GUIContent("C# Class File", "Leave empty to generate next to the .menutool file."));
                EditorGUI.indentLevel--;

                var resolved = MenuToolCodeGenerator.ResolveOutputPath(importer.assetPath, outputPath.stringValue);
                EditorGUILayout.LabelField("Resolved File", resolved, EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate C# Now"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        Apply();
                        MenuToolCodeGenerator.GenerateForAssetPath(importer.assetPath, true);
                    }

                    if (GUILayout.Button("Delete Generated File"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        if (EditorUtility.DisplayDialog(
                                "Delete Generated C#",
                                $"Delete '{resolved}'?",
                                "Delete",
                                "Cancel"))
                        {
                            MenuToolCodeGenerator.TryDeleteGeneratedFile(importer.assetPath);
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}
