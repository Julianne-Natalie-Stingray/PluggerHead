# Repository Guidelines

## 修改范围

- 当前用户是 **HeXie**。用户已于 2026-10-06 移除 Jill 目录的读写限制；项目不再按作者划分访问权限。
- 允许读取、新增、修改、删除或移动 `Assets/` 下的脚本、场景、预制体、资源、文档及对应 `.meta`，包括本 `AGENTS.md`。操作仍须遵守当前任务范围和执行环境的文件系统权限。
- 目录按功能组织，不再使用 `HeXie/` 或 `Jill/` 人名层级。移动资源时保留 GUID，并同步路径引用。
- `Docs/Development/WorkflowHistory.md` 是旧 DSH 工作流的历史记录，不是现行权限或审批规则；当前仓库规则以本文件为准。

## Subagent 协作

- 允许主代理按需创建 Subagent，执行一组阻塞性 I/O 操作（例如 MCP 通信、文件操作或等待外部工具结果），或承担边界明确、可独立完成的子任务，无需逐次征求用户许可。
- 委派时明确目标、操作范围、依赖及预期返回结果；主代理负责协调、整合与最终验证。
- 各代理共享工作区，避免同时修改同一文件；涉及同一 Unity Editor 状态的写操作应串行协调。
- Subagent 遵守相同的任务授权、仓库规则和文件系统权限；委派不扩大操作权限，也不绕过审批。

## Project Structure & Module Organization

This directory is `Assets/` in the PluggerHead Unity project; the project root is one level above, alongside `Packages/` and `ProjectSettings/`.

- `Scripts/Core/`: shared services, including input, audio, scene switching, and pooling.
- `Scripts/Game/`, `Scripts/Player/`, and `Scripts/Env/`: game state/settings, player gameplay, and environment interactions.
- `Scripts/Infra/` and `Scripts/Debug/`: generated integrations and debugging helpers.
- `Scenes/Tests/`: development scenes; existing scene names are preserved for scene-switch configurations.
- `Prefabs/Core/`, `SO/Audio/`, and `SO/SceneSwitch/`: shared service prefabs and configuration assets.
- `Docs/Development/`: development history and previous Inspector checklists.
- `Prefabs/`, `SO/`, `Audios/`, and `Visual/`: reusable objects, ScriptableObject data, audio, and visual assets.
- `Packages/`: bundled tools, including NaughtyAttributes and GrignardReagent utilities; distinguish this from the project-root UPM directory.

Read subsystem README files before changing their contracts.

## Build, Test, and Development Commands

Use Unity **2022.3.43f1c1**, as recorded in `../ProjectSettings/ProjectVersion.txt`.

- Open the parent project through Unity Hub. Load a development scene and press Play for local iteration.
- Use **File > Build Settings > Build** for a player build. The configured scene list contains `JillTestScene`, `JillTestSceneSwitch`, and `HeXieTestScene` under `Scenes/Tests/`; review it before building.
- Use **Window > General > Test Runner** to run EditMode and PlayMode tests.
- Run `git diff --check` before submitting to catch whitespace errors.

No project-specific command-line build script was found.

## Coding Style & Naming Conventions

Use four-space indentation and braces on separate lines. Use PascalCase for types and methods, camelCase for parameters and locals, and match nearby field conventions. Match MonoBehaviour filenames to their class names.

Preserve existing XML documentation and bilingual comments when updating behavior. Regenerate input bindings and `*.g.cs` integrations through their source tooling. No repository-specific formatter was identified.

## Testing Guidelines

Unity Test Framework 1.1.33 is installed. No dedicated project test suite or coverage threshold was found; bundled package examples are not gameplay regression coverage. For new automated tests, use EditMode or PlayMode test assemblies and descriptive names such as `Freeze_WhenAlreadyFrozen_PreservesTimeScale`.

Smoke-test affected scenes, check Console errors, and report reproduction steps and results.

## Commit & Pull Request Guidelines

Recent commits use short descriptive subjects without a consistent prefix convention. Prefer imperative subjects such as `Fix player movement input`.

Describe the behavior change, affected scenes or prefabs, and validation performed. Link relevant issues and include screenshots for visible changes. Commit asset `.meta` files, preserve GUIDs, and exclude generated Unity caches.

## Unity MCP 状态与功能

### 检查快照（2026-10-05 13:35，Asia/Shanghai）

