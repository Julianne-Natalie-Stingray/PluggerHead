# Menu Tool

## 本项目核查（2026-10-06）

本地 package.json 版本为1.0.1，要求 Unity 2022.3、无包依赖；Editor asmdef 仅限 Editor。当前是 `Assets/Packages/` 下的源码副本，根 manifest 未另行安装；下方 Git 安装步骤是分发说明，不要求本项目重复安装，也不表示本轮验证过远端地址。

已逐项核对 README、[CHANGELOG](CHANGELOG.md)、[LICENSE](LICENSE.md)、[Basic 示例](Samples~/Basic/README.md)、package/asmdef 与九个 Editor 脚本。许可及版本历史保持原文。生成的常量源码本身不依赖 Editor，现有项目使用 `Scripts/Infra/MenuTool/Game.menutool` 及其生成文件，见[项目接入说明](../../Scripts/Infra/MenuTool/README.md)。

### 当前实现限制

- Save 可保存无效定义；验证失败时不生成新 C#，旧输出仍可能保留。Save + Generate 也受 importer 的 Generate C# 开关控制，不强制越过关闭状态。
- 未显式填写的节点 FileName 回退到根 DefaultFileName，不继承父节点自己的 FileName。
- 节点 label 禁止 `/`，根 label 只要求非空。标识符校验不是编译保证：根名 Label、Path、DefaultFileName 仍可能与生成成员冲突。
- 输出应使用专属路径。生成器可能覆盖配置路径上的已有 `.cs`，没有来源归属检查；删除动作按当前解析路径执行，也未复用完整生成校验或验证文件归属。UI 确认框不能替代这些校验。
- 禁用生成不会删除旧文件，且当前 Inspector 的删除按钮也在禁用区域内。移动源资产、改变输出路径或删除源资产不会自动清理旧生成文件。
- 切换定义、关闭窗口或域重载可能丢失未保存编辑，没有统一确认流程；先保存需要保留的改动。未知 JSON 字段读取时忽略，保存后不保留。
- 自定义 JSON codec 服务于本格式，不是严格通用 JSON 校验器；未设置显式递归深度上限。导入对象只保存标量摘要，树由 codec 读写。

本轮未操作导入/生成/删除，没有 MenuTool 专项自动测试结果；项目玩法回归也不证明这些编辑器流程、无效输入或其他 Unity 版本已验证。

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

Editing a definition and pressing **Save** reimports the source asset. Valid definitions generate when generation is enabled. **Save + Generate** invokes generation immediately and logs the result, but still respects that setting and validation.

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

`identifier` and sibling identifiers must pass the package's identifier validation. Node labels may contain spaces but not `/`; the root label is only checked for emptiness. Hierarchy is represented by child nodes. Validation does not detect every possible generated member collision.

## Migration from a hand-written `MenuTools` class

The generated outer class defaults to `MenuTool` (singular), so an existing hand-written `MenuTools` helper can remain temporarily while you migrate call sites. If you change **C# Class Name** back to `MenuTools`, remove the old hard-coded `MenuTools` declaration first or the project will contain duplicate generated members.

## Notes

- Generated files should not be edited manually.
- Disabling generation does not delete an existing generated file; the Inspector also disables its delete button while generation is disabled. Review the resolved output path and ownership before deleting anything.
- If you move a `.menutool` that uses the default adjacent output path, Unity generates the file at the new location. The old generated file is intentionally not deleted automatically, to avoid deleting user-owned source unexpectedly.
- Multiple definitions may use the same outer partial class if their generated nested identifiers do not collide. Duplicate generated members will produce normal C# compiler errors, so keep generated roots unique unless you intentionally coordinate them.

## Package layout

```text
com.grignardreagent.menutool/
├── Editor/
│   ├── CodeGen/
│   ├── Data/
│   ├── Importing/
│   └── UI/
├── Samples~/Basic/
├── package.json
└── README.md
```
