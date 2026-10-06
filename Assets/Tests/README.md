# 整合集成测试

Unity 2022.3.43f1c1 / Unity Test Framework 1.1.33。

## Unity Test Runner

打开 **Window > General > Test Runner**，分别运行以下程序集；所有用例的 Category 都是 `Integration`。

| 模式 | 程序集 | 用例 | 覆盖 |
| --- | --- | --- | --- |
| EditMode | `PluggerHead.EditModeTests` | 1 个 Player/Env 集成用例 | 47 项断言（含线长边界、绕线超限死亡和单次死亡通知）：拾取、放下、UI 图标、最近目标、绕线、阻力、输入锁、跨场景隔离、换线、闭环、重开和死亡 |
| EditMode | `PluggerHead.EditModeTests` | 4 个场景参数用例 | Build Settings、场景加载、丢失脚本/预制体、电线材质、实际 Player 与电路配置 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个音频生命周期用例 | 原有 19 项断言：默认参数、Builder 覆盖、停止、自然结束、池复用、旧 Timer 隔离、循环及淡出 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个实际玩法场景用例 | `HeXieTestScene` 启动、帧推进、拾取/放下、绕线渲染、换线、通关一次、重开、超限死亡及单次死亡通知 |

共 7 个 Test Runner 用例。47/19 是用例内部的检查点数量，不是 NUnit 用例数量。PlayMode 用例由 Runner 自动进入/退出播放；音频用例会创建缺失的 TimerRunner 和 AudioListener，玩法用例会加载并卸载自有场景和 Core，无需预先打开或手工配置运行场景。

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

- Runner 启动前会要求处理未保存的场景修改；先保存自己的工作。测试实现不会保存被检查的场景。
- EditMode 的 Player/Env 用例使用独立预览场景并恢复临时全局状态；场景参数用例只关闭自己打开的场景。
- PlayMode 音频用例直接将异常交给 Runner，10 秒总超时，`finally` 清理声部、配置、clip、临时 TimerRunner 并恢复时间倍率、监听器暂停与后台运行设置。
- 玩法用例只修改自己加载的场景；`UnityTearDown` 在成功或失败时卸载场景并清理自有持久 Core。异步加载、卸载和动画锁等待各有 15 秒超时。
- 玩法用例在自有场景加载回调中临时取消线长限制，完成交互流程后设置有限线长，验证真实物理帧的超限死亡。这样兼容开局即超限的诊断配置，不修改或保存原场景资源。
- 原有 `EnvironmentIntegrationChecks.Run()` 和 `AudioIntegrationChecks.Run()` 手动入口仍可使用。自动测试调用 `RunForTests()`，不依赖手工轮询音频 `LastResult`。
