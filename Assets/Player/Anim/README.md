# Player 动画状态资源

核查日期：2026-10-07。Idle 和 Move 已接入 `Player/Sprite/pluggerIdle.png`、`pluggerWalk.png` 的序列帧；资源从地图目录迁移时保留 GUID。动画仅驱动 Visual 的 SpriteRenderer，不写角色位置或碰撞体。

| 文件 | 当前用途 |
| --- | --- |
| `PlayerIdleAnim.anim` | 6 帧待机，循环周期 1 秒。 |
| `PlayerMoveAnim.anim` | 4 帧行走，保留 0.5 秒循环周期及 0、0.5 秒的 `PlayMoveAudio` 事件。 |
| `PlayerInteractAnim.anim` | 非循环 1 秒，显式保持待机首帧；保留首帧 `PlayInteractAudio` 事件，不直接执行交互。 |
| `PlayerDeathAnim.anim` | 保留原爆炸序列与 `PlayDeathAudio` 事件。 |
| `PlayerAC.controller` | 单 Base Layer、默认 Idle；bool `tryMoving` 控制 Idle/Move，trigger `Interact` 进入交互，交互仅在 ExitTime=1 返回 Idle。trigger `Die` 从 Idle/Move 进入 Death，Death 无出过渡。过渡时长均为0，无 AnyState 过渡或 StateMachineBehaviour。 |

Interact 状态的标签为 `PlayerInteract`，与 PlayerAnimationCallbacks 的锁判断一致；锁依据标签而不是状态名。GameplayIntegration 引用此控制器，使用普通时间更新、始终更新模式并关闭 Root Motion。实际持续时间受 timeScale、Animator speed 和启用状态影响，不能承诺一秒真实时间后解锁。

Player prefab 默认 Sprite 为待机首帧。角色物理运动由脚本控制，详见 [Player 行为](../Script/README.md)。动画采样与相关玩法测试的结果见本次美术接入记录。
