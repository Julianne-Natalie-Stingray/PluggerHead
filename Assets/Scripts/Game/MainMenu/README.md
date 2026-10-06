# 主菜单

本目录的 `MainMenuScreen.cs` 驱动 `Assets/Scenes/MainMenuScene.unity` 的 uGUI 菜单。该场景是 Build Settings 第一项，Core prefab 提供持久服务；菜单组件自身随场景卸载，不负责创建 Core。进度文件契约见 [Progress](../Progress/README.md)，音量面板实现位于 `../Setting/SettingsScreen.cs`。

## 装配与文件职责

`FinalSceneScreen.cs` 单独驱动 `Assets/Scenes/FinalScene.unity` 的 Exit 持久事件，不依赖主菜单引用。Editor 中停止播放，Player 中调用 Application.Quit。结尾场景的祝贺和示例制作组文本保存在场景内，详见 [场景总览](../../../Scenes/README.md)。

- `MainMenuScreen.cs`：刷新按钮可用性，处理四个按钮的动作，向 SceneSwitch 服务请求加载。
- `MainMenuScreen.cs.meta`：保留组件 GUID `07f8c2408f313e64d836e5961bd7412e`；主菜单场景通过它引用脚本。
- `README.md` 及 `.meta`：目录说明及 Unity 文档资源标识。

Inspector 必须配置 `configs`、`firstLevel`、四个 Button、`settingsScreen` 和状态 TMP 文本 `status`。现有场景将 `firstLevel` 设为 `GameplayIntegration`（3），使用 `DefaultSceneSwitchConfigs`。四个 Button 的持久 onClick 分别指向 `NewGame`、`ContinueGame`、`OpenSettings`、`ExitGame`；脚本没有在运行时自动注册按钮回调。

场景还需 Canvas、GraphicRaycaster 和 EventSystem 接收 UI 输入。菜单不对所有引用做空值校验；遗漏按钮或面板等引用可能在 `Start` / `Update` 抛异常，应在复用场景时完整装配。

## 动作与可用性

`Start` 和每次 `Update` 都刷新按钮。共同条件是 Core 存在、SceneSwitch 没有切换在途、设置面板的 `activeSelf` 为 false。New Game 还要求首关可加载；Continue 还要求存储返回有效枚举且该关卡可加载。可加载指配置标记为玩法关卡、有场景名映射且可从 Build Settings 取得构建索引。这个过程查询缓存进度，不每帧读盘。

| 动作 | 实际行为 |
| --- | --- |
| New Game | 请求首关，不提前清除进度；成功加载后由 LevelProgressTracker 尝试保存。 |
| Continue Game | 查询最近关卡，再请求 Single 加载；从场景默认状态开始，不恢复角色位置、背包或电路。 |
| Settings | 调用已有 SettingsScreen.Open，由面板负责暂停及关闭后的恢复。 |
| Exit | Player 调用 Application.Quit；Editor 设置 isPlaying 为 false。 |

`LoadLevel` 对不可加载、Core 缺失或面板打开的调用直接返回；SceneSwitch 拒绝请求时才显示英文错误提示。按钮不可交互并不等于公开方法都被锁住：程序直接调用 `OpenSettings` / `ExitGame` 不检查共同条件，`LoadLevel` 的并发拒绝由 SceneSwitch 执行。状态文本没有成功后自动清空的分支，成功切换通过卸载菜单清除它。

## 核查与验证（2026-10-06）

先逐项检查脚本、原有 `.meta`，再创建本目录总文档；交叉核对了主菜单场景组件与持久事件、默认切换配置、Core、Build Settings、SettingsScreen 和进度实现。默认装配下未发现确定性流程缺陷；缺失 Inspector 引用及直接调用方法的边界如上，不把按钮禁用当成业务级调用保护。

现有 PlayMode `MainMenuTests.MainMenu_NewGameSettingsReturnAndContinueFromDefaultState` 经 `MainMenuIntegrationChecks.CheckMenuFlow` 验证按钮目标、无存档、设置暂停/恢复、New Game、返回主菜单、读盘续关及非玩法进度过滤。它通过 `onClick.Invoke()` 触发按钮，未模拟鼠标点击或验证 Exit（否则会中止测试），也不证明最终画面排版。需要运行验证时先执行 `PluggerHead.EditModeTests`，再执行 `PluggerHead.PlayModeTests`；本次仅新增文档，未运行 Test Runner。
