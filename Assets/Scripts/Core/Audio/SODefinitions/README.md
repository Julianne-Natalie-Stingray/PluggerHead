# 音频开发者配置

本目录定义两种 ScriptableObject，供 AudioManager 与 AudioEmitter 读取。它们是工程内的开发者配置；玩家音量数据位于 [Setting](../Setting/README.md)。CreateAssetMenu 的路径与默认文件名来自生成的 MenuTool 常量。

## 文件职责

| 文件 | 行为 |
| --- | --- |
| `AudioClipData.cs` | 定义 AudioId、实际 AudioClip、MixerGroup、循环、同 ID 实例上限、默认音量/音高、空间参数、冻结选项及渐变时间。只读属性暴露序列化字段。 |
| `AudioManagerConfigs.cs` | 保存音频映射列表、Mixer 与三个暴露参数名、池参数和全局声部上限；提供 TryGetClip 查询与编辑器检查/去重入口。 |
| 两个脚本 `.meta` | 保留脚本 GUID，现有配置资产通过这些 GUID 引用类型；没有特殊执行顺序或默认引用。 |

## AudioClipData 的消费方式

AudioManager 先查询数据并拒绝缺少实际 clip 的请求，再执行冻结期入口、实例上限和池容量检查。AudioEmitter.Configure 把 clip、loop、MixerGroup、SpatialBlend、MinDistance、MaxDistance 写入 AudioSource，同时建立默认音量、音高、渐变和 ignoreListenerPause。Builder 可覆盖单次请求参数；已经开始的播放通过句柄 TrySetVolume/TrySetPitch 修改。

DefaultSurviveFreeze 对应 AudioSource.ignoreListenerPause，与 Loop 独立。false 表示服从监听器暂停，不是直接 Stop 或归还池。冻结期间新请求还需 WithAllowWhileFrozen(true)，并且有效的 SurviveFreeze 为 true，才会被入口接受。

FadeIn/FadeOut 默认 0。正数用于淡入、句柄请求的优雅停止，以及非循环音的尾部淡出；自然尾部检查在淡出时间小于当前音高下的整段播放时长（clip.length / pitch）时启动。自然尾部音量由剩余播放时间决定，减速时不回升，音源暂停时保持；淡入和手动停止仍使用非缩放时间，抢占采用立即停止。字段描述默认参数，不保证某个已播放声部会随着资产编辑自动更新。

空间字段只配置 AudioSource，未强制校验请求是否提供坐标；没有 Position/FollowTarget 时仍可播放，使用 emitter 当前坐标。MaxDistance 不是本项目实现的越界静音判断，距离效果还取决于音源本身的衰减配置；本类型不配置 rolloffMode 或自定义曲线。

编辑器 OnValidate 只显式把负 MaxInstances 夹为 0，并对空 clip、空 MixerGroup 发出警告。Range/Min 是 Inspector 提示或约束，不能当作运行时数据的完整校验；没有检查距离先后关系或所有数值的有限性。空 MixerGroup 不会触发管理器的缺 clip 拒绝分支。MaxInstances 为 0 且当前没有该 ID 声部时，请求会被限流拒绝；它不是“无限制”。

## AudioManagerConfigs 的查询与池参数

TryGetClip 返回列表中首个非空且 AudioId 匹配的 AudioClipData；没有匹配项时记录错误，返回 false/null。它不检查该数据内的 clip 是否存在，后者由管理器播放入口检查。列表本身为 null 时没有保护。Remove Duplicates 是手动编辑器按钮，删除空项及重复 ID，保留首项；不会自动随查询或 OnValidate 执行。

| 参数 | 实际用途 |
| --- | --- |
| CollectionCheck | 传入底层 ObjectPool 的重复归还检查开关。 |
| DefaultCapacity | 底层池容器的初始容量，不会预先实例化这些对象。 |
| MaxPoolSize | 底层池的闲置保留上限；只读属性在运行时将旧序列化或动态配置中的非正值规范到至少 1。管理器 ReserveEmitter 另外通过 CanReuse 用它限制正常请求的总创建量。 |
| PrewarmAmount | Start 中借出再归还的预热数量；预热路径不执行 ReserveEmitter 的容量检查，过大值可临时创建超额对象后销毁。 |
| MaxSoundInstance | 注册表的全局声部上限，与每个 AudioClipData.MaxInstances 分别检查；只抢占最旧的非循环候选。 |

