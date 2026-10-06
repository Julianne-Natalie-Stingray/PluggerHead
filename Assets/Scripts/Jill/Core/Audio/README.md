# Core/Audio 子系统

## 职能

音频总线负责"把一次播放请求变成一次真实发声", 并管住并发与生命周期.

- 作为启动声音的**唯一**入口: 调用方只经 `AudioBuilder` 提交请求.
- 解析 `AudioId` 到 `AudioClipData`, 并拥有 emitter 对象池与活跃注册表.
- 拥有两级实例上限及其抢占策略.
- 拥有每次播放的作用域句柄 `ISoundHandle`.
- 负责一次播放的实时效果: 音量, 音高, 位置与跟随, 以及淡入淡出.
- 负责把玩家持久化的音量推送到 mixer 的总线参数上(读 `Setting`, **单向**依赖).
- 不负责 clip 的静态特征(归 `AudioClipData`)与总线配置(归 `AudioManagerConfigs`).
- 不负责游戏状态本身: 它只**订阅** `GameStateManager.Changed`, 而 `GameState` 对音频一无所知.
- 不负责"该不该播放"的玩法判断; 那是调用方的决定.
- 不负责"音量改动自动生效": 目前没有变更通知, 改完需调用 `ApplyAudioSettings()`(见本文 TODO).

## 公共API

- `CoreFacade.Audio` -> `AudioManager`: 音频总线本体.
- `AudioManager.CreateBuilder()` -> `AudioBuilder`(struct, 单次请求的链式凭证):
  - `WithVolume` / `WithPitch` / `WithRandomPitch` / **`WithFade`** / `WithPosition` / `WithFollowTarget` /
    `WithSurviveFreeze` / `WithAllowWhileFrozen`
  - `.Play(audioId)` -> `ISoundHandle`; 请求被拒绝时返回 `null`.
- `ISoundHandle`: `AudioId` / `IsPlaying` / `IsFinished` / `Finished` / `Stop()` / `TrySetVolume` / `TrySetPitch`.
- `CoreFacade.Audio.ApplyAudioSettings()`: 把 `Setting` 里的 `MasterVolume` / `OstVolume` / `SfxVolume`
  推送到 mixer 的三个暴露参数上. 初始化时自动调用一次; 玩家改完音量后由调用方显式调用.
- `AudioManager.Registry` -> `AudioRegistry`: 只读的事实查询, 供需要知道"现在在播什么"的代码使用.
- 数据定义: `AudioClipData`(单个 clip), `AudioManagerConfigs`(总线参数), `AudioId`(查找键).
- `AudioEmitter` 是池化组件, 由池创建与销毁, 只对外暴露单次播放的开关与查询.

## 内部实现思路

### 一次请求的路径

检查冻结期入口 -> 解析 `AudioId` -> 应用每 clip 上限 -> 应用全局上限 -> 预定 emitter -> 注册 -> 配置 -> 定位 -> 启动.
两级上限的抢占都只做一件事: 停止最旧且**非循环**的受害者; 其余工作交给常规结束路径,
因此池归还, 注册表注销与句柄失效都只发生在一处.

### 上限模型

| 项 | 载体 | 含义 |
| --- | --- | --- |
| 每 clip 上限 | `AudioClipData.MaxInstances` | 设计意图: 同一 clip 同时最多几个 |
| 全局上限 | `AudioManagerConfigs.MaxSoundInstance` | 保护总声部数 |
| 池大小 | `AudioManagerConfigs.MaxPoolSize` | 只界定保留多少 emitter 用于复用 |

三者互不相同, 且 `MaxPoolSize >= MaxSoundInstance` 是硬性关系, 否则超出的 emitter 会被销毁而不是回收.

**循环音永不被抢占**, 且抢占只从**非循环**的候选里挑最旧的: `AudioRegistry.TryGetOldest(..., includeLooping: false)`.
若触顶时**所有**同类实例都在循环(全局上限同理), 则**拒绝该请求并记一条 warning**, 而不是静默突破上限.
这一点是 2026-10-05 A1 交付修掉的真实缺陷: 旧实现选中循环受害者后直接返回, 既不重试也不拒绝, 于是上限形同虚设.

