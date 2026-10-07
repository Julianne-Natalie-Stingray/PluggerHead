# Tilemap 环境与 Player 接入

`EnvironmentFacade` 管理每关的矩形 XY Tilemap、持线、插接、换线及通关。`Wire.TilePath` 保存每根线独立的有序格子路径。Corner、WirePoint 及其专用交互载荷已删除。

2026-10-07：Socket 单次占用规则已生效，EditMode 53/53、PlayMode 72/72 顺序通过，独立复审通过；详见测试记录。Level0 当前两个双极插座布局需由关卡作者调整，本次仅改规则，不改场景。下方旧验证数量为历史记录。

## 场景配置

- 每关在 `EnvironmentFacade` 的 **Background Music** 中选择 BGM 的 `AudioId`，默认 `DefaultOst`。对应 `AudioClipData` 须加入 Core 的 `AudioManagerConfigs`，配置循环播放、2D 和 OST 混音组；玩家设置中的音乐音量沿用 OST 总线。
- BGM 在 Env 的 Start 已执行且关卡结束 Loading 后请求播放，由 Core 的 AudioManager 持有。SceneSwitch 的 Loading 及旧 Env 卸载保持原声部播放；新关卡请求相同 AudioId 时复用句柄和播放位置，不叠加声部，不从头重播；不同 AudioId 才停止旧曲并播放新曲。没有 Env 的目标场景也会继续播放已有 BGM。
- 设置页通过监听器暂停并从原位置续播；BGM 强制服从暂停，不受音频资源的 DefaultSurviveFreeze 影响。通关、死亡、非 Loading 期间 Env 禁用/卸载仍立即停止；通关/死亡后解除暂停不会重播。调试重开仍存活玩家的关卡会重新播放。没有 Core 时等待其可用；音频请求被拒绝时不逐帧重试。
- 2026-10-07 BGM 验证：编译无错误，独立审查未发现问题；EditMode **69/69**（`e482d8e14b6440f881f7808fd270d2ec`）后 PlayMode **79/79**（`7b0c9e3c704d43d7bc31333f941fe4e9`）顺序终态通过，无失败或跳过。真实 GameplayIntegration 用例覆盖加载门控、设置暂停位置保持及续播、禁用释放与启用重播、通关停止、重开、死亡停止及解除冻结不重播。未做人工听感验收。

- 关卡配置一个 Grid 和已绘制的路由 Tilemap，将其赋给 `EnvironmentFacade.routingTilemap`。目前支持 Rectangle / XYZ；必须属于同一场景。场景仅有一个 Tilemap 时可自动查找，多个 Tilemap 时须显式绑定。
- PowerSocket、PolaritySocket 和 Anchor 放在已绘制 tile 的中心；`RefreshNodes()` 对有效节点执行格心对齐。路由层不需要 Collider，实体地面使用独立 TilemapCollider2D。
- PowerSocket 的 `wires` 配置本插座的电线，首个有效火线或零线引用是开局持线。所属 Wire 的固定端取插座位置；未绑定插座时使用自身 Transform。
- 玩家使用 Player Tag。路径根据 Player 根位置采样，渲染、Gizmo 和长度均以格心路径为准，不再依赖 WireAttach。采样忽略 Z 深度，使用 XY 网格第 0 层。
- Wire 的 LineRenderer 绘制本线新增的格心折线，复制的历史前缀继续由原线绘制，避免换线时改变已有路径颜色。Trigger EdgeCollider2D 保留完整逻辑路径（含复制段）；碰撞体仅供查询，零长度时关闭；Player 接地排除 Trigger，交互排除 Wire 碰撞体。

## Wire 外观

`Env/Wire/SO/WireVisualConfigs.asset` 集中配置 Live / Neutral / Ground 三个共享材质。预设 Wire 初始化及运行时续线均按自身精确极性取材质；续线继承该配置，不继承前一根线的材质，也不由代码写入红、蓝、绿颜色。换线的 Sorting Layer / Sorting Order 规则保持不变，宽度、UV、端点与转角样式仍继承原线。

当前三种独立材质共用 `Env/Wire/Sprite/WireSprite.alt.png`，保持白色且不额外染色。WireSprite.png 保留为源资源；后续专用图片可分别配置到 LiveWire、NeutralWire、GroundWire 材质。LineRenderer 参数及极性选择机制保持不变。

无视觉配置的临时 Wire 保留原材质；已绑定配置但对应槽为空，或极性不是单独 Live / Neutral / Ground 时，材质清空，避免悄悄沿用其他种类外观。生产 Wire 必须绑定完整配置。

