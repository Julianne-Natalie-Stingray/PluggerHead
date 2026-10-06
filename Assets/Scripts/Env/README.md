# Tilemap 环境与 Player 接入

`EnvironmentFacade` 管理每关的矩形 XY Tilemap、持线、插接、换线及通关。`Wire.TilePath` 保存每根线独立的有序格子路径。Corner、WirePoint 及其专用交互载荷已删除。

## 场景配置

- 关卡配置一个 Grid 和已绘制的路由 Tilemap，将其赋给 `EnvironmentFacade.routingTilemap`。目前支持 Rectangle / XYZ；必须属于同一场景。场景仅有一个 Tilemap 时可自动查找，多个 Tilemap 时须显式绑定。
- PowerSocket、PolaritySocket 和 Anchor 放在已绘制 tile 的中心；`RefreshNodes()` 对有效节点执行格心对齐。路由层不需要 Collider，实体地面使用独立 TilemapCollider2D。
- PowerSocket 的 `wires` 配置本插座的电线，首个有效引用是开局持线。所属 Wire 的固定端取插座位置；未绑定插座时使用自身 Transform。
- 玩家使用 Player Tag。路径根据 Player 根位置采样，渲染、Gizmo 和长度均以格心路径为准，不再依赖 WireAttach。采样忽略 Z 深度，使用 XY 网格第 0 层。
- Wire 的 LineRenderer 和 Trigger EdgeCollider2D 共用格心折线。碰撞体仅供查询，零长度时关闭；Player 接地排除 Trigger，交互排除 Wire 碰撞体。

## 路径、长度和 Anchor

`SamplePlayerPath(worldPosition)` 记录玩家移动经过的格子。PlayerMove 在物理帧通过 `GetResistance` 采样；环境 LateUpdate 为无 PlayerMove 的 MockPlayer 补充采样并重绘。开局从插座到玩家初始格子补齐路径，同格移动不会增加长度。两次采样跨越多格时按边界相交顺序补齐四连通路径；精确经过格角时采用可逆的确定顺序。它不做最短路寻路，也不根据障碍自动拉直。

- 接触当前路径中任意未固定的格子时，截断该格之后的全部路径；跨格采样中的途中接触也立即生效。如果同格有多个合法位置，保留最早的位置。最后一个 Anchor 及此前固定段不会被截断。
- K 在玩家当前已绘制的 tile 中心放置 Anchor。无库存消耗或数量上限，有持线时固定该线截至当前格的全部路径；空格拒绝放置。普通移动仍可记录路由 Tilemap 范围外的网格单元，路由层不是移动边界。
- Anchor 固定此前路径；玩家从 Anchor 朝此前格子走时追加新的尾段。J 收回最近的有效 Anchor，释放固定约束；下次采样若当前位置已接触新解锁的旧路径，就截断其后的路径，无需先移动到相邻格。
- 手动固定 Anchor 也要求 Actor 与 Anchor 位于同一格；它同时最多固定一根线。不进入背包，不返回拾取实例。
- 换线时新线复制当前线已经走过的格子，独立保存，**不复制 Anchor 归属**。已插入的旧线保留路径和自己的 Anchor。插入接口时补齐到接口格子的末段。
- 重开清除各线路径、Anchor 固定状态、插接、换线和通关状态，再从插座建立初始路径；保留手动 Anchor 对象，不复活或移动 Player。

长度是相邻格子中心的世界距离之和，包含 Grid 缩放。`maxLength == 0` 表示不限长；严格超过正数上限时 `GetResistance` 返回 `Vector2.negativeInfinity`，由 PlayerMove 在物理帧死亡。渲染、碰撞和长度来自同一份路径，不再使用玩家挂点到 Anchor 的自由直线段。死亡不自动重开关卡。

## 重叠颜色与接口清理

每帧统计本环境内所有非零长度线路的格子出现次数，同一根线重复经过及不同线共享格子都计入。出现两次或以上的格子，其内部线段使用 `Wire.overlapColor`（默认粉色）覆盖显示，其余线段保留原 LineRenderer 渐变色。提示反映当前路径，不保存已经截断的历史访问次数。只有一个格子的闲置线不算重叠。

覆盖线只包含该格中心到前后格中点的线段，不改变路径、长度或 EdgeCollider2D。覆盖渲染器按需创建并复用，多余部分关闭；不保存到场景/构建，随 Wire 清理。渲染数量随重叠格子的出现次数增长，未做大规模压力测试。运行效果见 [WireOverlap](../../Docs/Development/WireOverlap.png)。

