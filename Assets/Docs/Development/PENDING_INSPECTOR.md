# PENDING INSPECTOR

保留旧开发流程中的 Inspector 检查清单；2026-10-06 迁移至按功能组织的目录。
以下勾选状态沿用原记录，不表示本次目录整理重新验证了相关功能。旧 DSH 审批规则已归档，现行规则见 `Assets/AGENTS.md`。

## 历史检查项（非现行待办）

### 1. 音频限流的数值关系 — 已完成, 记录判定依据

> 2026-10-06 核查：现行勘误：两个容量不等式不是代码硬约束。maxPoolSize 也经正常请求的 CanReuse 检查限制扩容；defaultCapacity 是容器初始容量，不是创建数。当前 GenericSfx/GenericOst/MouseClick 单 ID 上限分别为 5/1/10，详见 [默认配置](../../SO/Audio/README.md)。

两个实例上限与池大小是三件不同的事:

| 字段                 | 位置                  | 当前值    | 含义                 |
| ------------------ | ------------------- | ------ | ------------------ |
| `maxSoundInstance` | AudioManagerConfigs | 30     | 全局并发上限: 同时最多几个声部   |
| `maxPoolSize`      | AudioManagerConfigs | **30** | 只保留多少 emitter 用于复用 |
| `defaultCapacity`  | AudioManagerConfigs | 10     | 池的初始容量             |
| `prewarmAmount`    | AudioManagerConfigs | 10     | 启动时预热多少个           |
| `maxInstances`     | 每个 AudioClipData    | 10     | 单个 clip 的并发上限      |

**硬性关系**: `MaxPoolSize >= MaxSoundInstance`, 且 `PrewarmAmount <= DefaultCapacity <= MaxPoolSize`.

**已满足**: `MaxPoolSize` 已由你从 `10` 改为 `30`, `30 >= 30` 成立.
依据: 2026-10-03 的运行日志在**第 31 个并发请求**处出现 `Pool exhausted at 30 emitters`, 证明池上限确实取到了 30.

- [x] 把 DefaultAudioManagerConfigs.asset 的 MaxPoolSize 改为不小于 MaxSoundInstance(当前 30).
- [x] 复核 PrewarmAmount <= DefaultCapacity <= MaxPoolSize 成立.
- [x] 为每个 AudioClipData 资产填写新增的 Volume 与 Pitch(默认 1 / 1 即保持原听感).
- [x] 按每个音效的设计意图复核 MaxInstances: 它此前从未生效, 因此现有的 10 是未经验证的默认值.

### 2. 验证本次音频交付

> 2026-10-06 核查：以下是相继替代的历史实现，不可作为当前修改步骤。现行 Awake 建池，Start 预热并应用设置；重复 AudioEmitter.Play 直接返回，不先无条件 Stop。第4条归还处理曾被第5条替代，不应再次在池归还回调内调用 Release。详见 [Managers](../../Scripts/Core/Managers/README.md)。

- [x] Unity Console 无编译错误; 四个新脚本已导入并生成 .meta.

- [x] Core.prefab 上 AudioManager 的 emitterPrefab / emitterRoot / configs 三个引用完好.

- [x] 已确认**正常发声**.

- [x] 日志出现 `AudioEmitter is released`, 且调用栈经过 `Timer/<Run>d__34:MoveNext`, 证明 Timer 完成并归还了 emitter.
  本次 Play 模式验证**失败过一次**, 记录两次修复:
1. **反射假阴性**: 旧实现用 `Type.GetType("TimerRunner, GrignardReagent.Timer")` 在运行时按字符串解析,
   而该程序集在 `AudioManager.Awake()` 时尚未加载 —— 恒为假阴性, 且失败分支什么也不打印.
   已改为直接读取 `TimerRunner.Instance`(Timer 包已将 `Instance` 改为 public).

2. **Awake 顺序竞争**: `AudioManager` 与 `TimerRunner` 同在一个 GameObject 上, Unity 不保证两者 `Awake()` 的先后,
   因此 `AudioManager.Awake()` 可能先跑并读到 `null`. 已把宿主校验移到 `Start`, 池也在校验通过后才建立.

3. **`AudioEmitter.Play()` 的重入守卫**: 池化复用时, 上一次播放尚未结束, `Play()` 因 `isPlaying` 为真而静默返回.
   已改为 `Play()` 首先调用 `Stop()` 终结在飞的播放.(**注意**: 它**不是**池枯竭的原因, 见第 4 条.)

