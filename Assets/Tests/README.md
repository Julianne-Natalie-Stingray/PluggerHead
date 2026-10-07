# 整合集成测试

2026-10-07 主菜单音乐接入：仅运行 PlayMode `MenuMusicTests.RegisteredMenuMusic_UsesAudioApiAndResumesWithoutRestart` 和 `MainMenuTests.MainMenu_LoadsHubMenuAndPortalsPreserveSessionProgress`，**2/2** 终态通过（`7620b77f7ab9474f9bedae03c60f4400`）。覆盖新增 WAV 的独立 AudioId/配置与 OST 引用、真实播放及同 ID 复用、暂停位置保持/恢复、停止清理，以及主菜单自动播放、设置暂停、拒绝开始保持、大厅换曲和返回主菜单重新播放。Console 无 error，EditorSettings 恢复 0/3，Level1 用户修改保留且排除提交。未做人工听感或独立 Player 构建验证。

2026-10-07 Wire 类型材质：`WireVisualTests` 九项覆盖三种类型选材质、续线继承视觉配置而不继承源材质、不覆盖源及目标 Gradient、缺配置/空槽、继承段与排序、全部关卡和 Env 预制体的有效材质引用。加上 `CircuitClosureTests.GroundWire_CanRouteAndConnectAfterPoweredWireIsReleased` 和 `Outlet_HandoverPinsInheritedRoute`，EditMode **11/11** 终态通过（`4b32b83623fb458687dbbd62d7ca28c2`）；随后仅运行 `TilemapTests.WireSwap_PreservesIndependentPaths_RestartResetsPins`、`SceneGameplayTests.DiagnosticScene_RealPlayerPlacesAnchorAndSwapsWire`，PlayMode **2/2** 通过（`f76682c1699f498eab2d103fb7590e5f`），无失败或跳过。Console 无 error，EditorSettings 恢复本轮基线 0/3，Level1 恢复为用户授权保存后的编辑状态。

独立 review 检查 [三类型材质折线预览](../Docs/Development/WireTypeMaterials.png)：自有预览场景使用生产 Wire prefab 的宽度/UV 与三类型真实材质，由上到下 Live / Neutral / Ground；现阶段共用现有贴图，三线外观相同，待三张独立贴图交付后替换对应材质。该图只作人工渲染冒烟，不作像素验收，不代表所有关卡、显示比例或未来贴图已验证；临时相机、RenderTexture、Texture2D 与预览场景均已清理。

2026-10-07 独立大厅：MainMenuScene 仅保留主菜单，开始加载 HubScene；大厅 MenuBtn 打开暂停设置，支持继续/返回主菜单，Level0 通关返回大厅。独立 review 选择 `SceneAssetTests` 和 `MainMenuTests`，最终顺序终态通过 EditMode **8/8**（`ee05ab3d506b46b889bef4c0def6c604`）与 PlayMode **2/2**（`d762aba6b49540808e9b4e4656b19dc6`），失败/跳过均为 0。覆盖七场景引用与注册、纯主菜单隔离、真实大厅加载、拒绝重试、暂停继续、通关返回/主菜单再开始保留进度、关卡重开及 BGM 连续播放。

运行冒烟发现复制来的返回主菜单按钮默认隐藏，已在 HubScene 激活并补充可见/可交互断言，上述结果为修复后复测。独立播放大厅，MenuBtn 与返回主菜单按钮中心 UI Raycast 均首命中本人，派发 pointerClick 后分别暂停以及真实返回 MainMenuScene（Playing、timeScale=1）；中文 TMP 字形覆盖且无溢出。Console 无 error，存在原大厅无 PowerSocket 和跨场景 Core/Timer 去重 warning。EditorSettings 已恢复开始时 0/3，恢复 Level1 编辑状态且无未保存修改，保留其他外部资产修改。画面：[大厅](../Docs/Development/HubScene.png)、[暂停设置](../Docs/Development/HubScene-Settings.png)、[主菜单](../Docs/Development/MainMenuScene.png)、[加载失败提示](../Docs/Development/MainMenuScene-StartFailure.png)。未构建独立 Player，未覆盖所有屏幕比例或真实物理鼠标硬件事件；pointerClick 为 EventSystem 派发。


