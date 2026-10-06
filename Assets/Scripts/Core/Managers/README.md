# 音频请求管理器

本目录唯一运行脚本为 `AudioManager.cs`，对应 meta 保留其身份。它是 Core 上的 MonoBehaviour，持有 AudioRegistry 和 GameObjectPool<AudioEmitter>，正常业务入口为 `CoreFacade.Instance.Audio.CreateBuilder().With...().Play(id)`。Builder、句柄和声部的详细契约见 [Audio](../Audio/README.md)。脚本 GUID 与 Core prefab 一致，无特殊执行顺序或默认资源引用。

## 初始化与配置

Awake 先创建 Registry，然后检查 emitterPrefab；缺失时记录错误并禁用整个 GameObject。emitterRoot 未配置则警告并使用自身 Transform。configs 缺失时记录错误并禁用组件；成功时根据配置创建池。Start 才执行 Prewarm 和 ApplyAudioSettings。失败后补齐引用并重新启用不会自动重走 Awake 建池。

`Assets/Prefabs/Core/Core.prefab` 已配置 emitterPrefab、子物体 AudioRoot 和 `Assets/SO/Audio/DefaultAudioManagerConfigs.asset`；默认初始容量/预热数为 10，池容量/全局声部上限为 30。配置、映射、Mixer 参数和容量有效性见 [SODefinitions](../Audio/SODefinitions/README.md)。

池在 Awake 建立，使其他组件 Start 中的请求可以使用它，但各组件 Start 的先后顺序不固定：已有声部借出时再预热，会额外借出并归还 PrewarmAmount 个对象，不是只补齐池的空缺。预热直接调用通用池，不经过正常请求的容量检查。

## 请求与拒绝

Play 依次检查 enabled/configs/池、解析 AudioId 与实际 clip、数值参数、冻结入口、同 ID 上限、全局上限、池容量，再借出、注册、配置、定位并启动声部。返回 ISoundHandle 或 null。检查 enabled 不等于检查 activeInHierarchy；初始化后只让 Core 失活仍可能通过公开方法发起请求，但子声部无法正常活动，不应依赖该用法。

| 拒绝原因 | 当前日志 |
| --- | --- |
| 组件禁用、缺配置、池尚未建立 | 本次 Play 静默返回 null；初始化可能已有日志。 |
| 未找到 AudioId 映射 | Configs 与 ResolveClip 各记录 Error。 |
| 找到 AudioClipData 但其中 Clip 为空 | 静默返回 null。 |
| 默认或请求音量/音高为 NaN，或淡入淡出时长非有限 | Warning 后返回 null，在抢占与借池前拒绝；音量/音高无穷值仍按原规则夹取。 |
| Freezed 期间没有同时允许进入并忽略监听器暂停 | Info 后返回 null。 |
| 实例上限没有可抢占候选或池容量耗尽 | Warning 后返回 null。 |

限流先按请求 ID，再按全部注册项检查；每级至多硬停一个最旧的非循环声部，全部候选循环或没有候选则拒绝。任一实例上限非正时直接拒绝，不先抢占已有声部。同 ID 抢占后立即复查；全局抢占后、放行前再次检查两种当前上限。MaxPoolSize 除了底层闲置保留上限，还通过 ReserveEmitter.CanReuse 限制正常请求扩容。它小于全局上限时可能更早拒绝请求。

victim.Stop 同步触发 Finished，回调可重入播放或更改配置。复查读取当前 AudioClipData.MaxInstances 与 Configs.MaxSoundInstance，不使用抢占前的上限快照；若回调占回名额、全局抢占回调填满请求 ID，或动态降限后一次抢占仍未腾出空间，外层请求返回 null，不继续追逐新候选。降低上限不会立即清空多余旧声部；本次请求每级仍最多抢占一次，拒绝后允许剩余旧声部继续播放。后续检查失败不恢复之前已经抢占的声音，不提供事务回滚。

## 归还与销毁

ReleaseEmitter 是完成事件的输入，只调用池 Release；OnEmitterRelease 是池归还回调，只退订、注销、ResetEmitter、重挂 emitterRoot 并清零局部位置，不能在其中再次 Release，否则递归。正常完成链先归还池，后通知句柄；带淡出的句柄 Stop 则提前使句柄失效，实际声部仍占槽至尾音结束。