抢占是**硬切**, 不走淡出: 它必须在同一帧释放池槽位(池的"能否复用"判定看的是空闲实例数), 而淡出做不到这一点.
因此"淡出"与"抢占"是两件事, 不要混为一谈.

### 淡入淡出

- 数据: `AudioClipData.FadeIn` / `FadeOut`(秒, 默认 `0`, 即原先的硬起与硬切); 请求可用 `AudioBuilder.WithFade(fadeIn, fadeOut)` 覆盖.
- 实现: `AudioEmitter` 在 `LateUpdate` 中以 `Time.unscaledDeltaTime` 推进渐变, 因此 `Time.timeScale = 0` 时仍然推进;
  渐变系数是**乘在**播放音量上的, 因此渐变途中调用 `TrySetVolume` 不会在渐变结束后被覆盖.
- 三处用法:
  1. **起播淡入**: 从静音升到基线音量.
  2. **尾部淡出**: 非循环 clip 播到末尾前 `FadeOut` 秒开始降音. 判定直接读 `AudioSource.time`,
     不额外排程, 因此上一次播放遗留的排程不可能给后一次播放启动渐变.
  3. **优雅停止**: `AudioEmitter.RequestStop()`(由 `SoundHandle.Stop()` 使用)先降音再停止.
- 代价与边界: 带淡出的停止会让 emitter 在渐变期间保持被占用, 只有声明了淡出的 clip 才付这个代价;
  `FadeOut` 不小于 clip 长度时忽略该淡出, 而不是从第一帧就开始降音.

### 玩家音量到总线

`AudioSettings` 的三个值是**线性 0–1**, 而 mixer 的音量参数以**分贝**表示, 换算写在 `AudioManager.ApplyBusVolume`:
`v <= 0.0001 ⇒ -80 dB`(取 mixer 自身的静音阈值), 否则 `20 * log10(v)`. 默认配置 `1 / .5 / .5` ⇒ `0 / -6 / -6 dB`.

mixer 引用与三个参数名都来自 `AudioManagerConfigs` 的 `Bus Volume` 组 —— 它们是**参数**, 因此按 §5.2.1 用
序列化字段 + Inspector 赋值表达(mixer 是资产, 无法 `GetComponent<>()`). 参数名是**数据**而不是代码常量,
所以在 AudioMixer 窗口改名无需改代码.

依赖方向是**单向**的: 音频读 `SettingBootstrap.Settings`, 而 `Setting` 对音频一无所知.

> **注意 (2026-10-05)**: 本节依赖的 `SettingBootstrap` / `GameSettings` / `GameStateManager` 现已位于 `HeXie/` 树
> (`Assets/Scripts/HeXie/Game/Setting/` 与 `Assets/Scripts/HeXie/Game/GameState/`), 且属**纯移动**(逐字节相同).
> 因此本子系统**无需改动**, 但它现在**跨树依赖 HeXie 的代码**: 若那边改了这些类型的 API, 受影响的是这里.

`ApplyAudioSettings()` 在初始化时调用一次; 它是公开的, 因为"改了音量立刻听见"需要调用方主动触发 ——
**变更通知目前不存在**, 因此这是本子系统已知的一个缺口(见 TODO).

### 归还链路

池归还有**两个方向**, 必须分开, 否则会互相递归:

- **归还输出**: `OnEmitterRelease` 作为池的归还回调运行, 只做清理(注销, 重置, 重挂父级, 日志), **绝不**调用池的 `Release`.
- **归还输入**: `ReleaseEmitter` 订阅在 `AudioEmitter.onAudioFinished` 上, 只做一件事 —— 转发给 `GameObjectPool.Release`.

`onAudioFinished` 是**所有**结束路径的唯一汇合点: 自然播完, 句柄 `Stop()`, 以及被抢占.
这条链一旦断掉, emitter 就会永久滞留在池外.

结束判定由一个 Timer 完成, 并使用 `UseUnscaledTime()`: `GameStateManager.Freeze()` 会设 `Time.timeScale = 0`,
走 scaled time 的判定在暂停期间不会推进 —— 若不清除这一处, 暂停期间所有 emitter 都将永不归还池. 这两处是一对, 不能只保留其一.

