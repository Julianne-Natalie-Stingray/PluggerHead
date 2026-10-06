# Editor 集成检查实现

这些脚本位于 Unity 的 Editor 特殊目录，编译进预定义 Editor 程序集。它们提供实际断言和测试夹具，由 `Tests/` 的 NUnit 包装通过反射调用；不是独立 Player 的运行时代码。逐文件核查日期：2026-10-06。运行入口和程序集说明见[测试总说明](../../Tests/README.md)。

## 文件与入口

| 文件 | 入口与实际检查 |
| --- | --- |
| `EnvironmentIntegrationChecks.cs` | `Run()`、菜单 `Tools > PluggerHead > Verify Player and Environment`、`RunBatch()`；独立预览场景验证 Anchor 放置/收回、目标选择、场景隔离、锁/暂停、路由长度、插接换线、闭合重开及死亡幂等。不是实际键盘输入或完整背包道具测试。 |
| `SceneIntegrationChecks.cs` | `CheckSceneRegistry()` 核对枚举、配置及四场景构建列表；`CheckSceneAsset(path)` 核对脚本/预制体/材质/组件与布局；`CheckGameplay()` 运行真实 GameplayIntegration，验证 J/K 处理器、回路、实际左上角挂线退绕和超长死亡。 |
| `MainMenuIntegrationChecks.cs` | `CheckProgressStorage()` 检查临时关卡存档；`CheckMenuFlow()` 调用实际按钮事件，检查设置、菜单往返及仅恢复关卡默认状态的 Continue。提供进度存储替换/恢复与清理入口。 |
| `SceneSwitchRecoveryChecks.cs` | `CheckRecovery(interruption)` 的失活、销毁、禁用及异常回调四条路径，检查重入拒绝、加载结束、预约释放与下一次请求；`Cleanup()` 清理服务与加载场景。 |
| `GroundPolarityIntegrationChecks.cs` | 三个同步检查与一个帧推进协程；真实 2D 接触、极性、法向、层/Trigger/禁用过滤、换线、锁定危险及自动物理帧死亡。 |
| `CornerIntegrationChecks.cs` | 六个同步检查与一个帧推进协程；方向退绕、快速多角、静止/微步、双线归属与清理、变换/offset、实际 LateUpdate 与延迟销毁。 |
| `FloatingIntegrationChecks.cs` | `CheckMotion(bool)` 对根物体及复杂父级执行真实 Update；关闭位移幅度以隔离旋转，检查位置不漂移、暂停与恢复，不验证完整漂浮噪声效果。 |
| `SettingsIntegrationChecks.cs` | `CheckAudioSetters()` 验证三路属性、NaN/钳制及默认实例独立；`CheckStorage()` 在临时路径验证读写、非法数据回退与失败替换；`CreateStore(path)` 通过反射注入临时存储。 |
| `AudioConfigurationIntegrationChecks.cs` | `CheckMaxPoolSize(int)` 检查五种池容量，区分运行时 getter 归一化与 OnValidate 写回，并构造真实 ObjectPool 借还。 |
| `AudioIntegrationChecks.cs` | `RunForTests()` 提供原有 19 项生命周期断言；手动 `Run()` 通过 `LastResult` 报告异步进展，需要 Play Mode、TimerRunner 和未暂停的监听器。 |
| `AudioHandleIntegrationChecks.cs` | `Run(bool)` 检查自然完成/淡出停止、句柄参数及非法请求、完成订阅者异常隔离；包装测试声明预期异常日志。 |
| `AudioTailIntegrationChecks.cs` | `Run(scenario)` 五种情形：反射驱动包络数值、真实慢速/快速播放、尾部变速、暂停时间及监听器时停止归池。 |
| `AudioLimitIntegrationChecks.cs` | `Run(scenario)` 十种情形：完成回调重入、跨限额补位、动态降限、最旧抢占、循环保护及非正限额拒绝。 |

对应 `.meta` 保留脚本 GUID；本轮未改变资源引用或执行顺序。测试数量以 NUnit 包装和 Runner 展开的用例为准，不能把内部断言数量当用例数。

## 隔离与验证边界

Environment 的同步检查通过 finally 关闭自有预览场景并恢复活动场景、时间倍率和环境静态引用。场景资产检查会复用已加载场景，因此可能检查尚未保存的内存修改；仅自己打开的场景会在结束时关闭，不强制重新读取磁盘。

Gameplay 使用 Additive 自有场景并要求没有现存 Core、场景未加载且处于 Playing。Menu 和 Recovery 使用真实 Single 切换，会卸载先前场景，不能当作任意当前场景上的无扰动诊断。Menu 清理还按名称卸载场景，无法还原原场景内容。测试应在隔离的 Runner 会话串行运行。

Corner/Ground 使用独立的 2D 物理场景；同步检查显式模拟物理或反射调用帧方法，另有自动帧用例。Ground 自动用例在需要时创建并清理真实 Core，会执行该 Core 的初始化。TearDown 尝试卸载场景并恢复静态状态，但部分清理步骤超时抛异常后会跳过后续恢复，不保证任何失败下均无残留。

音频检查使用当前活动场景中的临时层级、生成的静音 clip 和配置。Manager 宿主保持 inactive，通过反射初始化池，emitter 在活动层级播放；不验证正常 Awake/Start、预热及 OnEnable 订阅。缺少 TimerRunner/AudioListener 时才创建自有对象，清理不删除借用的对象。部分检查临时修改时间倍率、监听器暂停或后台运行设置，再在协程 finally 中恢复；不等于没有全局影响，也不证明听感正确。

超时并不统一：原 AudioIntegration 外层 10 秒；Handle 等待 4 秒；Tail 播放等待 8 秒、暂停停止 2 秒，Values 同步；Limit 无独立总期限。Gameplay 的加载/卸载/动画锁等待各 15 秒，Menu 切换等待 20 秒但首次加载和清理卸载无独立期限；Recovery 加载等待 15 秒，卸载无独立期限。Floating 的运动等待 5 秒，卸载等待无独立期限。单次阻塞调用不能被这些协程期限抢占。

Settings 文件替换失败检查依赖 Windows 文件共享锁语义；当前项目使用 Windows Editor，不能据此承诺其他平台同样失败。它预期产生一条 `Settings were not saved` 错误日志，由包装用例的 LogAssert 接收。菜单设置只验证保存调用成功路径与即时 Mixer 应用，不覆盖失败回滚；单独存储检查负责读盘数据验证。

大量检查以字符串和反射访问私有成员，重构后需要同步。按钮通过 `onClick.Invoke()`、玩法通过处理器调用和直接调整刚体驱动，不能替代输入设备、UI 布局、真实道具拾取或完整角色运动的冒烟验证。断言通过也不证明异常清理、域重载或 Editor 崩溃后必然恢复。

本轮仅新增目录说明并修正 GroundPolarity 检查的 Core 注释，未改测试逻辑。独立审查通过；编译后 EditMode 17/17、PlayMode 37/37 通过，具体 job 记录见测试总说明。