- MCP 服务 `unityMCP` 通过 HTTP 正常响应，已连接 1 个 Unity Editor 实例：`PluggerHead@95792808555e24c6`，Unity 版本为 `2022.3.43f1c1`。
- `mcpforunity://project/info` 确认项目根目录为 `C:/Users/hx/UnityProjects/PluggerHead`，目标平台为 `StandaloneWindows64`。
- `mcpforunity://editor/state` 返回 `ready_for_tools: true`，快照未过期且无阻塞原因；编辑器处于 Edit Mode、空闲状态，未编译、未等待 Domain Reload、未刷新资源，也未运行测试。
- 已实测 `manage_scene(action="get_active")`：活动场景为 `Assets/Scenes/HeXie/HeXieTestScene.unity`，已加载，包含 4 个根对象；`isDirty: true` 表示存在未保存修改。本次检查未保存或改变场景。
- 已实测 `read_console(action="get", types=["error", "warning"], count=10)`，返回 0 条记录；仅代表本次 Console 查询结果，不等于完整测试通过。
- `mcpforunity://custom-tools` 返回 36 个注册工具条目，包括场景、脚本、资源、测试、渲染及生成工具；这是插件报告的工具列表，不表示存在 36 个项目自行编写的扩展。
- 本次验证了项目与编辑器资源读取、场景查询和 Console 读取。未执行写操作、构建、测试或资源生成；这些功能仍需按实际任务验证。

### 已注册功能（不等于已实测可用）

服务声明 10 个工具分组，默认启用 `core`；其他组需按需通过 `manage_tools(action="activate", group="...")` 启用，实际执行还取决于编辑器连接、包和服务配置。

| 分组 | 主要功能与工具示例 |
| --- | --- |
| `core` | 场景、GameObject、组件、预制体、资源、脚本、材质、相机、渲染、物理、包、构建和编辑器控制；`manage_scene`、`manage_gameobject`、`read_console`、`batch_execute` 等，共 25 个工具 |
| `testing` | `run_tests`、`get_test_job`：运行测试及查询异步结果 |
| `docs` | `unity_reflect`、`unity_docs`：实时 API 反射与官方文档查询 |
| `animation` / `ui` | 动画控制及 AnimationClip 创建；UI Toolkit 的 UXML、USS、UIDocument 操作 |
| `vfx` / `profiling` | 特效、Shader、纹理；Profiler、内存快照和 Frame Debugger |
| `scripting_ext` | C# 执行及 ScriptableObject 管理 |
| `probuilder` | 3D 建模，需要 ProBuilder 包 |
| `asset_gen` | 模型、图像、音频生成与模型导入；生成服务需要相应密钥配置 |

### 后续使用规则

- 此状态仅为检查时的快照。操作前重新读取 `mcpforunity://instances`、`mcpforunity://custom-tools` 和 `mcpforunity://editor/state`；确认连接的是 PluggerHead，且编辑器已就绪。多个实例时使用 `set_active_instance` 明确选择目标。
- 无会话时，先在 Unity 中打开本项目并检查 MCP 插件连接，再重新查询；不要将工具已注册视为编辑器已连接。
- MCP 写操作同样遵守上方修改范围。修改对象前确认所属场景或资源路径，并保留已有未提交或未保存的用户修改。
- 修改脚本后等待编译完成，并通过 `read_console` 检查错误；按需验证受影响场景和测试。

## 共享工具脚本：抽象与功能

以下基于 2026-10-05 的源码静态检查，路径已按 2026-10-06 的目录整理更新为相对于 `Assets/Scripts/`。用于接入现有服务，不代表运行时验证通过。README 与实现不一致时，以当前代码为准。上方 MCP 检查快照中的旧路径仅为历史记录。

### Core：共享运行时服务

| 模块 / 路径 | 抽象、功能与使用方式 |
| --- | --- |
| `Core/CoreFacade.cs` | 服务门面 MonoBehaviour。`Instance` 提供全局入口，实例属性 `Input`、`Audio`、`SceneSwitch` 暴露服务；`Awake` 缓存同物体组件，销毁重复 Core，并通过 `DontDestroyOnLoad` 保留自身。依赖 InputManager、AudioManager、SceneSwitchManager、TimerRunner；不会自动生成 Core 物体。 |
| `Core/Input/InputManager.cs` | 输入适配层，持有并管理生成的 `PlayerControls` 生命周期。公开 `PointerPosition`、`MovementInput`、`IsPrimaryPressed`，以及 `PrimaryPressed` / `PrimaryReleased` 事件。调用方解释玩法含义；当前不公开完整 Controls 对象，也不自动按游戏状态禁用输入。 |
| `Core/Input/DragAndDropService2D.cs` | 可序列化的普通 C# 辅助类，不是组件。`Initialize` 注入输入、Rigidbody2D、Collider2D、相机；`Enable` / `Disable` 订阅与释放输入事件。命中碰撞体后临时切为 Kinematic，保留可选拖拽偏移，结束时恢复原刚体类型。宿主需主动调用 `Drag()`，建议置于 `FixedUpdate`。 |
| `Core/Managers/AudioManager.cs` 与 `Core/Audio/` | 音频请求服务。通过 `CreateBuilder().With...().Play(AudioId)` 请求播放；`AudioBuilder` 描述一次请求，`AudioEmitter` 封装实际 AudioSource，`AudioRegistry` 记录声部及先后顺序，`ISoundHandle` 提供 `Stop`、`TrySetVolume`、`TrySetPitch` 和 `Finished` 事件。请求被拒绝时返回 `null`。 |
| `Core/Audio/SODefinitions/` | `AudioClipData` 定义 clip、混音组、循环、空间参数及默认冻结行为；`AudioManagerConfigs` 保存 AudioId 映射、预热量、池容量和实例限制。Inspector 需配置 emitterPrefab 和 configs；emitterRoot 未指定时回退到自身 Transform。 |
| `Core/GameObjectPool/GameObjectPool.cs` | `GameObjectPool<TPooled>` 包装 Unity ObjectPool，支持 `Get`、`Release`、`Prewarm`、计数和回调。需要硬性创建上限时，调用者先检查 `CanReuse(maxSize)`；`Get()` 本身没有该限制。`Clear()` 不负责销毁仍借出的活跃对象。 |
| `Core/SceneSwitch/` | `SceneId` 为类型化场景键，`SceneSwitchConfigs` 映射到场景名。`RequestSwitch(id)` 校验配置与 Build Settings，以 Single 模式异步加载并管理 Loading 状态；`IsSwitching` 防止并发请求，拒绝时返回 `null`。调用方决定目标场景。 |

