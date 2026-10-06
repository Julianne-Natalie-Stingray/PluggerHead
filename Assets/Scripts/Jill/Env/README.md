# Environment 子系统

## 职能

关卡内的**电学环境**: 玩家拖着线从插座孔进入, 绕过锚点改变方向, 把线接进电性对应的接口, 在双性接口处换线, 直到回路闭合.

- 拥有可交互物与可拾取物的契约: `IEnvironmentInteractable` / `IEnvironmentPickup` / `IPickupInstance`.
- 拥有回路状态: 玩家携带哪根线, 什么插在哪里, 换过几次线, 以及是否通关.
- 拥有折线的几何与渲染(直线与折线, 无物理, 无动画).
- 不负责**范围检测**: 玩家在不在范围内由 Player 侧判断.
- 不负责**拾取-携带-放下-吸附**这条回路: 它属于 Player 侧(携带需要动画与手持挂点).
- 不负责资产与 Inspector 装配: 场景层级, `LineRenderer`, 碰撞体, Tag 都由用户完成.

## 构成

| 文件 | 类型 | 职责 |
| --- | --- | --- |
| `WirePolarity.cs` | flags 枚举 | 线的电性, 也是接口接受的电性: `Live` / `Neutral` / `Ground` |
| `InteractionDetails.cs` | class | 一次环境请求的共享载荷(`Actor` / `Target`); 各实现者按需派生 |
| `WirePointDetails.cs` | class | 与绕线点交互的细节; 今天不额外携带字段(方向由点自身状态决定) |
| `IEnvironmentInteractable.cs` | interface | 可交互但不可拾起的内容: `CanInteract` / `OnInteracted` / `Interact` |
| `IEnvironmentPickup.cs` | interface | 可被搬动的内容: `CanPickup` / `Pickup` |
| `IPickupInstance.cs` | interface | 某物在背包中的形态: `MaxStackAmount` / `CurrentStackAmount` |
| `AnchorInstance.cs` | `[Serializable]` class | `IPickupInstance` 的实现; 持有源 Anchor 的运行期绑定 |
| `PolaritySocket.cs` | MonoBehaviour | **带电接口**: 声明接受的电性; 双性接口即换线点 |
| `Wire.cs` | MonoBehaviour | 一根线: 电性, 固定端, 自由端状态, 折线渲染 |
| `WirePoint.cs` | MonoBehaviour | 线上的一个点(端点/折点); 接入或解除 |
| `Anchor.cs` | MonoBehaviour | 锚点: 绕线(**交互**), 以及能否被搬动(**拾取**) |
| `PowerSocket.cs` | MonoBehaviour | **插座孔**: 拥有从它引出的线, 也是回路闭合处 |
| `EnvironmentFacade.cs` | MonoBehaviour | 每关一个的枢纽: 回路状态, 状态机, 折线重建, 通关判定, 断言, Gizmos |

## 公共API

- `EnvironmentFacade.Current` -> 每场景一个的访问点(唯一用途: 回答"这个交互者携带哪根线").
- `EnvironmentFacade.HeldWireOf(details)` -> `Wire`: 静态查询, 供绕线节点使用.
- `EnvironmentFacade.EvaluateCircuit()` -> `bool`: 重算并打印一行 `###ASSERT`.
- `EnvironmentFacade.LevelCleared` -> `event Action`: 通关第一次成立时触发, **由玩法订阅**.
- `EnvironmentFacade.HeldWire` / `.IsCircuitClosed` / `.SwapCount`: 只读的回路状态.
- `EnvironmentFacade.RefreshNodes()`: 关卡在运行期新建节点后重新扫描(同时重新订阅).
- `IEnvironmentInteractable` 的链式约定: 先查 `CanInteract`, 再 `Interact`; 实现**必须**在 `CanInteract == false` 时保持无副作用.
- 线的入口(供 Player 侧接线): `Wire.SetAttachPoint(Transform)` / `Wire.Hold()` / `Wire.PlugInto(Transform, bool)`.
  **注意**: 正常玩法下这些由门面调用; Player 侧目前不需要直接调它们.

## 内部实现思路