4. **`OnEmitterRelease` 从未归还 emitter**(池枯竭的真正原因): 原始实现用 `emitter.onAudioFinished += emitterPool.Release`
   直接归还池, 我的改写把这行替换成了一个**不含 `Release` 调用**的处理器, 于是池的租约永不回滚.
   已在该处理器末尾补上 `emitterPool.Release(emitter)`.

5. **归还调用与归还回调互相递归**(上一条修复引发的回归): `OnEmitterRelease` 同时是池的归还回调,
   又被我改成了调用 `emitterPool.Release`, 于是 `OnEmitterRelease -> Release -> actionOnRelease -> OnEmitterRelease`
   无限递归, 栈溢出. 已拆成两个方法: `ReleaseEmitter`(只转发给池)与 `OnEmitterRelease`(只做清理, 绝不调用 Release).
   同时修掉一个潜在缺陷: `ReleaseEmitter` 此前只订阅不退订, 复用 emitter 时会累积订阅, 导致同一次结束被多次归还.

6. **空间化交付**: `AudioClipData` 新增 `SpatialBlend` / `MinDistance` / `MaxDistance`,
   `AudioEmitter.Configure()` 写入它们, 并新增 `SetPosition` / `AudioBuilder.WithPosition`.

### 6b. 冻结语义重构后的验收矩阵 (待实测)

> 2026-10-06 核查：现行勘误：入口使用有效 SurviveFreeze（请求覆盖或 clip 默认值），同时要求 AllowWhileFrozen。OST 默认 SurviveFreeze=true，故只显式设置 AllowWhileFrozen 也可能获准；有效 SurviveFreeze=false 则拒绝。Freeze 通过监听器暂停，不主动切断并归还全部声部；循环声无自然结束 Timer。获准不等于保证可听。下表保留旧验收记录，不作为当前语义矩阵，见 [Audio](../../Scripts/Core/Audio/README.md)。

`SurviveFreeze` 与 `AllowWhileFrozen` 是**两个独立**的决定, 因此需要分别验证, 不能只看"暂停有没有生效".

**冻结期入口门的最终规则**: 只有**同时**满足"允许进入"与"会熬过冻结"的请求才被受理.
因此 `WithAllowWhileFrozen(true)` 单独使用在冻结期间**不会出声** —— 它只是不被入口拒绝.

| #   | 场景                                                                      | 期望                                                              |
| --- | ----------------------------------------------------------------------- | --------------------------------------------------------------- |
| 1   | 冻结**前**播放, 未声明 `SurviveFreeze`(默认 `false`)                              | 冻结时被切断, 但仍**正常归还池**                                             |
| 2   | 冻结**前**播放, `WithSurviveFreeze(true)`                                    | 冻结期间继续播放; 解冻后不受影响                                               |
| 3   | 冻结**期间**请求, 未声明 `AllowWhileFrozen`                                      | 被拒绝, 返回 `null`, 日志含 `did not allow entry`                       |
| 4   | 冻结**期间**请求, 只声明 `WithAllowWhileFrozen(true)`                            | **仍被拒绝**, 返回 `null`, 日志含 `would not be kept`. **不占用任何 emitter** |
| 5   | 冻结**期间**请求, 同时 `WithAllowWhileFrozen(true)` + `WithSurviveFreeze(true)` | 被受理且**能听到**; 解冻后正常归还                                            |

- [x] 逐条验证上表五项. 第 4 项是本轮修正的核心: 旧实现会取走 emitter 且直到解冻才可能归还.
- [x] 确认第 4 项被拒时, Hierarchy **不新增** emitter, 且日志**没有** `is pooled`.
- [x] 特别确认 **1 与 3/4 互不牵连**: 冻结期间被拒绝的请求, 不应影响已在播放的音; 反之亦然.
- [x] **补一个仍未闭合的观测**: 冻结期间若确有 emitters 归还(用户上次见到 2 次 release),
  请在断点或日志中确认当时 `AudioSource.isPlaying` 的值. 这决定"被暂停的音源是否报告 isPlaying == false",
  而我无法从日志静态判定. 该值影响"冻结期间能否归还"这一问题的答案.

