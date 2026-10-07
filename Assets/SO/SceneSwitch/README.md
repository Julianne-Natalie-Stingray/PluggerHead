# 默认场景切换配置

`DefaultSceneSwitchConfigs.asset` 由 Core 上的 SceneSwitchManager 与大厅 Portal 共用。

| 配置列表顺序 | SceneId 数值 | 场景名 | 玩法关卡 |
| --- | --- | --- | --- |
| 1 | 1 | SceneSwitchTarget | false |
| 2 | 3 | GameplayIntegration | true |
| 3 | 2 | CircuitDiagnostics | false |
| 4 | 4 | MainMenuScene | false |
| 5 | 5 | FinalScene | false |
| 6 | 6 | Level0 | true |

GameplayIntegration 与 Level0 标记为玩法关卡；大厅第一关使用 Level0。解锁进度由通关事件记录在内存中，不再从场景配置生成存档。配置顺序不是构建顺序；实际 Build Settings 以 MainMenuScene 为首位，见[场景总览](../../Scenes/README.md)。场景键与数值应保持兼容，不要依靠移动列表元素修改序列化标识。

此映射不包含玩家位置或回路存档；切换与通关解锁行为见 [SceneSwitch](../../Scripts/Core/SceneSwitch/README.md) 与 [Progress](../../Scripts/Game/Progress/README.md)。FinalScene 为非玩法结尾场景。
