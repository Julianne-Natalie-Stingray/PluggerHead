# 整合集成测试

2026-10-07 SUCCESS_RULE 新规则：双极交互必定切换异极主线，独立携带/连接地线，降压器按连接去重；通关只检查全部插座端口接线及电压达标，移除旧闭环限制。最终 EditMode **55/55**（`724ffc88f177437aaea6fb829287faef`）、随后 PlayMode **75/75**（`200544930a19421b9ba2051ef1ccc32e`）均终态通过，无失败或跳过。新增边界与回归覆盖地线漏接、降压去重、相等/超标电压、生成续线、跨格双持路径、插座交接固定、重开及示例预制体组件/精灵引用。独立审查的两项路径问题已修复并复审通过。中途 PlayMode 曾因脚本重载留下孤儿任务，退出播放后清理 MCP 任务与服务标识再完整重跑；中断任务不计作通过。示例与使用步骤见 [Env](../Scripts/Env/README.md)。

2026-10-06 回路闭合规则更新：新增 15 项 EditMode 回归，覆盖火/零两种起始顺序、全部接口交互后继续铺线、漏接/仅经过接口、独立回插、异源插座、无返回端、连接被销毁、重复交互、刷新与重开，以及单极/单极加地线/None 初始化拒绝。多线用例验证三根线同极回插不能通关、四根线异极回插可通关。接入点固定路径，原 Tilemap 回退与渲染用例同步验证返回段保留。完整顺序通过 EditMode **49/49**（`c156b9d71b1d45f187a37e5567ff5204`）、PlayMode **75/75**（`61610d56a0e64b05bbf367f8a1be5a6c`），无失败或跳过。

2026-10-06 本地测试修复：贴墙回归中的地面跳跃检查按 Player 预制体实际 `jumpSpeed`、重力倍率和模拟步长验证位移与速度，移除对旧速度 8 的依赖；CircuitDiagnostics 的电线列表覆盖项改为引用当前 PowerSocket 组件 fileID，恢复两根场景电线绑定。顺序通过 EditMode **34/34**（`78409b778bdf400d9e5779060e5b3da0`）、PlayMode **75/75**（`580446209be04a79a6001fd6398a6fbc`），无失败或跳过。

2026-10-06 线渲染修复：按实际出线顺序稳定排序，复制前缀由原线显示，新铺段使用当前线颜色。新增四项离屏像素回归覆盖 Live/Neutral 两种起始极性、双接口/原插座交接、反向对象创建顺序、相反预设排序值、路径包围盒变化、刷新节点、回退重铺及重开；同时确认完整长度和碰撞路径保留。完整回归暴露的故障测试暂停状态污染已修复：`IntegrationSceneState` 同步保存/恢复手动暂停、冻结持有者和加载后时间恢复标记，并保留加载在途时延迟恢复的约定；故障用例主动污染并验证这些字段。最终顺序通过 EditMode **34/34**（`95636ec60b9b48db8c0ea0d9ae53b089`）、PlayMode **75/75**（`8200782231ae4666969cdaabff7b75f5`），无失败或跳过，独立复审通过。渲染示例见 [WireRendering](../Docs/Development/WireRendering.png)：从左到右为原线、刚换线、新铺段重叠，使用实际 Wire 组件及项目材质在临时场景中绘制，不代表真实键盘操作录像。

2026-10-06 Player 朝向：Visual 的 PlayerVisual 更新精灵翻转与深色标记，保留物理根及挂点。真实 InputSystem 键盘状态覆盖 A/D、松键、输入锁、暂停、禁用/启用；测试显式处理排队输入后等待物理帧，避免输入更新时序导致假失败。最终顺序通过 EditMode **22/22**（`ba62ffa6f0a84e42bfa50d48f93e6549`）、PlayMode **71/71**（`842381cf2e3848b5a26927f895ec54d1`）。运行时近景验证见 [朝左](../Docs/Development/PlayerFacingLeft.png)、[朝右](../Docs/Development/PlayerFacingRight.png)；截图直接设置朝向，键盘到朝向的链路由前述集成测试验证。