**已解决的历史项**(原 6b 记录): 冻结期间 `WithAllowWhileFrozen(true)` 但 `SurviveFreeze = false` 的请求,
曾出现"`is pooled` + `played` 但无 release 且 Hierarchy 不可见". 现由入口门在**预定之前**拒绝而消除.

- [x] 确认 `SurviveFreeze` **与 `Loop` 无关**: 用同一个 `Loop == false` 的 clip,
  分别在 `SurviveFreeze` 为 `true` / `false` 下试一次, 结果应不同. 这是本次重构的核心断言.
- [x] 在 Unity 的 `AudioClipData` 检视面板确认 `FreezeLoop` 已被 `DefaultSurviveFreeze` 取代,
  且旧字段没有残留在任何 `.asset` 里(这是序列化字段改名, 旧值不会自动迁移, 默认值即 `false`).

### 请复验(按顺序)

> 2026-10-06 核查：以下命令、行号、日志和临时 requestTime 是历史操作，不需照做。未指定 Position/Follow 时仍使用 emitter Transform；当前三个数据资产都是2D。十次同 ID 请求受单 ID 上限约束，不等于十声部叠加。

- [x] **脚本编译已完成并核实**: 首次编译报 `CS0246`(`BoxGroup` 缺少 `using NaughtyAttributes;`), 已修正.
  修正后 `Assembly-CSharp.dll` @ `17:48:01`(`51712` -> `52736` 字节), 产物含 `SetPosition` / `WithPosition` /
  `SpatialBlend` / `MinDistance` / `MaxDistance`, 且导入日志无任何 `CS####` 诊断.

- [x] 在三个 `AudioClipData` 上按需设置 `SpatialBlend`(0 为纯 2D). 默认 `0`, 故现有听感不变;
  只有 `SpatialBlend > 0` 且**同时**给出 `WithPosition` 或 `WithFollowTarget` 时才会听到位置差异.

- [x] 确认 `Main Camera` 的 z 值(`-10`)带来的距离偏移可接受. 你已确认"计算不需太精确, 只做大致 spatial 效果",
  此处仅作提醒: 相机移动会改变音量, 因为唯一 `AudioListener` 在相机上而 `AudioRoot` 在原点.

- [x] **确认不再栈溢出**: 这是本轮的阻塞项. 你此前在 `AudioManager.cs:287` 临时注释掉的那行 `emitterPool.Release(emitter)`
  现在已被正确的两方法结构取代, **请撤掉那处注释** —— 保留它会导致永不归还.

- [x] 日志出现 `TimerRunner is available`(已确认).

- [x] **先用确定性断言检查归还**: 在 `Assets/Scripts/Core/GameObjectPool/GameObjectPool.cs:58` 的
  `actionOnRelease` 委托内打一个断点, 进入 Play 模式并触发一次播放, 确认该断点**在播放结束后命中**.
  这比"看日志里有没有某条消息"可靠得多, 也是最初定位本缺陷的手段.

- [x] **把场景覆盖的 `requestTime` 从 `10` 临时调回 `1`**, 然后确认:`AudioEmitter is released` 的次数与播放次数**一一对应**,
  而不是像此前那样恒为 `2`.
  说明: 此前的恒值 `2` 全部来自 `Prewarm`(`GameObjectPool.cs:99` 内部也会调用 `Release`), 与真实播放无关 ——
  **恒定的计数是可疑信号, 不是正常信号.**

- [x] 连点若干次, 确认不再出现 `Pool exhausted`.

- [x] 确认能正常发声. 注意 `requestTime = 10` 会在同一帧发出 10 个同 clip 请求, 听感上是叠加失真,
  这属于测试强度而非缺陷.

### 3. "Pool exhausted" 警告的正确读法 (此前写错, 特此更正)

> 2026-10-06 核查：现行勘误：抢占硬停会同帧同步归池，不等待下一帧。点击次数不能直接换算并发数；Pool exhausted 单条警告既不证明限流完全正确，也不单独证明泄漏，应结合配置、借还与拒绝原因检查。

**该警告是设计生效的证据, 不是缺陷.** 判据:

- 池上限为 `30`, 而 `Test Audio Request` 每次点击触发 `10` 次请求 —— 连点 3 次即达 30 并发.
- 第 31 个请求被拒绝并记录警告, 正是刻意加的硬上限. Unity 的 `ObjectPool` 自身在池空时**只会新建实例, 从不拒绝**, 因此这个上限必须由代码在 `AudioManager.ReserveEmitter` 里强制.
- 先前的验收条目写成"没有 Pool exhausted 警告"是**错误的**: 以这个测试强度, 该警告出现才是正确行为.

