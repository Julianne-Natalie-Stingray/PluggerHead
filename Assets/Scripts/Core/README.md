# Core 子系统

## 职能

Core 是本工程共享运行时基础设施的单一入口.

- 拥有共享服务门面, 其他 Subsystem 由它取得依赖.
- 拥有输入处理, 音频播放, 场景切换与对象池.
- 不负责游戏规则, 玩法状态, 以及任何场景专属行为.
- 不负责生成的输入绑定资产; 那些位于 `Assets/Scripts/Infra/InputSystem/` 下.
- 不负责"接下来该去哪个场景"; 那是调用方的决定, 以参数传入.

## 构成

| 路径 | 类型 | 职责 |
| --- | --- | --- |
| `CoreFacade.cs` | Core 预制体上的 MonoBehaviour | 单一入口; 暴露 `Input` / `Audio` / `SceneSwitch`; 提供跨场景访问点 `Instance`; 负责 `DontDestroyOnLoad` 与重复实例守卫 |
| `Input/InputManager.cs` | MonoBehaviour | 输入服务; 发布移动、指针状态及按键事件 |
| `Managers/AudioManager.cs` | MonoBehaviour | 音频总线; 拥有 emitter 池, 注册表与两级实例上限 |
| `SceneSwitch/SceneSwitchManager.cs` | MonoBehaviour | 场景切换总线; 唯一的切换入口, 拥有 `Loading` 状态 |
| `SceneSwitch/SceneId.cs` | enum | 可切换场景的键; 编译期防打错 |
| `SceneSwitch/SceneSwitchConfigs.cs` | ScriptableObject | 白名单: `SceneId` -> 场景名 |
| `Audio/AudioBuilder.cs` | struct | 单次播放请求的链式配置凭证, 承载实时效果参数与位置 |
| `Audio/ISoundHandle.cs` | 接口 | 单次播放的作用域契约 |
| `Audio/SoundHandle.cs` | class | `ISoundHandle` 的唯一实现 |
| `Audio/AudioRegistry.cs` | class | 活跃实例的注册, 注销与顺序查询 |
| `Audio/AudioEmitter.cs` | MonoBehaviour | 单个池化声部; 播放, 定位, 跟随, 报告结束 |
| `Audio/SODefinitions/` | ScriptableObject | `AudioClipData` 描述单个 clip; `AudioManagerConfigs` 是音频总线配置 |
| `Audio/Setting/` | `[Serializable]` class | `AudioSettings`, 音频总线的持久化音量 |
| `GameObjectPool/` | class | 复用对象池; 额外提供"能否复用"的硬上限探查 |
| `Input/` | class | `DragAndDropService2D`, 纯可序列化辅助类; 早期代码 |

## 公共API

- `CoreFacade.Input` -> `InputManager`: 输入服务.
- `CoreFacade.Audio` -> `AudioManager`: 音频总线. 启动声音的唯一入口是
  `CoreFacade.Audio.CreateBuilder().With...().Play(audioId)`, 返回 `ISoundHandle`; 请求被拒绝时返回 `null`.
- `CoreFacade.SceneSwitch` -> `SceneSwitchManager`: 场景切换总线. 唯一的切换入口是
  `CoreFacade.SceneSwitch.RequestSwitch(sceneId)`, 返回 `AsyncOperation`; 请求被拒绝时返回 `null`.
- `CoreFacade.Instance` -> `CoreFacade`: **唯一**能熬过场景切换的访问点. 新场景中的代码用它取回 Core.
- `SceneSwitchManager.IsSwitching`: 是否有一次切换在途. 调用方可以查, 但 `RequestSwitch` 无论如何都会强制该约束.
- `AudioBuilder` 的链式项: `WithVolume` / `WithPitch` / `WithRandomPitch` / `WithPosition` / `WithFollowTarget` /
  `WithSurviveFreeze` / `WithAllowWhileFrozen` / `WithFade`.
