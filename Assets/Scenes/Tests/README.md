# 功能场景

| 场景 | 配置与用途 |
| --- | --- |
| `GameplayIntegration.unity` | 真实 Player、Core、设置/死亡重开 UI、双线回路、Routing Tiles 和 Ground Tiles。主菜单 New Game 的首关。背包与 InventoryUI 保存在 feature/player-inventory。 |
| `CircuitDiagnostics.unity` | Core、真实 Player、双线回路、Routing Tiles 和 Ground Tiles；可直接用 A/D/Space/J/K 操作。 |
| `SceneSwitchTarget.unity` | 相机和 AudioListener，无 Core；验证切换后共享服务保活。 |

两个回路场景使用 Rectangle Grid，原点 `(0,-0.5,0)`，格子尺寸 1。Routing Tiles 覆盖 x=-16..16、y=-10..10，使用无碰撞 RoutingTile；插座及 Anchor 位于格心。Gameplay 的 Ground Tiles 覆盖 x=-10..9、y=-4，使用 Grid 碰撞类型的 GroundTile 和 TilemapCollider2D。Corner、WirePoint 已移除。

Gameplay 的 Player 初始位置 `(0,0,0)`，J 操作/收回、K 在当前 tile 放 Anchor。PowerSocket 为 `(-5.5,-3,0)`，Anchor 为 `(-0.5,-3,0)`，DualSocket 为 `(5.5,-3,0)`。线序保持 Neutral、Live，初始持 Neutral；两根线的长度上限均为 64 世界单位。路径按玩家经过的四连通格子记录，原路回退逐格收线，Anchor 固定此前路径。按全部插座端口接线及电压达标判定通关，不限制换线次数。默认地面没有 GroundPolarity。

CircuitDiagnostics 已改用 `Assets/Prefabs/Player.prefab`，出生位置 `(-2.5,0.25,-0.05)`，沿用移动、跳跃和 J/K 操作。新增 Ground Tiles 覆盖 x=-9..8、y=-1，并带 TilemapCollider2D，避免真实玩家落出关卡。线序与长度上限同上。调试按钮会修改当前回路，不能把缺少节点时允许跳过的脚本化验收当作完整测试。

本次场景结构、真实玩家 Tilemap 落地、J/K、回退、换线、重开及死亡检查已通过。完整记录见 [测试说明](../../Tests/README.md)，画面见 [TilemapEnvironment](../../Docs/Development/TilemapEnvironment.png)。真实键盘输入和发布构建未在本次验证。

2026-10-07：诊断场景真实 Player 替换已通过 EditMode 55/55、PlayMode 76/76（含新增落地与 J/K 交互检查），独立复审通过。[场景预览](../../Docs/Development/CircuitDiagnosticsPlayer.png)为编辑器相机渲染，运行时行为由上述测试验证。
