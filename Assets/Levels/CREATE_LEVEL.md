# 从空白 Scene 创建玩法关卡

本文以 [`GameplayIntegration`](../Tests/Scenes/GameplayIntegration.unity) 为可运行参照，使用项目现有预制体从空白 Scene 装配一关。示例目标是：玩家能够移动、跳跃、绕线、放置或收回 Anchor、插接与换线；死亡能重新加载本关，通关能进入指定场景，主菜单能进入并记录本关。关卡名以下用 `Level1` 举例；实际名称以保存的 `.unity` 文件名为准。

## 1. 创建场景与基础对象

1. 在 Unity 中新建空的 2D Scene，保存到 `Assets/Levels/Scene/Level1.unity`。每一关使用不同场景资产及其 GUID；复制场景时让 Unity 管理 `.meta`。
2. 保留一个启用的正交 Main Camera，Tag 为 `MainCamera`，并配置一个 AudioListener。GameplayIntegration 的相机位于 `(0, 0, -10)`、orthographic size 为 `5`；按实际关卡范围调整相机视野。一个运行中的场景只需要一个有效 AudioListener。
3. 将以下六个资源作为**场景实例**放在根层级，保持启用：

   | 资源 | 提供的功能 |
   | --- | --- |
   | [`Prefabs/Core/Core.prefab`](../Prefabs/Core/Core.prefab) | Input、Audio、SceneSwitch、TimerRunner、关卡进度记录；跨场景保活。 |
   | [`Prefabs/Player.prefab`](../Prefabs/Player.prefab) | 带 `Player` Tag 的真实玩家、Dynamic Rigidbody2D、非 Trigger 碰撞体、移动/交互脚本及视觉。 |
   | [`Prefabs/Env/ScenePrefab/Env.prefab`](../Prefabs/Env/ScenePrefab/Env.prefab) | EnvironmentFacade、PowerSocket、DualSocket、两根线与初始 Anchor。 |
   | [`Prefabs/Env/ScenePrefab/Environment Grid.prefab`](<../Prefabs/Env/ScenePrefab/Environment Grid.prefab>) | Rectangle Grid、Routing Tiles、带 TilemapCollider2D 的 Ground Tiles。 |
   | [`Prefabs/Env/ScenePrefab/GlobalUI.prefab`](../Prefabs/Env/ScenePrefab/GlobalUI.prefab) | 设置、线长 HUD、死亡重开及通关面板。 |
   | [`Prefabs/Env/ScenePrefab/EventSystem.prefab`](../Prefabs/Env/ScenePrefab/EventSystem.prefab) | UI 的 Input System 事件模块。 |

   Core 必须是根对象。场景切换时新场景里的 Core 副本会被去重；直接在 Editor 打开本关播放时，该实例提供所需服务。不要再放第二个 Player 或第二个 EventSystem。

## 2. 接好场景实例之间的引用

这些引用跨越不同预制体，不能仅靠预制体资产本身保存；在**新场景的实例 Inspector**中设置：

1. 将 `Environment Grid/Routing Tiles` 的 Tilemap 拖给 `Env` 根对象上 `EnvironmentFacade.routingTilemap`。场景里同时有 Routing Tiles 和 Ground Tiles，必须明确选择前者；不要让自动查找决定用哪一个。两者应属于同一场景。
2. 将本场景 `Player` 的 `PlayerMove` 拖给 `GlobalUI` 根对象上的 `RestartLevelScreen.player`。该组件也能查找同场景玩家，但显式引用便于在 Inspector 中确认装配。`NextLevelScreen` 会在运行时查找同场景 Env，不需要手动绑定环境。

复用上述预制体时，Core 的音频/场景配置、Player 的 Anchor prefab、Env 的 PowerSocket 电线列表和 GlobalUI 的面板/按钮引用已经保存在各自预制体中。若拆开重组 Env，需重新给 PowerSocket 的 `wires` 指定本关 Wire；**第一个有效引用是开局持线**。不要假设独立的 PowerSocket prefab 会自动生成或找到电线。场景独有的引用仍逐关检查。

