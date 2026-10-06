# 默认场景切换配置

逐文件核查日期：2026-10-06。`DefaultSceneSwitchConfigs.asset` 由 Core 上的 SceneSwitchManager 与 LevelProgressTracker 共用。

| 配置列表顺序 | SceneId 数值 | 场景名 | 玩法关卡 |
| --- | --- | --- | --- |
| 1 | 1 | SceneSwitchTarget | false |
| 2 | 3 | GameplayIntegration | true |
| 3 | 2 | CircuitDiagnostics | false |
| 4 | 4 | MainMenuScene | false |

只有 GameplayIntegration 被标记为可记录/恢复的玩法关卡。配置顺序不是构建顺序；实际 Build Settings 以 MainMenuScene 为首位，见[场景总览](../../Scenes/README.md)。场景键与数值应保持兼容，不要依靠移动列表元素修改序列化标识。

已核对配置脚本、meta、四个场景名、枚举和构建列表。此映射不包含玩家位置、背包或回路存档；切换和关卡记录的行为见 [SceneSwitch](../../Scripts/Core/SceneSwitch/README.md) 与 [Progress](../../Scripts/Game/Progress/README.md)。本轮只新增说明，无资源变更。
