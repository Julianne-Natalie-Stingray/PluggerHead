# 场景总览

逐文件核查日期：2026-10-06。当前 Build Settings 启用五场景，顺序如下；同目录的 Level0、Level1、TestLevel 为未注册开发关卡。SceneId 数值与构建索引是不同概念。

| 构建索引 | 文件 | SceneId 数值 |
| --- | --- | --- |
| 0 | `MainMenuScene.unity` | 4 |
| 1 | `Tests/GameplayIntegration.unity` | 3 |
| 2 | `Tests/CircuitDiagnostics.unity` | 2 |
| 3 | `Tests/SceneSwitchTarget.unity` | 1 |
| 4 | `FinalScene.unity` | 5 |

`FinalScene` 是可独立播放的结尾场景：包含“恭喜通关！”标题、标明“示例团队”的制作组名单和“退出游戏”按钮，使用中文 TMP/uGUI，专名保留原文。Canvas 按 1280×720 缩放，配有 Input System EventSystem、相机/监听器及 Core prefab。Exit 在 Editor 停止播放，在构建版退出程序；文本可直接在场景 Inspector 修改。它注册为非玩法场景，不覆盖最近关卡进度；本次不改变关卡通关后的跳转逻辑，可通过 `RequestSwitch(SceneId.FinalScene)` 接入。旧英文运行画面见 [FinalScene](../Docs/Development/FinalScene.png)。

`MainMenuScene` 是构建入口，包含 Core prefab、相机/监听器、uGUI 菜单与 EventSystem。MainMenuScreen 引用默认切换配置，firstLevel=3；NewGame、ContinueGame、OpenSettings、ExitGame 持久事件均有接线。默认隐藏的设置面板引用三路滑块及 Save/Continue/ReturnToMainMenu 按钮。

New Game 进入 GameplayIntegration；Continue 只恢复已记录关卡的默认状态，不加载位置、背包或电路。其他三个场景逐文件说明见[功能场景](../Tests/Scenes/README.md)，菜单行为见 [MainMenu](../Scripts/Game/MainMenu/README.md)，配置见 [SO/SceneSwitch](../SO/SceneSwitch/README.md)。

MainMenu、GameplayIntegration、CircuitDiagnostics、FinalScene 均配置同一 Core prefab，运行时去重与保活由 CoreFacade 实现。SceneSwitchTarget 不含 Core，需要从已有服务的场景进入。不要因在编辑器单独打开目标场景而推断服务失效。

五个场景 GUID、默认映射与构建列表已核对，无路径断链。结构测试与菜单流程测试已通过，记录见[测试说明](../Tests/README.md)；按钮事件调用不证明真实鼠标输入或排版，Exit 未由流程测试执行。FinalScene 已验证画面、UI 射线及点击 Exit 停止 Editor 播放；独立 Player 退出未构建实测。
