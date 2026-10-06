# Player 动画状态资源

逐文件核查日期：2026-10-06。四个动画片段均为 60 fps、0–1 秒范围，曲线与事件为空，是状态和时间占位资源，不是已制作好的角色视觉动画。

| 文件 | 当前用途 |
| --- | --- |
| `PlayerIdleAnim.anim` | 循环 Idle 占位，不改变外观或位置。 |
| `PlayerMoveAnim.anim` | 循环 Move 占位，没有行走或位移曲线。 |
| `PlayerDashAnim.anim` | 非循环 Dash 占位，不产生冲刺位移。 |
| `PlayerInteractAnim.anim` | 非循环 Interact 占位，不直接执行交互。 |
| `PlayerAC.controller` | 单 Base Layer、默认 Idle；bool `tryMoving` 控制 Idle/Move，trigger `Dash`、`Interact` 进入动作，动作 ExitTime=1 返回 Idle。过渡时长均为0，无 AnyState 过渡或 StateMachineBehaviour。 |

Dash/Interact 状态的标签为 `PlayerDash`/`PlayerInteract`，与 PlayerAnimationCallbacks 的锁判断一致；锁依据标签而不是状态名。GameplayIntegration 引用此控制器，使用普通时间更新、始终更新模式并关闭 Root Motion。实际持续时间受 timeScale、Animator speed 和启用状态影响，不能承诺一秒真实时间后解锁。

meta、片段与控制器、场景引用已核对。角色物理运动由脚本控制，现有代码没有因为存在 Dash trigger 就自动提供冲刺输入或位移。详见 [Player 行为](../../Scripts/Player/README.md)。本轮仅新增说明；现有测试覆盖部分动作锁与帧推进，不证明动画视觉效果或完整运动输入。