2026-10-07 BGM 跨场景续播：编译无错误，独立复审通过；顺序运行 EditMode **74/74**（`fd7ea61bbcd44e2fa8988c35305412da`）、PlayMode **86/86**（`558817d3fbe54acd9f70a65eea396621`），终态通过且无失败或跳过。菜单集成用例实际执行 Level0 → SceneSwitchTarget → GameplayIntegration，使用测试自建音频验证 Loading 不停播、卸载后继续推进、同曲复用同一句柄/音源且不叠加，以及暂停位置保持和拒绝切换不影响 BGM；复用现有 Gameplay 用例验证死亡、通关和普通禁用仍停止。音频隔离夹具新增同曲复用、换曲、停止后重播、失败恢复及 Finished 重入保护。未做人工听感验收。测试后恢复 EditorSettings 的 Enter Play Mode 开关，MainMenuScene 已恢复且未标脏。

2026-10-07 大厅选关与音频销毁修复：EditMode **74/74**（`0f6575ae18474c68a4d9e75a259dcc94`）后 PlayMode **84/84**（`c31852e92fa34410bce9a58f5e282a94`）顺序终态通过，无失败或跳过。覆盖菜单角色锁与设置门控、开始后留在大厅、正常 Player 交互进入 Level0、未开放门拒绝与相邻门误触、顺序解锁/重玩/运行重置、通关返回，以及音频发射器销毁后的 Stop/环境清理。新增大厅地板碰撞存在检查。

用户要求保留 Level0 原布局（1 个电源插座与 2 个双极插座，按现行规则不能全部接通）；菜单集成测试只在自有运行时场景构造最小有效电路来验证真实通关事件，不将测试通过表述为原关卡已可通关。第二、三关尚未实现。

独立审查无阻断问题。实际画面检查：点击开始按钮的 UI 射线命中正确，菜单隐藏、输入解锁，角色移动与地板碰撞正常；传送门中文静态字形完整且无溢出。画面见[初始菜单](../Docs/Development/MainMenu-Hub-Start.png)与[大厅](../Docs/Development/MainMenu-Hub-Layout-1.png)。EditorSettings 恢复至本次任务前的 Enter Play Mode 设置。

## 目录组织

测试脚本集中在本目录；三个功能测试场景位于 `Tests/Scenes/`。未注册的开发关卡 `Scenes/TestLevel.unity` 与其他关卡场景保存在 `Scenes/`。

| 目录 | 用途与编译归属 |
| --- | --- |
| `EditMode/` | `PluggerHead.EditModeTests`，编辑模式测试入口。 |
| `PlayMode/` | `PluggerHead.PlayModeTests`，Editor 播放模式测试入口。 |
| `Shared/` | `PluggerHead.TestSupport`，测试程序集使用的反射桥。 |
| `Editor/` | 检查实现、夹具和清理辅助；保留 Unity Editor 特殊目录及预定义 `Assembly-CSharp-Editor` 归属，以便直接访问生产类型。此目录不增加 asmdef。 |
| `Manual/` | 手工设置诊断 `DebugScript`；保留 `Assembly-CSharp` 归属，可在 Play Mode 挂载使用，不是 Test Runner 用例。 |
| `ThirdParty/NaughtyAttributes/` | 第三方属性测试示例，保留原 `NaughtyAttributes.Test` 程序集与 GUID；不是项目自动化回归用例。 |
| `Scenes/` | GameplayIntegration、CircuitDiagnostics、SceneSwitchTarget；保留场景名、GUID 和构建顺序。 |

新增专用测试脚本应放入相应目录。`Scripts/Debug/FloatingLogic.cs` 是被测试的视觉行为，保留在业务脚本目录；第三方素材、演示场景及许可仍保留在原包中。目录名称本身不会排除 Player 构建内容：Editor 实现与 TestAssemblies 有原有排除规则，Manual 和第三方演示程序集保持原有编译行为。

2026-10-07 目录迁移验证：迁移 63 个脚本与 3 个场景，保留已有 GUID；场景路径、注册扫描范围、Build Settings 和文档链接已同步。编译后 Console 无错误；EditMode **73/73**（`8adf201ff9a54b779f36d3a763b2fa2a`）、PlayMode **80/80**（`e52086aa2f5c42d1bd8479da3726b370`）依次终态通过，无失败或跳过。场景资源检查与真实菜单/玩法/切换回归通过；未进行新的手工输入或美术验收。测试后恢复 Level0，场景未标脏，EditorSettings 已恢复任务开始状态。独立静态审查提出的文档路径遗漏已修正。

