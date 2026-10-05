using System;
using System.Collections.Generic;
using UnityEditor;

namespace ProjectTools.MenuTool
{
    internal sealed class MenuToolGenerationPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool flushScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            Queue(importedAssets);
            Queue(movedAssets);
        }

        private static void Queue(IEnumerable<string> assetPaths)
        {
            foreach (var path in assetPaths)
            {
                if (!path.EndsWith("." + MenuToolImporter.Extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                PendingAssets.Add(path);
            }

            if (PendingAssets.Count == 0 || flushScheduled)
                return;

            flushScheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            flushScheduled = false;
            var paths = new List<string>(PendingAssets);
            PendingAssets.Clear();

            foreach (var path in paths)
                MenuToolCodeGenerator.GenerateForAssetPath(path);
        }
    }
}
