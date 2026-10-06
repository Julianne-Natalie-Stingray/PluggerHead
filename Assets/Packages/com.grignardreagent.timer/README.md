# Timer

## 本项目接入与逐文件核查（2026-10-06）

本目录是 `Assets/Packages/` 内的源码副本，不是项目根 UPM Packages 目录。package.json 标记版本1.2.0、最低 Unity 2022.2、GameLog 依赖1.1.0；下方安装说明是包的分发方式，不要求在本项目重复安装。版本历史见 [CHANGELOG](CHANGELOG.md)。

| 文件 | 实际职责 |
| --- | --- |
| `Runtime/Timer.cs` | 普通 C# 计时器；配置时间点、完成回调、时钟与完成条件，由 Runner 协程推进。 |
| `Runtime/TimerRunner.cs` | MonoBehaviour 协程宿主；Awake 注册 Instance，重复实例只禁用自身组件，销毁当前实例时清空引用。它不会自行 DontDestroyOnLoad，本项目由 Core 保活。 |
| `Runtime/GrignardReagent.Timer.asmdef` | 自动引用、无平台限制，显式程序集引用列表为空；package.json 依赖声明不等于 asmdef 引用列表。 |
| `package.json` | 包版本与分发依赖元数据。 |
| `CHANGELOG.md` | 1.0.0–1.2.0 历史记录；CompleteWhen 和公开 Instance 与当前源码一致，历史示例不是完整实现。 |

### API 与边界

- `At`、`OnComplete`、`UseUnscaledTime`、`CompleteWhen` 只允许在首次 Start 找到 Runner 并进入启动流程前配置；没有 Runner 的提前返回不锁定配置。找到但失活的宿主可能无法启动协程，配置仍已锁定；Stop 后也不能重新配置，Restart 保留原配置。
- Start 启动或继续，Stop 保留 Elapsed 与已执行时间点；完成后必须 Restart 才会再运行。Restart 重置进度并立即 Start。零时长完成和时间0回调可在 Start 调用内同步发生，但也先要求 Runner 存在。
- CompleteWhen 每次循环检查条件，成立时把 Elapsed 推到 Duration，执行剩余时间点再触发完成；不是取消。scaled time 在 timeScale=0 时不推进，但条件仍可被轮询并触发完成，不能笼统理解为所有逻辑冻结。
- 时间点按时间排序，相同时间的注册顺序没有稳定排序保证。多次 OnComplete 按委托注册顺序追加，但任何回调抛错都会中断该次调用链，没有异常隔离。
- 负时长钳到0；没有完整的 NaN/Infinity 输入校验。回调内 Stop/Restart/修改宿主等重入没有版本守卫，可能使外层协程继续执行过时流程；不要据流式接口推断这些组合已安全处理。
- Runner 被禁用、失活、销毁或替换时，计时器没有统一的中断通知与状态修复；IsRunning 不保证底层协程仍在推进。Stop 使用当时的全局 Runner，并未保存最初宿主引用。

本项目 AudioEmitter 使用 Timer，并在复用/重置时取消旧任务。现有音频测试覆盖部分自然结束和旧任务隔离，不等于 Timer 全部参数、异常、重入及宿主生命周期分支已验证。本轮为静态核查及文档修订，未改变包代码。

A lightweight coroutine-backed fluent timer for Unity.

## Dependency

This package depends on:

- `com.grignardreagent.gamelog` `1.1.0`

When using local tarballs, install the GameLog package first.

## Install

- Add from url "https://github.com/Julianne-Natalie-Stingray/Timer.git"

## Setup

Add `TimerRunner` to a persistent GameObject (for example your `GameCore` object).

## Example

```csharp
new Timer(5f)
    .At(1f, WarnPlayer)
    .At(3f, FlashSomething)
    .OnComplete(Explode)
    .Start();
```

Timers use scaled time by default, so elapsed time stops advancing when `Time.timeScale == 0`; completion conditions can still be evaluated.

Use:

```csharp
new Timer(2f)
    .UseUnscaledTime()
    .OnComplete(HideMessage)
    .Start();
```

to continue during a time-scale pause.

## Requirements

- Unity 2022.2 or newer.
- `com.grignardreagent.gamelog` `1.1.0`.