2026-10-06 通关面板：NextLevelScreen 经同场景 Env 胜利事件显示，按钮按当前配置重新进入 GameplayIntegration。顺序通过 EditMode **22/22**（`d7fd9ed64ca645d7a229a416d766506e`）、PlayMode **71/71**（`660506935c984787bd51d2968d37e11c`）。检查默认隐藏、胜利显示、禁用退订/恢复、调试重开隐藏、请求失败重试、成功后防重复及真实关卡重载。实际画面中文无溢出，EventSystem 射线首个命中 NextLevelButton；画面见 [NextLevelScreen](../Docs/Development/NextLevelScreen.png)。

2026-10-06 中文 UI：AGENTS 已规定玩家界面默认简体中文；MainMenuScene、FinalScene、GlobalUI 和运行时状态/错误提示已同步，ScoreText 保持空白。新增静态字体覆盖检查，顺序通过 EditMode **22/22**（`ebaeb33f53774b65b155fcf82a0c78e6`）、PlayMode **71/71**（`612aac8b531c4d368ea318ee8c6afd2a`），无失败或跳过。实际检查主菜单、设置、线长、死亡及结尾画面；运行时注入五种状态/错误文案验证字形与布局，均无溢出，未以此声称触发了全部业务失败分支。检查时 Console 无错误或警告。画面：[主菜单](../Docs/Development/ChineseUI-MainMenu.png)、[设置](../Docs/Development/ChineseUI-Settings.png)。

2026-10-06 背包拆分：原功能保存在 `feature/player-inventory`（`72f22e6`），master 已解除移动/交互对背包的依赖，并使用不含 InventoryUI 的 GlobalUI prefab。最终版本顺序通过 EditMode **22/22**（`5b1dddad95e34aff8eeddfb952b7b8d6`）、PlayMode **71/71**（`90fc40b70f3e44b28a7c5a7ec7999f88`），无失败或跳过。

2026-10-06 功能拆分回退：恢复原路逐格回退，保留废弃接口清理；完整新功能保存在 `feature/wire-path-overlap`。在保留工作区已有 Player/动画修改的状态下，EditMode 22/22（`425318e665924c2197934a880f634685`）、PlayMode 71/71（`3a74fa202046412c8173671e7afbac24`）顺序终态通过。

2026-10-06 Tilemap 改造验证：EditMode 22/22（job `812b828e3ce84ea889c26457cc594fda`）、PlayMode 71/71（job `c12304087033435ebdf260a2b5541579`）均终态通过；以下较早 job 为历史记录。原 Corner 用例已替换为 TilemapTests，覆盖近角非格心往返、调试验收同格操作，并新增真实场景 Tilemap 落地及两个关卡的格心资源检查。

Unity 2022.3.43f1c1 / Unity Test Framework 1.1.33。

FinalScene 验证（2026-10-06）：EditMode job `8895237b90a4409ab8a6455304242a01` 终态通过 22/22，随后 PlayMode job `7d775ea32d8a433397fbba0ff49acee0` 终态通过 71/71，无失败或跳过。新增场景检查覆盖祝贺标题、示例制作组、Exit 持久事件、输入组件、字体尺寸及非玩法注册；另以 MCP 验证运行画面无文本溢出、射线命中 Exit，派发左键 pointerClick 后 Editor 停止播放。Player 的 Application.Quit 分支未构建实测。独立审查未发现功能缺陷，文档遗漏已补齐，测试临时 Editor 设置已恢复。

FinalScene 加入前验证（2026-10-06）：代码及文档独立审查通过，编译后 Console 错误为 0；EditMode job `21b6cbceec95472ca70ce3a965876b63` 已结束并通过 21/21。首次 PlayMode job `1ba9928905c54f868d855c4d7d283532` 因 Editor 会话中断没有有效终态；连接恢复后重跑，job `a8881108238d4eb5860ef817658ccfdf` 已结束并通过 71/71，失败及跳过均为 0。测试中的预期异常由 LogAssert 接收，不能把测试后 Console 异常记录等同于编译错误。临时场景及 Test Runner 修改的 Editor 设置已清理恢复。

