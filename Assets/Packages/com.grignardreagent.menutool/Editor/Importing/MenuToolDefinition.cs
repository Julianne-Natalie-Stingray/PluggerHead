using UnityEngine;

namespace ProjectTools.MenuTool
{
    /// <summary>
    /// Lightweight imported representation of a .menutool file.
    ///
    /// The recursive menu tree intentionally is not serialized onto this ScriptableObject.
    /// Unity 2022's serializer recursively inspects self-nesting serializable types and can
    /// emit serialization-depth warnings even for shallow instances. The source JSON remains
    /// the authority; this imported object only carries inspector-friendly summary metadata.
    /// </summary>
    internal sealed class MenuToolDefinition : ScriptableObject
    {
        [SerializeField] private int version;
        [SerializeField] private string identifier;
        [SerializeField] private string label;
        [SerializeField] private string defaultFileName;
        [SerializeField] private int nodeCount;

        public int Version => version;
        public string Identifier => identifier;
        public string Label => label;
        public string DefaultFileName => defaultFileName;
        public int NodeCount => nodeCount;

        public void Initialize(MenuToolData data)
        {
            if (data == null)
                return;

            version = data.version;
            identifier = data.identifier;
            label = data.label;
            defaultFileName = data.defaultFileName;
            nodeCount = CountNodes(data.nodes);
        }

        private static int CountNodes(System.Collections.Generic.List<MenuToolNode> nodes)
        {
            if (nodes == null)
                return 0;

            var count = 0;
            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                count++;
                count += CountNodes(node.children);
            }
            return count;
        }
    }
}
