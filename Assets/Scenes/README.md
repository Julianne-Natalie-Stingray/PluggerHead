# 场景总览

当前 Build Settings 启用六场景，顺序如下；TestLevel 为未注册开发关卡。SceneId 数值与构建索引是不同概念。

| 构建索引 | 文件 | SceneId 数值 |
| --- | --- | --- |
| 0 | `MainMenuScene.unity` | 4 |
| 1 | `../Tests/Scenes/GameplayIntegration.unity` | 3 |
| 2 | `../Tests/Scenes/CircuitDiagnostics.unity` | 2 |
| 3 | `../Tests/Scenes/SceneSwitchTarget.unity` | 1 |
| 4 | `FinalScene.unity` | 5 |
| 5 | `Level0.unity` | 6 |

`FinalScene` 是可独立播放的结尾场景：包含“恭喜通关！”标题、标明“示例团队”的制作组名单和“退出游戏”按钮，使用中文 TMP/uGUI，专名保留原文。Canvas 按 1280×720 缩放，配有 Input System EventSystem、相机/监听器及 Core prefab。Exit 在 Editor 停止播放，在构建版退出程序；文本可直接在场景 Inspector 修改。它注册为非玩法场景，不改变内存通关进度，可通过 `RequestSwitch(SceneId.FinalScene)` 接入。旧英文运行画面见 [FinalScene](../Docs/Development/FinalScene.png)。

`MainMenuScene` 是构建入口，包含 Core prefab、相机/监听器、uGUI 菜单与 EventSystem。MainMenuScreen 引用大厅 Player，默认显示菜单并锁定角色；NewGame、OpenSettings、ExitGame 持久事件均有接线。默认隐藏的设置面板引用三路滑块及 Save/Continue/ReturnToMainMenu 按钮。

开始按钮隐藏菜单并解锁角色。三个 Portal 从左到右代表第一至三关，第一关进入 Level0，第二、三关显示“尚未开放”。Level0 的通关按钮返回大厅，顺序解锁进度仅在本次运行内保留，允许重玩。原 Level0 布局的通关限制按用户要求保留，见[进度说明](../Scripts/Game/Progress/README.md)。其他三个场景逐文件说明见[功能场景](../Tests/Scenes/README.md)，菜单行为见 [MainMenu](../Scripts/Game/MainMenu/README.md)，配置见 [SO/SceneSwitch](../SO/SceneSwitch/README.md)。

MainMenu、GameplayIntegration、CircuitDiagnostics、FinalScene、Level0 均配置同一 Core prefab，运行时去重与保活由 CoreFacade 实现。SceneSwitchTarget 不含 Core，需要从已有服务的场景进入。不要因在编辑器单独打开目标场景而推断服务失效。

六个场景 GUID、默认映射与构建列表已核对，无路径断链。结构测试与菜单流程测试已通过，记录见[测试说明](../Tests/README.md)；按钮事件调用不证明真实鼠标输入或排版，Exit 未由流程测试执行。FinalScene 已验证画面、UI 射线及点击 Exit 停止 Editor 播放；独立 Player 退出未构建实测。

## Level1 相机范围

`Levels/Level1.unity` 使用场景内覆盖配置 Cinemachine Confiner 的 2D 模式，并约束屏幕边缘。独立的 `Camera Bounds` PolygonCollider2D 使用 Trigger 和 Ignore Raycast 层，不作为玩家支撑面或交互目标。世界范围为 X `[-10, 20]`、Y `[-6, 6]`；Orthographic Size 为 `6`，精确包含上、下各两层 tile，共四层，并排除左、右各一列 tile，共两列。

本场景禁用 Pixel Perfect Camera，避免其重新计算正交视野大小；共享 Camera 预制体和其他关卡未修改。4:3、16:9、21:9 画幅的边界探针已验证。宽高比超过 `2.5` 时，画面宽度超过约束区域，不能同时保持完整垂直范围并排除左右边框；本次未适配该超宽画幅。[Level1 运行画面](../Docs/Development/Level1-CameraBounds.png)。

验证记录（2026-10-07）：独立 review 选择 Level1 临时运行探针，实际 Cinemachine Brain 在 16:9 下经过 21 个连续跟随位置及中心位置采样，视野大小始终为 6，上下边缘始终为 -6/6，水平边缘未超出 -10/20。Console 无错误或警告；临时跟随对象已销毁，Follow 已恢复 Player，退出 Play 后场景干净，EditorSettings 与开始时一致。已有 Test Runner 用例不覆盖 Level1 相机配置，本次仅变更场景配置，未运行无关程序集，未新增固定调参值的自动化测试。
