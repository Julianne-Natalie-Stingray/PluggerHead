# 场景总览

逐文件核查日期：2026-10-06。当前 Build Settings 启用四场景，顺序如下；SceneId 数值与构建索引是不同概念。

| 构建索引 | 文件 | SceneId 数值 |
| --- | --- | --- |
| 0 | `MainMenuScene.unity` | 4 |
| 1 | `Tests/GameplayIntegration.unity` | 3 |
| 2 | `Tests/CircuitDiagnostics.unity` | 2 |
| 3 | `Tests/SceneSwitchTarget.unity` | 1 |

`MainMenuScene` 是构建入口，包含 Core prefab、相机/监听器、uGUI 菜单与 EventSystem。MainMenuScreen 引用默认切换配置，firstLevel=3；NewGame、ContinueGame、OpenSettings、ExitGame 持久事件均有接线。默认隐藏的设置面板引用三路滑块及 Save/Continue/ReturnToMainMenu 按钮。

New Game 进入 GameplayIntegration；Continue 只恢复已记录关卡的默认状态，不加载位置、背包或电路。其他三个场景逐文件说明见[功能场景](Tests/README.md)，菜单行为见 [MainMenu](../Scripts/Game/MainMenu/README.md)，配置见 [SO/SceneSwitch](../SO/SceneSwitch/README.md)。

MainMenu、GameplayIntegration、CircuitDiagnostics 均配置同一 Core prefab，运行时去重与保活由 CoreFacade 实现。SceneSwitchTarget 不含 Core，需要从已有服务的场景进入。不要因在编辑器单独打开目标场景而推断服务失效。

四个场景 GUID、默认映射与构建列表已核对，无路径断链。结构测试与菜单流程测试已通过，记录见[测试说明](../Tests/README.md)；按钮事件调用不证明真实鼠标输入或排版，Exit 未由流程测试执行。此次只新增说明，不保存场景、不改变 GUID 或构建配置。
