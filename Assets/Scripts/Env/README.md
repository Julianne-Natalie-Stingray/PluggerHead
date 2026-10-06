# 环境与 Player 接入

`EnvironmentFacade` 是每关的真实回路实现，负责节点扫描、持线状态、插口占用、换线次数、折线路径和通关判定。原示例 `EnvFacade`、请求数据及 `EnvInteractionTarget` 已移除；调用方直接使用以下契约。

## 交互与背包

- `IEnvironmentInteractable`：查询 `CanInteract`，用 `InteractionDetails(actor, target)` 调用 `Interact`；`WirePoint` 使用 `WirePointDetails`。节点通过 `OnInteracted` 通知环境重建回路。事件表示节点已处理请求，插入是否成功仍由极性、占用和持线状态决定。
- `IEnvironmentPickup.Pickup(details)`：成功返回 `IPickupInstance`，拒绝返回 `null`。`Anchor` 拾取时解除绕线并隐藏原物体，不生成替身、不销毁原物体。
- `PlayerInventory` 保存返回的实例。`IPickupInstance.SourceObject` 指向原场景物体，`TryDrop(position)` 恢复它；`PlayerInventory.DropItem` 仅在放置成功后移除背包条目。当前 `AnchorInstance` 不可堆叠、不支持存读档；源物体销毁后背包会清理失效条目。重复放置、无效坐标、暂停或玩家操作锁定不会丢失物品。
- Player 按 **J** 处理范围内最近的一个有效目标；**K** 切换同时可拾取、可交互目标的拾取/交互模式，默认拾取。仅有一种能力的目标直接使用该能力。K 本身不会向 Env 发出请求。放置由背包的 `DropItem(instance, worldPosition)` 入口处理。

`PlayerInventory.environment` 可显式绑定本关环境，未绑定或引用其他场景时按自身场景查找并缓存。`PlayerMove` 通过该环境绑定 `GetResistance`。场景需有配置完整的 Core；环境扫描与 Tag 查找限定在自身场景。

## 回路与 Inspector 配置

1. 场景放置一个 `EnvironmentFacade`。玩家使用 `Player` Tag，其子挂点使用 `WireAttach` Tag；携带电线的自由端跟随挂点。
2. `PowerSocket.wires` 配置本插座电线，第一根作为开局持线。`Wire` 配置电性、固定端及 LineRenderer；其子 `WirePoint` 与场景 `Anchor` 按交互先后决定折线路径。
3. `PolaritySocket` 按可接受的电性拒绝错误插入。双电性插口接入当前线后交出同插座另一根未终止的线并计一次换线；在原插座闭合时交出下一根未终止线，不计换线。完成最后一根后不再持线。
4. 火线、零线完成终止且换线不超过 `maxSwaps` 才能通关；`requireGround` 开启时还需地线完成。`LevelCleared` 在状态首次变为闭合时触发。重开清除绕线、插入、换线及通关状态。刷新节点保留当前回路状态。
5. 可操作目标需有可被 Player 扫描到的 Collider2D；`Anchor.canInteract` 与 `canPickup` 独立配置。拾取期间两种能力均关闭，放回后恢复。

`Wire.maxLength` 是整条绕线路径的长度上限，**0 表示不限长**。`EnvironmentFacade.GetResistance` 累加固定端、所有折点和玩家挂点之间的长度；严格超过上限时返回 `Vector2.negativeInfinity`，由 `PlayerMove` 在物理帧调用 `Die()`。未超限（含恰好等于上限）、无持线或不限长时返回零。`pullStrength` 仅为旧资源兼容保留，不再参与超限处理。

`PlayerMove.Died` 在玩家死亡、锁定输入、清零速度、关闭物理模拟并停用物体后触发一次。Env 当前没有订阅该事件，也没有死亡处理回调；超限死亡不会自动重置回路或重开关卡。

## 场景与验证

在 Unity Test Runner 运行 `PluggerHead.EditModeTests` 和 `PluggerHead.PlayModeTests` 可自动复用下方检查及实际场景流程；MCP 调用、用例范围和隔离规则见 `Assets/Tests/README.md`。

- `Scenes/Tests/HeXieTestScene.unity`：真实 Player、输入、背包与回路集成场景，用于实际移动及 J/K 操作冒烟测试。
- `Scenes/Tests/JillTestWireScene.unity`：保留独立回路诊断场景及模拟玩家，不是完整 Player 操作场景；可用 EnvironmentFacade 的调试按钮驱动交互和验收。
- Edit Mode 执行菜单 **Tools > PluggerHead > Verify Player and Environment**，或调用 `EnvironmentIntegrationChecks.Run()`。验证在独立未保存的预览场景中运行真实组件，覆盖拾取/放置、J/K 选择逻辑、绕线、阻力、极性拒绝、换线、通关及重开，并清理测试对象。当前覆盖 47 项检查（含跨场景隔离和无效 Actor）；这不替代 Play Mode 场景冒烟测试。
- 关闭同项目 Editor 后可批处理运行：`Unity.exe -batchmode -projectPath <项目根目录> -executeMethod EnvironmentIntegrationChecks.RunBatch -quit -logFile <日志路径>`。使用项目指定 Unity 2022.3.43f1c1，并检查退出码及日志中的 PASS。
