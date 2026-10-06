# Editor PlayMode 测试

程序集 `PluggerHead.PlayModeTests`，Category 均为 `Integration`。逐文件核查日期：2026-10-06；当前共 37 个用例。各类通过 UnityPlatform 限定 Windows/Linux/macOS Editor；程序集本身无平台过滤，不能据此推断预定义 Editor 检查可用于独立 Player。执行方式见[测试总说明](../README.md)。

| 文件 | 用例数与实际覆盖 |
| --- | --- |
| `AudioIntegrationTests.cs` | 1：`RunForTests()` 的 19 项内部生命周期断言。 |
| `AudioHandleTests.cs` | 2：自然结束与淡出 Stop、参数校验、句柄失效及完成事件异常隔离；预期异常日志由 LogAssert 接收。 |
| `AudioTailTests.cs` | 5：Values/Slow/Fast/Change/FrozenStop；Values 反射模拟包络游标，其余检查真实播放或暂停停止。FrozenStop 不进入 GameState.Freezed。 |
| `AudioLimitTests.cs` | 10：重入、交叉限额、动态降限、正常抢占及循环/非正限额保护。 |
| `CornerTests.cs` | 7：六个同步 Test 与一个 UnityTest；进退绕、多角、静止/微步、双线归属、清理、变换/offset 与实际帧更新。 |
| `FloatingTests.cs` | 2：根对象及复杂父级中的实际旋转、位置保持和暂停恢复；位移幅度设为零，不验证完整漂浮轨迹。 |
| `GroundPolarityTests.cs` | 4：三个同步 Test 与一个 UnityTest；实际支撑接触、危险/安全位、过滤、换线、锁定与物理帧死亡。 |
| `MainMenuTests.cs` | 1：实际按钮事件、设置成功路径、New Game、往返菜单和 Continue 默认关卡状态；不测试真实鼠标、布局或 Exit 执行。 |
| `SceneGameplayTests.cs` | 1：真实场景中的 Anchor 放置/收回、路由、左上角挂线退绕、换线闭合重开与超长死亡。方法名保留历史 Pickup 字样，实际没有普通道具背包拾取验证。 |
| `SceneSwitchRecoveryTests.cs` | 4：失活、销毁、禁用及异常订阅者；重入拒绝、Single 切换完成和后续恢复。 |
| `PluggerHead.PlayModeTests.asmdef` | 显式引用 TestSupport，关闭自动引用，标记 TestAssemblies，无平台过滤。 |

对应 `.meta` 保留 GUID，无自定义执行顺序。检查实现和具体等待期限见[Editor 目录说明](../../Scripts/Editor/README.md)。

音频包装在 finally 中 Dispose 检查协程，使其自有对象和全局设置清理可执行；测试使用静音 clip，不证明听感。其他测试多通过 UnityTearDown 调用清理，部分清理等待再次失败时，后续恢复可能未执行。

MainMenu 与 SceneGameplay 在 SetUp 替换进度存储，TearDown 的 finally 恢复引用。菜单/切换恢复会执行真实 Single 加载，不能恢复此前场景内容；应在隔离 Runner 会话运行。Gameplay 则加载自有 Additive 场景，并要求起始无 Core、目标场景未加载且状态为 Playing。

同步物理检查使用真实接触及显式模拟/帧方法调用；实际帧用例另行验证自动更新。玩法输入通过处理器调用和刚体位置调整触发，不能替代真实按键绑定、完整角色运动或 UI 输入冒烟验证。本轮新增说明，未改变测试逻辑。
