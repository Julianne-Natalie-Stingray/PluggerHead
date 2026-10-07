# 测试反射桥

本目录连接测试程序集与项目预定义程序集。逐文件核查日期：2026-10-06。

| 文件 | 实际行为 |
| --- | --- |
| `IntegrationCheckBridge.cs` | `FindType` 按当前 AppDomain 已加载程序集顺序返回首个名称匹配类型；找不到时抛出异常。`Invoke` 查找公开静态方法并反射调用，解包 `TargetInvocationException.InnerException`，通过 `ExceptionDispatchInfo` 保留原异常堆栈。 |
| `PluggerHead.TestSupport.asmdef` | 名称为 `PluggerHead.TestSupport`，无程序集引用、无平台过滤、关闭自动引用，标记 `TestAssemblies`；EditMode 和 PlayMode 测试程序集显式引用它。 |
| 对应 `.meta` | 保留脚本和程序集 GUID；没有脚本默认引用或特殊执行顺序。 |

生产脚本仍在预定义程序集，测试通过类型名和方法名调用，不需要改变游戏程序集布局。桥本身不自动加载程序集，也不限制被调用类型必须属于某个程序集；类型重名时依赖枚举顺序。方法查询未提供参数类型，因此应使用名称唯一的公开静态入口，重载可能导致 `AmbiguousMatchException`。类型、方法或私有字段改名后，需要同步各测试中的字符串引用。

桥不负责测试隔离、清理或执行协程。返回 `IEnumerator` 的入口由调用方 `yield return`；协程推进时抛出的异常直接交给 Runner，并不经过反射调用的异常解包分支。全局状态、临时场景和文件清理由具体检查脚本负责。

当前测试在 Unity Editor 内调用 `Tests/Editor/` 的检查实现；这里的 asmdef 没有 Editor 平台限制，不代表这些检查可用于独立 Player。测试模式、运行命令和清理约定见[测试总说明](../README.md)。本轮只新增文档，未修改反射桥或程序集配置，无需重跑 Unity 测试。