OnDestroy 只调用池 Clear，清理闲置池对象；不主动为所有借出声部完成停止/通知。默认 Core 父子层级销毁会销毁子物体，但外部销毁声部不等于句柄收到 Finished。仅销毁管理器组件而保留声部或使用外部 emitterRoot，会留下不安全的生命周期组合。池的边界见 [GameObjectPool](../GameObjectPool/README.md)。

## 游戏状态与音量设置

OnEnable 订阅 GameStateManager.Changed，OnDisable 退订；启用时不主动同步 Current。通知为 Freezed 才设置 AudioListener.pause=true，其他状态（包括 Loading）设置 false。这个回调不停止单个声部；SurviveFreeze 映射为 ignoreListenerPause。冻结期入口另外要求 AllowWhileFrozen=true 且有效 SurviveFreeze=true。暂停中加载会暂时解除监听器暂停，需按实际玩法验证这一组合。

ApplyAudioSettings 读取当前 `SettingBootstrap.Settings.Audio`，逐个设置配置指定的 Master/Ost/Sfx 参数。小于等于 0.0001 时取本类常量 -80 dB，其余进入 `20*log10(value)`（包括 NaN）；不额外完整校验任意内存数值。Start、Play Mode 调试按钮、SettingsScreen 保存成功后会调用它；直接改属性或存储层 Save 不自动应用。

缺 Mixer 时 Error 并返回；空参数名或 SetFloat 失败记录 Warning。方法没有返回成功汇总，个别参数失败后仍会输出 Bus volumes applied，因此该 Info 不能证明三个参数均成功。正常调用还要求设置自举完成。保存与数据默认值见 [Game/Setting](../../Game/Setting/README.md)。

两个 NaughtyAttributes 按钮均检查 Application.isPlaying。Test Audio Request 每次新建 Builder，按 requestTime 请求并显式覆盖两个冻结开关；它不持有返回句柄。编辑器 OnValidate 将非正 requestTime 改为 1。这些是手动入口，不是回归测试。

## 核查与验证（2026-10-06）

### 后续功能修复

独立代码与测试审查通过，编译后 Console 无错误。完整集成回归按顺序通过 EditMode 17/17、PlayMode 37/37（job `75a538ebe47a405cb9355c44af47c9bb`、`2358194029c74554a63ff3e7630d4284`），其中包含新增 10 项限流用例。

本轮补齐同步完成回调重入和动态降低实例上限后的复查，并对非正上限前置拒绝。新增 `AudioLimitIntegrationChecks` / `AudioLimitTests`，通过真实 Manager、ISoundHandle、Registry 与池的隔离夹具覆盖：同 ID/全局重入占回名额、全局回调填回请求 ID、回调内降低请求 ID 上限、调用前降低同 ID/全局上限、普通最旧候选抢占、循环保护、非正上限不抢占。测试只创建临时对象和音频数据，清理后恢复监听器暂停及后台运行状态；不保存资产或启动管理器的 Mixer 应用。最终编译和专项/集成测试结果由本轮主任务收尾记录。

下方为文档核查当时的历史说明；其中“未改运行逻辑”和“源码去除注释后相同”仅描述该文档核查所做的编辑，不概括整个并行工作区或提交。历史文档提交 `ffbf244` 同时包含数值请求校验：默认或请求音量/音高 NaN、非有限渐变在限流与借池前拒绝。当前 Play XML 已同步这个检查顺序；历史 job 结果仍保留其原始范围。

### 修复前文档核查

逐一检查源码及 meta、配置和 Core prefab 引用，并核对 GameState、Setting、Audio、通用池和测试。修正“始终记录拒绝日志”“池只限制闲置数”“冻结直接释放”“没有其他设置应用调用”等过时双语注释，未改运行逻辑。

AudioIntegrationChecks 覆盖默认/覆盖参数、停止、复用、旧 Timer、自然完成和循环淡出；MainMenuIntegrationChecks 检查真实 Mixer 三个参数；AudioConfigurationIntegrationChecks 检查 MaxPoolSize 规范化。没有覆盖超限抢占重入、预热与抢先播放交错、失活/销毁活跃管理器、冻结组合、日志失败误报或失败后重新初始化。

独立复审通过，已按意见修正目录文件范围、NaN 转换分支和 Start 注释。源码去除注释/空白后与原版本一致。编译完成且 Console 无错误；依次运行 EditMode 17/17、PlayMode 27/27，job 为 `1d96f6f716264310af0df0e75726f731`、`21336b3daa794474982d80666cea66c0`。该工作区包含并行新增的音频尾淡出/句柄检查；这些通过不代替上述管理器边界专项验证。
