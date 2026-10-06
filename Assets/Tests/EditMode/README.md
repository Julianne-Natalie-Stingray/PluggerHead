# EditMode 测试

程序集 `PluggerHead.EditModeTests`，Category 均为 `Integration`。逐文件核查日期：2026-10-06；当前共 17 个展开后的用例。执行方式见[测试总说明](../README.md)，检查实现见[Editor 目录](../../Scripts/Editor/README.md)。

| 文件 | 用例数与实际覆盖 |
| --- | --- |
| `AudioConfigurationTests.cs` | 5：池容量 0、-1、int.MinValue、1、30，运行时归一化、OnValidate 写回及真实 ObjectPool 借还。 |
| `DebugSettingsTests.cs` | 1：暂时清空 SettingBootstrap.store，反射调用两个 Debug 设置按钮，确认 EditMode 不初始化或访问存储；finally 恢复引用并销毁宿主。 |
| `EnvironmentIntegrationTests.cs` | 1：调用 `EnvironmentIntegrationChecks.Run()` 并检查 PASS 前缀，实际断言和预览场景清理在该实现内。 |
| `GameStateTests.cs` | 2：从 Playing/Freezed 开始，重复进入 Loading 并交错 Resume/Freeze，确认 Loading 标签、时间倍率与监听器状态，退出后恢复原冻结/播放状态。 |
| `LevelProgressTests.cs` | 1：调用 `CheckProgressStorage()`，在临时路径验证只保存关卡、重新读盘、无效输入及写入失败保留旧文件。 |
| `SceneAssetTests.cs` | 5：四个场景参数用例和注册表检查；已加载场景直接复用，因此可能验证内存修改而非磁盘文件。 |
| `SettingsTests.cs` | 2：音量属性与默认实例独立性；临时设置文件的合法/非法数据及失败替换。存储用例声明一条预期 Error 日志。 |
| `PluggerHead.EditModeTests.asmdef` | 仅 Editor 平台，显式引用 TestSupport，关闭自动引用，标记 TestAssemblies。 |

对应 `.meta` 保留 GUID，无自定义脚本执行顺序。这里的用例不通过真实输入设备驱动 Player，也不验证实际画面或听感。

GameState 用例暂时移除已有 Changed 订阅者，反射保存/恢复私有静态字段及时间/监听器状态，避免触发现有场景订阅；因此不覆盖真实订阅链行为。Debug 用例仅验证 EditMode 拒绝入口，不证明 PlayMode 修改或保存成功。Settings 的文件锁失败情形依赖 Windows 文件共享语义，未证明其他平台一致。

测试用例和实现含私有字段、类型及方法名的字符串引用，重命名时必须同步。每个用例的隔离范围不同，应串行执行并检查失败堆栈及残留对象；不能仅由程序集运行通过推断所有运行时分支均已覆盖。本轮新增说明，未改变测试逻辑。