清理缺陷修复历史验证（2026-10-06，新增 Timer/MenuTool 及后续 Physics 用例之前）：编译后 Console 无错误，独立审查通过；按顺序运行 EditMode 17/17（job `3eb8b79ef39f415abbed40cead84b714`）和 PlayMode 57/57（job `e648646396f14d7ab119a800823a87c8`），均终态通过。新增 20 项故障注入检查覆盖失败恢复、在途句柄保留、异常聚合和临时进度存储延期释放。

2026-10-06 逐文件核查：当时 EditMode 21 个、PlayMode 71 个用例；本轮 MenuTool 4 项、Timer 12 项及新增 Physics 2 项均包含在上述当前版本通过结果中。文件级职责及边界分别见 [EditMode](EditMode/README.md)、[PlayMode](PlayMode/README.md)、[Shared 反射桥](Shared/README.md)；实际断言、隔离和超时实现见 [Scripts/Editor](../Scripts/Editor/README.md)。

以下为清理修复前的审计验证记录：EditMode 17/17（job `3fa8c86afa1348cb8f60ce2d1e6faa98`），PlayMode 37/37（job `ed6e536d01d7477d941943af149956d1`）。该快照包含 `7c91a3a` 的碰撞体 offset 修复；本轮只改说明和一处检查脚本注释。独立审查已通过，后续行为改动需重新验证。

## 功能场景

主菜单位于 `Assets/Scenes/MainMenuScene.unity`；功能验证场景位于 `Assets/Scenes/Tests/`。第三方包内的示例场景不纳入构建列表。

| 场景 | 功能与使用入口 |
| --- | --- |
| `MainMenuScene` | 构建启动场景。New Game、Continue Game、Settings、Exit；无有效进度时 Continue 禁用。 |
| `FinalScene` | 结尾祝贺、示例制作组和 Exit；可独立播放，通过 SceneId.FinalScene 请求切换，不记录为玩法进度。 |
| `GameplayIntegration` | New Game 的首关。真实 Player、Tilemap 格子路径、双线回路、设置及死亡重开 UI；Play 后移动、J 交互/收回、K 放置 Anchor。设置面板的 Main Menu 返回主菜单。 |
| `CircuitDiagnostics` | MockPlayer 与独立电路布局；Play 后用 EnvironmentFacade 调试按钮验证回路，拖动 MockPlayer 检查线端。含完整 Core，可用 AudioManager 的 Test Audio Request 检查音频。 |
| `SceneSwitchTarget` | 仅保留相机与 AudioListener；从前两者调用 `CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.SceneSwitchTarget)`，确认场景切换完成、Loading 退出且原 Core 存活。单独播放只显示背景。 |

2026-10-06 整理：`HeXieTestScene` → `GameplayIntegration`，`JillTestWireScene` → `CircuitDiagnostics`，`JillTestSceneSwitch` → `SceneSwitchTarget`，三者保留原 GUID。原 `JillTestScene` 仅含相机及同一个 Core prefab，其服务检查职责合并到 `CircuitDiagnostics`，删除重复场景及枚举项。现有 SceneId 的序列化值 1/2/3 保持不变，退役值 0 不复用；当时三功能场景依次为 GameplayIntegration、CircuitDiagnostics、SceneSwitchTarget；当前构建列表首位为 MainMenuScene，末尾新增 FinalScene，共五场景。

真实 Player 集成与 MockPlayer 电路诊断保持分开，便于区分输入/动画问题和回路问题。Core 继续随内容场景配置并通过 DontDestroyOnLoad 保活，无需额外 Bootstrap 或叠加加载。

## Unity Test Runner

打开 **Window > General > Test Runner**，分别运行以下程序集；所有用例的 Category 都是 `Integration`。