补充: 全局上限(30)与池上限(30)相等时, 抢占会在池耗尽**之前**介入 —— 每来一个新请求就先抢占最旧的**非循环**音, 释放一个槽位. 因此常规玩法下不应看到该警告; 它只在 30 个请求于**同一帧**内全部发出时出现(测试按钮正是这种形态, 因为抢占释放的 emitter 要到下一帧才真正回到池).

### 4. 本次不包含的缺口 (无需你操作, 仅备忘)

> 2026-10-06 核查：下列未勾选状态仅保留历史。淡入、自然尾淡出和句柄 Stop 淡出已实现，抢占仍硬切；Master.mixer 已暴露三路音量参数，设置面板会应用它们。见 [音频资源](../../Audios/README.md) 与 [Setting](../../Scripts/Game/Setting/README.md)。

- [ ] 淡入淡出未实现: 抢占是硬切. 参见 CHANGELOG 的"本次不做"一节.
- [ ] 总线整体缩放未实现: Master.mixer 未暴露参数, 需要一次独立的 AudioMixer 配置交付.

### 5. Setting 子系统交付 (待复验)

> 2026-10-06 核查：历史导入/编译与手工破坏设置文件的步骤已被后续实现和隔离测试替代，不是当前待办。现行设置脚本位于 Game/Setting，测试只操作临时路径，见 [Tests](../../Tests/README.md)。

**先决条件: 让 Unity 重新获得焦点并完成导入与编译.** 截至交付时, `Assets/Scripts/Game/` 下的 5 个 `.cs`
都还没有生成 `.meta`, `Assembly-CSharp.dll` 也未更新 —— 编辑器失焦导致导入与编译循环未推进.

- [x] 编译已核实: 首次失败于 `GameLog.Info(this)`(context 参数类型是 `UnityEngine.Object`, 普通类不能传), 5 处 `CS1503`;
  已改为 `GameLog.Info(null)`. 修正后 `Assembly-CSharp.dll` @ `23:17:34`(`52736` -> `57344` 字节),
  两个子系统的全部类型均进入产物. 目录与 `.cs` 的 `.meta` 已由 Unity 生成.
- [x] 运行一次, 确认日志出现"没有已保存的设置, 使用默认值"一类信息, 并记下它打印的文件路径.
- [x] 在 `GameSettings` 里加一个字段(同时记得在 `ResetToDefault()` 中给它赋值), 改动它并调用 `SettingBootstrap.Save()`.
- [x] 确认该路径下出现 JSON 文件, 内容含改动后的值.
- [x] 重新运行, 确认日志不再报"没有已保存的设置", 且读到的是改动后的值.
- [x] ~~把 JSON 文件内容改坏后重新运行~~ —— **首次失败**: 抛 `ArgumentException: JSON parse error`.
  根因是 `JsonUtility.FromJson` 以抛异常报错而我的注解误以为它返回 null. 已改用 `FromJsonOverwrite` + 捕获, **请重跑下面四条**.

### 5b. JSON 健壮性重验 (修正后待验)

> 2026-10-06 核查：现行字段示例为 `{"audio":{"masterVolume":0.3}}`，顶层 MasterVolume 不匹配当前数据。当前 FromJsonOverwrite 配合默认值与显式校验，Audio 非空且三路音量必须有限并位于0–1，否则整体回默认。嵌套缺字段边界以 [Setting](../../Scripts/Game/Setting/README.md) 的当前验证为准。

- [x] **只有大括号 `{}`**: 应回落到**设计默认值**(`MasterVolume` 1, `OstVolume` 0.5, `SfxVolume` 0.5), 而**不是**类型零值 0.
  这是本次修正的核心: 旧实现用 `FromJson` 新建实例, 因此缺失成员取零值.
- [x] **语法损坏的 json**: 应**不抛异常**, 记录一条 warning 并回落到设计默认值, 游戏正常启动.
- [x] **缺少部分成员的 json**(例如只有 `MasterVolume`): 应保留文件中给出的值, 其余取设计默认值.
  注: 这条只保证**顶层**; 嵌套成员内部是否如此, `JsonUtility` 无文档化保证, 若测出不符请告知.