## 3. 布置可玩的地形与电路

1. 在 Routing Tiles 上绘制玩家与电路节点要使用的格子。它用于格心定位、绕线路径和 K 放置 Anchor，通常不需要碰撞体；现有预制体的网格原点为 `(0, -0.5, 0)`、cell size 为 `1`。若调整 Grid 位置或大小，重新核对节点是否在已绘制格子的中心。
2. 在 Ground Tiles 上绘制实体地面，并确认使用的 Tile **本身有 Collider Type**，TilemapCollider2D 已启用且不是 Trigger。可见图块不一定有碰撞；玩家接地和跳跃依赖实际向上的碰撞接触。墙壁、边界和屋顶也按关卡意图分别检查碰撞，Routing Tiles 不会自动阻挡玩家。
3. 把 Player 放在有效地面上方、安全的出生点。Player 根物体保留 `Player` Tag；其物理、动画、J/K 输入及 Anchor prefab 已在预制体中。PlayerMove 在 Start 从**同场景** Env 获取移动规则；缺少 Env 时，正常水平移动和跳跃不会正确工作。
4. 在已绘制的 Routing tile 上布置 PowerSocket、DualSocket 与初始 Anchor，并安排一条玩家能够到达的路线。EnvironmentFacade 启动时会对有效节点做格心对齐。现有 Env prefab 的 PowerSocket 带 Live/Neutral 两根线，首条为 Neutral，两个 Wire 的 `maxLength` 均为 `64`；每关可以在实例上调整。`maxLength=0` 表示不限长；严格超过正数上限会使玩家死亡。
5. 带电接口必须同时接受 Live 与 Neutral，单极配置会初始化报错并禁用。通关路线必须从原插座出发，经实际交互插接覆盖全部带电接口，形成火线与零线首尾相连并回到原插座的闭环。首次接入时可换到另一根未使用的异极线；没有备用线时继续持线铺设。接入点固定此前线路，回走不能收掉接入；单纯经过接口不算接入。暂不考虑地线、降压器及换线次数上限。若需危险地面，可在非 Trigger 的地面碰撞体或其父对象添加 GroundPolarity；GameplayIntegration 默认未启用该机制。

路径记录玩家经过的四连通格子，原路逐格回退会收线。K 只在**已绘制 Routing tile** 上放置 Anchor，J 在交互范围内操作插口或收回 Anchor。普通移动仍可能走出 Routing tile 范围，所以要用实际地形与碰撞体控制关卡边界。玩家输入绑定为 A/D 移动、Space 跳跃、J 交互、K 放 Anchor。

## 4. 为新关指定重开、通关与菜单目标

`GlobalUI` 预制体当前的 `RestartLevelScreen.level` 和 `NextLevelScreen.nextLevel` 都默认指向 `GameplayIntegration`。新关场景实例必须分别设置：

- `RestartLevelScreen.level`：**本关的 SceneId**。死亡面板的“重新开始”会以 Single 模式重新加载这个场景。
- `NextLevelScreen.nextLevel`：实际下一关的 SceneId；终关可指向 `FinalScene`。通关面板在 Env 发出 `LevelCleared` 后显示，按钮请求切换到这里指定的目标。若保留默认值，按钮会回到 GameplayIntegration。
- 需要玩家从主菜单“新游戏”进入本关时，在 `MainMenuScene` 的 `MainMenuScreen.firstLevel` 指定本关。无需逐关改变菜单按钮的 onClick 接线。

保持 GlobalUI 根对象启用，并让设置、死亡与通关面板默认隐藏。预制体已包含 SettingsScreen、RestartLevelScreen、NextLevelScreen、按钮、文本与线长 HUD；不要在新关再复制一套菜单逻辑。若改变这些预制体内部结构，重新核对引用及按钮事件。

## 5. 注册场景，才能切换、重开与续关