## 历史验证记录

2026-10-07 所需降压量合并：EnvironmentFacade 使用 `NeededVoltage` / `neededVoltage` 替代初始及目标电压，`CurrentVoltage` 表示剩余所需降压量，累计降压达到要求时满足电压条件。SceneRoot 从 220/190 迁移为 30；接线/UI 夹具显式配置自有降压量。补充零、负数、NaN、正负无穷配置验证，保留相等、不足、超额、重复降压及重开检查。独立审查通过；编译无错误，EditMode **69/69**（`e98e7a77a87042b1a519215b0fae467b`）、PlayMode **79/79**（`32cb10501f0640e3804c776a4ba91bcf`）顺序终态通过，无失败或跳过。测试后 Level0 恢复且未标脏，Console 仅有测试预期的音频、场景切换及 Timer 故障注入异常；EditorSettings 的运行模式差异保留，不纳入本次提交。

2026-10-07 合并远端 `ffd705b`（合并提交 `d9f4163`）：新增 TestLevel，生产目标电压保留远端的 190；接线及通关 UI 测试仅在自有夹具/场景副本中显式设定电压，不依赖生产调参。独立复审通过，合并后 EditMode **64/64**（`32a88b9347194b659b351297e9151b2d`）、PlayMode **79/79**（`d20d81fc3066450890897c01d9a36fb7`）顺序终态通过。TestLevel 的初始插座加两个双极插座布局在单次占用规则下仍不可完成，由用户调整；本次未改布局。

2026-10-07 地面极性配置限制：GroundPolarity 仅允许 Live/Neutral；Inspector 使用受限下拉，Awake/OnValidate/Polarity/CanSupport 对 None、Ground、组合及未知值抛出 InvalidOperationException。新增 11 项 EditMode 配置/选项验证，原地线持有测试改为验证地线不能代替匹配主线。独立复审通过，EditMode **64/64**（`6e9495fd130349daabe92d371c42d1fc`）、PlayMode **79/79**（`105f0c02ef9141b88037e8e75b8c9e41`）顺序终态通过，无失败或跳过。以下较早允许 Ground 地面等规则为历史记录。

2026-10-07 极性地面：仅持有与脚下地面相同单极性（Live/Neutral/Ground）的电线才能存活，主线或独立地线任一匹配即可；空手、已放下的匹配线、异极线、None/非法组合均不能保护玩家。保留普通地面、禁用组件、侧墙/天花板、Trigger 和层过滤。地面组现有 10 个展开用例，覆盖站立时换线/放线、双持/独持地线、同场景环境隔离、自动物理帧死亡及单次通知。独立审查通过。复用菜单重开会话在同一最终代码工作区顺序完成的 EditMode **53/53**（`532d644b23a14c6685789311f778cb79`）、PlayMode **79/79**（`d1712a8dbf214f65b5c839989b5c5ac1`），无失败或跳过；已核对 PlayMode 终态及全部地面用例明细，测试后未修改行为代码。此前本会话 EditMode **53/53**（`f2ed382693c74f79bfff1e6ebd0bddc3`）亦通过。场景/预制体未因本次规则修改而调整，原有 EditorSettings 修改保留。

2026-10-07 Socket 单次占用：出线也占用极性锚点，拒绝占用端口和电线自身起点回环；接入后若异极已占用则放下主线。新增 PowerSocket/PolaritySocket 双极与地线共四类占用、重复/自身回插、刷新保持、重开释放检查，以及无主线地线拾取、模板继承、路由、长度及完成检查。旧“重复换线/始终持线”断言同步更新。编译无错误、独立复审通过；EditMode **53/53**（`f9854e72a1e14d9ca7580cf2cce6bca2`）、PlayMode **72/72**（`8b693e952c12448f90fc1377ead7f996`）顺序终态通过，无失败或跳过。Level0 现有两个双极插座在新规则下无法完成回插，用户明确自行调整布局，本次未改关卡资源。