PowerSocket 开局持线以及 DualSocket / PowerSocket 成功交互后，Wire 从完整碰撞折线上距对应极性锚点最近的 XY 投影点绘制连接支线（单格路径使用唯一格心）。每条 Wire 的电气起点与插入终点各复用一个两点 LineRenderer，继承本线材质、宽度、颜色、纹理与排序；换线后的入线和出线分别连接各自极性的锚点。支线随路径重绘更新，不加入碰撞、格子路径、线长或占用判定；缺少锚点、跨场景引用、空路径、禁用时隐藏，重开清除旧连接，销毁 Wire 时一起清理。GroundSocket 保持原显示。此改动仅完成静态检查，未运行动态或视觉验证。

## 地图美术接入（2026-10-07）

地图图片统一归入 `Env/Map/Sprite/`，原 levelSheet 图片保留 GUID、切片和导入配置；角色 Idle/Walk 图片归入 `Player/Sprite/`。`Env/Map/Tile/Ground/LiveGround.asset`、`NeutralGround.asset` 保留原 `L 1`、`N 1` Tile GUID，使用现有 2D Tilemap Extras 的 AnimatedTile 播放红/蓝 Ground 四帧，默认以每秒 8 帧的速度配合当前 Tilemap 帧率。Level2/3 每关分别使用 6 格 Live 和 5 格 Neutral，位置、极性和物理组件保持不变。Grid 碰撞保持原满格矩形；四个生产 CompositeCollider 的全部路径顶点在接入前后完全相同。

PowerSocket 和 DualSocket（PolaritySocket 双极配置）成功交互后在插座位置生成 `Env/Socket/Fire.prefab`，按顺序播放 Fire 四帧一次并销毁；拒绝或重复交互不生成，特效随插座场景卸载清理。GroundSocket 不播放此特效。仅做静态检查，未进行运行时或视觉验证。

Ground1 继续供现有地形使用。Red/BluePlatform、Left/RightWall1 和 Test 保留为资源；尚无明确需替换的生产对象，不新增布局或把整格地面替换为薄平台。Wire 三种专用图片及 Portal 新美术尚未制作，本次不接入。

## 路径、长度和 Anchor

`SamplePlayerPath(worldPosition)` 记录玩家移动经过的格子。PlayerMove 在物理帧通过 `GetResistance` 采样；环境 LateUpdate 为无 PlayerMove 的 MockPlayer 补充采样并重绘。开局从插座到玩家初始格子补齐路径，同格移动不会增加长度。两次采样跨越多格时按边界相交顺序补齐四连通路径；精确经过格角时采用可逆的确定顺序。它不做最短路寻路，也不根据障碍自动拉直。

- 原路走回紧邻的上一格时删除末格，逐格收线。回到更早但不是紧邻上一格的格子，会追加路径，不会消除整个环。
- K 在玩家当前已绘制的 tile 中心放置 Anchor。无库存消耗或数量上限，有持线时固定该线截至当前格的全部路径；空格拒绝放置。普通移动仍可记录路由 Tilemap 范围外的网格单元，路由层不是移动边界。
- Anchor 固定此前路径；玩家从 Anchor 朝此前格子走时追加新的尾段。J 收回最近的有效 Anchor，释放固定约束，但不会立即剪掉已记录的路径；继续原路回退才收线。
- 手动固定 Anchor 也要求 Actor 与 Anchor 位于同一格；它同时最多固定一根线。不进入背包，不返回拾取实例。
- 换线时新线复制当前线已经走过的格子，独立保存，**不复制 Anchor 归属**。已插入的旧线保留路径和自己的 Anchor。插入接口时补齐到接口格子的末段，并固定此前线路；从接口原路回走会铺设返回段，不能收掉已经接入的连接。换线后的 `CircuitStart` 指向交接接口，`PreviousWire` 记录前一根线；复制的格子前缀仍只用于路径、长度与绘制。
- `InheritedEdgeCount` 记录复制前缀；回退收掉前缀后再次铺出的段使用当前线颜色。按本局实际出线顺序设置统一 Sorting Layer 和递增 Sorting Order，新线新增段稳定覆盖旧线，不依赖实例创建顺序或路径包围盒；刷新节点不改变顺序，重开归零。原线保持自身类型材质；排序不修改外观。
- 重开清除各线路径、Anchor 固定状态、插接、换线和通关状态，再从插座建立初始路径；保留手动 Anchor 对象，不复活或移动 Player。

长度是相邻格子中心的世界距离之和，包含 Grid 缩放。`maxLength == 0` 表示不限长；严格超过正数上限时 `GetResistance` 返回 `Vector2.negativeInfinity`，由 PlayerMove 在物理帧死亡。碰撞和长度使用完整路径，渲染只取本线新增尾段，继承段由原线显示；不再使用玩家挂点到 Anchor 的自由直线段。死亡不自动重开关卡。

## 接口清理