已检查 Wire 的调用方及序列化引用，删除 `PullStrength/pullStrength`、从未配置且不再需要的 `fixedEnd`、旧 `FreeEndPosition/SetAttachPoint` 及 Env 的 WireAttach 查找配置；Gizmo 直接显示真实路径末点。保留仍被插接、长度检查、渲染和碰撞测试使用的属性与方法。场景中的历史 WireAttach 子物体不再参与 Wire 逻辑。

## 插接与通关

PolaritySocket 按电性拒绝错误或已占用的插入。双电性接口接入后交出同插座另一根未终止线并计一次换线；回到原插座闭合后交出下一根线，不计换线。完成最后一根后不再持线。Player 负责交互范围、暂停、死亡及操作锁；直接调用 Socket.Interact 不执行距离检查。

首条 Live、Neutral 线终止且换线不超过 `maxSwaps` 才能通关；`requireGround` 开启时还需首条 Ground 终止。终止表示回原插座闭合或插入双电性接口。这仍是简化状态判定，不验证完整电路拓扑。`LevelCleared` 仅在判定由未闭合变为闭合时触发。

`Current` 是最近经 Awake/RefreshNodes 写入的环境；按场景使用 `ForScene`。节点扫描与玩家查找限定自身场景；每关只配置一个环境。刷新重订阅并重建节点清单，不重开或擦除已记录的格子路径。节点的运行时移动不会重写历史路径；动态关卡应显式决定何时重开。

## 地面极性与背包

GroundPolarity 可放在非 Trigger 地面 Collider2D 所在物体或父物体上，包括 Ground Tilemap。PlayerMove 仅对向上的支撑接触及 groundLayers 中的对象判定：Live/Neutral 相反会死亡；同极、无线、None、Ground 安全。墙、顶、Trigger 不算踩地。当前场景未配置地面极性，测试动态构建。

普通拾取仍通过 IEnvironmentPickup / IPickupInstance 交给 PlayerInventory，放下由实例 TryDrop 负责。Anchor 不属于该通道；当前没有具体普通拾取实例实现。

## 场景与验证

`GameplayIntegration` 和 `CircuitDiagnostics` 均已绑定路由 Tilemap，插座与 Anchor 已对齐格心；前者使用真实 Player 和 Ground TilemapCollider2D，后者保留 MockPlayer。两场景的线长上限为 64 世界单位，便于验证较长的格子路线。资源位于 `Visual/Environment/`，运行画面见 [TilemapEnvironment](../../Docs/Development/TilemapEnvironment.png)。

2026-10-06：编译无错误，EditMode **22/22**（`812b828e3ce84ea889c26457cc594fda`）、PlayMode **71/71**（`c12304087033435ebdf260a2b5541579`）顺序完成并通过。TilemapTests 覆盖 L 形长度边界、快速跨格、四象限对角及近角非格心回退、同格微动、空格拒绝放置、Anchor 固定/收回、每线独立、重开、缩放/碰撞体 offset 与真实帧推进；真实场景用例另验证玩家落到 Tilemap 地面、J/K、回退、换线和超长死亡。

测试通过不代表真实键盘输入、所有关卡设计或大规模长线路性能已验证。调试按钮直接修改当前场景状态；Run acceptance 会将角色移动到目标格并采样后操作节点，不模拟输入或物理移动；隔离 Test Runner 的使用方式见 [Tests](../../Tests/README.md)。

独立审查已复核近角回退、调试验收和场景落格修复，未留下未解决事项；提交前 git diff --check 通过。

本次 Wire 行为更新：EditMode 22/22（`3e2227f67e3f4dd58a41013bac8301f3`）、PlayMode 74/74（`6ca8189c3df64c8c95ce6c9c82fbacd3`）顺序终态通过；新增任意旧格接触截断、固定段重复与解除、同线/跨线颜色及提示清除回归。前述 71 项结果属于上一版 Tilemap 改造。

颜色池生命周期已验证：自动测试覆盖删除提示子物体后的重建、缺失闲置项的清理及两个 Anchor 依次解除；另用唯一临时场景实际保存/重开，确认两条 DontSave 提示引用恢复为空后均能重建，临时场景已删除。