- [x] **完全空白的 json**: 应记录"文件为空"并回落到设计默认值, 且该日志与"解析失败"可区分.

### 5c. AudioManager 修正项

> 2026-10-06 核查：建池已在 Awake 的 InitializeInternal 内进行，但不同对象的 Start 顺序不固定，不代表预热或 Mixer 应用必然先完成。下文 DSH 裁决要求属于旧流程，现行权限以 [AGENTS](../../AGENTS.md) 为准。

- [x] **场景内 `Start` 播放音频**: 应正常工作. 用户已把建池移到 `InitializeInternal`, 本次仅补齐注解与一个空值守卫, 未改变该行为.
- [x] **`configs` 故意留空时的行为**: 应记录一条错误并**不再**抛 `NullReferenceException`(旧代码在该分支仍会调用
  `BuildEmitterPool()`, 而它立即解引用 `configs`). 若你希望由 DSH 恢复"依赖缺失时仍可播放"的语义, 请给出裁决 ——
  当前实现是"缺依赖则不播放", 与"实例池的配置依赖"直接相关.

### 6. 音频冻结行为 (待实测)

> 2026-10-06 核查：现行勘误：SurviveFreeze=true 映射 ignoreListenerPause，表示忽略监听器暂停而继续播放，不是停在原处等待解冻。以下观察不能替代对两种配置的分别验证。

本项经两轮修订, 最终形态见 6b 的验收矩阵. 过程记录如下, 以免下次重新推导:

- 初版(A12): `AudioClipData.FreezeLoop`(默认 `true`), 把"是否被保留"绑在 `Loop` 上. **该字段已不存在.**
- 用户在 Play 模式实测后指出: "是否 Loop"与"是否能熬过冻结"是两个概念, 且后者又分两件事.
- 因此重构为 `DefaultSurviveFreeze`(静态初值) + `WithSurviveFreeze` / `WithAllowWhileFrozen`(动态覆盖).

唯一仍需实测的、我无法从 Unity 文档确认的一点: **冻结再解冻后, 被保留的音是否从原处继续**(而非从头或不响).
官方文档只写明"恢复时从暂停处继续", 未写明暂停期间 `AudioSource.isPlaying` 的值.

- [x] 解冻后确认被保留的音**从原处续播**. 若不响, 说明 `ignoreListenerPause` 与暂停状态冲突, 需改实现.

### 7. GameState 子系统交付 (待复验)

> 2026-10-06 核查：退出/重进 Play 后恢复是当时编辑器设置下的观察，不是跨 Enter Play Mode Options 的保证；当前无统一静态重置钩子。Loading 中 Freeze/Resume 请求会被忽略，详见 [GameState](../../Scripts/Game/GameState/README.md)。

- [x] 编译已核实(与 Setting 同一次编译).
- [x] **实测 `Freeze` 的幂等与还原**: 在玩法代码里调用 `GameStateManager.Freeze()`, 确认游戏时间停止, 音频暂停;
  再调用一次 `Freeze()`, 确认只记一条"已处于 Freezed"的日志且**无其他副作用**.
- [x] **实测时间缩放还原**: 先设 `Time.timeScale = 0.5f`, 再 `Freeze()` 然后 `Resume()`,
  确认最终仍是 `0.5f`(而不是 `1f`). 这是"记忆而非写死"的关键验收 —— 若写成 `1f`, 慢动作玩法会在暂停一次后静默失效.
- [x] **实测退出行为**: 在 `Freezed` 状态下退出播放模式, 确认下次进入播放模式时 `Time.timeScale` 已被 Unity 重置(不残留 0).
  若确实残留, 需要按 `README.md` 的 `TODO` 增加退出钩子.

### 8. 音频与 GameState 的相互作用 (交付 3 落地后必须验)

> 2026-10-06 核查：归池取决于入口条件、非循环结束判定、TimerRunner 和对象生命周期，不能将未归池唯一归因于非缩放时间。当前暂停停止回归只设置 timeScale/Listener，不进入 Freezed 标签，不代表旧冻结矩阵已完整重测。

`Time.timeScale = 0` 会让所有走 scaled time 的东西停摆, 因此音频侧已把 emitter 的结束判定改为 `UseUnscaledTime()`.
这两处是一对, 不能只保留其一.