已检查 Wire 的调用方及序列化引用，删除 `PullStrength/pullStrength`、从未配置且不再需要的 `fixedEnd`、旧 `FreeEndPosition/SetAttachPoint` 及 Env 的 WireAttach 查找配置；Gizmo 直接显示真实路径末点。保留仍被插接、长度检查、渲染和碰撞测试使用的属性与方法。场景中的历史 WireAttach 子物体不再参与 Wire 逻辑。

## 插接与通关

按 `SUCCESS_RULE.md`：仅支持双极插座（Live | Neutral）和纯地线插座（Ground）。PolaritySocket 的其他位组合，包括 None、单极、双极加地线及未知位，均初始化失败并禁用；无效节点仍阻止通关。PowerSocket 通过 `isGroundTerminal` 选择这两种类型，默认双极；首个配置了有效火线/零线的双极 PowerSocket 作为开局出线点。`wires` 列表中的 Ground/None 不作为主线。关卡须配置路由 Tilemap 和至少一条火线或零线。

两类组件都表示电气插座；逻辑占用仍使用插座 Transform 与极性，不复用用于路径固定的 `Anchor` 组件。DualSocket 和 PowerSocket 的 `liveAnchor` / `neutralAnchor` 分别引用预制体内的 LiveAnchor / NeutralAnchor，作为外观接线端点，两个端口可以位于同一格。

每个逻辑锚点（插座 + 极性）最多连接一条线，出线同样占用锚点。双极插座只接受接入空闲的同极端口；当前电线不能接回自己的 `CircuitStart` 插座，即使绕路或经过降压器也不能自身回环。接入成功后，异极端口空闲时交出异极线；异极端口已有线时仅完成接入并放下主线，不复用已占用端口。优先使用尚未使用的异极 Wire，不足时生成续线。重复操作被拒绝，不增加换线次数、不生成线、不改变端点。`CanInteract` 向 Player 返回当前占用和持线状态下是否可交互。

初始 PowerSocket 的开局出线端口已占用，须经其他插座换极后才能接入它剩余的空闲端口；完成后 `HeldWire` 可以为空。通关仍按全部端口接线和电压判定，不另加全局回原插座要求。

地线与主线独立：没有地线时从空闲地线端口生成并携带地线；已有地线时只能接入另一个空闲地线端口并放下。已有连接的地线端口不能再次拾取或接入；放下主线后仍可拾取、采样、检测长度并连接地线。`HeldGroundWire` 提供查询。两线分别采样格子路径、渲染与长度检查；任一持线超出正数长度上限会触发原有死亡信号。手动 Anchor 仍只固定主线。近距离跨格交互后，每根线分别从自己的连接点补齐到玩家当前格，避免跳格。

`VoltageReducer` 是带一个锚点及 `voltageDrop` 的交互组件。交互将主线接入并固定当前路径，但保留主线极性与携带状态。所需降压量 `neededVoltage` 配置在 EnvironmentFacade，通过 `NeededVoltage` 查询，默认 30；非负有限数才有效。原初始/目标电压合并为两者之差，SceneRoot 预制体当前配置为 0，场景可按关卡需要覆盖。`CurrentVoltage` 表示剩余所需降压量，为 `NeededVoltage` 减所有已连接降压器的降压数，每个降压器只计算一次；未连接的不参与，相加可使结果为负，不额外钳制。连接记录随 Wire 保存，刷新节点不会重复扣压，重开清除。

`ReducedVoltage` 为 `NeededVoltage - CurrentVoltage`，提供已连接降压器的累计降压量供关卡 HUD 查询，不钳制超额数值；显示组件不参与电路判定。

每次插座或降压器交互后检查成功，须同时满足：

1. 所有已发现插座的全部端口有电线连接，包括初始 PowerSocket 的两个端口和地线插座。
2. 累计降压大于等于 `NeededVoltage`，即 `CurrentVoltage <= 0`。

不额外要求独立的闭环拓扑或同源回插；单端口占用与禁止自身回环始终有效。沿途经过端口不算接线；从端口出线也算该端口有线连接。`IsCircuitClosed` 为兼容现有玩家 UI 保留名称，现表示满足上述成功条件。`LevelCleared` 仅在判定从失败变为成功时触发，重复判定不重复通知。空场景、无效插座或非法电压配置不能通关。

刷新重建连接占用；销毁电线后再次判定将移除其占用/降压贡献。重开销毁本局生成的地线与续线，重置预设电线、连接、路径固定位置、电压和通关状态。节点扫描和玩家查找限定自身场景；每关配置一个环境，动态新增节点后调用 `RefreshNodes()`。Player 负责距离、暂停、死亡及操作锁，直接调用节点 Interact 不执行距离校验。

### 可复用示例