| 模式 | 程序集 | 用例 | 覆盖 |
| --- | --- | --- | --- |
| EditMode | `PluggerHead.EditModeTests` | 4 个 MenuTool 安全用例 | 临时文件写删归属、路径约束、命名校验及现有项目常量兼容；不执行 AssetDatabase 导入/删除或脚本重载 |
| PlayMode | `PluggerHead.PlayModeTests` | 12 个 Timer 用例 | 时间点/条件/完成回调重入、零时长、异常状态、旧代隔离、Infinity＋条件完成及 Runner 替换后停止原宿主协程；每次等待上限 3 秒 |
| EditMode | `PluggerHead.EditModeTests` | 1 个 Player/Env 集成用例 | Anchor 不作为道具、无限放置/收回、无线放置、最近目标、绕线、阻力、输入锁、暂停、跨场景隔离、换线、闭环、重开、线长边界及单次死亡通知 |
| EditMode | `PluggerHead.EditModeTests` | 5 个场景参数用例＋1 个注册表用例 | 场景枚举/白名单/构建列表一致性、主菜单构建入口、场景加载、丢失脚本/预制体、电线材质、实际 Player 与电路配置 |
| EditMode | `PluggerHead.EditModeTests` | 1 个关卡存档用例 | 只序列化关卡、重新读盘、缺失/损坏/未知关卡、写入失败保留旧存档 |
| EditMode | `PluggerHead.EditModeTests` | 2 个 GameState 参数用例 | Playing/Freezed 起点下加载中交错暂停/恢复，保留标签与冻结前时间倍率 |
| EditMode | `PluggerHead.EditModeTests` | 5 个音频池配置用例 | 非正池容量在运行时和 OnValidate 后均安全，正常容量不变，真实 ObjectPool 可构造 |
| EditMode | `PluggerHead.EditModeTests` | 1 个音量属性用例 | 三路 NaN 保留原值、有限越界及无穷值钳制、总线互不影响、Default 对象独立性 |
| EditMode | `PluggerHead.EditModeTests` | 1 个设置存储用例 | 临时路径读写、越界/非有限音量恢复默认值、替换失败保留旧文件 |
| EditMode | `PluggerHead.EditModeTests` | 1 个 Debug 按钮用例 | EditMode 调用不访问未初始化设置 |
| PlayMode | `PluggerHead.PlayModeTests` | 2 个 Floating 用例 | 实际 Update 下根对象/复杂父级旋转位置保持、暂停与恢复 |
| PlayMode | `PluggerHead.PlayModeTests` | 4 个切换恢复用例 | 宿主失活/销毁、组件禁用、状态回调异常、重入拒绝与后续请求恢复 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个主菜单流程用例 | 实际按钮引用、无存档禁用 Continue、Settings 暂停恢复、保存后实际混音器即时更新、New Game、返回菜单、重新读盘 Continue、默认位置/默认电路、非玩法场景不覆盖存档 |
| PlayMode | `PluggerHead.PlayModeTests` | 5 个音频尾部＋2 个公开接口用例 | 变速尾部包络、淡入重叠、时间和监听器暂停时停止（未进入 Freezed 标签）、自然完成与归池复用、NaN 拒绝及 Finished 异常隔离 |
| PlayMode | `PluggerHead.PlayModeTests` | 10 个音频限流用例 | 完成回调重入、跨上限补位、运行时降低上限、普通最旧声部抢占、循环保护及非正上限拒绝 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个音频生命周期用例 | 原有 19 项断言：默认参数、Builder 覆盖、停止、自然结束、池复用、旧 Timer 隔离、循环及淡出 |
| PlayMode | `PluggerHead.PlayModeTests` | 1 个实际玩法场景用例 | `GameplayIntegration` 启动、帧推进、K 放置/J 收回 Anchor、绕线渲染、真实 Tilemap 落地、格子移动与逐格回退、换线、通关一次、重开、超限死亡及单次死亡通知 |
| PlayMode | `PluggerHead.PlayModeTests` | 4 个地面极性用例 | 真实 2D 支撑接触、双向异极死亡、同极/无线安全、侧墙与天花板排除、Trigger/层过滤、禁用组件、站立换线与输入锁、自动物理帧死亡 |
| PlayMode | `PluggerHead.PlayModeTests` | 7 个 Tilemap 用例 | L 形长度边界、快速跨格、对角可逆、同格微动、空格拒绝放置、Anchor 固定/释放、每线独立与重开、真实碰撞体坐标、远程交互排除、自动 LateUpdate |
| PlayMode | `PluggerHead.PlayModeTests` | 22 个清理故障用例 | 空操作、异常、超时、Dispose、场景已卸载但句柄未确认、句柄完成但场景仍加载两种归属保护、嵌套错误聚合、状态恢复、进度隔离和重试清理 |

