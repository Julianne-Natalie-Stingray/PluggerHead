# 音频请求、声部与句柄

本目录提供 AudioManager 使用的请求、播放和记账类型。业务通过 `CoreFacade.Instance.Audio.CreateBuilder().With...().Play(id)` 发起请求，并处理可能返回的 null；池容量、映射与抢占策略由 `../Managers/AudioManager.cs` 决定。

## 逐文件职责

| 文件 | 当前行为 |
| --- | --- |
| `AudioBuilder.cs` | 可变 struct，请求参数可空；With 修改接收者并返回副本，Play 委派管理器。 |
| `ISoundHandle.cs` | 业务侧控制契约：AudioId、IsPlaying、IsFinished、Finished、Stop、TrySetVolume/Pitch。 |
| `SoundHandle.cs` | 普通对象，由管理器创建；订阅 emitter 完成事件，锁存原因，失效后拒绝控制请求。 |
| `AudioEmitter.cs` | 池化 MonoBehaviour，需要 AudioSource；配置、播放、跟随、渐变、Timer 结束检测及完成事件。 |
| `AudioRegistry.cs` | 普通记账对象，按注册顺序统计和查询仍存在的 emitter，不验证实际播放状态。 |
| 各脚本 `.meta` | 保留 GUID，无特殊执行顺序或默认引用。AudioEmitter GUID 与 `Assets/Prefabs/Core/AudioEmitter.prefab` 对应。 |

子目录分别说明 [音频标识](Enum/README.md)、[玩家音量](Setting/README.md) 和 [开发者配置](SODefinitions/README.md)。Timer 的运行依赖 TimerRunner；正常 Core 预制体提供它。

## Builder 值语义

每次请求使用新的 CreateBuilder。Play 在 manager 有效时先设置当前副本 isUsed，再调用管理器，因此即使请求被拒绝，此副本也已消费；无 manager 的默认结构体返回 null 而不消费。已消费副本忽略 With，重复 Play 返回 null。

消费状态不跨副本共享。例如 `var b = audio.CreateBuilder(); b.WithVolume(0.5f).Play(id);` 的 With 修改 b，但 Play 发生在返回的临时副本上，b 本身仍可 Play。预先复制也能分别发起请求。它不是防重复播放的全局凭证，不应缓存并反复使用同一个 Builder。

未覆盖的音量、音高、SurviveFreeze 和渐变取 clip 配置；AllowWhileFrozen 未设置时为 false。WithRandomPitch 调用时立即抽样，加到当前 pitch 覆盖值上；若未设置则以 1 为基准，并非 clip 的默认 pitch。重复调用会累加，之后 WithPitch 会覆盖先前结果。最终音量/音高由 Manager/Emitter 钳制到 0..1 / 0.1..3；正常 Manager 入口在抢占和借出前拒绝 NaN 音量/音高与非有限渐变时长（包括配置默认值），无效请求返回 null；音量/音高的正负 Infinity 沿原规则钳制。位置和直接内部接口仍由调用方遵循输入契约。

位置与跟随同时提供时，管理器先写坐标再设目标，跟随胜出。目标为空时管理器不启用跟随。冻结期新请求需要 AllowWhileFrozen 为 true 且有效 SurviveFreeze 为 true；后者控制 ignoreListenerPause，不意味着切断其他声音。

## 播放、停止与通知顺序

管理器借出 emitter → 注册 → Configure → 应用请求覆盖/定位 → 订阅池归还回调 → 创建并订阅 SoundHandle → Play。Registry 的数量是已注册且 Unity 对象仍存在的条目数，所以包括尚未启动以及句柄已结束、但仍在淡出的声部。

非循环 Play 启动无限时长的非缩放 Timer，以 `!source.isPlaying` 作为完成条件；完成时分类为自然结束。它不是“确实播到 clip 末尾”的证明，外部直接停止 AudioSource 也可能被如此分类。循环声没有自然结束 Timer。停止、重置和销毁取消旧 Timer，正常复用不继承旧结束排程。

Emitter.Stop 立即停止，分类为非自然结束；RequestStop 按本次有效 FadeOut 渐变，正数时保留池槽直到停止完成。SoundHandle.Stop 则立即使句柄失效并触发 Finished，即使尾音还未结束。因此 Finished 表示句柄结束，不能作为“此刻已有空闲池槽”或“已经完全静音”的通用信号。失效后的 Stop 返回 false，TrySetVolume/Pitch 返回 false。

Emitter.Complete 先锁存状态，再按订阅顺序逐个调用 onAudioFinished。正常管理器路径先归还池、注销和 ResetEmitter，后通知 SoundHandle；ResetEmitter 保留 IsFinished 至下一次 Play，供句柄锁存。SoundHandle 先锁存失效及原因，再退订，最后同步触发 Finished。AudioId 和锁存的 IsFinished 在失效后仍可读。

IsPlaying 是有效句柄与音源播放状态的组合；返回 false 不证明完成回调已执行。TrySetVolume/Pitch 对 NaN 返回 false 并保留旧值，正负 Infinity 沿原规则钳制。TrySetVolume 成功表示写入已接受，实际音量通常在 LateUpdate 应用；Pitch 直接写 AudioSource。音量渐变乘在当前音量上。

## 跟随与渐变

