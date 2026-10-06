# Player 运行时组件

Player 使用 [Core 输入](../Core/Input/README.md) 和 [Env 真实交互契约](../Env/README.md)，通过关卡环境取得持线阻力和地面极性上下文。当前真实玩家直接配置在 `Assets/Scenes/Tests/GameplayIntegration.unity`，不是独立 Player prefab。

## 逐文件职责

| 文件 | 行为 |
| --- | --- |
| PlayerMove.cs | Rigidbody2D 水平移动、落地跳跃、阻力/地面危险、死亡与输入锁，设置 Animator 参数。 |
| PlayerAnimationCallbacks.cs | 按 Animator 状态标签聚合动作，通知 PlayerMove 进入/退出动画锁；不负责位移。 |
| PlayerInteraction.cs | 订阅 J/K 输入，查找最近目标、发起交互/拾取或放置 Anchor。 |
| PlayerInventory.cs | 保存真实 IPickupInstance，委托拾取/放下，按访问时机清理已销毁源对象。 |
| InventoryUI.cs | uGUI 槽位显示，从源对象读取 Sprite，复用/扩展槽位并低频检查图标变化。 |
| 各脚本 .meta | GUID 与 GameplayIntegration 的组件引用一致，无默认引用；PlayerAnimationCallbacks 的 -100 执行顺序来自源码属性，meta 未覆盖。 |

## 初始化与当前场景配置

玩家需 Dynamic Rigidbody2D 和非 Trigger Collider2D。PlayerMove 要求刚体，在 Awake 缓存刚体和子 Animator，并补齐/同步 PlayerAnimationCallbacks；Start 优先保留显式 GetResistance 委托，否则从 PlayerInventory.Environment 绑定。缺环境查询仅记录错误，之后不会应用正常水平移动或跳跃；缺 Core/Input 则禁用组件。

PlayerInteraction 要求 PlayerInventory，在 Start 获取 Core.Input 并确认环境存在，缺失时禁用。重新启用只重新订阅已缓存输入，不自动重走失败的 Start。输入事件均成对订阅/退订。PlayerInventory.Environment 的显式引用若为空或属于别的场景，就查询自身场景的 EnvironmentFacade；同场景已有引用不会因为另一个环境出现而自动替换。

GameplayIntegration 中移动速度为 5、跳跃速度为 8、重力倍率为 1；groundLayers 和 interactionLayers 当前为全部层，交互半径 2，背包最少显示 8 格。InventoryUI 的 inventory、slotsRoot、slotTemplate 已装配。Anchor prefab 引用为 `Assets/Prefabs/Env/Anchor.prefab`；Player Tag 供 Env 采样根位置；历史 WireAttach 子挂点已不参与线逻辑。动画控制器为 `Assets/Visual/Player/PlayerAC.controller`。

## 移动、跳跃与死亡

当前输入绑定为 A/D 水平移动、Space 跳跃、J 操作、K 放 Anchor。PlayerMove 在 FixedUpdate 将输入 X 钳到 -1..1，乘非负 moveSpeed 写入水平速度，保留竖直速度；不再乘 fixedDeltaTime。随后通过 AddForce 施加 GetResistance 返回值。移动动画 tryMoving 依据输入幅度，不是实际位移。

跳跃仅缓存按下时已落地的请求，执行物理帧再次检查落地；没有空中按下后自动落地起跳的缓冲。接地需要 groundLayers 内非 Trigger 接触、法线 Y≥0.65，且当前竖直速度≤0.1。有效跳跃将竖直速度设为非负 jumpSpeed。没有有效阻力查询时，正常分支将水平置零并保留原竖直速度，计算出的跳跃也不会应用。

FixedUpdate 先检查地面极性，再查询阻力，再处理输入锁。踩到与持线相反的有效 GroundPolarity 会死亡；阻力 X/Y 同时为负无穷也会死亡，普通非有限值不属于完整校验范围。线长规则与地面极性组合详见 Env 文档。

Die 首次设置 IsDead、手动锁输入、清空线速度/角速度、关闭刚体模拟、停用玩家，最后同步触发 Died。重复调用不重复通知；没有复活 API，也不自动重开关卡。Died 订阅者异常没有逐项隔离。Die 假定 Awake 已完成，不能当作可在任意初始化阶段调用的无依赖函数。

## 输入锁与动画

手动锁、交互动画锁为两个独立布尔来源，任一个生效即 IsInputLocked。锁会清除待跳跃请求并停止用输入覆盖速度，但保留当前速度、继续施加阻力；它不是冻结物理，地面与线长危险也不会被免除。禁用 PlayerMove 才会清除水平速度并保留竖直运动。

Animator 需要 bool 参数 tryMoving、trigger 参数 Interact；交互状态标签为 PlayerInteract。Callbacks 在 OnEnable、FixedUpdate 和 LateUpdate 同步，聚合当前及过渡目标状态；基础层始终检查，其他层仅权重>0 时检查。聚合后仅在交互状态变化时通知输入锁；禁用回调组件释放它跟踪的动画锁，不清除手动锁。Animator 引用仅在为空/已销毁时重新查询，替换控制器对象布局后应重新确认绑定。