- `AudioManager.Registry` -> `AudioRegistry`: 只读的事实查询, 供需要知道"现在在播什么"的代码使用.
- `InputManager`, `AudioManager` 与 `SceneSwitchManager` 是 `CoreFacade` 的必需组件, 由 `InitializeInternal()` 通过 `GetComponent<>()` 解析.
- `AudioClipData` 与 `AudioManagerConfigs` 是数据定义; `AudioId` 是 clip 查找键. `SceneSwitchConfigs` 是场景白名单.

## 内部实现思路

- 初始化在 `Awake()` 中完成; Subsystem 内部依赖由 `InitializeInternal()` 通过 `GetComponent<>()` 加缓存解决, 不需要 Inspector 接线.
- 管理器引用缓存在私有字段中, 以只读 Property 暴露.
- **Configs** 指**内部参数**, 由开发者维护, 放在工程内的 ScriptableObject, 初始化**之后**读取.
- **Settings** 专指**游戏内玩家可调设置**, 由 `SettingStore<TData>` 承载, 初始化**之前**可用.
- **不使用 `Config` 这个中间词.** 它介于两者之间, 只会制造歧义; 命名时必须在上面两者中二选一.
- 跨场景存活的东西只有一处: `CoreFacade` 及其挂载的 Core 对象. 其余一切随场景销毁并重建.

### 场景切换与跨场景访问

这是本 Subsystem 里**唯一**需要打破"少用单例范式"的地方, 因此把理由写明.

**为什么必须有 `CoreFacade.Instance`**: 场景切换会销毁**所有**场景内引用. 切换前, 场景里的对象可以靠 Inspector 拖拽拿到 `CoreFacade`; 切换后那个引用指向的实例早已随旧场景销毁, 于是它变成悬空引用(Unity 会把它置空). 因此**某个不属于任何场景的东西必须能在没有场景的情况下被访问到**. 这是硬约束, 不是偏好.

**为什么不用其他形态**:

| 形态 | 为何否决 |
| --- | --- |
| 每个新场景在 Inspector 里重新拖拽引用 | 漏接时表现为运行时 null 而不是编译错误; 且每个场景都要重复手工接线 |
| 服务定位器(按类型取) | 为**恰好一个**消费者引入一张注册表 |
| 静态门面(静态类内部持有实例) | 仍然要持有实例, 只是把单例藏起来, 并没有消除它 |

**重复实例守卫**: Core 是 Core 预制体的实例, 而预制体可能被放进多个场景. 因此 `CoreFacade.Awake` 在发现已有实例时**销毁自己的 GameObject** 并记一条 warning. 先到者保留, 后到者退出 —— 因为只有第一个才是游戏其余部分据以解析的那个访问点.

**新场景如何与 Core 重新连接**: 依赖方向是**单向**的, 即"场景内容 -> Core", 禁止反向. 因此不需要任何"重连机制": 场景内的组件在**自己的 `Awake`** 里通过 `CoreFacade.Instance` 取回依赖即可. Unity 已在场景加载时替你把该场景的所有 `Awake` 调完.

- 必须遵守一条硬约束: **场景内容不得在 `Awake` 之前的时机(如字段初始化器、`OnEnable` 早于其它组件的场合)访问 `CoreFacade.Instance`**, 因为那时持久 Core 可能尚未建立.
- 禁止反向: `CoreFacade` 与各管理器**不得**认识场景里的具体类型. 一旦反向, 每加一个场景子系统都要改 Core, 那正是"初始化与生命周期问题"的根源.

### 场景切换的实现要点

**场景架构**: Core 与内容**同处一个场景**, 靠 `DontDestroyOnLoad` 续命, 切换用 `LoadSceneMode.Single`. 已否决"Core 独占常驻场景 + 加法加载"的方案, 理由是**单场景无法直接播放**: 那样每个内容场景里都没有 Core, 测试单个场景时必须先播 Bootstrap 场景, 或手动把 Core 加回去 —— 而"测试时的手工接线"正是要消灭的成本.

**激活时机**: 加载会先被 `allowSceneActivation = false` 压住, 等 `progress >= 0.9` 后再放行, 随后立刻退出 `Loading`. Unity 的 `progress` 在 0.9 处停住, 因此必须显式放行, 否则加载永不完成.