2026-10-07 可变配置测试清理：删除 Player 生产参数驱动的 5 个移动/跳跃用例与电线生产材质驱动的 4 个像素用例及其专用辅助代码；移除固定落地高度/时长与交互距离、HUD 宽度、示例降压值、跨加载位置差与四格等于四世界单位的断言，以及固定默认音量断言。保留交互、路径、引用、存储恢复和显式夹具边界测试。今后禁止将预期可变配置作为固定验收标准，见 `Assets/AGENTS.md`；下方旧测试数量和覆盖说明为历史记录。 最终编译无错误，独立复审通过；EditMode **50/50**（`92e7f4dfa388411eb71fd84692c9363e`）与 PlayMode **72/72**（`1028a1fbb473441f8597cbe0a472a526`）按顺序终态通过，无失败或跳过。

2026-10-07 预制体目录整理与真实 Player 诊断场景：五个场景预制体及 Anchor 依赖归入 `Prefabs/Env/ScenePrefab`，保留 GUID 并同步硬编码路径与文档链接；`CircuitDiagnostics` 用真实 Player 替换 MockPlayer，并增加实体 Tilemap 地面。新增实际场景 PlayMode 用例检查初始化、落地、K 放置/J 收回与 J 换线。最终 EditMode **55/55**（`55936b3a00b0456d8a13aaae64d682d3`）、PlayMode **76/76**（`f9b25c507def4be58af52d031df2a97d`）顺序终态通过；独立审查通过。画面见 [诊断场景 Player](../Docs/Development/CircuitDiagnosticsPlayer.png)。

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

2026-10-06 逐文件核查：当时 EditMode 21 个、PlayMode 71 个用例；本轮 MenuTool 4 项、Timer 12 项及新增 Physics 2 项均包含在上述当前版本通过结果中。文件级职责及边界分别见 [EditMode](EditMode/README.md)、[PlayMode](PlayMode/README.md)、[Shared 反射桥](Shared/README.md)；实际断言、隔离和超时实现见 [Tests/Editor](Editor/README.md)。

以下为清理修复前的审计验证记录：EditMode 17/17（job `3fa8c86afa1348cb8f60ce2d1e6faa98`），PlayMode 37/37（job `ed6e536d01d7477d941943af149956d1`）。该快照包含 `7c91a3a` 的碰撞体 offset 修复；本轮只改说明和一处检查脚本注释。独立审查已通过，后续行为改动需重新验证。

## 功能场景

主菜单位于 `Assets/Scenes/MainMenuScene.unity`；功能验证场景位于 `Assets/Tests/Scenes/`。第三方包内的示例场景不纳入构建列表。

| 场景 | 功能与使用入口 |
| --- | --- |
| `MainMenuScene` | 构建启动场景。New Game、Continue Game、Settings、Exit；无有效进度时 Continue 禁用。 |
| `FinalScene` | 结尾祝贺、示例制作组和 Exit；可独立播放，通过 SceneId.FinalScene 请求切换，不记录为玩法进度。 |
| `GameplayIntegration` | New Game 的首关。真实 Player、Tilemap 格子路径、双线回路、设置及死亡重开 UI；Play 后移动、J 交互/收回、K 放置 Anchor。设置面板的 Main Menu 返回主菜单。 |
| `CircuitDiagnostics` | 真实 Player、实体地面与独立电路布局；Play 后用 EnvironmentFacade 调试按钮验证回路，拖动 MockPlayer 检查线端。含完整 Core，可用 AudioManager 的 Test Audio Request 检查音频。 |
| `SceneSwitchTarget` | 仅保留相机与 AudioListener；从前两者调用 `CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.SceneSwitchTarget)`，确认场景切换完成、Loading 退出且原 Core 存活。单独播放只显示背景。 |

2026-10-06 整理：`HeXieTestScene` → `GameplayIntegration`，`JillTestWireScene` → `CircuitDiagnostics`，`JillTestSceneSwitch` → `SceneSwitchTarget`，三者保留原 GUID。原 `JillTestScene` 仅含相机及同一个 Core prefab，其服务检查职责合并到 `CircuitDiagnostics`，删除重复场景及枚举项。现有 SceneId 的序列化值 1/2/3 保持不变，退役值 0 不复用；当时三功能场景依次为 GameplayIntegration、CircuitDiagnostics、SceneSwitchTarget；当前构建列表首位为 MainMenuScene，末尾新增 FinalScene，共五场景。