- [x] 冻结期间播放一个一次性音效, 确认它仍会在播完后**归还池**(日志出现 `AudioEmitter is released`).
  若没有, 说明 `UseUnscaledTime()` 未生效, emitter 会在暂停期间永久滞留池外.

### 9. SceneSwitch 子系统交付 (已交付并已全面实测)

> 2026-10-06 核查：本节“全面实测”“唯一待办”和缺口结论均限定在历史交付时点，不代表当前测试覆盖或现行待办。当前证据与未覆盖范围见 [Tests](../../Tests/README.md)。

**本份交付已编译并已全面实测通过** —— 资产侧配置(9a / 9b)与全部实测项(9c / 9d)均已由用户完成并显式确认.

**本文件当前唯一真正待办的项**: 9d 末尾那条 —— 冻结期间 `AudioSource.isPlaying` 本身的取值仍未被观测. 它不影响本交付, 说明见该处.

**另有两个"仅备忘"的未勾项**位于 `### 4. 本次不包含的缺口`, 由用户标注为无需操作, 不是待办.

> 关于 9a: `SceneSwitchConfigs.cs` 顶部的 `[CreateAssetMenu]` **已由用户恢复**, 因此该资产现在可以通过右键菜单创建. 该往返已完成.

#### 9a. `[CreateAssetMenu]` 往返 (§5.2.3.2) — 必须先做, 否则无法右键创建资产

> 2026-10-06 核查：现行 CreateAssetMenu 已启用；下文取消注释往返与人工审批仅作历史记录。

`SceneSwitchConfigs.cs` 顶部的 `[CreateAssetMenu]` **已被我注解掉**, 因为它引用 `MenuTool` 包的常量. 按约定分工:

- [x] 你修改 `MenuTool` 的 `.menutool` 资产, 仿 `AudioManagerConfigs` 的结构加入 `Game/Basic Settings/SceneSwitchConfigs` 节点.
- [x] 你**取消注释** `SceneSwitchConfigs.cs` 顶部那三行 `[CreateAssetMenu(...)]`(带 `TODO:` 标记).
- [x] 你确认 `Game.MenuTool.g.cs` 重新生成且包含 `SceneSwitchConfigs` 节点.

#### 9b. 资产与 Inspector

> 2026-10-06 核查：现行 SampleScene 已退休，配置含四个场景；路径为 Prefabs/Core/Core.prefab 与 SO/SceneSwitch/DefaultSceneSwitchConfigs.asset。下文旧禁区已撤销，不能据此要求额外审批。见 [场景映射](../../SO/SceneSwitch/README.md) 与 [AGENTS](../../AGENTS.md)。

- [x] 创建 `SceneSwitchConfigs` 资产(建议与 `DefaultAudioManagerConfigs.asset` 同级放在 `Assets/SO/`).
- [x] 在该资产里加一条映射: `SampleScene` -> `SampleScene`. **名字必须与场景文件名逐字一致, 且不含 `.unity`**.
- [x] 把 `SceneSwitchManager` 组件加到 `Assets/Prefabs/Core.prefab` 的 `Core` 对象上.
- [x] 在该组件的 `Configs` 字段拖入上一步的资产.
- [x] Play 一次, 确认 Console **没有** `configs` 未赋值的错误.

> **为什么我没有代做**: 新增场景、改动 Build Settings、编辑 `.prefab` 与创建 `.asset` 全部属于 §2.1 / §3.2 的禁区.

#### 9c. 实测清单 (全部通过)

> 2026-10-06 核查：保留旧 SampleScene 日志，不作当前可复制代码。Loading 日志顺序不能证明画面首帧时机；Loading 不触发冻结入口门，但仍受音频校验和限流，且 AudioManager 会解除监听器暂停。切换通过 completed 回调收尾，空闲时允许重载同场景。见 [SceneSwitch](../../Scripts/Core/SceneSwitch/README.md)。

**八项全部通过**, 由用户实测并确认. 前三项有运行日志作为证据; 后五项是"证明某事没有发生", 在日志里天然不留证据, 因此以用户的实测确认作为依据 —— 这也是本节此前把未经证实的项标为未勾选的原因.

> **验收形态的一处反思**: 后五项(如"音乐未被暂停")属于"证明不存在", 而我最初把它们写成了与"证明存在"相同的验收标准, 还要求逐条对照一个只有作者能判定的表 —— 那是无效的验收设计. 正确的形态是让每项**打印一行可读的断言**(例如 `###ASSERT Loading 期间 AudioListener.pause = False (期望 False)`), 使其与前三项具备同一种证据形式. 记此以免下次重犯.