**为什么加载 0.9 才放行而不是更早**: 一旦放行, 新场景就会激活并显示. `Loading` 要精确覆盖"旧场景还在屏幕上、或新场景尚未出现"的那一段, 所以放行与退出 `Loading` 必须相邻, 中间不留空档.

**为什么切换目标是参数而不是字段**: "接下来去哪个场景"是玩法决定(打完关卡 -> 下一关), 不是总线配置. 若把 `NextScene` 放在管理器上, 它就成了第二个真相来源, 且每次测试换场景都要改 Inspector.

**校验顺序**(四道, 全部通过才加载):

| # | 检查 | 失败时 |
| --- | --- | --- |
| 1 | `configs` 已赋值 | 组件已在 `Awake` 中自禁 |
| 2 | 当前无切换在途 | 记日志并返回 `null` |
| 3 | `SceneId` 在白名单中有映射 | 记错误并返回 `null` |
| 4 | 映射到的场景已在 Build Settings 注册 | 记错误并返回 `null` |

第 4 道用的是 `SceneUtility.GetBuildIndexByScenePath`, 它把"忘了把场景加进 Build Settings"变成一条**明确日志**, 而不是引擎层的异常或静默停在当前场景.

**同一场景再次请求也走同一条拒绝路径**: 没有单独的"是否已在目标场景"判断, 因为那会引入一个需要维持的额外状态, 而重复加载同一场景本就是调用方的意图问题, 应当被日志暴露.

**已否决**: 取消一次进行中的 `LoadSceneAsync`(它没有 Cancel, 只能标记忽略, 于是留下一个仍在加载的场景); 排队下一次切换(顺序自动切两个场景几乎总是发起方的 bug).
- `CoreFacade` 是唯一调用 `DontDestroyOnLoad` 的类型; 各管理器自身不调用.
- 跨组件协调的初始化放在 `Start`, 不放 `Awake`: `Awake` 只解析自身依赖, 凡是需要"另一个组件已完成初始化"的判断都等到 `Start`.
- 唯一的例外是音频的**建池**: 它放在 `InitializeInternal` 而非 `Start`, 因为其他组件的 `Start` 里就可能有播放请求, 而那时池必须已经存在. 只有**预热**放在 `Start`, 因为实例化可以推迟.

### 输入的层级边界

输入只做两层, **不做语义层**:

| 层 | 内容 | 是否在模版内 |
| --- | --- | --- |
| L1 设备与绑定 | 物理控件, 控制方案(control schemes), 重绑定, 多设备 | 属 asset, 归使用者; 模版不预设 |
| L2 原始轴 | 把设备映射为无名的模拟轴与按钮 | 模版提供 |
| L3 语义 | 哪一轴是什么, 与重力的关系, 谁拥有哪个自由度 | **刻意排除** |

排除 L3 的理由: 二维输入只有两个自由度, 而"这两个自由度是什么"是玩法信息. 横版与俯视两类玩法在**某一轴上恰好重合**, 因此 asset 里那根已接线的轴与那根留空的轴不是模版的断言, 而是"当下两类玩法的重合面". 模版不对重力方向、轴向命名或额外键位做任何猜测.

由此推出两条边界:

- 输入不耦合重力. 重力方向是工程设置(`Physics2D`)或玩法实现, 输入层无需知道.
- 输入不提供消费者. `MovementInput`, `PointerPosition`, `IsPrimaryPressed` 是**扩展点**, 不是功能; 读取它们并作出响应的代码属于玩法, 不属于模版.

底层类型不外泄: 生成的 `PlayerControls` **不**通过 wrapper 暴露. `InputManager` 的事件与 properties 是唯一 entry point,
因此在 asset 里新增动作时,**改动 `InputManager` 是不可避免的** —— 这一点被明确接受, 而不是用一条访问器去回避.
理由: `controls` 属于底层, 一旦外泄, "输入如何被消费"就不再由这个包装层决定; 而输入设计一旦改变, wrapper 本来就要跟着变.
代价: 新增一个按键需要在此处加一个成员. 这是**每次一个**的一次性成本, 换来的是消费面始终明确且封闭.

