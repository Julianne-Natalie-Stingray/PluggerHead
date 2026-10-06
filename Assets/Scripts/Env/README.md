# Tilemap 环境与 Player 接入

`EnvironmentFacade` 管理每关的矩形 XY Tilemap、持线、插接、换线及通关。`Wire.TilePath` 保存每根线独立的有序格子路径。Corner、WirePoint 及其专用交互载荷已删除。

## 场景配置

- 关卡配置一个 Grid 和已绘制的路由 Tilemap，将其赋给 `EnvironmentFacade.routingTilemap`。目前支持 Rectangle / XYZ；必须属于同一场景。场景仅有一个 Tilemap 时可自动查找，多个 Tilemap 时须显式绑定。
- PowerSocket、PolaritySocket 和 Anchor 放在已绘制 tile 的中心；`RefreshNodes()` 对有效节点执行格心对齐。路由层不需要 Collider，实体地面使用独立 TilemapCollider2D。
- PowerSocket 的 `wires` 配置本插座的电线，首个有效引用是开局持线。所属 Wire 的固定端取插座位置；未绑定插座时使用自身 Transform。
- 玩家使用 Player Tag。路径根据 Player 根位置采样，渲染、Gizmo 和长度均以格心路径为准，不再依赖 WireAttach。采样忽略 Z 深度，使用 XY 网格第 0 层。
- Wire 的 LineRenderer 绘制本线新增的格心折线，复制的历史前缀继续由原线绘制，避免换线时改变已有路径颜色。Trigger EdgeCollider2D 保留完整逻辑路径（含复制段）；碰撞体仅供查询，零长度时关闭；Player 接地排除 Trigger，交互排除 Wire 碰撞体。

## 路径、长度和 Anchor

`SamplePlayerPath(worldPosition)` 记录玩家移动经过的格子。PlayerMove 在物理帧通过 `GetResistance` 采样；环境 LateUpdate 为无 PlayerMove 的 MockPlayer 补充采样并重绘。开局从插座到玩家初始格子补齐路径，同格移动不会增加长度。两次采样跨越多格时按边界相交顺序补齐四连通路径；精确经过格角时采用可逆的确定顺序。它不做最短路寻路，也不根据障碍自动拉直。

- 原路走回紧邻的上一格时删除末格，逐格收线。回到更早但不是紧邻上一格的格子，会追加路径，不会消除整个环。
- K 在玩家当前已绘制的 tile 中心放置 Anchor。无库存消耗或数量上限，有持线时固定该线截至当前格的全部路径；空格拒绝放置。普通移动仍可记录路由 Tilemap 范围外的网格单元，路由层不是移动边界。
- Anchor 固定此前路径；玩家从 Anchor 朝此前格子走时追加新的尾段。J 收回最近的有效 Anchor，释放固定约束，但不会立即剪掉已记录的路径；继续原路回退才收线。
- 手动固定 Anchor 也要求 Actor 与 Anchor 位于同一格；它同时最多固定一根线。不进入背包，不返回拾取实例。
- 换线时新线复制当前线已经走过的格子，独立保存，**不复制 Anchor 归属**。已插入的旧线保留路径和自己的 Anchor。插入接口时补齐到接口格子的末段，并固定此前线路；从接口原路回走会铺设返回段，不能收掉已经接入的连接。换线后的 `CircuitStart` 指向交接接口，`PreviousWire` 记录前一根线；复制的格子前缀仍只用于路径、长度与绘制。
- `InheritedEdgeCount` 记录复制前缀；回退收掉前缀后再次铺出的段使用当前线颜色。按本局实际出线顺序设置统一 Sorting Layer 和递增 Sorting Order，新线新增段稳定覆盖旧线，不依赖实例创建顺序或路径包围盒；刷新节点不改变顺序，重开归零。原线的 Gradient 与共享材质保持原配置。
- 重开清除各线路径、Anchor 固定状态、插接、换线和通关状态，再从插座建立初始路径；保留手动 Anchor 对象，不复活或移动 Player。

长度是相邻格子中心的世界距离之和，包含 Grid 缩放。`maxLength == 0` 表示不限长；严格超过正数上限时 `GetResistance` 返回 `Vector2.negativeInfinity`，由 PlayerMove 在物理帧死亡。碰撞和长度使用完整路径，渲染只取本线新增尾段，继承段由原线显示；不再使用玩家挂点到 Anchor 的自由直线段。死亡不自动重开关卡。

## 接口清理

已检查 Wire 的调用方及序列化引用，删除 `PullStrength/pullStrength`、从未配置且不再需要的 `fixedEnd`、旧 `FreeEndPosition/SetAttachPoint` 及 Env 的 WireAttach 查找配置；Gizmo 直接显示真实路径末点。保留仍被插接、长度检查、渲染和碰撞测试使用的属性与方法。场景中的历史 WireAttach 子物体不再参与 Wire 逻辑。

