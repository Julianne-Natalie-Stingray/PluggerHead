# Menu Tool

`Menu Tool` is an Editor-only Unity 2022.3 package that turns project-specific `CreateAssetMenu` structure into an imported `.menutool` asset and a generated compile-time C# API.

It follows the same broad workflow as Input Actions:

1. Author a source asset (`.menutool`).
2. Unity imports it through `ScriptedImporter`.
3. Configure code generation on the importer.
4. Generate stable `const string` members that are valid in attributes.

## Requirements

- Unity 2022.3 LTS or newer 2022.3 editor revision.
- No package dependencies.
- `.menutool` JSON is parsed by the package rather than `JsonUtility`, so recursive menu hierarchies do not pass through Unity serialization.

## Installation

- UnityEditor/Window/Package Manager/Upper-left cross button.
- Choose "Add package from git url". Paste the following url.
- "https://github.com/Julianne-Natalie-Stingray/MenuTool.git"

## Create a definition

Use:

`Assets > Create > Menu Tool Definition`

This creates `Game.menutool` and opens the Menu Tool editor.

A definition contains:

- **Identifier**: C# class identifier, e.g. `Game` or `BasicSettings`.
- **Menu Label**: visible Unity menu text, e.g. `Basic Settings`.
- **Default File Name**: inherited by menu items with no explicit file name.
- **Children**: nested menu hierarchy.

Double-click a `.menutool` asset to reopen the hierarchy editor.

## Generated API

The default definition generates an API like:

```csharp
public static partial class MenuTool
{
    public static partial class Game
    {
        public const string Label = "Game";
        public const string Path = "Game";
        public const string DefaultFileName = "Default";

        public static partial class BasicSettings
        {
            public const string Label = "Basic Settings";
            public const string FileName = "Default";
            public const string Path = "Game/Basic Settings";
        }

        public static partial class Configs
        {
            public const string Label = "Configs";
            public const string FileName = "Default";
            public const string Path = "Game/Configs";
        }
    }
}
```

Use the constants directly in attributes:

```csharp
using UnityEngine;

[CreateAssetMenu(
    fileName = MenuTool.Game.Configs.FileName,
    menuName = MenuTool.Game.Configs.Path)]
public sealed class GameConfig : ScriptableObject
{
}
```

Because the generated members are `const string`, they are valid attribute arguments.

## Importer settings

Select the `.menutool` asset to configure:

- **Generate C# Class**: enable/disable code generation.
- **C# Class Name**: outer generated class. Default: `MenuTool`.
- **C# Namespace**: optional namespace for the generated class.
- **C# Class File**: project-relative output path under `Assets/`. Leave empty to generate beside the source asset as `<AssetName>.MenuTool.g.cs`.

The importer Inspector also provides **Generate C# Now** and **Delete Generated File** actions.

## Asset-pipeline behavior

Whenever a `.menutool` asset is imported or moved, an `AssetPostprocessor` queues generation after the import completes. The generator compares the desired source with the existing `.cs` file and only writes when content changed. This avoids unnecessary script recompiles.

Editing a definition and pressing **Save** reimports the source asset, so generation happens automatically. **Save + Generate** forces the same operation immediately and logs the result.

## Source format

`.menutool` files are JSON so they remain diff-friendly and mergeable. The tree-shaped version 1 format is retained; Menu Tool uses its own JSON codec so Unity never serializes the recursive `children` graph:

```json
{
  "version": 1,
  "identifier": "Game",
  "label": "Game",
  "defaultFileName": "Default",
  "nodes": [
    {
      "identifier": "Configs",
      "label": "Configs",
      "fileName": "GameConfig",
      "children": []
    }
  ]
}
```

`identifier` and sibling identifiers must be valid, unique C# identifiers. Labels may contain spaces but not `/`; hierarchy is represented by child nodes.

## Migration from a hand-written `MenuTools` class

The generated outer class defaults to `MenuTool` (singular), so an existing hand-written `MenuTools` helper can remain temporarily while you migrate call sites. If you change **C# Class Name** back to `MenuTools`, remove the old hard-coded `MenuTools` declaration first or the project will contain duplicate generated members.

## Notes

- Generated files should not be edited manually.
- Disabling generation does not automatically delete an existing generated file. Use **Delete Generated File** if desired.
- If you move a `.menutool` that uses the default adjacent output path, Unity generates the file at the new location. The old generated file is intentionally not deleted automatically, to avoid deleting user-owned source unexpectedly.
- Multiple definitions may use the same outer partial class if their generated nested identifiers do not collide. Duplicate generated members will produce normal C# compiler errors, so keep generated roots unique unless you intentionally coordinate them.

## Package layout

```text
com.menutool.tooling/
├── Editor/
│   ├── CodeGen/
│   ├── Data/
│   ├── Importing/
│   └── UI/
├── Samples~/Basic/
├── package.json
└── README.md
```