场景文件存在不等于 `SceneSwitchManager` 可以加载。对每个新关同步完成以下三项：

1. 在 [`SceneId.cs`](../Scripts/Core/SceneSwitch/SceneId.cs) 增加唯一枚举成员和**未使用的整数值**。不要改已有成员的数值，也不要复用已退役的 `0`，以免已有场景序列化值或存档改变含义。
2. 在 [`DefaultSceneSwitchConfigs.asset`](../SO/SceneSwitch/DefaultSceneSwitchConfigs.asset) 增加该 SceneId 到**不带 `.unity` 后缀的准确场景名**的映射，并为正式玩法关勾选 `isGameplayLevel`。Core 的 SceneSwitchManager 和 LevelProgressTracker 共用这份配置；该标记决定菜单能否加载、进入后是否记录进度。
3. 在 **File > Build Settings** 中加入并启用该 `.unity` 场景。保持 `MainMenuScene` 为索引 0；Build Settings 索引与 SceneId 整数是不同概念。SceneSwitch 会拒绝未在构建列表中的目标。

`Continue` 只重新加载最近进入的玩法关卡的**场景默认状态**，不会恢复角色位置或绕线。新增关卡后，更新 [`SceneIntegrationChecks.CheckSceneRegistry`](../Tests/Editor/SceneIntegrationChecks.cs)：它目前搜索 `Assets/Scenes` 和 `Assets/Tests/Scenes`，还按“找到的场景数等于 SceneId 数”逐一检查映射。当前 `Assets/Levels/Scene` 同时有 `Level0.unity` 和 `Level1.unity`，**不能直接把整个目录加入搜索**，否则未注册的场景也会被要求加入 SceneId、配置和 Build Settings。可以让检查只纳入明确要发布的关卡，或把该目录中的所有关卡都完整注册；两种方式都要保持场景、SceneId、配置与构建列表一致。在 [`SceneAssetTests`](../Tests/EditMode/SceneAssetTests.cs) 加入新关的场景用例。若更改主菜单首关，同步调整 [`MainMenuIntegrationChecks`](../Tests/Editor/MainMenuIntegrationChecks.cs) 里对 GameplayIntegration 的固定预期及相应流程测试；原 GameplayIntegration 的独立回归仍应保留。

## 6. 验收顺序

1. 保存场景，等待编译和资源刷新结束，检查 Console 中脚本、缺引用和初始化错误。直接打开新关播放，确认只有一个有效 Core、EventSystem 和 AudioListener；玩家能落在地面、A/D 移动、Space 跳跃。
2. 走完一条真实输入路线：J 操作插口、K 放置及 J 收回 Anchor、原路回退、换线并闭合回路。确认线条与线长 HUD 更新，通关面板只在满足本关条件时出现，“下一关”进入指定场景。
3. 故意超过线长或触发本关设置的危险，确认死亡面板出现，“重新开始”加载**本关**，出生点、电线和回路回到场景默认状态。检查设置面板的暂停、继续及返回主菜单。
4. 从主菜单 New Game 进入本关，再返回菜单点 Continue，确认能进入保存的玩法关卡；随后检查构建版从索引 0 启动的流程。直接在 Editor 点 Play 不足以验证菜单和 Build Settings 接线。
5. 变更脚本、场景或预制体时，在 Unity Test Runner 中依次运行 `PluggerHead.EditModeTests`、`PluggerHead.PlayModeTests`，等待两组测试都结束并检查结果。既有测试针对 GameplayIntegration；新关的实际地形、真实按键路线和画面仍要单独验收。

本流程描述当前预制体的职责。若从零手工重建 Player、Env、Grid 或 UI 对象，组件、脚本、碰撞体及序列化字段必须分别按对应[Player](../Scripts/Player/README.md)、[Env](../Scripts/Env/README.md) 和[预制体说明](../Prefabs/Env/README.md)逐项装配，不能只复制同名 GameObject。