- `Assets/Prefabs/Env/Sockets/GroundSocket.prefab`：绿色地线插座，PolaritySocket.accepted=Ground，Trigger 交互碰撞体。
- `Assets/Prefabs/Env/VoltageReducer.prefab`：黄色降压器，默认降压 30V，Trigger 交互碰撞体。
- 在已有路由 Tilemap 的关卡中放置一个初始双极 PowerSocket、一个双极 PolaritySocket、两个地线插座和一个降压器，均对齐已绘制格子；PowerSocket 配置一条 Live Wire，环境设为所需降压 30V。运行后依次操作地线 A、降压器、地线 B、双极插座、初始插座：拾取地线、完成 30V 降压、放下地线，最后补齐初始插座的零线端口并通关。提高所需降压量到 31V 则不通关；需要在放下主线之前连接额外降压器。

示例不修改当前关卡布局。[示例预制体预览](../../Docs/Development/SuccessRuleExamples.png)：左为地线插座，右为降压器。

最终 EditMode **55/55**、PlayMode **75/75** 顺序终态通过，独立复审无未解决问题；job 及中断恢复记录见 [Tests](../../Tests/README.md)。

`CircuitClosureIntegrationChecks` 在隔离预览场景构造同样流程，验证电压相等/不足、未连接降压器、地线漏接、重复交互、销毁连接、重开、跨格双持线路由与示例资源引用。`Run acceptance` 调试按钮会移动玩家，先访问降压器，再访问可交互插座，最后尝试初始插座；若关卡配置本身无法满足电压目标，会报告失败。

## 地面极性与背包

GroundPolarity 可放在非 Trigger 地面 Collider2D 所在物体或父物体上，包括 Ground Tilemap。PlayerMove 仅对向上的支撑接触及 groundLayers 中的对象判定：地面仅允许 Live/Neutral，Inspector 下拉框仅提供这两项。Ground、None、组合值或未知值属于配置错误，在 Awake、OnValidate、Polarity 查询与 CanSupport 检测时抛出 InvalidOperationException。只有本场景中正在持有的电线与地面极性完全相同才安全；无线、已放下的匹配线、仅持异极线或地线均死亡；同时踩到多个极性支撑面时，每个面都须有匹配持线。普通无此组件的地面与禁用组件保持安全。墙、顶、Trigger 不算踩地。当前场景未配置地面极性，测试动态构建。

普通拾取、背包及 UI 接口已移至 `feature/player-inventory`；master 的 Player 直接通过 EnvironmentFacade.ForScene 获取环境，以 IEnvironmentInteractable / InteractionDetails 交互。Anchor 放置和收回独立保留。

## 场景与验证

2026-10-06 线颜色与覆盖顺序：新增四项真实材质离屏像素测试，覆盖两种起始极性、双接口和原插座交接、回退重铺、刷新及重开。最终 EditMode **34/34**、PlayMode **75/75** 顺序通过，独立复审通过；job 和测试隔离修复记录见 [Tests](../../Tests/README.md)。[渲染示例](../../Docs/Development/WireRendering.png)从左到右为原线、刚换线、新铺段重叠：继承段保持原色，新段按出线顺序覆盖旧段。

`GameplayIntegration` 和 `CircuitDiagnostics` 均已绑定路由 Tilemap，插座与 Anchor 已对齐格心；两者均使用真实 Player 和 Ground TilemapCollider2D。两场景的线长上限为 64 世界单位，便于验证较长的格子路线。资源位于 `Visual/Tile/`，运行画面见 [TilemapEnvironment](../../Docs/Development/TilemapEnvironment.png)。

2026-10-06：编译无错误，EditMode **22/22**（`812b828e3ce84ea889c26457cc594fda`）、PlayMode **71/71**（`c12304087033435ebdf260a2b5541579`）顺序完成并通过。TilemapTests 覆盖 L 形长度边界、快速跨格、四象限对角及近角非格心回退、同格微动、空格拒绝放置、Anchor 固定/收回、每线独立、重开、缩放/碰撞体 offset 与真实帧推进；真实场景用例另验证玩家落到 Tilemap 地面、J/K、回退、换线和超长死亡。

测试通过不代表真实键盘输入、所有关卡设计或大规模长线路性能已验证。调试按钮直接修改当前场景状态；Run acceptance 会将角色移动到目标格并采样后操作节点，不模拟输入或物理移动；隔离 Test Runner 的使用方式见 [Tests](../../Tests/README.md)。

独立审查已复核近角回退、调试验收和场景落格修复，未留下未解决事项；提交前 git diff --check 通过。


任意旧格接触截断和重叠颜色功能保存在 `feature/wire-path-overlap`（`286d01f`）。当前分支恢复上一版原路逐格回退行为，保留上述废弃接口及资源字段清理。