## 插接与通关

PolaritySocket 的带电配置必须同时包含 Live 与 Neutral。`Awake` 和环境节点扫描调用 `Initialize()`；只有 Live 或只有 Neutral（即使同时包含 Ground）以及 None 的配置会初始化失败、记录错误并禁用组件，不能通过手动重新启用绕过交互校验。无效配置仍阻止通关，不会因组件禁用而被忽略；Ground-only 配置暂不参与本次回路要求。

所有已发现的带电接口必须通过交互实际插接，经过所在格子不算接入。接口接入后若同插座还有未使用的异极线，就在此交接并记录首尾连接关系；否则当前线保持在手，可继续接入其他接口。重复插接已占用的同极槽位被拒绝；刷新节点从各线的 `ConnectedInterfaces` 重建占用。

通关要求从本局原插座出发，沿真实接口交接形成同时包含火线与零线的连续线路，最终以与出线端相反的极性回到同一插座，且该线路覆盖全部带电接口。各线独立回插、火出火回或零出零回、仅分别终止在不同接口、漏接接口均不能通关。不以格子重叠作为电气连接；也不将其他独立线路上的接入计入本回路。当前不要求地线、降压器或换线次数上限，`SwapCount` 仅保留操作计数。`LevelCleared` 在判定由未闭合变为闭合时触发，重复判定不会重发；重开清除接入列表、连接关系、占用和线路固定位置。

回原插座后若还有未使用的线，仍会交出下一根线，但这属于另一条出线，不能将两条独立出线拼成闭环。Player 负责交互范围、暂停、死亡及操作锁；直接调用 Socket.Interact 不执行距离检查。动态新增接口后需刷新环境节点。

`Current` 是最近经 Awake/RefreshNodes 写入的环境；按场景使用 `ForScene`。节点扫描与玩家查找限定自身场景；每关只配置一个环境。刷新重订阅并重建节点清单，不重开或擦除已记录的格子路径。节点的运行时移动不会重写历史路径；动态关卡应显式决定何时重开。

## 地面极性与背包

GroundPolarity 可放在非 Trigger 地面 Collider2D 所在物体或父物体上，包括 Ground Tilemap。PlayerMove 仅对向上的支撑接触及 groundLayers 中的对象判定：Live/Neutral 相反会死亡；同极、无线、None、Ground 安全。墙、顶、Trigger 不算踩地。当前场景未配置地面极性，测试动态构建。

普通拾取、背包及 UI 接口已移至 `feature/player-inventory`；master 的 Player 直接通过 EnvironmentFacade.ForScene 获取环境，以 IEnvironmentInteractable / InteractionDetails 交互。Anchor 放置和收回独立保留。

## 场景与验证

2026-10-06 线颜色与覆盖顺序：新增四项真实材质离屏像素测试，覆盖两种起始极性、双接口和原插座交接、回退重铺、刷新及重开。最终 EditMode **34/34**、PlayMode **75/75** 顺序通过，独立复审通过；job 和测试隔离修复记录见 [Tests](../../Tests/README.md)。[渲染示例](../../Docs/Development/WireRendering.png)从左到右为原线、刚换线、新铺段重叠：继承段保持原色，新段按出线顺序覆盖旧段。

`GameplayIntegration` 和 `CircuitDiagnostics` 均已绑定路由 Tilemap，插座与 Anchor 已对齐格心；前者使用真实 Player 和 Ground TilemapCollider2D，后者保留 MockPlayer。两场景的线长上限为 64 世界单位，便于验证较长的格子路线。资源位于 `Visual/Environment/`，运行画面见 [TilemapEnvironment](../../Docs/Development/TilemapEnvironment.png)。

2026-10-06：编译无错误，EditMode **22/22**（`812b828e3ce84ea889c26457cc594fda`）、PlayMode **71/71**（`c12304087033435ebdf260a2b5541579`）顺序完成并通过。TilemapTests 覆盖 L 形长度边界、快速跨格、四象限对角及近角非格心回退、同格微动、空格拒绝放置、Anchor 固定/收回、每线独立、重开、缩放/碰撞体 offset 与真实帧推进；真实场景用例另验证玩家落到 Tilemap 地面、J/K、回退、换线和超长死亡。

测试通过不代表真实键盘输入、所有关卡设计或大规模长线路性能已验证。调试按钮直接修改当前场景状态；Run acceptance 会将角色移动到目标格并采样后操作节点，不模拟输入或物理移动；隔离 Test Runner 的使用方式见 [Tests](../../Tests/README.md)。

独立审查已复核近角回退、调试验收和场景落格修复，未留下未解决事项；提交前 git diff --check 通过。


任意旧格接触截断和重叠颜色功能保存在 `feature/wire-path-overlap`（`286d01f`）。当前分支恢复上一版原路逐格回退行为，保留上述废弃接口及资源字段清理。