已由 2026-10-05 的运行日志证实的三项:

- [x] **切换成功路径**: `CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.SampleScene)`.
  证据: 日志出现 `Switching to SampleScene (SampleScene).` 与 `Switched to SampleScene (SampleScene).` 两条.
- [x] **`Loading` 的进出时机**: 证据: `Game state is now Loading.` 出现在上述两条之间,
  且其后的 `Game state is now Playing.` 由 `ExitLoading` 触发 —— 即新场景显示之后不再处于 `Loading`.
- [x] **重复实例守卫**: 你切换到的场景本身也含一个 Core 预制体, 因此天然构成该场景.
  证据: 日志出现 `A second Core was found; its GameObject is being destroyed.` 与
  `Another TimerRunner already exists. Component disabled.` 两条, 且被销毁的是新场景那一份(先到者保留).

以下五项是"证明某事没有发生", 因此都需要**故意制造一个错误条件或调用两次**, 而不是正常跑一遍. 均已通过:

- [x] **`Loading` 不施加机制**: 需要一个在切换期间持续播放的音(如循环 OST), 确认切换期间它**未**被暂停.
  这是"`Loading` 与 `Freezed` 确已分开"的直接验收.
- [x] **`Loading` 不认音频门**: 在切换进行中发起一次音频请求, 确认它**照常通过**(这是有意行为, 非缺陷).
  需要一个能在切换那一帧执行请求的调用点.
- [x] **未注册场景被拒**: 在 configs 里写一个不存在于 Build Settings 的场景名, 确认日志出现
  `is not registered in Build Settings` 且**没有**开始加载.
- [x] **切换在途中再次请求**: 连续两次调用 `RequestSwitch`, 确认第二次被拒(返回 `null`).
- [x] **冻结中切换的还原**: 先 `Freeze()`, 再 `RequestSwitch(...)`, 切换完成后确认状态**回到 `Freezed`**
  而不是 `Playing`. 这是 `stateBeforeLoading` 的核心验收; 若回到 `Playing`, 说明记忆失效.

> **9c 的每一项都依赖 9a 与 9b 先完成.** 两者均已完成.
> 
> **注意**: `RemoveDuplicates` 已由用户改为**手动 `[Button]`**(`OnValidate` 只报空名), 理由是 Unity 界面上新增条目会默认复制上一项, 因此编辑过程中必然短暂出现重复, 自动去重会打断填写. 这意味着**去重不再是自动的** —— 需要你点一次按钮才会消除重复项. 已记此以免下次误以为它是自动的.

#### 9d. 结转: 仍未闭合的观测

> 2026-10-06 核查：句柄有效性守卫仍成立，行号及探针观察属于历史；此处“唯一未知量”只限当时非循环冻结实验，不涵盖循环、停用/销毁、手动停止或 TimerRunner 等生命周期边界。

**本项已通过.** `ISoundHandle.IsPlaying` 确实反映底层 `AudioSource.isPlaying`:

```csharp
// AudioEmitter.cs:40
public bool IsPlaying => source && source.isPlaying;
// SoundHandle.cs:32  —— 多一道有效性守卫
public bool IsPlaying => isValid && emitter.IsPlaying;
```

它比"单纯反映 `isPlaying`"更强: 句柄一经失效(播放结束 / 被抢占 / 被 Stop)立即返回 `false`,
因此**不会误报复用同一池化 emitter 的后一次播放**. 用户用一条协程探针实测, Console 中未出现探针输出, 与实现一致.

- [x] 确认 `ISoundHandle.IsPlaying` 反映 `AudioSource.isPlaying`, 且失效后立即为 `false`.
- [ ] **仍未被观测**: 冻结期间 `AudioSource.isPlaying` 本身的取值(见 6b 节讨论).
  这是"冻结期间能否归还 emitter"的唯一未知量; 本次 SceneSwitch 交付不依赖它.
  注意它与上一项不同: 上一项问的是"句柄是否正确转述", 这一项问的是"音源在被暂停时报告什么".

## 历史

- 交付 (C) 的探针验证与提交清单已全部勾完并合并, 记录见 CHANGELOG 的"探针移除记录".