### Game：状态与设置

- `Game/GameState/`：静态 `GameStateManager` 持有 `Playing`、`Freezed`、`Loading`，通过 `Changed` 通知订阅方。`Freeze()` 暂停时间与监听器，`Resume()` 恢复此前时间倍率；`EnterLoading()` / `ExitLoading()` 记录并恢复加载前状态。业务方应成对订阅与退订事件。
- `Game/Setting/`：`ISettingData` 定义默认值重置；`SettingStore<TData>` 负责文件存取；`FileSettingStore` 使用 JsonUtility；`SettingBootstrap` 在首个场景加载前初始化，并在退出时保存。数据路径为 `Application.persistentDataPath/GameSettings.json`。
- 当前 `GameSettings.Audio` 已包含 Master/Ost/Sfx 音量，默认值分别为 `1`、`0.5`、`0.5`。`ResetToDefault()` 只更新内存，需要立即落盘时调用 `Save()` 并检查返回值。设置层只存储数据，当前 AudioManager 未将这些值应用到混音器。
- 命名区分：`Configs` 是开发者维护的 ScriptableObject 参数；`Settings` 是玩家可调且可持久化的数据。

### Debug、Infra 与工具依赖

- `Debug/DebugScript.cs`：启动时打印音量；NaughtyAttributes 按钮可随机修改音量或恢复默认值。这些是手动验证入口，不是自动化测试；按钮不会立即保存设置，应在 Play Mode 使用。
- `Debug/FloatingLogic.cs`：基于初始局部位置，叠加周期漂移、Perlin 噪声摆动和 Z 轴旋转；提供质量、幅度、频率等 Inspector 参数及选中 Gizmos。属于视觉实验行为，会持续写 Transform，避免与角色移动同时控制同一物体。
- AudioManager 的 `Test Audio Request` 按钮可测试音效、重复请求及冻结选项，代码要求 Play Mode。配置资产上的 `Remove Duplicates` 按钮会实际修改列表，仅在任务需要时执行。
- `Infra/InputSystem/PlayerControls.cs` 是 Input System 生成代码；输入变更应先协商并修改源 `.inputactions`，不要手改生成文件。`Infra/MenuTool/Game.MenuTool.g.cs` 从 `.menutool` 生成，为 CreateAssetMenu 提供路径与文件名常量。
- `Assets/Packages/` 下的 GameLog、Timer、NaughtyAttributes 等是工具依赖，不属于项目业务脚本。现有代码用 `GameLog.Info/Warning/Error(...).Subsystem(...).Issue(...).Write()` 输出结构化日志；Timer 支持时间点回调、条件完成、停止/重启及非缩放时间，运行依赖 TimerRunner。

### 接入约束与当前实现注意点

- 在测试场景中确认 Core 实例及配置齐全，再通过 `CoreFacade.Instance.Input` 等接口访问；不要假设其他物体的 `Awake` 一定晚于 Core，通常在 `Start` 或明确初始化后获取服务。跨场景重新获取场景对象依赖。
- 音频冻结期间启动新声音，需要同时允许 `WithAllowWhileFrozen(true)` 与有效的 SurviveFreeze。池耗尽、配置缺失等情况下必须处理空句柄。
- 当前音频实现中，`AudioEmitter.Play()` 会重置音量和音高，因此 Builder 的对应覆盖值可能被覆盖；自然结束也走 `Stop()`，不能将 `ISoundHandle.IsFinished` 当作已验证的自然播完判据。循环声部被选作抢占对象时会跳过停止，故实例限制不应视作严格保证。
- `AudioManager` 按 `state == Freezed` 设置监听器暂停，进入 Loading 时可能改变冻结期间的声音行为。需要“暂停中切场景”等组合行为时，先在测试场景验证，并根据任务范围修复实现。
