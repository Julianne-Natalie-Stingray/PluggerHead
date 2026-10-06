# 游戏状态

本目录维护 `Playing`、`Freezed`、`Loading` 标签，以及暂停时的时间倍率和音频监听器状态。业务代码通过静态 `GameStateManager` 访问，不需要挂载组件；这不代表整个项目无需装配 Core。管理器没有 AudioManager 引用，但通过 `GameLog` 输出日志。

## 逐文件职责

| 文件 | 行为 |
| --- | --- |
| `GameState.cs` | 定义枚举，默认零值为 Playing；枚举本身不执行状态变化。 |
| `GameStateManager.cs` | 保存 Current、冻结前倍率和加载前标签，提供四个转换方法及 Changed 事件。 |
| `README.md` | 本目录契约、调用约束与检查结论。 |

对应 `.meta` 保存资源 GUID，无 Inspector 默认引用或执行顺序配置。静态字段随脚本域初始化，不随切场景重置；没有 `RuntimeInitializeOnLoadMethod`。当前项目关闭了 Enter Play Mode Options，因此常规进入播放会重载域；以后若禁用 Domain Reload，不能假定 Current、缓存倍率和事件订阅自动清空。

## 转换规则

| 调用 | 忽略条件 | 实际变化 |
| --- | --- | --- |
| `Freeze()` | Current 是 Loading（静默）或 Freezed（记录 Info） | 缓存当前 timeScale，设为 0，设 AudioListener.pause 为 true，再改为 Freezed 并广播。 |
| `Resume()` | Current 是 Loading（静默）或 Playing（记录 Info） | 恢复缓存倍率（初始为 1），解除监听器暂停，再改为 Playing 并广播。 |
| `EnterLoading()` | Current 已是 Loading | 缓存当前标签，改为 Loading 并广播；不直接写倍率或监听器。 |
| `ExitLoading()` | Current 不是 Loading | 还原缓存标签并广播；不检查 Unity 的加载操作，也不直接写倍率或监听器。 |

`Apply` 先赋 Current，再记录日志并同步调用 Changed。事件没有异常隔离或重入保护；订阅者应成对退订，不在回调中递归转换状态。Loading 期间 Freeze/Resume 均为空操作，不改标签、倍率缓存或监听器，也不广播事件；ExitLoading 还原加载前标签。它不是多层暂停的计数器。

正常的 `Playing → Freeze → Resume` 会恢复进入暂停前的倍率，包括非 1 倍速。缩放时间为零不停止 Update 或使用 unscaled time 的任务。音频监听器暂停也不等于全部声源停止，忽略监听器暂停的声源可继续播放。

## 与场景、音频和设置的关系

`SceneSwitchManager` 在开始异步加载后调用 EnterLoading，放行激活并等到 `AsyncOperation.isDone` 后调用 ExitLoading；不是在首个可见帧的精确瞬间切换状态。Loading 不自行恢复倍率，所以从 Freezed 进入 Loading 时，倍率仍为 0。

当前生产代码中 Changed 的订阅者是 `AudioManager`，在 OnEnable/OnDisable 成对订阅；它按 `state == Freezed` 设置监听器暂停。因此 Core 存在且音频组件订阅时，`Freezed → Loading` 会解除监听器暂停，`ExitLoading → Freezed` 才重新暂停。没有该订阅者时，EnterLoading/ExitLoading 都保留原监听器值。不能把管理器“不直接写音频”描述成全系统“不影响音频”。

音频的新请求仅在 Freezed 时受冻结入口条件限制，Loading 会绕过该条件，但仍受配置、音频实例上限和池容量限制，不保证所有请求成功。非循环声的结束判定使用 unscaled Timer；具体声音的暂停、生存和释放属于 [Core](../../Core/README.md) 的实现。

`SettingsScreen.Open` 拒绝在 Loading 时打开，只在 Playing 时取得暂停所有权；关闭时仅恢复自己取得的暂停。返回主菜单先关闭面板释放暂停，再请求加载。因此现有菜单流程通常从 Playing 进入 Loading，不依赖“冻结中切场景仍持续静音”。

## 行为检查结论与已知缺口

2026-10-06 已分别核查本目录所有文件，再核对 SceneSwitchManager、AudioManager、SettingsScreen、EditorSettings 和现有测试调用。

- 已修复检查指出的交错调用缺陷：Loading 期间忽略 Freeze/Resume，保留加载前状态与冻结前倍率。新增 GameStateTests 覆盖 Playing/Freezed 两种起点、重复 EnterLoading 与交错暂停/恢复，退出加载后恢复原标签，最终 Resume 恢复 0.5 倍速。
- `Freezed → Loading` 的监听器解除暂停是当前 AudioManager 联动行为；若玩法要求冻结加载期间一直静音，需要另行实现并验证该契约。
- Changed 的异常/重入与关闭 Domain Reload 后的状态遗留没有自动防护。

原文档检查仅修正文档和 XML 注释；后续修复改变了上述 Loading 守卫。现有主菜单 PlayMode 测试覆盖设置面板暂停/恢复与返回菜单后的 Playing、正倍率、未暂停监听器；新增 GameStateTests 专门覆盖上述交错调用；订阅者异常或关闭 Domain Reload 不在该用例覆盖内。完整集成回归按 `PluggerHead.EditModeTests`、`PluggerHead.PlayModeTests` 顺序运行；运行结果须以本次 Test Runner job 为准，不能从测试源码存在推断通过。

本次 Unity 临时探针确认重复 Freeze 后 Resume 可恢复 0.5 倍速，交错 Loading/Freeze 序列则复现零倍率；探针在 finally 恢复原有静态字段、倍率与监听器状态。最终注释版本编译后 Console 无 error，EditMode job `1e2e42ea301c468198bf3f48e81930ca` 通过 7/7，随后 PlayMode job `644096a808d74b91bfb8da18e91d38e0` 通过 14/14。较早的 PlayMode job `40575a87385946479ba34d739db0a41a` 因检查期间脚本重载丢失执行器而中断，已记为失败，不作为通过证据。这是修复前的验证记录，不能作为后续行为修复的通过证据。


## 检查问题修复验证（2026-10-06）

Loading 守卫、设置数据校验、临时文件替换和 UI 保存后即时应用已通过独立代码审查。Unity 编译完成后 Console 无编译错误；依次运行 EditMode job 3e84e015f6db440190779233af42d88e（10/10）和 PlayMode job 607888f664274ec8a6e726cae021d5d2（14/14），均已结束并通过。设置替换失败用例通过独占锁制造预期错误并确认旧文件字节不变；实际混音器验证使用临时设置存储，结束后恢复原存储和混音参数。