SetFollowTarget 初次立即定位，之后 LateUpdate 同步世界坐标，不将 emitter 挂到目标下面。目标丢失时一次性音停留在最后位置继续播放，循环声硬停释放。没有位置或目标时仍使用 emitter 当前 Transform；管理器归还时重挂 AudioRoot 并清零局部位置，首次创建还受 prefab 初始 Transform 影响。

淡入与主动 Stop 的渐变按非缩放时间推进，时间缩放或监听器暂停不会阻止主动停止归池。自然尾部独立按 `(clip.length - source.time) / Pitch` 的剩余实际秒数计算包络，进入 FadeOut 秒窗口才淡出；开始前 FadeOut 必须短于 `clip.length / Pitch` 整段播放时间。已进入尾部后不因改 Pitch 取消，音量包络单调不增：减速不会回升，加速会按更近的片尾收束。音源暂停时自然包络保持，播放头完成后归零也不会抬高音量。尾部优先于尚未结束的淡入，从进入时有效音量继续下降；手动 Stop 从当时有效音量开始非缩放渐变。总音量取淡入/主动停止与自然尾部包络的较低值。

ResetEmitter 清空 clip、loop、data、跟随及渐变状态，恢复音量/音高和 ignoreListenerPause；它不清空完成事件、MixerGroup、空间参数或 Transform。正常复用依靠管理器重定位和下一次 Configure 补齐这些参数。

## 公开内部接口的边界

这些类型的公开方法并不都具有业务入口的防护。应经 Manager/Handle 使用正常流程，并注意当前未修复的边界：

- Registry 允许重复注册同一 emitter，一次 Unregister 只删除首项；Compact 清除空槽和已销毁对象，Register 会自动调用它，也可显式调用。TryGetOldest 按最小序号选择，可按 ID 筛选或跳过循环，策略由管理器决定。
- Configure(null) 先清空 data 后报错返回，不清空先前 AudioSource.clip；直接调用后再 Play 可能播放残留 clip。正常 Manager 入口先验证 data/clip。
- 外部直接销毁 emitter 时 OnDestroy 只取消 Timer，不主动触发完成通知。旧句柄的 IsPlaying 返回 false，音量/音高操作返回 false；Stop 不访问已销毁组件，将句柄按中断失效并只通知一次 Finished，返回 false。公开 SoundHandle 构造器仍不验证 null 或播放归属。
- Emitter.onAudioFinished 和 SoundHandle.Finished 均逐订阅者隔离异常并记录；某个 Finished 订阅者抛异常不会遗漏后续通知，也不会传播给 Stop 调用方。句柄仍先锁存和退订，再开始通知。
- 没有播放代次标识来防止任意回调重入或直接内部接口操作；常规旧句柄失效测试通过，不代表所有重入或外部误用下都能保证复用隔离。

## 核查与验证（2026-10-06）

后续功能修复已通过独立代码审查和完整回归：EditMode 17/17、PlayMode 27/27（job `a16cd78f2e594cb3b3f2570a393dbeb5`、`96c79817858e4fd0a584cf0a995784cc`）。新增 AudioHandleTests 验证自然完成和主动停止的异常订阅者隔离、NaN 句柄参数拒绝、无效 Manager 请求不会抢占已有声部；新增 AudioTailTests 验证下述尾部契约。以下保留此前文档审计记录，其“仅说明修改”不描述这次功能修复。

逐一核对本目录五个脚本、对应 meta、三个子目录说明，以及 AudioManager、Emitter prefab 和 AudioIntegrationChecks。修正单次凭证、回调先后、空间化和重置范围的双语注释，仅改变说明，不修改运行逻辑。

现有音频检查验证默认音量/音高、请求覆盖、停止幂等、Timer 取消、顺序复用、旧句柄拒绝修改、真实单次播放完成和循环淡出释放。新增 AudioTailTests 覆盖固定/变化音高的自然尾包络、淡入重叠、暂停及归零播放头、循环/零时长/整段时长忽略、尾部中主动 Stop 起点，以及 .5/2 倍速和播放中改 Pitch 的真实自然完成、归池复用；还验证时间缩放和监听器暂停时，循环声主动停止仍按非缩放时间归池。该暂停用例保持 Playing 标签，不覆盖 Freezed 入口矩阵。AudioHandleTests 覆盖 Finished 异常订阅者隔离、声部先销毁后的句柄控制，以及 Environment.OnDisable 对旧音乐句柄的清理（零淡出和正淡出），后续 AudioLimitTests 覆盖完成回调重入播放及动态限流，详见 [Managers](../Managers/README.md)。现有检查仍未覆盖 Builder 副本消费、随机音高顺序、Registry 重复注册/排序、跟随丢失或任意内部接口操作下的重入隔离。新增测试结果以本轮 Test Runner 报告为准。

以下是自然尾部修复前的文档审计记录，不作为新增 AudioTailTests 的通过证据：两位独立审查者核对后，修正了 AllowWhileFrozen 默认值被误归入 clip 配置的表述，并补齐 Builder 可覆盖渐变的说明。五个源码去除注释与空白后与提交前一致。编译完成，Console 错误为 0；EditMode 17/17、PlayMode 20/20 通过（job `1d27539ba4e0413e84d3a6e8c6ff2cf1`、`20bbfa5da28d4484b00e6b5f2a3ac3b7`）。这次结果包含并行提交 `1fa318e` 的池配置检查，不等于补齐了上述播放边界测试。该次审计仅记录边界，没有修改自然尾部实现；当前尾部修复见上方契约，需独立验证。
