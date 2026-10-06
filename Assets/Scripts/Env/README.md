# 环境与 Player 接入

`EnvironmentFacade` 是每关的真实回路实现，负责节点扫描、持线状态、插口占用、换线次数、折线路径和通关判定。原示例 `EnvFacade`、请求数据及 `EnvInteractionTarget` 已移除；调用方直接使用以下契约。

## 交互与背包

- `IEnvironmentInteractable`：查询 `CanInteract`，用 `InteractionDetails(actor, target)` 调用 `Interact`；`WirePoint` 使用 `WirePointDetails`。节点通过 `OnInteracted` 通知环境重建回路。事件表示节点已处理请求，插入是否成功仍由极性、占用和持线状态决定。
- `Anchor` 是可反复放置、收回的环境节点，不实现道具拾取接口，也不占用背包。Player 按 **K** 在当前位置创建一个 Anchor，数量不限；有持线时自动将它加入当前绕线路径，无持线时仍可放置。
- Player 按 **J** 处理范围内最近的一个有效目标。目标是 Anchor 时解除其绕线并销毁该节点；其他目标继续按其交互或拾取能力处理。收回 Anchor 不产生背包实例，也不影响再次放置的数量。
- 放置与交互受暂停、死亡及玩家操作锁限制；不再存在 K 切换拾取/交互模式。
- 普通道具仍通过 `IEnvironmentPickup.Pickup(details)` 返回 `IPickupInstance`，由 `PlayerInventory` 保存。`IPickupInstance.SourceObject` 提供原物体；背包的 `DropItem(instance, worldPosition)` 仅在 `TryDrop(position)` 成功后移除条目。

`PlayerInventory.environment` 可显式绑定本关环境，未绑定或引用其他场景时按自身场景查找并缓存。`PlayerMove` 通过该环境绑定 `GetResistance`。场景需有配置完整的 Core；环境扫描与 Tag 查找限定在自身场景。

## 回路与 Inspector 配置

1. 场景放置一个 `EnvironmentFacade`。玩家使用 `Player` Tag，其子挂点使用 `WireAttach` Tag；携带电线的自由端跟随挂点。
2. `PowerSocket.wires` 配置本插座电线，第一根作为开局持线。`Wire` 配置电性、固定端及 LineRenderer；其子 `WirePoint` 与场景 `Anchor` 按交互先后决定折线路径。
3. `PolaritySocket` 按可接受的电性拒绝错误插入。双电性插口接入当前线后交出同插座另一根未终止的线并计一次换线；在原插座闭合时交出下一根未终止线，不计换线。完成最后一根后不再持线。
4. 火线、零线完成终止且换线不超过 `maxSwaps` 才能通关；`requireGround` 开启时还需地线完成。`LevelCleared` 在状态首次变为闭合时触发。重开清除绕线、插入、换线及通关状态。刷新节点保留当前回路状态。
5. 可操作目标需有可被 Player 扫描到的 Collider2D；`Anchor.canInteract` 控制是否允许绕线，不限制收回。`PlayerInteraction.anchorPrefab` 绑定 `Prefabs/Env/Anchor.prefab`，其中包含可见 SpriteRenderer 和 Trigger CircleCollider2D，供 K 放置使用。

`Wire.maxLength` 是整条绕线路径的长度上限，**0 表示不限长**。`EnvironmentFacade.GetResistance` 累加固定端、所有折点和玩家挂点之间的长度；严格超过上限时返回 `Vector2.negativeInfinity`，由 `PlayerMove` 在物理帧调用 `Die()`。未超限（含恰好等于上限）、无持线或不限长时返回零。`pullStrength` 仅为旧资源兼容保留，不再参与超限处理。

`PlayerMove.Died` 在玩家死亡、锁定输入、清零速度、关闭物理模拟并停用物体后触发一次。Env 当前没有订阅该事件，也没有死亡处理回调；超限死亡不会自动重置回路或重开关卡。

## 地面极性

在 Ground 的非 Trigger `Collider2D` 所在物体或其父物体上添加 `GroundPolarity`，在 Inspector 设置 `Polarity`，并确保碰撞体所在层包含在 `PlayerMove.groundLayers` 中。无需为普通地面添加组件；禁用组件即可关闭该地面的极性判定。

`PlayerMove` 每个物理帧检查脚下向上的支撑接触，并从自身关卡环境读取当前持线：`Live`（火线）与 `Neutral`（零线）互为相反极性，踩到相反极性的地面时调用现有 `Die()`。同极、未持线、`None` 和 `Ground`（地线）安全；组合极性包含相反电性时仍会死亡。侧墙、天花板和 Trigger 不算踩地。站立期间换线会在下一次物理帧重新判定，输入锁不免除危险；死亡沿用单次 `Died` 通知，不自动重开关卡。

## 场景与验证

在 Unity Test Runner 运行 `PluggerHead.EditModeTests` 和 `PluggerHead.PlayModeTests` 可自动复用下方检查及实际场景流程；MCP 调用、用例范围和隔离规则见 `Assets/Tests/README.md`。

- `Scenes/Tests/HeXieTestScene.unity`：真实 Player、输入、背包与回路集成场景，用于实际移动及 J/K 操作冒烟测试。
- `Scenes/Tests/JillTestWireScene.unity`：保留独立回路诊断场景及模拟玩家，不是完整 Player 操作场景；可用 EnvironmentFacade 的调试按钮驱动交互和验收。
- Edit Mode 执行菜单 **Tools > PluggerHead > Verify Player and Environment**，或调用 `EnvironmentIntegrationChecks.Run()`。验证在独立未保存的预览场景中运行真实组件，覆盖 Anchor 放置/收回、J 最近目标选择、操作限制、绕线、阻力、极性拒绝、换线、通关及重开，并清理测试对象；包含跨场景隔离和无效 Actor 检查。这不替代 Play Mode 场景冒烟测试。
- 关闭同项目 Editor 后可批处理运行：`Unity.exe -batchmode -projectPath <项目根目录> -executeMethod EnvironmentIntegrationChecks.RunBatch -quit -logFile <日志路径>`。使用项目指定 Unity 2022.3.43f1c1，并检查退出码及日志中的 PASS。