TryStartInteractionAnimation 在 J 操作被认为成功后请求；操作本身先执行，动画请求失败不会回滚操作。K 放置不主动请求交互动画。

## J/K 操作的判定

两类操作都要求组件 activeAndEnabled、timeScale>0，且存在 PlayerMove 时未死亡/未锁输入；没有 PlayerMove 组件时不额外拒绝。它们不直接检查 GameState 标签，Loading 也不一定会自动屏蔽输入。

J 在自身 PhysicsScene2D 中查询半径内碰撞体，包括 Trigger，排除其他场景、玩家自身子层级以及碰撞体同物体上有 Wire 的情况。对命中物体及父级 MonoBehaviour 检查拾取/交互能力，以碰撞体 ClosestPoint 到玩家的平方距离选择最近候选；相等时用较小实例 ID 稳定选择，保证只适用于当前会话。没有视线遮挡检测，也不会在最近目标操作失败后继续尝试第二个目标。

Anchor 优先 TryReclaim；普通目标优先 CanPickup/PickUpItem，其次 CanInteract/Interact。交互载荷统一使用 InteractionDetails。普通交互临时订阅 OnInteracted，以同步回调是否发生作为返回成功的依据，finally 退订；这表示节点处理了请求，不保证最终插接或通关成功。异步才发事件不会被此次调用捕获，订阅者异常也可能向外传播。

K 将玩家当前 XY 格子投影到已绘制 tile 中心，再实例化 anchorPrefab，移动到玩家场景、启用并注册；有持线时请求 Anchor.Interact。返回 true 表示完成放置，不保证 prefab 禁止绕线时也能挂线。空格拒绝放置；没有库存上限、消耗、位置占用或地形重叠检查。手动 Anchor 收回会销毁节点而不产生背包物品。Corner 自动 Anchor 已随 Tilemap 改造移除。

## 背包与 UI

直接调用 PickUpItem/DropItem 也要求背包组件 activeAndEnabled、timeScale>0，且存在 PlayerMove 时未死亡/未锁输入；这些限制不只存在于 J/K 入口。

PickUpItem 要求传入对象实现接口且是 Component、同场景、GameObject activeInHierarchy、CanPickup=true、尚未按源物体收录。直接调用本 API 不另查该 MonoBehaviour.enabled；J 候选扫描则会查。收到非空实例后直接加入并触发 Changed，不验证返回 SourceObject 是否与请求源一致，也不使用 MaxStackAmount/CurrentStackAmount 做堆叠、数量显示或容量限制。

DropItem 只在实例属于当前背包且 TryDrop(position) 返回 true 后移除并通知；放下位置合法性和对象恢复由实例负责。Items 是实时只读包装而非快照；Items/Count/Contains/DropItem 等访问会删除 null 实例或已销毁 SourceObject，并可能同步触发 Changed。没有跨场景持久化、自动丢弃全部或事件异常隔离。

当前项目业务脚本中没有 IEnvironmentPickup/IPickupInstance 的具体实现；Anchor 明确不实现拾取。通用背包路径是保留的扩展接口，不能把 Anchor 操作测试当作完整拾取/放下验证。

UI 订阅 Changed 请求刷新，并每 0.1 秒非缩放时间检查 Sprite。从 SourceObject 的首个子 SpriteRenderer（包括 inactive）读取 sprite；无 Sprite 时关闭 Icon Image，背景样式由场景槽位决定。槽位数显示 max(minimumSlots, itemCount)，只扩建并隐藏多余槽，不销毁缩减；slotTemplate 需具有名为 Icon 的子物体及 Image。slotsRoot 直接子物体中有 Icon 和 RectTransform 的才被缓存，模板可也是现有槽之一。

UI 不提供拖放/点击放下或堆叠数字。minimumSlots 的 Min(1) 属性不提供运行时钳制。运行时换 inventory 可以自动重绑；换 slotsRoot/template 或外部删除已缓存槽位没有完整缓存重建保护。缺 root/template 时 Refresh 提前返回，无法补齐错误配置。

## 核查与验证（2026-10-06）

逐一检查五个脚本及 meta，再核对输入源、Env 接口、GameplayIntegration 组件与动画资源、现有测试。原脚本注释与正常路径基本一致，本轮只新增总文档和 meta，未改代码或资源行为。

EnvironmentIntegrationChecks 覆盖最近 Anchor、J/K、操作锁、环境隔离和超长死亡；SceneGameplayTests 在真实场景走 Anchor/回路流程及超长死亡；GroundPolarityTests 验证真实接触、极性组合、墙顶/Trigger/排除层、站立换线和锁定时死亡。场景流程会等待交互动画解锁，但不能证明所有动画进入/过渡/多层时序正确。没有发现真实 A/D/Space 输入、跳跃、完整通用背包/UI 及异常订阅者的专项测试。

最近已完成的回归为 EditMode 17/17、PlayMode 27/27（job `1d96f6f716264310af0df0e75726f731`、`21336b3daa794474982d80666cea66c0`），覆盖当时的 Player 与场景代码。该结果不验证上面列出的缺口或其后并行修复；纯文档新增无需重跑 Unity 测试。两位独立审查者复核后已补齐直接背包 API 的操作限制和 Inspector 范围属性边界；文档链接有效，新增 GUID 唯一，未留下文档审查问题。
