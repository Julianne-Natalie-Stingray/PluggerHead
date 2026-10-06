# Editor PlayMode 测试

2026-10-07 可变配置测试清理：删除 Player 生产参数驱动的 5 个移动/跳跃用例与电线生产材质驱动的 4 个像素用例及其专用辅助代码；移除固定落地高度/时长与交互距离、HUD 宽度、示例降压值、跨加载位置差与四格等于四世界单位的断言，以及固定默认音量断言。保留交互、路径、引用、存储恢复和显式夹具边界测试。今后禁止将预期可变配置作为固定验收标准，见 `Assets/AGENTS.md`；下方旧测试数量和覆盖说明为历史记录。

2026-10-06 Tilemap 改造验证：EditMode 22/22（job `812b828e3ce84ea889c26457cc594fda`）、PlayMode 71/71（job `c12304087033435ebdf260a2b5541579`）均终态通过；以下较早 job 为历史记录。原 Corner 用例已替换为 TilemapTests，覆盖近角非格心往返、调试验收同格操作，并新增真实场景 Tilemap 落地及两个关卡的格心资源检查。

程序集 `PluggerHead.PlayModeTests`，Category 均为 `Integration`。逐文件核查日期：2026-10-06；当前共 71 个用例，本轮 job `a8881108238d4eb5860ef817658ccfdf` 已结束并通过 71/71。各类通过 UnityPlatform 限定 Windows/Linux/macOS Editor；程序集本身无平台过滤，不能据此推断预定义 Editor 检查可用于独立 Player。执行方式见[测试总说明](../README.md)。

| 文件 | 用例数与实际覆盖 |
| --- | --- |
| `TimerTests.cs` | 12：时间点/条件/完成回调中的 Stop、Restart、异常状态与旧代隔离，零时长、Infinity＋条件完成、后续帧重入及全局 Runner 替换后的原宿主停止。四项异步异常声明预期日志，零时长异常由检查同步捕获。 |
| `AudioIntegrationTests.cs` | 1：`RunForTests()` 的 19 项内部生命周期断言。 |
| `AudioHandleTests.cs` | 2：自然结束与淡出 Stop、参数校验、句柄失效及完成事件异常隔离；预期异常日志由 LogAssert 接收。 |
| `AudioTailTests.cs` | 5：Values/Slow/Fast/Change/FrozenStop；Values 反射模拟包络游标，其余检查真实播放或暂停停止。FrozenStop 不进入 GameState.Freezed。 |
| `AudioLimitTests.cs` | 10：重入、交叉限额、动态降限、正常抢占及循环/非正限额保护。 |
| `TilemapTests.cs` | 7：六个同步 Test 与一个 UnityTest；格子路径/长度、跨格/对角回退、微动、Anchor 固定/收回、双线独立/重开、变换/offset 与实际帧更新。 |
| `PhysicsCleanupTests.cs` | 12：Tilemap/Ground 各六种情形：空卸载、异常、超时、Dispose、场景已卸载但句柄未确认、句柄完成但场景仍加载；恢复状态并保留归属供重试。 |
| `SceneCleanupFailureTests.cs` | 10：嵌套协程超时/异常/Dispose/取消及错误聚合，四组实际清理失败，挂起加载的进度隔离和重试释放。 |
| `FloatingTests.cs` | 2：根对象及复杂父级中的实际旋转、位置保持和暂停恢复；位移幅度设为零，不验证完整漂浮轨迹。 |
| `GroundPolarityTests.cs` | 4：三个同步 Test 与一个 UnityTest；实际支撑接触、危险/安全位、过滤、换线、锁定与物理帧死亡。 |
| `MainMenuTests.cs` | 1：实际按钮事件、设置成功路径、New Game、往返菜单和 Continue 默认关卡状态；不测试真实鼠标、布局或 Exit 执行。 |
| `SceneGameplayTests.cs` | 1：真实场景中的 Anchor 放置/收回、路由、Tilemap 落地与格子回退、换线闭合重开与超长死亡。方法名保留历史 Pickup 字样，实际没有普通道具背包拾取验证。 |
| `SceneSwitchRecoveryTests.cs` | 4：失活、销毁、禁用及异常订阅者；重入拒绝、Single 切换完成和后续恢复。 |
| `PluggerHead.PlayModeTests.asmdef` | 显式引用 TestSupport，关闭自动引用，标记 TestAssemblies，无平台过滤。 |

对应 `.meta` 保留 GUID，无自定义执行顺序。检查实现和具体等待期限见[Editor 目录说明](../../Scripts/Editor/README.md)。

Timer 使用真实协程宿主，每次等待最多 3 秒；借用已有 Runner，缺失时创建。finally 停止本用例 Timer、恢复原 Runner 引用和 timeScale、销毁自建 Runner。测试临时设置 timeScale=1，不覆盖所有计时参数、非缩放模式、Runner 销毁/停用或独立 Player 行为。

音频包装在 finally 中 Dispose 检查协程，使其自有对象和全局设置清理可执行；测试使用静音 clip，不证明听感。Floating、Tilemap、Ground、PhysicsCleanup、Menu、Gameplay、Recovery 及 SceneCleanupFailure 包装通过 UnityTearDown 调用清理。加载仍在途时，IntegrationSceneState 只恢复 timeScale、监听器暂停和 Environment 引用，GameState 标签及两个缓存字段留待加载结束后的清理重试恢复；在途操作和未卸载场景仍保留归属。

PhysicsCleanupTests 已有独立 UnityTearDown：正常路径在用例内部等待重试卸载完成；Run 的 finally 恢复注入设置与同步状态，剩余异步卸载交由 Cleanup 等待。Cleanup 恢复原 Environment 引用，仅在自有场景列表清空后释放 activeOwner；失败仍保留归属并阻止新夹具覆盖。句柄完成但场景仍加载使用注入代理模拟，不代表制造了 Unity 原生操作故障。

MainMenu 与 SceneGameplay 在 SetUp 替换进度存储，TearDown 请求恢复引用；加载或清理尚未完成时保留临时存储，成功重试清理后自动完成延迟恢复。菜单/切换恢复会执行真实 Single 加载，不能恢复此前场景内容；应在隔离 Runner 会话运行。Gameplay 则加载自有 Additive 场景，并要求起始无 Core、目标场景未加载且状态为 Playing。

同步物理检查使用真实接触及显式模拟/帧方法调用；实际帧用例另行验证自动更新。玩法输入通过处理器调用和刚体位置调整触发，不能替代真实按键绑定、完整角色运动或 UI 输入冒烟验证。当前清理故障组共 22 个用例；测试总说明中的 17/57 job 属于新增 Timer/MenuTool 和后续 Physics 用例之前的历史版本，当前版本通过结果见测试总说明。
