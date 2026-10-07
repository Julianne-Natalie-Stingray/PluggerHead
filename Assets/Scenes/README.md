# 场景总览

当前 Build Settings 启用七场景，顺序如下；TestLevel 为未注册开发关卡。SceneId 数值与构建索引是不同概念。

| 构建索引 | 文件 | SceneId 数值 |
| --- | --- | --- |
| 0 | `MainMenuScene.unity` | 4 |
| 1 | `../Tests/Scenes/GameplayIntegration.unity` | 3 |
| 2 | `../Tests/Scenes/CircuitDiagnostics.unity` | 2 |
| 3 | `../Tests/Scenes/SceneSwitchTarget.unity` | 1 |
| 4 | `FinalScene.unity` | 5 |
| 5 | `Level0.unity` | 6 |
| 6 | `HubScene.unity` | 7 |

`FinalScene` 是可独立播放的结尾场景：包含“恭喜通关！”标题、标明“示例团队”的制作组名单和“退出游戏”按钮，使用中文 TMP/uGUI，专名保留原文。Canvas 按 1280×720 缩放，配有 Input System EventSystem、相机/监听器及 Core prefab。Exit 在 Editor 停止播放，在构建版退出程序；文本可直接在场景 Inspector 修改。它注册为非玩法场景，不改变内存通关进度，可通过 `RequestSwitch(SceneId.FinalScene)` 接入。旧英文运行画面见 [FinalScene](../Docs/Development/FinalScene.png)。

`MainMenuScene` 是构建入口，包含 Core prefab、相机/监听器、uGUI 菜单与 EventSystem。主菜单不含 Player、环境或 Portal；NewGame、OpenSettings、ExitGame 持久事件均有接线。默认隐藏的设置面板引用三路滑块及 Save/Continue/ReturnToMainMenu 按钮。

开始按钮通过场景切换服务加载独立 `HubScene`。大厅保留原 Player、环境、地板和传送门布局，可独立播放；右上角 `MenuBtn` 显示“菜单”，调用 SettingsScreen.Open 打开暂停设置，支持继续和返回主菜单。三个 Portal 从左到右代表第一至三关，第一关进入 Level0，第二、三关显示“尚未开放”。Level0 的通关按钮返回 HubScene 大厅，顺序解锁进度仅在本次运行内保留，允许重玩。原 Level0 布局的通关限制按用户要求保留，见[进度说明](../Scripts/Game/Progress/README.md)。其他三个场景逐文件说明见[功能场景](../Tests/Scenes/README.md)，菜单行为见 [MainMenu](../Scripts/Game/MainMenu/README.md)，配置见 [SO/SceneSwitch](../SO/SceneSwitch/README.md)。

MainMenu、HubScene、GameplayIntegration、CircuitDiagnostics、FinalScene、Level0 均配置同一 Core prefab，运行时去重与保活由 CoreFacade 实现。SceneSwitchTarget 不含 Core，需要从已有服务的场景进入。不要因在编辑器单独打开目标场景而推断服务失效。

七个场景 GUID、默认映射与构建列表已核对，无路径断链。本次结构测试 8/8 与菜单流程测试 2/2 已通过，记录及 UI 冒烟画面见[测试说明](../Tests/README.md)；按钮事件调用不证明真实鼠标输入或排版，Exit 未由流程测试执行。FinalScene 已验证画面、UI 射线及点击 Exit 停止 Editor 播放；独立 Player 退出未构建实测。

## Level1 相机范围

`Levels/Level1.unity` 继承 `Prefabs/Env/ScenePrefab/Camera.prefab` 的 Orthographic Size `5.5`、禁用的 Pixel Perfect Camera 与 Cinemachine Confiner（2D模式） 屏幕边缘限制。独立的 `Camera Bounds` PolygonCollider2D 使用 Trigger 和 Ignore Raycast 层，通过场景覆盖绑定 Confiner，不作为玩家支撑面或交互目标。世界范围为 X `[-10, 20]`、Y `[-5.5, 5.5]`，精确包含上、下各 1.5 层 tile，并排除左、右各一列 tile。

Prefab 统一提供视野及限制组件，范围碰撞体仍由各场景绑定；Level1 不再覆盖旧 size 6。其他场景已有覆盖保留，未绑定范围的实例不会被限制。固定可视高度 11，宽高比超过 `30/11` 时画面宽度超过关卡内部宽度，不能同时保持完整垂直范围并排除左右边框；本次未适配该超宽画幅。[Level1 运行画面](../Docs/Development/Level1-CameraBounds.png)。

验证记录（2026-10-07）：独立 review 选择的 MainMenu、Hub、Level0 资源完整性 EditMode 用例 3/3 通过。Level1 实际 Brain 在 4:3、16:9、21:9 的极端位置及中心位置，以及连续跟随位置共 36 次采样全部满足范围；Level1、Level0 无 Console 错误或警告。Hub 相机正常启用，另有环境未找到带线插座的既有警告；Level0 保留启用 Pixel Perfect Camera 的场景覆盖，实际 size 为 5.625。新实例继承双 size 5.5、禁用的 Pixel Perfect Camera 和未绑定范围的限制组件。临时对象、Follow、活动场景及 EditorSettings 已恢复；未新增固定调参值的自动化测试。