表中的断言数是用例内部的检查点数量，不是 NUnit 用例数量；实际用例数与结果以 Test Runner 报告为准。PlayMode 用例由 Runner 自动进入/退出播放；音频用例会创建缺失的 TimerRunner 和 AudioListener，玩法用例会加载并卸载自有场景和 Core，无需预先打开或手工配置运行场景。

这些是 **Editor 内运行的 EditMode/PlayMode 测试**。验证实现复用 `Scripts/Editor/` 中的现有脚本；PlayMode 用例声明仅支持 Editor 平台，不用于独立 Player 测试包。测试程序集通过 `IntegrationCheckBridge` 调用预定义程序集，避免为了测试改动生产脚本的程序集布局。普通 Player 构建不包含 TestAssemblies。

通过结果仅证明实际断言覆盖的路径：按钮使用事件调用、玩法使用处理器和刚体调整，不验证真实键盘/鼠标输入、UI 排版或全部角色移动。音频使用静音 clip，不能证明听感；多数音频夹具手工初始化 inactive Manager，不覆盖正常 Awake/Start 与预热。资产检查复用已加载场景时，结果对应内存版本。Settings 的失败替换测试依赖 Windows 文件锁语义。

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
- 音频用例在当前活动场景创建临时层级，协程 finally 清理自有对象并恢复其改动的全局设置；不属于独立场景隔离。10 秒外层期限仅属于原生命周期检查，Handle/Tail/Limit 的期限不同，详见 [Editor 检查说明](../Scripts/Editor/README.md)。
- Gameplay 用例操作自有 Additive 场景；加载、卸载与动画锁等待各有 15 秒期限。Menu/Recovery 使用真实 Single 加载，会卸载原场景，不能还原原内容。场景等待均有期限；嵌套协程失败也执行同步状态恢复，未完成的操作和场景保留归属供清理重试。加载仍在途时，IntegrationSceneState 仅恢复 timeScale、AudioListener.pause 和 Environment 引用，暂不恢复 GameState 标签及冻结前倍率、加载前状态两个缓存字段；加载结束后重试清理才恢复完整快照。菜单/玩法加载未清完前保留临时进度存储并拒绝开始新夹具，成功重试后兑现延迟释放。
- 玩法用例在自有场景加载回调中临时取消线长限制，完成交互流程后设置有限线长，验证真实物理帧的超限死亡。这样兼容开局即超限的诊断配置，不修改或保存原场景资源。
- 地面极性用例创建独立的 2D 物理场景，并先验证实际接触与法向，再检查死亡结果；`UnityTearDown` 卸载自有场景、恢复环境静态引用并清理自有 Core，不保存或修改关卡资源。
- Tilemap 用例使用独立 2D 物理场景及真实 Tilemap/Wire/Anchor，校验 LineRenderer 与 EdgeCollider2D 的顶点一致；包含旋转、非均匀缩放、非零 offset，以及只改变 offset 后重绘相同路径的实际碰撞查询。帧推进用例验证实际 LateUpdate 与延迟销毁。测试后卸载自有场景并恢复 Environment 静态引用，不修改场景资源。
- Physics 清理故障用例保留本轮所有者；正常路径验证真实重试，独立 `UnityTearDown` 在自身断言失败后也有界排空剩余真实卸载。终态句柄与场景卸载必须同时确认，失败仍保留归属，不重放故障注入。
- 原有 `EnvironmentIntegrationChecks.Run()` 和 `AudioIntegrationChecks.Run()` 手动入口仍可使用。Env 自动测试仍调用 `Run()`；原音频自动测试调用 `RunForTests()`，不依赖手工轮询音频 `LastResult`。