### 节点愚蠢, 枢纽独裁

节点(`WirePoint` / `Anchor` / `PolaritySocket` / `PowerSocket`)**只做两件事**: 按自己的 `CanInteract` 设门, 翻转自己的状态旗标, 然后触发 `OnInteracted`. 一切**回路后果**(插在哪里, 是否允许, 是否换线, 是否通关)只发生在 `EnvironmentFacade` 一处. 这样做的理由: 回路状态只能有一个拥有者, 否则"接口是否被占用"这类判断会在两处各有一份, 并且迟早互相矛盾.

### 折线的几何

`path(线) = [固定端] + [已接入的点, 按接入顺序] + [自由端]`, 直线连接, 由 `LineRenderer` 绘制.

- **固定端**: `Wire.FixedEndPosition`(未指定时取线自身的位置), 也就是插座孔处.
- **已接入的点**: 属于该线的 `WirePoint` 中 `IsEngaged` 为真者, 加上"以该线为 `EngagedBy`"的 `Anchor`; 两者按**接入顺序号**合并. 顺序号由 `Wire.NextEngagementSequence()` 单调发放 —— 用序号而不是时间戳, 因为同一帧内的两次接入无法用时间区分.
- **自由端**: `Wire.FreeEndPosition` —— 被持有时跟随携带者的挂点, 已插入时是该插入点的位置.
- 折线每帧重画(自由端在动), 但**顺序只在交互时重建**: `RebuildWaypoints()` 缓存各线的绕线点, `LateUpdate` 只拼接.
- 接入顺序与缓存: 位置缓存用复用的 `List`, 绘制缓冲也用复用的数组, 避免每帧分配.

### 交互状态机

| 目标 | 条件 | 效果 |
| --- | --- | --- |
| `PolaritySocket`(带电接口) | 手上有线 **且** 电性被接受 **且** 该电性槽位未被占用 | 线**终止**在该接口(自由端停在此处); 若该接口是**双性**(`IsDual`), 随即**换线**: 同一插座孔下另一根未闭合的线回到手中, 换线计数 +1 |
| 同上 | 电性不匹配 或 槽位已占用 | **拒绝**并记一条 warning, 无副作用 |
| `PowerSocket`(插座孔) | 手上的线属于该插座孔 | 线**闭合**(插回插座孔); 若该孔下还有既未闭合也未插入的线, 交到手上(**不计入换线**) |
| `PowerSocket` | 线不属于该孔 | 拒绝并记 warning |
| `Anchor` | `CanInteract` | 若已被某线绕过则解除; 否则把**当前携带者手中的线**绕过它(取下一个顺序号) |
| `Anchor` | 手上无线 | 拒绝并记信息 |
| `WirePoint` | `CanInteract` | 接入或解除该点(取下一个顺序号) |
| `WirePoint` / `Anchor` / 任何 | `CanInteract == false` | 无副作用空操作 |

### 通关判定(四个条件)

`EvaluateCircuit()` 每次都打印一行可 grep 的断言, 例如:

```
###ASSERT circuit closed=True polarity=True terminated=True ground=not-required swaps=1/1 live=closed at Socket neutral=parked at DualJack
```

1. **电性对应**: 每根已插入接口的线, 其电性必须被该接口接受. 插入时已强制一次; 这里再查一次, 因为接口的电性数据可能在装配后被改过.
2. **不交错**: `swaps <= maxSwaps`(序列化参数, 默认 1). 双性接口可以有很多个, 玩家理论上能来回换线, 这条把"零段与火段各自连续"表达成一个可计数的事实.
3. **闭合/终止**: 零线与火线都必须**终止** —— 插回插座孔(`IsClosed`), **或**停在双性接口上.
4. **地线**(仅当关卡声明 `requireGround`): 地线也必须终止. 插座孔的 `IsGroundTerminal` 决定它能否作为地线终点, 因此"破裂地线"的端点可以是插座孔, 也可以是别的接口.

