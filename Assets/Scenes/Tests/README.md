# 功能场景

| 场景 | 配置与用途 |
| --- | --- |
| `GameplayIntegration.unity` | 真实 Player、Core、背包/设置 UI、双线回路、Routing Tiles 和 Ground Tiles。主菜单 New Game 的首关。 |
| `CircuitDiagnostics.unity` | Core、MockPlayer、双线回路与 Routing Tiles；用于独立电路诊断，没有真实玩家控制器。 |
| `SceneSwitchTarget.unity` | 相机和 AudioListener，无 Core；验证切换后共享服务保活。 |

两个回路场景使用 Rectangle Grid，原点 `(0,-0.5,0)`，格子尺寸 1。Routing Tiles 覆盖 x=-16..16、y=-10..10，使用无碰撞 RoutingTile；插座及 Anchor 位于格心。Gameplay 的 Ground Tiles 覆盖 x=-10..9、y=-4，使用 Grid 碰撞类型的 GroundTile 和 TilemapCollider2D。Corner、WirePoint 已移除。

Gameplay 的 Player 初始位置 `(0,0,0)`，J 操作/收回、K 在当前 tile 放 Anchor。PowerSocket 为 `(-5.5,-3,0)`，Anchor 为 `(-0.5,-3,0)`，DualSocket 为 `(5.5,-3,0)`。线序保持 Neutral、Live，初始持 Neutral；两根线的长度上限均为 64 世界单位。路径按玩家经过的四连通格子记录，接触任意未固定旧格时截断后续路径，Anchor 固定此前路径。环境不要求地线、最多换线一次。默认地面没有 GroundPolarity。

CircuitDiagnostics 的 MockPlayer 只有显示与 Player/WireAttach 标记，可在播放中移动 Transform 观察格子路径及重复 tile 的颜色提示；没有鼠标拖拽或移动控制器。线序与长度上限同上。调试按钮会修改当前回路，不能把缺少节点时允许跳过的脚本化验收当作完整测试。

本次场景结构、真实玩家 Tilemap 落地、J/K、回退、换线、重开及死亡检查已通过。完整记录见 [测试说明](../../Tests/README.md)，画面见 [TilemapEnvironment](../../Docs/Development/TilemapEnvironment.png)。真实键盘输入和发布构建未在本次验证。
