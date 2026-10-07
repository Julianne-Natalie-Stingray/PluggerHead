# 游戏流程、状态与持久化

本目录连接主菜单、全局状态、玩家设置和关卡进度。角色操作在 Player，电路与绕线在 Env，共享输入/音频/场景切换在 Core；Game 不另建一套玩家或电路数据模型。

## 子目录与职责

| 目录 | 入口 | 当前职责 |
| --- | --- | --- |
| [MainMenu](MainMenu/README.md) | MainMenuScreen | 显示主菜单，开始后加载独立大厅，打开设置及退出。 |
| [GameState](GameState/README.md) | GameStateManager | Playing/Freezed/Loading 标签、暂停倍率、监听器及 Changed 通知。 |
| [Setting](Setting/README.md) | SettingBootstrap / SettingsScreen | BeforeSceneLoad 读音量设置、显式及退出保存、暂停面板与返回菜单。 |
| [Progress](Progress/README.md) | GameProgress / LevelProgressTracker | 运行期间保存连续通关进度，按顺序解锁，退出后清除。 |
| `RestartLevelScreen.cs` | RestartLevelScreen | 监听 PlayerMove.Died，显示死亡提示，并通过 SceneSwitch 重载当前关卡场景。 |
| `NextLevelScreen.cs` | NextLevelScreen | 监听同场景 EnvironmentFacade.LevelCleared，显示祝贺提示，通过 SceneSwitch 进入配置的下一关。 |

根目录直接包含 `RestartLevelScreen.cs`、`NextLevelScreen.cs`、`WireLengthDisplay.cs` 和本 README/meta；其余实现与逐文件检查结果位于上表链接中。

`WireLengthDisplay` 在环境更新路径后的 LateUpdate 查询同场景当前持线，以世界单位显示长度上限减实际绕线路径长度，保留一位小数，超限归零。不限长显示 `剩余线长：不限`，未持线或缺少必要环境引用显示 `剩余线长：--`。GlobalUI 左上角 GameplayHUD 的右侧预留空白 ScoreText，尚未接入计分。

## 运行流程与装配

构建首场景为 `Assets/Scenes/MainMenuScene.unity`。主菜单与当前玩法场景装配 Core prefab；Core 保活且移除重复实例。“开始游戏”加载独立 HubScene；大厅右上角 MenuBtn 打开设置/暂停面板，可继续游戏或返回主菜单。三个 Portal 从左到右对应第一至三关，第一关进入 Level0，后两关尚未开放。进度只在本次运行中保存通关解锁状态，不读取或写入关卡存档，允许重玩。

设置、死亡、通关面板在可见时独立请求冻结，隐藏或销毁时释放自身请求；仍有其他面板请求或手动暂停时不恢复游戏。返回菜单先关闭设置面板再切换。SceneSwitchManager 驱动 Loading，SettingsScreen 在 Loading 时拒绝 Open。Loading 期间手动 Freeze/Resume 为空操作；面板请求可登记和释放，ExitLoading 据此恢复状态与冻结前倍率，避免切关后遗留暂停。

`GlobalUI` 挂载 RestartLevelScreen 组件，引用默认隐藏的死亡面板和 Restart 按钮；Player 未手动配置时自动查找同场景对象（包含停用对象），缺失时每 0.5 秒重试。公开 FindPlayer 方法也可通过组件菜单调用。禁用时退订并隐藏面板，重新启用时恢复已死亡玩家的提示。玩家死亡事件会显示提示并请求冻结；按钮以 Single 模式重载 UI 所属关卡，从默认状态开始。关卡 Menu 的“重开关卡”按钮复用 `TryRestartLevel()`，不要求玩家死亡或显示死亡面板。重开失败显示中文提示并保留暂停和重试，成功后防止重复请求；旧面板卸载会释放暂停。TestLevel 等未注册 Build Settings 的已保存开发场景可在 Editor 中重开，发布构建仍需注册关卡。

`NextLevelScreen` 同样挂在保持启用的 GlobalUI 根对象上，控制默认隐藏的同名子面板。Start 查询同场景 Env 并订阅 LevelCleared；通关后显示“恭喜通关！你已接通电路。”和“下一关”，面板可见期间请求冻结，隐藏时释放。共享 prefab 默认重新加载 `GameplayIntegration`；Level0 场景覆盖为“返回大厅”，进入 `HubScene`。以后在组件的 `nextLevel` 中修改 SceneId。请求被拒时显示中文失败提示，保留重试；请求成功后禁用按钮，防止重复加载。组件禁用时退订并隐藏，重新启用会恢复已通关状态；Env 调试重开后面板在下一帧隐藏。运行画面见 [NextLevelScreen](../../Docs/Development/NextLevelScreen.png)。

音量设置持久化到 Application.persistentDataPath；关卡进度只在内存中：

| 文件 | 数据 | 保存时机 | 当前写入策略 |
| --- | --- | --- | --- |
| GameSettings.json | Master/Ost/Sfx 音量 | SaveSettings/显式 Save，Application.quitting 也尝试保存 | 先写同目录临时文件，再替换/移动；写入失败保留旧文件 |
| GameProgress.Store（内存） | 已连续通关关数 | LevelProgressTracker 响应 EnvironmentFacade.LevelCleared | 每次启动清空；返回大厅或重玩保留 |

音量默认 1/0.5/0.5；存储 Save 不自动调用 AudioManager.ApplyAudioSettings；SettingsScreen 在保存成功后显式应用实际混音器，保存失败则恢复内存值。Portal 检验关卡是否已实现、顺序解锁、玩法标记及构建注册；新增关卡须更新枚举、配置、场景、Build Settings、Portal 目的地及环境上的 LevelProgressTracker。Level0 原布局按用户要求保留，已知通关限制见 [Progress](Progress/README.md)。

## 核查结论（2026-10-06）

先分别核查并提交 MainMenu、Progress、GameState、Setting，再检查根目录四个 folder meta 和本总览；子目录间的真实依赖、初始化、事件和序列化引用已交叉核对。独立审查确认文档描述与实现一致。后续针对检查结果修复了加载中交错暂停、UI 保存音量即时应用、设置 JSON 业务校验及文件替换，具体契约与覆盖见对应子目录文档。

原文档检查阶段最后一次影响脚本文件的变更仅为 Setting XML 注释，编译后顺序通过 EditMode 7/7 与 PlayMode 14/14，job 及探针证据见 Setting 文档。之后新增本总览只改变文档，不重复运行测试；该历史回归不作为后续行为修复的测试证据。
