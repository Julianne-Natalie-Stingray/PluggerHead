# Menu Tool

## 本项目核查（2026-10-06）

本地 package.json 版本为1.0.1，要求 Unity 2022.3、无包依赖；Editor asmdef 仅限 Editor。当前是 `Assets/Packages/` 下的源码副本，根 manifest 未另行安装；下方 Git 安装步骤是分发说明，不要求本项目重复安装，也不表示本轮验证过远端地址。

已逐项核对 README、[CHANGELOG](CHANGELOG.md)、[LICENSE](LICENSE.md)、[Basic 示例](Samples~/Basic/README.md)、package/asmdef 与九个 Editor 脚本。许可及版本历史保持原文。生成的常量源码本身不依赖 Editor，现有项目使用 `Scripts/Infra/MenuTool/Game.menutool` 及其生成文件，见[项目接入说明](../../Scripts/Infra/MenuTool/README.md)。

### 当前实现限制

- Save 可保存无效定义；验证失败时不生成新 C#，旧输出仍可能保留。Save + Generate 也受 importer 的 Generate C# 开关控制，不强制越过关闭状态。
- 未显式填写的节点 FileName 回退到根 DefaultFileName，不继承父节点自己的 FileName。
- 节点 label 禁止 `/`，根 label 只要求非空。根标识符拒绝 Label、Path、DefaultFileName；节点还拒绝 FileName、同级重复及与直接父类型同名。生成入口另检查外层类名、命名空间，以及根类型与外层类同名冲突。跨定义或手写 partial 类型冲突仍需调用方协调，不保证任意组合都能编译。
- 生成和删除共用输出路径校验：要求 Assets 内的 `.cs` 路径，规范化绝对路径后拒绝逃逸到 Assets 外。操作现有文件还要求来源归属可确认：标准生成头第二行的源 GUID 与当前资产一致，或无 GUID 的旧文件与按当前定义及 importer 设置重建的完整旧格式内容一致；比较忽略换行差异。不能确认归属时拒绝覆盖或删除。
- 禁用生成不会删除旧文件，且当前 Inspector 的删除按钮也在禁用区域内。移动源资产、改变输出路径或删除源资产不会自动清理旧生成文件。
- 切换定义、关闭窗口或域重载可能丢失未保存编辑，没有统一确认流程；先保存需要保留的改动。未知 JSON 字段读取时忽略，保存后不保留。
- 自定义 JSON codec 服务于本格式，不是严格通用 JSON 校验器；未设置显式递归深度上限。导入对象只保存标量摘要，树由 codec 读写。

新生成及成功迁移的输出加入源 GUID 标记。已有匹配 GUID 标记的文件可在定义无效时删除；无标记旧文件必须能按当前定义重建并完整匹配，文件名相同不能证明归属。旧文件首次迁移前若已改动定义或 importer 设置，可能因无法匹配而拒绝操作。写入源码不改现有 `.meta`；正常删除通过 AssetDatabase.DeleteAsset 执行。同 GUID 文件即使被手工改动仍可被覆盖或删除，因此不要手改生成文件。此校验不保证符号链接或并发修改场景的安全，写入使用 File.WriteAllText，不是原子替换。

首次文档核查未操作导入/生成/删除，当时没有 MenuTool 专项自动测试结果。后续新增四个 MenuToolSafetyTests，覆盖真实文件读写归属守卫、生成/删除共用路径校验、标识符冲突，以及现有项目定义和常量兼容。测试通过反射调用内部实现，在临时目录操作文件；删除使用注入委托，不执行真实 AssetDatabase 删除，也不导入生成脚本或触发 Domain Reload。新增用例的运行记录见[测试总说明](../../Tests/README.md)，不据此宣称完整 Inspector、AssetPostprocessor、真实资产删除、代码编译、所有 I/O 故障或其他 Unity 版本已验证。

后续修复已通过独立代码审查，并调用真实 `GenerateForAssetPath` 迁移本项目 `Game.MenuTool.g.cs`：仅新增源 GUID 注释，原菜单常量及 `.meta` GUID 保持不变；迁移后编译完成，Console 错误为 0。此生成与编译证据和专项 Test Runner 结果覆盖不同路径，均不扩大到真实资产删除流程。

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

Whenever a `.menutool` asset is imported or moved, an `AssetPostprocessor` queues generation after the import completes. Before writing, the generator validates the definition, output path and source ownership. It compares normalized newlines and only writes when the owned output differs. Migrating an exact legacy output adds its source GUID header and therefore writes once.

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

`identifier` and sibling identifiers must pass the package's identifier validation. Root identifiers cannot be Label, Path or DefaultFileName; node identifiers also cannot be FileName, duplicate a sibling, or match their immediate containing type. Generation also rejects a root identifier matching the outer class. Node labels may contain spaces but not `/`; the root label is only checked for emptiness. Hierarchy is represented by child nodes. Validation does not detect collisions across separate definitions or hand-written partial types.

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