订阅是**先退订再订阅**的: emitter 是池化复用的, 若只订阅不退订, 复用会让处理器累积, 导致同一次结束被多次归还.

### 与游戏状态

`AudioManager` 在 `OnEnable` / `OnDisable` 中订阅 `GameStateManager.Changed`, 据状态暂停或恢复 `AudioListener.pause`.
依赖方向是**单向**的: 音频认识 `GameState`, 而 `GameState` 对其一无所知.

冻结涉及**两个互相独立**的问题, 合并会得到错误的模型:

| 问题 | 含义 | 机制 |
| --- | --- | --- |
| 进入冻结时, 已在播放的音是否被掐断 | 一次性的保留决定 | `AudioSource.ignoreListenerPause` |
| 冻结期间, 新到达的请求是否被受理 | 一道入口门 | `AudioManager.Play` 的入口检查 |

`SurviveFreeze` 回答第一个问题, 且**与 `Loop` 无关**: 循环音可以被冻结掐断, 一次性音也可以熬过冻结.
`WithAllowWhileFrozen` 回答第二个问题, 默认**拒绝**并记日志; 它存在的意义是让"冻结期间必须被拒绝"的请求得以表达.

两者**不是同一个开关的正反两面**: 一个已在播放的音可以被保留, 而一个新的请求仍被拒绝; 反之亦然.

### 位置与空间化

位置来源有三种, 优先级明确:

| 来源 | 入口 | 说明 |
| --- | --- | --- |
| 跟随目标 | `WithFollowTarget(Transform)` | 每帧同步到目标; 目标消失后按下一节规则处理 |
| 一次性坐标 | `WithPosition(Vector3)` | 只写一次世界坐标 |
| 都不给 | —— | emitter 停在池留下的位置, 即 `AudioRoot` |

两者同时给出时**跟随胜出**. `willFollowTarget` 表示"本次播放是否请求了跟随", 与 `followTarget == null`("当前没有目标")必须分开,
否则每个不跟随的声音都会被误判为跟丢了.

位置同步发生在 `LateUpdate`, 且**不**改变父子关系: emitter 始终挂在 `AudioRoot` 下.
因此跟随的声音不会随目标销毁而消失 —— 一次性音在目标最后出现的位置播完, 循环音则在目标消失时停止.

空间化由 `AudioClipData` 的 `SpatialBlend` / `MinDistance` / `MaxDistance` 描述, 由 `AudioEmitter.Configure()` 写入 `AudioSource`.
注意 `SpatialBlend == 0` 时位置**完全无影响**; 而 `SpatialBlend > 0` 时若既不跟随也不给坐标, 空间化形同无效.

## TODO

- TODO: 音量改动**不会自动生效**. 改完 `AudioSettings` 之后必须显式调用 `AudioManager.ApplyAudioSettings()`;
  要做到"改完即生效", 需要 `Setting` 侧提供一个变更通知(例如 `AudioSettings` 或 `SettingStore` 上的事件)并由音频订阅,
  而那是 `Game/Setting` 这个**另一个交付单元**的改动, 按 §4.2 必须先问再写.
- TODO: 单条总线的独立静音 / 独奏未实现. 未实现原因: 目前没有玩法需求, 且它同样需要 mixer 侧先暴露对应参数.
- **已裁决不做**（2026-10-05，选项甲）: 抢占时的淡出. 原因: 它需要一种"正在淡出的 emitter 不再占用池槽位"的记账模型,
  而那属于 `Core/GameObjectPool`(另一个交付单元); 强行做会把"抢占并服务"退化成"抢占后仍被拒绝".
  因此淡出只存在于**起播 / 尾部 / 优雅停止**三处, 抢占**永远是硬切**.
- TODO: `com.grignardreagent.timer` 无异常隔离: 某个回调抛异常会杀死整条 Timer 且 `IsRunning` 永久为真.
  未实现原因: 修复位于 `Assets/Packages/`(§2.2 禁写区), 只能由用户裁决. 详见 `AudioEmitter.Play` 中的注解.
