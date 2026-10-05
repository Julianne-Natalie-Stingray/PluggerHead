using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ProjectTools.MenuTool
{
    internal sealed class MenuToolEditorWindow : EditorWindow
    {
        [SerializeField] private string assetPath;
        [NonSerialized] private MenuToolData data;
        private Vector2 scrollPosition;
        private bool dirty;
        private GUIStyle pathStyle;

        public static void Open(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var window = GetWindow<MenuToolEditorWindow>();
            window.assetPath = MenuToolDataUtility.NormalizeAssetPath(path);
            window.titleContent = new GUIContent(Path.GetFileNameWithoutExtension(path) + " Menu Tool");
            window.minSize = new Vector2(480f, 360f);
            window.Reload();
            window.Show();
            window.Focus();
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId);
            if (asset == null)
                return false;

            var path = AssetDatabase.GetAssetPath(asset);
            if (!path.EndsWith("." + MenuToolImporter.Extension, StringComparison.OrdinalIgnoreCase))
                return false;

            Open(path);
            return true;
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(assetPath) && data == null)
                Reload();
        }

        private void OnFocus()
        {
            if (!dirty && !string.IsNullOrEmpty(assetPath))
                Reload();
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (string.IsNullOrEmpty(assetPath))
            {
                EditorGUILayout.HelpBox("Open a .menutool asset to edit it.", MessageType.Info);
                return;
            }

            if (data == null)
            {
                EditorGUILayout.HelpBox($"Could not load '{assetPath}'.", MessageType.Error);
                if (GUILayout.Button("Retry"))
                    Reload();
                return;
            }

            DrawToolbar();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Root", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                data.identifier = EditorGUILayout.TextField(new GUIContent("Identifier", "Generated C# nested class name."), data.identifier);
                data.label = EditorGUILayout.TextField(new GUIContent("Menu Label", "Visible root path in Unity's Create menu."), data.label);
                data.defaultFileName = EditorGUILayout.TextField(new GUIContent("Default File Name", "Used by menu items whose File Name is empty."), data.defaultFileName);
                EditorGUILayout.LabelField("Path", data.label ?? string.Empty, pathStyle);
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Menu Items", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Root Item", GUILayout.Width(115f)))
                {
                    data.nodes ??= new List<MenuToolNode>();
                    data.nodes.Add(CreateNewNode(data.nodes.Count + 1));
                    dirty = true;
                }
            }

            data.nodes ??= new List<MenuToolNode>();
            DrawNodeList(data.nodes, data.label ?? string.Empty, 0);

            if (EditorGUI.EndChangeCheck())
                dirty = true;

            EditorGUILayout.Space(8f);
            DrawValidation();
            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(dirty ? Path.GetFileName(assetPath) + " *" : Path.GetFileName(assetPath), EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                        Reload();
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                    Save(false);

                var errors = MenuToolDataUtility.Validate(data);
                using (new EditorGUI.DisabledScope(errors.Count > 0))
                {
                    if (GUILayout.Button("Save + Generate", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                        Save(true);
                }
            }
        }

        private void DrawNodeList(List<MenuToolNode> nodes, string parentPath, int depth)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i] ?? (nodes[i] = CreateNewNode(i + 1));
                var delete = DrawNode(node, nodes, i, parentPath, depth);
                if (delete)
                {
                    nodes.RemoveAt(i);
                    dirty = true;
                    i--;
                }
            }
        }

        private bool DrawNode(MenuToolNode node, List<MenuToolNode> siblings, int index, string parentPath, int depth)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(depth * 12f);
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(node.identifier) ? "Menu Item" : node.identifier, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(index <= 0))
                    {
                        if (GUILayout.Button("↑", GUILayout.Width(26f)))
                        {
                            (siblings[index - 1], siblings[index]) = (siblings[index], siblings[index - 1]);
                            dirty = true;
                        }
                    }

                    using (new EditorGUI.DisabledScope(index >= siblings.Count - 1))
                    {
                        if (GUILayout.Button("↓", GUILayout.Width(26f)))
                        {
                            (siblings[index + 1], siblings[index]) = (siblings[index], siblings[index + 1]);
                            dirty = true;
                        }
                    }

                    if (GUILayout.Button("Delete", GUILayout.Width(55f)))
                        return true;
                }

                EditorGUI.indentLevel = depth + 1;
                node.identifier = EditorGUILayout.TextField(new GUIContent("Identifier", "Generated C# nested class name."), node.identifier);
                node.label = EditorGUILayout.TextField(new GUIContent("Menu Label", "Visible segment in the CreateAssetMenu path."), node.label);
                node.fileName = EditorGUILayout.TextField(new GUIContent("File Name", "Leave empty to inherit Default File Name."), node.fileName);

                var path = MenuToolDataUtility.CombineMenuPath(parentPath, node.label ?? string.Empty);
                EditorGUILayout.LabelField("Path", path, pathStyle);

                node.children ??= new List<MenuToolNode>();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space((depth + 1) * 12f);
                    if (GUILayout.Button("+ Add Child", GUILayout.Width(90f)))
                    {
                        node.children.Add(CreateNewNode(node.children.Count + 1));
                        dirty = true;
                    }
                }

                EditorGUI.indentLevel = 0;
                if (node.children.Count > 0)
                    DrawNodeList(node.children, path, depth + 1);
            }

            return false;
        }

        private void DrawValidation()
        {
            var errors = MenuToolDataUtility.Validate(data);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Definition is valid. Generated paths are compile-time constants and can be used in CreateAssetMenu attributes.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        }

        private void Reload()
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                data = null;
                dirty = false;
                Repaint();
                return;
            }

            try
            {
                data = MenuToolJson.Deserialize(File.ReadAllText(assetPath));
                if (data == null)
                    data = MenuToolDataUtility.CreateDefault();
                data.nodes ??= new List<MenuToolNode>();
                dirty = false;
            }
            catch (Exception exception)
            {
                data = null;
                Debug.LogError($"Menu Tool: could not read '{assetPath}': {exception.Message}");
            }

            Repaint();
        }

        private void Save(bool generateAfterSave)
        {
            if (data == null || string.IsNullOrEmpty(assetPath))
                return;

            try
            {
                File.WriteAllText(assetPath, MenuToolJson.Serialize(data, true) + Environment.NewLine);
                dirty = false;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                if (generateAfterSave)
                    MenuToolCodeGenerator.GenerateForAssetPath(assetPath, true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Menu Tool: could not save '{assetPath}': {exception.Message}");
            }
        }

        private static MenuToolNode CreateNewNode(int index)
        {
            return new MenuToolNode
            {
                identifier = "NewItem" + index,
                label = "New Item " + index,
                fileName = string.Empty,
                children = new List<MenuToolNode>()
            };
        }

        private void EnsureStyles()
        {
            if (pathStyle != null)
                return;

            pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };
        }
    }
}