### 已知的早期代码

`DragAndDropService2D` 是本工程早期阶段的产物, 目前**没有任何使用者**: 它需要调用方自行 `Initialize(...)` 与 `Enable()`, 并在自己的帧循环里调用 `Drag()`, 而这些都没有发生. 它作为可复用服务保留在原处, 不在模版范围内继续演进.

### 音频的限流与释放链路

音频有**两个互相独立**的实例上限, 都**不是**池大小:

- **每 clip 上限**: `AudioClipData.MaxInstances`, 表达设计意图.
- **全局上限**: `AudioManagerConfigs.MaxSoundInstance`, 保护总声部数.
- **池大小**: `AudioManagerConfigs.MaxPoolSize`, 只界定保留多少 emitter 用于复用.

`MaxPoolSize` 还由预定入口限制总创建量; 建议不小于 `MaxSoundInstance`, 否则池容量会先于声部上限拒绝请求.

请求路径是: 解析 `AudioId` 并检查 clip -> 检查冻结期入口 -> 应用每 clip 上限 -> 应用全局上限 -> 预定 emitter -> 注册 -> 配置 -> 定位 -> 启动.
两级上限只抢占最旧且**非循环**的声部; 如果全是循环声则拒绝请求. 停止后统一归还池、注销注册表并使句柄失效. 注册表的总数只统计存活项, 不把延迟清理的空槽计入限制.

池归还链路有**两个方向**, 必须分开, 否则会互相递归:

- **归还输出**: `OnEmitterRelease` 作为池的归还回调运行, 只做清理(注销, 重置, 重挂父级, 日志), **绝不**调用池的 `Release`.
- **归还输入**: `ReleaseEmitter` 订阅在 `AudioEmitter.onAudioFinished` 上, 只做一件事 —— 转发给 `GameObjectPool.Release`.

`AudioEmitter.onAudioFinished` 是**所有**结束路径的唯一汇合点: 自然播完, 句柄 `Stop()`, 以及被抢占.
这条链一旦断掉, emitter 就会永久滞留在池外.

非循环播放由 emitter 持有的 Timer 检测自然结束, 使用 `UseUnscaledTime()`; 停止、重置和销毁都会取消它, 防止旧排程影响复用后的播放. 循环播放没有自然结束 Timer.

结束先归还池和注销注册表, 再通知句柄. `IsFinished` 记录自然结束, 主动停止或抢占则为 false; emitter 在重置时保留本次结果, 到下一次 Play 才清除. `Finished` 的调用方可以立刻请求新声音. 完成回调各自隔离异常, 避免某个调用方阻断池归还.

### 音频与游戏状态

`AudioManager` 在 `OnEnable` / `OnDisable` 中成对订阅 `GameStateManager.Changed`, 按 `state == Freezed` 暂停或恢复 `AudioListener.pause`. 音频读取 GameState, 后者不依赖音频. 当前 Loading 不保留监听器暂停, 暂停中切场景需按实际玩法验证.

`AudioClipData.DefaultSurviveFreeze` 提供静态默认值, `WithSurviveFreeze` 按次覆盖并映射到 `AudioSource.ignoreListenerPause`; 它与 Loop 无关. 不保留的音受监听器暂停, 保留的音继续播放.

冻结期间的新请求必须同时允许 `WithAllowWhileFrozen(true)` 且有效的 SurviveFreeze 为 true, 否则入口拒绝并返回 null. 默认不允许冻结期间的新请求; 仅设置其中一个开关不足以放行.

### 音量、音高与渐变

`AudioClipData.Volume` / `Pitch` 提供默认值. Builder 未指定时保留默认值; `WithVolume` / `WithPitch` 覆盖本次播放, 不会被 Play 重置. `WithRandomPitch` 以显式音高或 1 为基准增加随机偏移. 活跃句柄可继续通过 `TrySetVolume` / `TrySetPitch` 修改.

