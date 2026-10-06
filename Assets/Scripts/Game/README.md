# 游戏流程、状态与持久化

本目录连接主菜单、全局状态、玩家设置和关卡进度。角色操作在 Player，电路与绕线在 Env，共享输入/音频/场景切换在 Core；Game 不另建一套玩家或电路数据模型。

## 子目录与职责

| 目录 | 入口 | 当前职责 |
| --- | --- | --- |
| [MainMenu](MainMenu/README.md) | MainMenuScreen | 主菜单按钮可用性、首关/续关请求、打开设置及退出。 |
| [GameState](GameState/README.md) | GameStateManager | Playing/Freezed/Loading 标签、暂停倍率、监听器及 Changed 通知。 |
| [Setting](Setting/README.md) | SettingBootstrap / SettingsScreen | BeforeSceneLoad 读音量设置、显式及退出保存、暂停面板与返回菜单。 |
| [Progress](Progress/README.md) | GameProgress / LevelProgressTracker | 缓存/保存最近进入的玩法关卡标识，成功加载后记录。 |
| `RestartLevelScreen.cs` | RestartLevelScreen | 监听 PlayerMove.Died，显示死亡提示，并通过 SceneSwitch 重载当前关卡场景。 |
| `NextLevelScreen.cs` | NextLevelScreen | 监听同场景 EnvironmentFacade.LevelCleared，显示祝贺提示，通过 SceneSwitch 进入配置的下一关。 |

根目录直接包含 `RestartLevelScreen.cs`、`NextLevelScreen.cs`、`WireLengthDisplay.cs` 和本 README/meta；其余实现与逐文件检查结果位于上表链接中。

`WireLengthDisplay` 在环境更新路径后的 LateUpdate 查询同场景当前持线，以世界单位显示长度上限减实际绕线路径长度，保留一位小数，超限归零。不限长显示 `剩余线长：不限`，未持线或缺少必要环境引用显示 `剩余线长：--`。GlobalUI 左上角 GameplayHUD 的右侧预留空白 ScoreText，尚未接入计分。

## 运行流程与装配

构建首场景为 `Assets/Scenes/MainMenuScene.unity`。主菜单与当前玩法场景装配 Core prefab；Core 保活且移除重复实例。New Game 默认请求 GameplayIntegration，Continue 请求进度中有效且可加载的玩法关卡。两者都按 Single 方式加载场景默认状态；进度不保存位置、背包、电路、绕线或通关状态。

暂停设置面板只有在 Playing 打开时取得暂停所有权，关闭时释放；返回菜单先解除本面板的暂停再切换。SceneSwitchManager 驱动 Loading，SettingsScreen 在 Loading 时拒绝 Open。Loading 期间 Freeze/Resume 为空操作，ExitLoading 恢复加载前状态，保留冻结前倍率。

`GameplayIntegration` 的 `GlobalUI` 挂载 RestartLevelScreen 组件，引用默认隐藏的死亡面板和 Restart 按钮；Player 未手动配置时自动查找同场景对象（包含停用对象），缺失时每 0.5 秒重试。公开 FindPlayer 方法也可通过组件菜单调用。禁用时退订并隐藏面板，重新启用时恢复已死亡玩家的提示。玩家死亡事件会显示提示；按钮请求以 Single 模式重新加载 `GameplayIntegration`，从关卡默认状态开始。

`NextLevelScreen` 同样挂在保持启用的 GlobalUI 根对象上，控制默认隐藏的同名子面板。Start 查询同场景 Env 并订阅 LevelCleared；通关后显示“恭喜通关！你已接通电路。”和“下一关”，不改变暂停状态。按当前配置，下一关重新加载 `GameplayIntegration`；以后在组件的 `nextLevel` 中修改 SceneId。请求被拒时显示中文失败提示，保留重试；请求成功后禁用按钮，防止重复加载。组件禁用时退订并隐藏，重新启用会恢复已通关状态；Env 调试重开后面板在下一帧隐藏。运行画面见 [NextLevelScreen](../../Docs/Development/NextLevelScreen.png)。

两个持久化文件都位于 Application.persistentDataPath，但契约不同：

| 文件 | 数据 | 保存时机 | 当前写入策略 |
| --- | --- | --- | --- |
| GameSettings.json | Master/Ost/Sfx 音量 | SaveSettings/显式 Save，Application.quitting 也尝试保存 | 先写同目录临时文件，再替换/移动；写入失败保留旧文件 |
| LevelProgress.json | 最近玩法关卡的 SceneId 整数 | Tracker.Start 检查活动场景，随后响应 sceneLoaded | 写 .tmp 后替换/移动，成功后才更新内存 |

音量默认 1/0.5/0.5；存储 Save 不自动调用 AudioManager.ApplyAudioSettings；SettingsScreen 在保存成功后显式应用实际混音器，保存失败则恢复内存值。进度 TryGetLevel 只检验枚举，主菜单还检验玩法标记与构建注册；新增玩法关卡需同时更新枚举、配置、场景与 Build Settings。

## 核查结论（2026-10-06）

先分别核查并提交 MainMenu、Progress、GameState、Setting，再检查根目录四个 folder meta 和本总览；子目录间的真实依赖、初始化、事件和序列化引用已交叉核对。独立审查确认文档描述与实现一致。后续针对检查结果修复了加载中交错暂停、UI 保存音量即时应用、设置 JSON 业务校验及文件替换，具体契约与覆盖见对应子目录文档。

原文档检查阶段最后一次影响脚本文件的变更仅为 Setting XML 注释，编译后顺序通过 EditMode 7/7 与 PlayMode 14/14，job 及探针证据见 Setting 文档。之后新增本总览只改变文档，不重复运行测试；该历史回归不作为后续行为修复的测试证据。