> **⚠️ 条件 3 相对 grill 确认稿做了一处放宽, 请你裁决.** 确认稿写的是"两个自由端都插回插座孔". 但在双性接口换线的机制下, 它与条件 2"最多换线一次"**互相矛盾**: 玩家先把零线停在双性接口(换线 1 次), 走到插座孔闭合火线, 此时若必须回去取零线再插回插座孔, 就需要第 2 次换线, 于是条件 2 永远不成立 —— **关卡不可通关**. 电力学的真相是双性接口本身把两根线连成了一环, 因此本实现把它当作"终止". 严格字面语义只需把 `IsTerminated` 里的双性接口分支删掉即可, 一行改动.

### 为什么不给节点做接口抽象以外的抽象

按 §5.2.1, 接口抽象本应等到有 ≥3 个类型共享特性时才做. 这里只有两个实现者(`Anchor`, `WirePoint`)加两个接口实现(`PolaritySocket`, `PowerSocket`), 属于**用户明确批准的偏离**: 契约的读者是 Player 侧(跨树), 因此必须是一份稳定的书面契约, 而不是"看代码就知道".

## 场景装配清单(你需要做的)

1. **Tag**: 在 `Project Settings > Tags and Layers` 新增 `WireAttach`(挂线点); `Player` 若还没有也需新增. 门面按这两个 Tag 找玩家与挂点.
2. **SceneRoot**: 场景里唯一的 Root GO, 挂 `EnvironmentFacade`.
3. **插座孔**: GO + `PowerSocket` + `Collider2D`; 在 `Wires` 列表里拖入从它引出的两根线.
4. **线**: 每根一个 GO(或其父物体) + `LineRenderer`(`RequireComponent` 会自动加) + `Wire`; 把线上的点做成的**子物体**, 各挂 `WirePoint`. `Fixed End` 可留空(用线自身位置).
5. **带电接口**: GO + `PolaritySocket` + `Collider2D`; 用 `Accepted` 选零/火/双性.
6. **锚点**: GO + `Anchor`; 不可动的锚点把 `Can Pickup` 留空, 可绕线的保持 `Can Interact` 勾选.
7. 让 Unity 编译一次, **必须无编译错误**; 然后在门面上点 `Refresh nodes and evaluate`, 看 Console 里的 `###ASSERT` 行.

## 验证方式

- **断言**: 任何一次判定都会打印 `###ASSERT circuit closed=... polarity=... terminated=... ground=... swaps=n/max live=<状态> neutral=<状态>`, 因此"什么都没发生"也有正面证据.
- **Debug 按钮**: 门面上的 `Refresh nodes and evaluate`(编辑期与运行期都可用).
- **Gizmos**: 选中门面即可看到各线的折线, 颜色按电性(火=红, 零=蓝, 地=绿), 自由端画小球.
- **最小 loop(待 Player 侧接入后)**: 拿起/放下锚点 → 绕线 → 在双性接口换线 → 连回插座孔 → 通关. 目前只能由门面的断言与 Debug 按钮间接观察.

## TODO

- TODO: **拾取-携带-放下-吸附**未实现. `Anchor.Pickup` 目前只记一条 warning. 未实现原因: 那是 Player 侧的回路(手持挂点, 携带动画, 放下吸附), 需要他们先提供入口; 按 §4.2 不得先写半个接口.
- TODO: **Player 侧的目标发现未切换**. 现有 `HeXie/Player/PlayerInteraction.cs` 仍按 `EnvInteractionTarget.OperationType` 分派到旧桩 `EnvFacade`; 要切到本子系统的接口, 需要 HeXie 侧改用 `GetComponentInParent<IEnvironmentPickup>()` / `IEnvironmentInteractable>()`. 同样属于跨树改动.
- TODO: **`AnchorInstance` 的持久化与堆叠变更**未实现(无无参构造函数, 无计数修改入口). 未实现原因: 决定这两件事的背包模型归 Player 侧且未定案.
- TODO: **地线的精确语义未定**. 现在地线与零/火走同一条判定路径(必须终止). "破裂地线"的两个端点如何授权、是否需要专用接口, 等玩法需要时再定.
- TODO: **多玩家 / 多 Env 未支持**. `EnvironmentFacade.Current` 与 `heldWire` 都是"每个场景一个", 门面按 Tag 找玩家也只取第一个.
