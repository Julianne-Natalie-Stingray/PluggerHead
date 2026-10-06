# 输入服务与 2D 拖拽

本目录提供设备输入查询/事件和可选拖拽辅助类。实际绑定源与生成代码在 [Infra/InputSystem](../../Infra/InputSystem/README.md)，角色移动、拾取和 Anchor 规则由 Player/Env 实现。

## 逐文件职责

| 文件 | 职责 |
| --- | --- |
| InputManager.cs | Core 上的组件，管理私有 PlayerControls，公开原始查询和按钮事件。 |
| DragAndDropService2D.cs | 普通可序列化类；宿主注入依赖，订阅鼠标事件，按物理帧调用 Drag。不是可直接挂载的组件。 |
| 两个脚本 .meta | 保留 GUID，无默认引用或自定义执行顺序；InputManager 在 Core.prefab 的脚本引用有效。 |
| README.md / .meta | 本目录总文档与资源标识。 |

## InputManager

Awake 创建 PlayerControls；OnEnable 订阅事件并启用 Gameplay；OnDisable 先退订再禁用 Gameplay；OnDestroy Dispose。无 Inspector 绑定字段，不公开完整生成集合。依赖 Core 的调用方不应假定自己的 Awake 晚于 Core 初始化。

| 接口 | 当前源绑定/事件 |
| --- | --- |
| PointerPosition | 鼠标屏幕坐标 Vector2；拖拽服务读取它。 |
| MovementInput | A/D 的 X 输入，Y 未绑定；PlayerMove 读取它。 |
| IsPrimaryPressed / IsUpPressed | 鼠标左键/Space 当前按住查询；controls 未创建时为 false。 |
| PrimaryPressed / PrimaryReleased | PrimaryPress.performed / canceled。 |
| UpPressed / UpReleased | Up.performed / canceled；当前为 Space。 |
| SecondaryPressed / TertiaryPressed | J/K 的 performed，当前不提供对应释放事件。 |

位置/移动查询在 controls 未创建时返回 Vector2.zero。本服务不按 GameState 禁用输入，不拦截 UI、不实现重绑定；消费者负责暂停、输入锁和动作意义。生成包装当前只有键盘和鼠标绑定，不代表其他设备自动可用。

## DragAndDropService2D

Initialize(input, rb, cld, camera) 先 Disable 旧订阅并结束旧拖拽，再保存引用；未传相机时仅在此刻尝试 Camera.main。Enable 订阅 PrimaryPressed/Released，对同一个已订阅输入重复调用为空操作；Disable 成对退订并 EndDrag。宿主需要在自己的禁用/销毁路径调用 Disable，通常在 FixedUpdate 调用 Drag。

BeginDrag 检查 allowDragging、已有拖拽、依赖非空以及 Collider2D.OverlapPoint 命中，随后缓存 bodyType、切 Kinematic，并把线速度/角速度清零。retainDragOffset 控制是否保留抓取点偏移。Drag 通过 MovePosition 跟随鼠标；EndDrag 只恢复原 bodyType，不恢复开始前速度。下一次调用 Drag 时，若 allowDragging 为 false 或 input/body/camera 丢失才会结束；修改字段本身不会触发清理。运行中相机替换不会自动重新查找。

## 当前边界与缺口

- 当前未发现业务脚本实例化/调用这个拖拽服务；需要宿主主动接入才会工作。
- 仅禁用 InputManager 不等于 Disable 拖拽服务。Manager 先退订 canceled 再禁用动作，不会转发这一释放；宿主遗漏清理时可能保持 IsDragging 和 Kinematic。
- BeginDrag 是公开方法，不要求鼠标实际按住，也不检查 GameState、UI 遮挡或宿主启用状态。多个重叠目标订阅同一事件时没有统一选中仲裁。
- 鼠标换算把世界 Z 差的绝对值作为 ScreenToWorldPoint 深度，适用于常规沿 Z 观察 XY 的 2D 布局；倾斜相机没有通用射线与拖拽平面求交保证。
- 不在拖拽期间持续检查 dragCollider 是否仍启用/存在；绑定的刚体和碰撞体也应由宿主保证属于同一目标。

## 核查与验证（2026-10-06）

已逐一检查两个脚本及 meta，交叉核对生成输入 JSON、Core prefab、PlayerMove/交互调用和测试实现，再创建此文档。InputManager XML 中“公开完整 actions”“没有消费者”等旧说明已修正，没有修改运行逻辑。

SceneGameplayTests 经 SceneIntegrationChecks 反射调用 Secondary/Tertiary 处理器，验证事件到玩法的链路；它绕过真实按键和设备绑定。未发现鼠标拖拽、禁用时拖拽清理或真实设备输入的自动化覆盖，不能把现有集成测试通过扩展为这些边界已验证。

独立复审指出 allowDragging 的清理时机须表述为“下一次 Drag 调用”，已修正。本轮最终脚本编译后 Console 无 error，顺序通过 EditMode job `ffde23500166454aad0d97b5b4ec39f2`（10/10）和 PlayMode job `cb4f5995eafe45c3aaad666f473cb1cf`（14/14），运行工作区包含同期 GameState/Setting 修复和新增测试。没有改变输入绑定或拖拽逻辑。
