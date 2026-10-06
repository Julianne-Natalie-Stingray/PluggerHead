# 功能场景

逐文件核查日期：2026-10-06。三个 `.unity` 及 meta 的名称、GUID、SceneId、默认切换配置和 Build Settings 已逐项核对。

| 文件 | 实际用途与配置 |
| --- | --- |
| `GameplayIntegration.unity` | 真实 Player、Core、环境、背包/设置 UI、双线回路及 Ground 四角 Corner。唯一标记为玩法关卡的场景，主菜单 New Game 进入这里。 |
| `CircuitDiagnostics.unity` | Core、相机/监听器、SceneRoot、MockPlayer、两类插口、Anchor、两根线及 WirePoint。用于独立电路诊断，没有真实 Player 控制器。 |
| `SceneSwitchTarget.unity` | 只有 Main Camera 根对象及 Camera/AudioListener/Transform，无 Core 或玩法对象。用于观察从其他场景切换后的服务保活；单独打开只显示背景。 |

## GameplayIntegration

Player 初始位置 `(0,0,0)`，WireAttach 子挂点局部位置为零；Inventory 显式绑定环境，Interaction 使用 Anchor prefab，交互半径 2、层掩码为全部层，仍需通过业务目标筛选。J 处理最近有效目标，K 放置 Anchor，详见 [Player](../../Scripts/Player/README.md)。

PowerSocket 位于 `(-6,-2.7,0)`，电线列表顺序为 Neutral、Live，故初始持线为 Neutral。Neutral 的长度上限为 10，Live 为 0（不限长），固定端未指定时取线对象 Transform。当前开局直线距离小于 10，不是必然立即超长死亡。环境不要求地线，通关要求换线次数不超过 1，双电性接口 accepted=3。

四角使用 Corner prefab，手动与自动 Anchor 使用共享 Anchor prefab；电线、插座与环境本体直接序列化在场景中，不是对应 prefab 的实例。默认场景没有 GroundPolarity 组件；地面极性回归由测试动态建立夹具。

## CircuitDiagnostics

MockPlayer 是带 Player/WireAttach Tag 的显示与挂点层级，不具有玩家移动、输入、刚体或碰撞体。播放时可通过编辑器移动它观察线端；没有已实现的鼠标拖拽玩法。两线同样以 Neutral、Live 排列，均不限长；环境不要求地线、通关要求换线次数不超过 1，debugTarget 指向 DualSocket。

可用 EnvironmentFacade 调试按钮驱动当前回路，或 Core 的音频测试入口检查服务。调试按钮会修改当前状态，不提供隔离，且验收缺少节点时允许跳过；其 PASS 不能替代完整 Player 测试。

## 验证边界

最近 EditMode 17/17、PlayMode 37/37 通过，具体记录见[测试总说明](../../Tests/README.md)；本轮仅新增文档，复用相同资源版本的结果。结构测试可能复用已加载的内存场景。Gameplay 流程测试先临时取消线长限制，再单独设置有限长度验证死亡，不证明完整流程在原始 Neutral 长度 10 下均可完成；实际绕角流程覆盖左上角，其余角有结构检查。

测试不替代真实输入设备、所有 UI 布局、四角逐一游玩或发布构建验证。SceneSwitchTarget 依赖进入前已经存在的持久服务，不能单独证明 Core 保活。