完整 UI 玩法集成与真实 Player 电路诊断保持分开，便于区分输入/动画问题和回路问题。Core 继续随内容场景配置并通过 DontDestroyOnLoad 保活，无需额外 Bootstrap 或叠加加载。

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
| PlayMode | `PluggerHead.PlayModeTests` | 10 个地面极性用例 | 真实 2D 支撑接触、同极持线存活、空手/异极/已放下线死亡、地线不替代主线及环境隔离、侧墙与天花板排除、Trigger/层过滤、禁用组件、站立换线与输入锁、自动物理帧死亡 |
| PlayMode | `PluggerHead.PlayModeTests` | 7 个 Tilemap 用例 | L 形长度边界、快速跨格、对角可逆、同格微动、空格拒绝放置、Anchor 固定/释放、每线独立与重开、真实碰撞体坐标、远程交互排除、自动 LateUpdate |
| PlayMode | `PluggerHead.PlayModeTests` | 22 个清理故障用例 | 空操作、异常、超时、Dispose、场景已卸载但句柄未确认、句柄完成但场景仍加载两种归属保护、嵌套错误聚合、状态恢复、进度隔离和重试清理 |

表中的断言数是用例内部的检查点数量，不是 NUnit 用例数量；实际用例数与结果以 Test Runner 报告为准。PlayMode 用例由 Runner 自动进入/退出播放；音频用例会创建缺失的 TimerRunner 和 AudioListener，玩法用例会加载并卸载自有场景和 Core，无需预先打开或手工配置运行场景。

这些是 **Editor 内运行的 EditMode/PlayMode 测试**。验证实现复用 `Tests/Editor/` 中的现有脚本；PlayMode 用例声明仅支持 Editor 平台，不用于独立 Player 测试包。测试程序集通过 `IntegrationCheckBridge` 调用预定义程序集，避免为了测试改动生产脚本的程序集布局。普通 Player 构建不包含 TestAssemblies。

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
- 音频用例在当前活动场景创建临时层级，协程 finally 清理自有对象并恢复其改动的全局设置；不属于独立场景隔离。10 秒外层期限仅属于原生命周期检查，Handle/Tail/Limit 的期限不同，详见 [Editor 检查说明](Editor/README.md)。
- Gameplay 用例操作自有 Additive 场景；加载、卸载与动画锁等待各有 15 秒期限。Menu/Recovery 使用真实 Single 加载，会卸载原场景，不能还原原内容。场景等待均有期限；嵌套协程失败也执行同步状态恢复，未完成的操作和场景保留归属供清理重试。加载仍在途时，IntegrationSceneState 仅恢复 timeScale、AudioListener.pause 和 Environment 引用，暂不恢复 GameState 标签及冻结前倍率、加载前状态两个缓存字段；加载结束后重试清理才恢复完整快照。菜单/玩法加载未清完前保留临时进度存储并拒绝开始新夹具，成功重试后兑现延迟释放。
- 玩法用例在自有场景加载回调中临时取消线长限制，完成交互流程后设置有限线长，验证真实物理帧的超限死亡。这样兼容开局即超限的诊断配置，不修改或保存原场景资源。
- 地面极性用例创建独立的 2D 物理场景，并先验证实际接触与法向，再检查死亡结果；`UnityTearDown` 卸载自有场景、恢复环境静态引用并清理自有 Core，不保存或修改关卡资源。
- Tilemap 用例使用独立 2D 物理场景及真实 Tilemap/Wire/Anchor，校验 LineRenderer 与 EdgeCollider2D 的顶点一致；包含旋转、非均匀缩放、非零 offset，以及只改变 offset 后重绘相同路径的实际碰撞查询。帧推进用例验证实际 LateUpdate 与延迟销毁。测试后卸载自有场景并恢复 Environment 静态引用，不修改场景资源。
- Physics 清理故障用例保留本轮所有者；正常路径验证真实重试，独立 `UnityTearDown` 在自身断言失败后也有界排空剩余真实卸载。终态句柄与场景卸载必须同时确认，失败仍保留归属，不重放故障注入。
- 原有 `EnvironmentIntegrationChecks.Run()` 和 `AudioIntegrationChecks.Run()` 手动入口仍可使用。Env 自动测试仍调用 `Run()`；原音频自动测试调用 `RunForTests()`，不依赖手工轮询音频 `LastResult`。
