# GameLog

## 本项目接入与核查（2026-10-06）

当前通过 `Assets/Packages/com.grignardreagent.gamelog/Runtime/GrignardReagent.GameLog.dll` 导入，项目根 manifest 没有另行注册此包；无需按下方分发说明重复安装。package.json 为1.1.0、Unity 2022.2；DLL 的 AssemblyVersion 为0.0.0.0，与包版本不是同一字段。

逐文件核查了本 README、[CHANGELOG](CHANGELOG.md)、package.json、DLL 元数据/相关 IL 及 meta。目录没有源码或 asmdef；CHANGELOG 1.1.0 的 DLL 分发记录与当前文件一致。PluginImporter 使用 Any 平台规则且非显式引用，不能只看单个 Editor 平台条目推断整个 DLL 被禁用。

### API 与行为

- `GameLog.Info/Warning/Error` 接受可选 UnityEngine.Object context，返回值类型 LogBuilder。普通 C# 对象不能直接作为 context 传入 `this`。
- Builder 通过 Subsystem、Name、Issue、Action 配置文字，Write 调用相应 Unity Debug 日志。**Action 只描述业务方将执行的动作，不会替调用者禁用组件、删除对象、钳制数值或返回方法。**
- LogName 支持 Class、GameObject、ClassAndGameObject、None；LogIssue 与 LogAction 提供预设文字及 Specify 自定义消息。
- Builder 是值类型，复制后修改不会自动同步其他副本；重复 Write 会再次输出，不是一次性消费接口。现有完整链式调用示例与接口一致。

项目 Core、Env、Game 等使用本 DLL。没有包内专项测试，当前集成回归中的日志调用不证明全部格式化分支或平台兼容性。本轮未替换 DLL、改变导入设置或联网验证上游安装地址。

A lightweight fluent logging utility for Unity.

## Installation

- Install `com.grignardreagent.gamelog` through Unity Package Manager.
- Or add with url "https://github.com/Julianne-Natalie-Stingray/GameLog.git".

## Example

```csharp
GameLog.Warning(this)
    .Subsystem("Movement")
    .Name(LogName.ClassAndGameObject)
    .Issue(LogIssue.Invalid("speed"))
    .Action(LogAction.ClampValue)
    .Write();
```

`context` is optional. Passing a Unity object lets Unity associate the console entry with that object; omit it for static/non-Unity systems.

## Requirements

- Unity 2022.2 or newer.
