# 环境预制体

场景组合及其依赖预制体统一放在 `ScenePrefab/`；移动保留原 GUID，Player 和测试场景继续引用同一资源。

逐文件核查日期：2026-10-06。八个 prefab 及 meta 已分别核对；均为独立 prefab，无 Variant 或嵌套 prefab 继承关系，外部引用可解析。

| 文件 | 实际组件与配置 |
| --- | --- |
| `ScenePrefab/Anchor.prefab` | Anchor、SpriteRenderer、Trigger CircleCollider2D；canInteract=true，局部半径0.3、缩放0.5。用于在当前 tile 手动放置并固定路径，不是背包物品。 |
| `ScenePrefab/SceneRoot.prefab` | EnvironmentFacade、Player Tag；检查所有插座端口接线及初始电压减已接降压器是否不高于目标；不包含 Core、玩家或完整关卡。 |
| `Sockets/PowerSocket.prefab` | PowerSocket、SpriteRenderer、非 Trigger PolygonCollider2D，缩放(0.5,7,1)，canInteract=true、isGroundTerminal=false，**wires 为空**，需场景实例配置。 |
| `Sockets/DualSocket.prefab` | PolaritySocket、SpriteRenderer、非 Trigger CircleCollider2D，accepted=3（Live/Neutral）、canInteract=true；半径0.5、缩放3，独立实例世界半径1.5。 |
| `Sockets/GroundSocket.prefab` | 新增地线示例：PolaritySocket.accepted=Ground，绿色 SpriteRenderer、Trigger PolygonCollider2D。 |
| `VoltageReducer.prefab` | 新增降压示例：VoltageReducer.voltageDrop=30，黄色 SpriteRenderer、Trigger PolygonCollider2D。 |
| `Wires/Wire.prefab` | Live 电性的基础线，功能配置与 LiveWire 基本相同；当前未发现场景引用。 |
| `Wires/LiveWire.prefab` | Live=1，橙红渐变。 |
| `Wires/NeutralWire.prefab` | Neutral=2，蓝色渐变。 |

三个 Wire 均含 Wire、LineRenderer 和初始禁用的 Trigger EdgeCollider2D；共用 `Visual/Material/Wire.mat`，线宽0.045、世界坐标、不闭合。maxLength=0（不限），没有 WirePoint 子物体；固定端取所属 PowerSocket 的格子位置。保存的线段是占位数据，环境重绘实际路径后更新碰撞体；颜色不会随 polarity 自动改变。

CircuitDiagnostics 使用 SceneRoot、真实 Player、插口、Live/Neutral 与 Anchor，并以场景覆盖填写电线列表和绑定路由 Tilemap。GameplayIntegration 使用新提取的 Env、Environment Grid、GlobalUI 和 EventSystem prefab，仍通过 Anchor prefab 动态放置锚点。Env 内的插座和线是整体关卡预制体的一部分，与上表独立插口/线 prefab 没有继承关系。

新增 `ScenePrefab/Env.prefab` 保存本关电路布局；`ScenePrefab/Environment Grid.prefab` 保存路由与地面 Tilemap；场景为 Env 绑定该实例的 Routing Tiles。`ScenePrefab/GlobalUI.prefab` 包含菜单按钮、SettingsScreen 和 RestartLevelScreen，已移除 InventoryUI；RestartLevelScreen 保留手动指定的 Player；引用为空时自动查找同场景 Player（包含已死亡而停用的对象），未找到时每 0.5 秒重试。组件菜单 Find Player 可手动执行查找，无需逐场景绑定 player。`ScenePrefab/EventSystem.prefab` 配置 InputSystemUIInputModule。复制这些预制体到其他关卡时，需重新绑定跨预制体的场景引用。

这些资源不是全都只用于查询：Socket 的非 Trigger 碰撞体可能形成实体阻挡。实例化时检查位置与父级缩放，不将保存的 Transform 当作通用关卡坐标；PowerSocket 不会创建线；已归属电线以插座格子为固定端。MockPlayer 预制体已删除；CircuitDiagnostics 使用 `Assets/Prefabs/Player/Player.prefab` 并配置实体 Tilemap 地面。

GlobalUI 左上角 `GameplayHUD` 包含实时剩余线长文本与等宽空白 `ScoreText`。WireLengthDisplay 自动查询同场景当前持线，无需绑定具体 Wire；两个文本均不拦截射线，菜单及模态面板绘制在 HUD 上方。ScoreText 只预留布局空间，尚无计分行为。

GlobalUI 根组件 `NextLevelScreen` 控制默认隐藏的通关面板，包含中文祝贺 TextMeshPro 文本和“下一关”按钮；同场景 Env 的 LevelCleared 事件负责触发显示，无需跨 prefab 绑定 Env。当前下一关配置为 GameplayIntegration，按钮通过 Core.SceneSwitch 重新加载。复制到其他关卡时按需修改 nextLevel，组件应保留在启用的根对象上。

接入与行为边界见 [Env 实现](../../Scripts/Env/README.md)。最近场景测试已通过，但不等于每个 prefab 均已单独实例化验证；Tilemap 改造删除了 Corner prefab，保留其他 prefab 的 GUID。