`AudioClipData.FadeIn` / `FadeOut` 定义默认渐变时长; `WithFade` 可按次覆盖, 0 保留硬起/硬切. 渐变使用非缩放时间, 乘在当前音量上. 非循环播放在尾部淡出; 句柄 `Stop()` 请求优雅淡出并立即失效, 但声部在淡出完成前仍占用池槽位. 抢占和跟随目标丢失时的循环停止立即硬切, 以同步释放槽位.

### 持久化音量与 Mixer

`AudioManager.Start()` 在 Mixer 初始化后调用 `ApplyAudioSettings()`, 从 `SettingBootstrap.Settings.Audio` 读取 Master/Ost/Sfx 音量. 修改设置后需再次调用该方法, 当前没有自动变更订阅; 保存设置仍由 Setting 层负责.

`SO/Audio/DefaultAudioManagerConfigs.asset` 指向 `Audios/Mixers/Master.mixer`, 使用已暴露的 `MasterVolume` / `OstVolume` / `SfxVolume` 参数. 线性音量换算为 `20 * log10(volume)`, 0 使用 -80 dB. 缺少 mixer、空参数名或未暴露参数会记录日志.

### 音频的位置与空间化

位置来源有三种, 优先级明确:

| 来源 | 入口 | 说明 |
| --- | --- | --- |
| 跟随目标 | `WithFollowTarget(Transform)` | 每帧同步到目标; 目标消失后按下一节规则处理 |
| 一次性坐标 | `WithPosition(Vector3)` | 只写一次世界坐标 |
| 都不给 | —— | emitter 停在池留下的位置, 即 `AudioRoot` |

两者同时给出时**跟随胜出**, 因为跟随每帧覆盖位置.

`willFollowTarget` 是本模块的关键状态: 它表示"**本次播放是否请求了跟随**", 而 `followTarget == null` 只表示"当前没有目标".
两者必须分开 —— 否则每个不跟随的声音都会被误判为"跟丢了", 从而把循环音立刻停止.
该标志仅在 `SetFollowTarget()` 中置真, 在 `HandleLostTarget()` 与 `ResetEmitter()` 中置假:
跟随是**每次请求**的特性, 目标一丢失就必须重新请求, 不允许 emitter 开着跟随状态等待目标重新出现.

位置同步发生在 `LateUpdate`, 且**不**改变父子关系: emitter 始终挂在 `AudioRoot` 下, 位置靠写自身世界坐标实现.
因此跟随的声音不会随目标销毁而消失 —— 一次性音在目标最后出现的位置播完,
循环音则在目标消失时停止, 因为循环没有自然结尾, 会永久占住一个池化 emitter.

音量与位置是**互相独立**的两件事, 因此 `LateUpdate` 先写入待应用的音量, 再判断是否需要同步位置:
用"是否跟随"去门控音量写入, 会让每个不跟随的声音静默丢失 `SoundHandle.TrySetVolume` 的改动.

空间化由 `AudioClipData` 的三个字段描述, 并由 `AudioEmitter.Configure()` 写入 `AudioSource`:
`SpatialBlend`(0 为纯 2D), `MinDistance`, `MaxDistance`.

注意 `SpatialBlend == 0` 时位置**完全无影响**; 而 `SpatialBlend > 0` 时若既不跟随也不给坐标,
声音会始终从 `AudioRoot` 发出, 空间化形同无效. 二者必须一起使用才有意义.

## 音频回归检查

在 Unity Test Runner 的 PlayMode 中运行程序集 `PluggerHead.PlayModeTests` 下的 `PluggerHead.Tests.AudioIntegrationTests`, 或按 `Assets/Tests/README.md` 的 MCP calls 运行对应程序集. 用例复用原 19 项检查, 自动补齐 TimerRunner, 将失败和超时直接报告给 Runner, 并在结束时清理临时对象、静音 clip 及运行状态. 不需要预先打开 Core 场景; Mixer 资产配置仍需在实际 Core 场景验证.

手动诊断仍可在带 TimerRunner 的 Play Mode 场景调用 `AudioIntegrationChecks.Run()` 并读取 `LastResult`.