OnValidate 把非正 MaxPoolSize 写回为 1，符合底层 ObjectPool 对正上限的要求；DefaultCapacity、PrewarmAmount、MaxSoundInstance 的负值仍按原规则改成 0。MaxPoolSize 属性本身也保证返回至少 1，但读取不修改原序列化字段。不自动约束 PrewarmAmount 与池上限；建议预热不超过池上限。池上限小于总声部上限时，正常请求可能先被池拒绝。运行时减少实例上限后，当前管理器每次请求仅尝试一次同 ID 抢占和一次全局抢占，不保证立刻把已有声部降到新上限。通用池生命周期见 [GameObjectPool](../../GameObjectPool/README.md)。

Mixer 和 MasterVolume/OstVolume/SfxVolume 参数名通过此资产配置。缺 Mixer 只记录警告，不自动赋值；ApplyAudioSettings 也会检查 Mixer，并对空参数名或 SetFloat 失败记录日志。参数名需与 Mixer 中实际暴露的参数一致。

## 当前资源

`Assets/SO/Audio/DefaultAudioManagerConfigs.asset` 的 DefaultCapacity/MaxPoolSize/PrewarmAmount/MaxSoundInstance 为 10/30/10/30，指向 `Assets/Audios/Mixers/Master.mixer` 和三个正确命名的暴露参数。

映射位于 `Assets/Audios/SO/`：GenericSfx 为 ID 0、非循环、最多 5 个；GenericOst 为 ID 1、循环、最多 1 个且忽略监听器暂停；MouseClick 为 ID 2、非循环、最多 10 个。三者 SpatialBlend 都为 0；前两者淡入/淡出为 0.15/0.3，MouseClick 未序列化的渐变及冻结字段采用源码默认 0/false。音频标识的整数兼容性见 [Enum](../Enum/README.md)。

## 核查与验证（2026-10-06）

本轮修复 MaxPoolSize 非正配置导致池构造异常的缺口，同时覆盖编辑器写回与运行时只读规范化，不调整其他池参数。新增 `AudioConfigurationTests` 参数用例，在独立、未保存的 AudioManagerConfigs 对象中写入 0、-1、int.MinValue、1、30，经实际 MaxPoolSize 属性构造 Unity ObjectPool 并借还对象，再调用真实 OnValidate 检查字段写回和其他参数不变；finally 销毁临时配置。实际 Test Runner 结果由主任务收尾记录。

### 修复前核查记录

逐一检查两个源码及 meta，并核对默认配置、三个音频数据资产、Emitter 预制体、Manager/Emitter/Builder、通用池和 AudioIntegrationChecks。纠正了冻结即切断、Builder 实时修改和最大距离保证不可听的注释/Inspector 提示；本轮不改变播放逻辑或资产数据。

音频集成检查覆盖请求默认值与覆盖、顺序播放和复用、句柄失效、自然结束及渐变等正常路径。后续新增 AudioLimitTests 检查超限抢占/拒绝、完成回调重入补位、动态降低上限及循环保护；抢占后仍没有名额时拒绝外层请求，不保证立刻将已有声部降至新上限。空映射列表、超额预热或距离衰减边界仍未覆盖，限流修复的运行结果见 [Managers](../../Managers/README.md)。

本轮编译完成后 Console 无错误；EditMode job `c6fd2108319e4b33bdef27b7515d1a85` 结果 12/12 通过。PlayMode job `5f3bf863270b461a81fb0f8f9208c05a` 终态 succeeded、完成 20/20、失败列表为空；再次查询仍未返回 result 汇总，因此保留这一工具返回限制，不把它写成拥有完整逐项结果的报告。测试包括当时并行工作的场景恢复与 Debug 检查，不证明之后新增的音频设置修复已被本轮覆盖。

另用临时托管对象调用当前 Unity 2022.3.43f1c1 的 `ObjectPool<object>` 构造器，传 maxSize=0，实测抛出 ArgumentException（Max Size must be greater than 0）。此探针没有修改场景、配置资产或创建 Unity 对象。独立审查提出的测试覆盖表述过宽问题已修正。

### 池容量修复验证

独立代码审查通过。编译后无编译错误，依次完成 EditMode job `c5b179b30af94251856a5b93ca858ec1`（17/17）与 PlayMode job `4181c9b49e374ec6bd69803dcc768cfc`（20/20），均已结束并通过。新增五个参数用例覆盖 0、-1、int.MinValue、1、30，验证运行时读取和 Inspector 校验写回、真实 ObjectPool 构造/借还，以及其他参数不变。测试使用未保存的独立配置对象并销毁；Test Runner 临时场景和设置已清理恢复。
