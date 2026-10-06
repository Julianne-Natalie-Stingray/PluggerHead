# 资源创建菜单常量

本目录用 `.menutool` 资产统一 CreateAssetMenu 路径和默认文件名。工具实现在 `Assets/Packages/com.grignardreagent.menutool/`；此处仅保存项目菜单定义及生成输出，不负责运行时主菜单 UI。

## 逐文件说明

| 文件 | 职责 |
| --- | --- |
| `Game.menutool` | version=1 的 JSON 源定义，根 identifier/label 均为 Game，默认文件名 Default，BasicSettings 下有三个叶子节点。 |
| `Game.menutool.meta` | 自定义 importer，generateCSharp=1、className=MenuTool；namespaceName/outputPath 为空，使用全局命名空间和同目录默认输出。 |
| `Game.MenuTool.g.cs` | 自动生成的 partial static MenuTool.Game 常量树，含 Label、Path 及节点 FileName；不要手改。 |
| `Game.MenuTool.g.cs.meta` | 生成脚本 GUID，无默认引用或执行顺序覆盖。 |
| `README.md` / `.meta` | 本目录说明与资源标识。 |

## 当前输出与调用方

| 类型 / 叶子节点 | CreateAssetMenu 路径 | 默认文件名 |
| --- | --- | --- |
| AudioManagerConfigs | Game/Basic Settings/Audio Manager Configs | DefaultAudioManagerConfigs |
| AudioClipData | Game/Basic Settings/AudioClip Data | GenericAudioClipData |
| SceneSwitchConfigs | Game/Basic Settings/Scene Switch Configs | DefaultSceneSwitchConfigs |

前两者在 `Scripts/Core/Audio/SODefinitions/`，后者在 `Scripts/Core/SceneSwitch/`。BasicSettings 自身的 fileName 为空，生成值回退根 Default；identifier 用于 C# 类型名，label 用于菜单显示，两者不能混为一谈。

现有三个类型已在特性中引用生成常量，所以删除输出会导致编译错误。“可选”只适用于新调用方是否采用这种组织方式，不能认为现有项目不依赖它。

## 修改与核查（2026-10-06）

修改源 `.menutool` 后保存，包的 AssetPostprocessor 通过 delayCall 生成 C#；可用编辑器的 Save + Generate 或 importer Inspector 的 Generate C# Now 手动触发。保留源与输出的 meta GUID，一并提交源和结果。调整 identifier/生成类名需同步 C# 调用方；调整 label 会改菜单路径，不自动迁移已有资源文件。

已分别检查源定义、输出及两个 meta，再核对 importer、生成器/后处理器和三个 CreateAssetMenu 调用。三个路径与默认文件名完全一致，importer GUID 对应包内真实脚本，未发现本目录行为或引用缺陷。本次仅新增文档，没有重新生成或改动代码，不重复运行 Unity 测试；生成器自身的其他输入校验不由这三个有效节点证明。
