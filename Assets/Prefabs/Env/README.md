# 环境预制体

逐文件核查日期：2026-10-06。八个 prefab 及 meta 已分别核对；均为独立 prefab，无 Variant 或嵌套 prefab 继承关系，外部引用可解析。

| 文件 | 实际组件与配置 |
| --- | --- |
| `Anchor.prefab` | Anchor、SpriteRenderer、Trigger CircleCollider2D；canInteract=true，局部半径0.3、缩放0.5。用于在当前 tile 手动放置并固定路径，不是背包物品。 |
| `MockPlayer.prefab` | SpriteRenderer 与 WireAttach 子挂点；根 Tag=Player、缩放0.2，子 Tag=WireAttach、缩放5。没有 Player 脚本、刚体或碰撞体；图片引用 NaughtyAttributes 示例中的 icon-github.png。 |
| `SceneRoot.prefab` | EnvironmentFacade，Player Tag，不要求地线、maxSwaps=1；不包含 Core、玩家或完整关卡。 |
| `Sockets/PowerSocket.prefab` | PowerSocket、SpriteRenderer、非 Trigger PolygonCollider2D，缩放(0.5,7,1)，canInteract=true、isGroundTerminal=false，**wires 为空**，需场景实例配置。 |
| `Sockets/DualSocket.prefab` | PolaritySocket、SpriteRenderer、非 Trigger CircleCollider2D，accepted=3（Live/Neutral）、canInteract=true；半径0.5、缩放3，独立实例世界半径1.5。 |
| `Wires/Wire.prefab` | Live 电性的基础线，功能配置与 LiveWire 基本相同；当前未发现场景引用。 |
| `Wires/LiveWire.prefab` | Live=1，橙红渐变。 |
| `Wires/NeutralWire.prefab` | Neutral=2，蓝色渐变。 |

三个 Wire 均含 Wire、LineRenderer 和初始禁用的 Trigger EdgeCollider2D；共用 `Visual/Env/Wire.mat`，线宽0.045、世界坐标、不闭合。maxLength=0（不限），重复 tile 通过 Wire 的 overlapColor 显示，没有 WirePoint 子物体；固定端取所属 PowerSocket 的格子位置。保存的线段是占位数据，环境重绘实际路径后更新碰撞体；颜色不会随 polarity 自动改变。

CircuitDiagnostics 使用 SceneRoot、MockPlayer、插口、Live/Neutral 与 Anchor，并以场景覆盖填写电线列表和绑定路由 Tilemap。GameplayIntegration 使用 Anchor prefab，但其环境、插口和线直接写在场景内；修改这些 prefab 不会自动同步那些场景对象。

这些资源不是全都只用于查询：Socket 的非 Trigger 碰撞体可能形成实体阻挡。实例化时检查位置与父级缩放，不将保存的 Transform 当作通用关卡坐标；PowerSocket 不会创建线；已归属电线以插座格子为固定端。MockPlayer 对第三方示例图片有依赖，移除包示例前需同步替换引用。

接入与行为边界见 [Env 实现](../../Scripts/Env/README.md)。最近场景测试已通过，但不等于每个 prefab 均已单独实例化验证；Tilemap 改造删除了 Corner prefab，保留其他 prefab 的 GUID。
