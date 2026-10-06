# 整合集成测试

Unity 2022.3.43f1c1 / Unity Test Framework 1.1.33。

## 功能场景

主菜单位于 `Assets/Scenes/MainMenuScene.unity`；功能验证场景位于 `Assets/Scenes/Tests/`。第三方包内的示例场景不纳入构建列表。

| 场景 | 功能与使用入口 |
| --- | --- |
| `MainMenuScene` | 构建启动场景。New Game、Continue Game、Settings、Exit；无有效进度时 Continue 禁用。 |
| `GameplayIntegration` | New Game 的首关。真实 Player、地面四角绕线、双线回路、背包及设置 UI；Play 后移动、J 交互/收回、K 放置 Anchor。设置面板的 Main Menu 返回主菜单。 |
| `CircuitDiagnostics` | MockPlayer 与独立电路布局；Play 后用 EnvironmentFacade 调试按钮验证回路，拖动 MockPlayer 检查线端。含完整 Core，可用 AudioManager 的 Test Audio Request 检查音频。 |
| `SceneSwitchTarget` | 仅保留相机与 AudioListener；从前两者调用 `CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.SceneSwitchTarget)`，确认场景切换完成、Loading 退出且原 Core 存活。单独播放只显示背景。 |

2026-10-06 整理：`HeXieTestScene` → `GameplayIntegration`，`JillTestWireScene` → `CircuitDiagnostics`，`JillTestSceneSwitch` → `SceneSwitchTarget`，三者保留原 GUID。原 `JillTestScene` 仅含相机及同一个 Core prefab，其服务检查职责合并到 `CircuitDiagnostics`，删除重复场景及枚举项。现有 SceneId 的序列化值 1/2/3 保持不变，退役值 0 不复用；构建顺序改为 GameplayIntegration、CircuitDiagnostics、SceneSwitchTarget。

真实 Player 集成与 MockPlayer 电路诊断保持分开，便于区分输入/动画问题和回路问题。Core 继续随内容场景配置并通过 DontDestroyOnLoad 保活，无需额外 Bootstrap 或叠加加载。

## Unity Test Runner

打开 **Window > General > Test Runner**，分别运行以下程序集；所有用例的 Category 都是 `Integration`。

| 模式 | 程序集 | 用例 | 覆盖 |
| --- | --- | --- | --- |
| EditMode | `PluggerHead.EditModeTests` | 1 个 Player/Env 集成用例 | Anchor 不作为道具、无限放置/收回、无线放置、最近目标、绕线、阻力、输入锁、暂停、跨场景隔离、换线、闭环、重开、线长边界及单次死亡通知 |
| EditMode | `PluggerHead.EditModeTests` | 4 个场景参数用例＋1 个注册表用例 | 场景枚举/白名单/构建列表一致性、主菜单构建入口、场景加载、丢失脚本/预制体、电线材质、实际 Player 与电路配置 |
| EditMode | `PluggerHead.EditModeTests` | 1 个关卡存档用例 | 只序列化关卡、重新读盘、缺失/损坏/未知关卡、写入失败保留旧存档 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个主菜单流程用例 | 实际按钮引用、无存档禁用 Continue、Settings 暂停恢复、New Game、返回菜单、重新读盘 Continue、默认位置/空背包/默认电路、非玩法场景不覆盖存档 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个音频生命周期用例 | 原有 19 项断言：默认参数、Builder 覆盖、停止、自然结束、池复用、旧 Timer 隔离、循环及淡出 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个实际玩法场景用例 | `GameplayIntegration` 启动、帧推进、K 放置/J 收回 Anchor、绕线渲染、换线、通关一次、重开、超限死亡及单次死亡通知 |
| PlayMode | `PluggerHead.PlayModeTests` | 4 个地面极性用例 | 真实 2D 支撑接触、双向异极死亡、同极/无线安全、侧墙与天花板排除、Trigger/层过滤、禁用组件、站立换线与输入锁、自动物理帧死亡 |
| PlayMode | `PluggerHead.PlayModeTests` | 7 个 Corner 用例 | 活动线段进入、方向退绕、高速多角顺序、静止/向外移动、微小位移累积、双线独立 Anchor、重开/禁用清理、真实碰撞体坐标、远程交互排除、自动 LateUpdate 与 Anchor 销毁 |

表中的断言数是用例内部的检查点数量，不是 NUnit 用例数量；实际用例数与结果以 Test Runner 报告为准。PlayMode 用例由 Runner 自动进入/退出播放；音频用例会创建缺失的 TimerRunner 和 AudioListener，玩法用例会加载并卸载自有场景和 Core，无需预先打开或手工配置运行场景。

这些是 **Editor 内运行的 EditMode/PlayMode 测试**。验证实现复用 `Scripts/Editor/` 中的现有脚本；PlayMode 用例声明仅支持 Editor 平台，不用于独立 Player 测试包。测试程序集通过 `IntegrationCheckBridge` 调用预定义程序集，避免为了测试改动生产脚本的程序集布局。普通 Player 构建不包含 TestAssemblies。

## MCP calls

先检查 `mcpforunity://instances`、`mcpforunity://custom-tools`、`mcpforunity://editor/state`，确认连接 PluggerHead、编译完成且未运行其他测试。启用测试工具：

```json
{"action":"activate","group":"testing"}
```

调用 `run_tests`：

```json
{"mode":"EditMode","assembly_names":["PluggerHead.EditModeTests"],"include_details":true}
```

保存返回的 `job_id`，调用 `get_test_job` 直至 `status` 为终态：

```json
{"job_id":"<run_tests 返回的 job_id>","wait_timeout":30,"include_details":true,"include_failed_tests":true}
```

EditMode 完成后，再运行 PlayMode 并同样轮询自己的 job：

```json
{"mode":"PlayMode","assembly_names":["PluggerHead.PlayModeTests"],"init_timeout":120000,"include_details":true}
```

以返回的 `result.summary` 和失败用例堆栈判断结果，不把“已启动”当作测试通过。测试顺序必须串行；仍在运行的 job 应继续轮询，不重复启动。

## 隔离、失败与清理

- 主菜单和实际玩法用例将 `GameProgress` 指向临时目录，清理后恢复原存储引用并删除临时文件，不改写玩家的真实关卡进度。

- Runner 启动前会要求处理未保存的场景修改；先保存自己的工作。测试实现不会保存被检查的场景。
- EditMode 的 Player/Env 用例使用独立预览场景并恢复临时全局状态；场景参数用例只关闭自己打开的场景。
- PlayMode 音频用例直接将异常交给 Runner，10 秒总超时，`finally` 清理声部、配置、clip、临时 TimerRunner 并恢复时间倍率、监听器暂停与后台运行设置。
- 玩法用例只修改自己加载的场景；`UnityTearDown` 在成功或失败时卸载场景并清理自有持久 Core。异步加载、卸载和动画锁等待各有 15 秒超时。
- 玩法用例在自有场景加载回调中临时取消线长限制，完成交互流程后设置有限线长，验证真实物理帧的超限死亡。这样兼容开局即超限的诊断配置，不修改或保存原场景资源。
- 地面极性用例创建独立的 2D 物理场景，并先验证实际接触与法向，再检查死亡结果；`UnityTearDown` 卸载自有场景、恢复环境静态引用并清理自有 Core，不保存或修改关卡资源。
- Corner 用例使用独立 2D 物理场景及真实 Wire/Corner/Anchor，校验 LineRenderer 与 EdgeCollider2D 的顶点一致；帧推进用例验证实际 LateUpdate 与延迟销毁。测试后卸载自有场景并恢复 Environment 静态引用，不修改场景资源。
- 原有 `EnvironmentIntegrationChecks.Run()` 和 `AudioIntegrationChecks.Run()` 手动入口仍可使用。自动测试调用 `RunForTests()`，不依赖手工轮询音频 `LastResult`。
