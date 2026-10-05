# GameState 子系统

## 职能

维护游戏的全局状态标签, 并施加状态变化所隐含的机制.

- 拥有 `GameState` 枚举与 `GameStateManager`.
- 接受 `Freeze` / `Resume` 请求, 并在状态变化后对外广播 `Changed`.
- 拥有 `Loading` 状态的进入与退出, 由 `SceneSwitchManager` 驱动.
- **不负责**决定谁应当响应, 也不负责响应内容.
- 不引用任何其他 Subsystem, 包括 `AudioManager`.

## 构成

| 路径 | 类型 | 职责 |
| --- | --- | --- |
| `GameState.cs` | 枚举 | 状态标签; `Playing` / `Freezed` / `Loading` |
| `GameStateManager.cs` | 静态类 | 持有 `Current`; 提供 `Freeze` / `Resume` / `EnterLoading` / `ExitLoading`; 广播 `Changed` |

## 公共API

- `GameStateManager.Current` -> `GameState`: 当前状态. 从第一个场景加载之前到进程结束均有效.
- `GameStateManager.Freeze()`: 挂起游玩. 停止游戏时间, 暂停音频监听器.
- `GameStateManager.Resume()`: 恢复游玩. 还原进入 `Freezed` 之前的时间缩放.
- `GameStateManager.EnterLoading()` / `ExitLoading()`: 声明场景切换开始/结束. **不施加任何机制**.
- `GameStateManager.Changed` -> `event Action<GameState>`: 在 `Current` **改变之后**触发.

## 内部实现思路

### 为什么是静态类而不是 Component

状态必须在**第一个场景对象 `Awake` 之前**就是正确的. Component 要做到这一点, 就得依赖一个必须手动放置的物体, 而模版的约定是"无需手动装配". 因此与 `Setting` 一致, 采用静态访问器.

它**不需要自举**: `Current` 初始即 `Playing`, 而这正是一个尚未冻结、也未开始加载任何东西的会话所应处的值. 这里刻意**没有** `RuntimeInitializeOnLoadMethod` —— 早先的注解曾声称有, 那是文档与代码不符, 已修正.

**代价**: 与 `SettingBootstrap` 同样,**无法在 Hierarchy 中找到它**.

### 机制归属: 为什么 GameStateManager 动 timeScale

`Freezed` 不只是标签, 它真的会 `Time.timeScale = 0f` 并 `AudioListener.pause = true`. 这是刻意的: 若只发标签, 每个想暂停的调用方都得自己记住要停什么, 于是"暂停"会在各处被部分实现.

但这也意味着一条必须记住的相互作用: **`Time.timeScale = 0` 会让走 scaled time 的东西全部停摆**. 音频侧为此已把 emitter 的结束判定改为 `UseUnscaledTime()` —— 否则暂停期间 `AudioEmitter` 永不归还池.

### `Loading` 为什么不施加机制

`Loading` 由 `SceneSwitchManager` 在加载开始后进入, 在新场景被激活的瞬间离开 —— 那正是"新场景尚不可见"的窗口.

它**不设** `Time.timeScale`, 也**不暂停**音频. 理由是 `Freezed`(游玩被挂起)与 `Loading`(正在换成另一个场景)是两件不同的事:

- 加载期间你**可能**仍希望音乐延续、希望自己的过渡表现照常走动;
- 而 `Time.timeScale = 0` 会连带停掉走 scaled time 的一切, 这个副作用不该由"加载"顺带引入.

这条划分与音频侧把 `SurviveFreeze` 从 `Loop` 上剥离开是同一个判据: **把两个本就独立的决定合并, 会得到一个无法表达"只想要其中一个"的模型**.

**因此有一处必须记住的后果**: 因为 `Loading` 不动时间也不暂停音频, 所以音频的冻结入口门**不认** `Loading`, 加载期间所有音频请求照常通过. 这是有意的, 不是遗漏 —— 若将来希望加载期间挡掉玩法音效, 那需要独立裁决, 而不是让 `Loading` 悄悄变成第二个 `Freezed`.

### 幂等与可还原

`Freeze` 与 `Resume` 都是**幂等**的, 重复调用只记一条日志. `EnterLoading` / `ExitLoading` 同样幂等.

时间缩放不是固定写 `1f` 还原, 而是**记忆进入时所生效的值**:

- 若写死 `1f`, 任何故意以非 1 倍速运行的玩法(慢动作, 子弹时间)会在暂停一次后**永久丢失**其缩放, 且排查时毫无线索.
- 因此 `Freeze` 记录当时的 `Time.timeScale`, `Resume` 还原它.
- **关键约束**: 已处于 `Freezed` 时再次 `Freeze` 必须直接返回, 不得覆盖已记录的值 —— 否则会把 `0f` 记进去, 解冻后永久冻结.

`Loading` 同理**记忆进入前的状态并在退出时还原**(`stateBeforeLoading`), 而不是假定回到 `Playing`:

- 若写死还原为 `Playing`, 那么"在冻结状态下触发了一次场景切换"会**静默解冻** —— 调用方并未要求解冻, 却得到解冻.
- 这类"不报错但行为错了"的缺陷最难排查, 因此用一次记忆消除它.
- 与 `Freeze` 同理, 已处于 `Loading` 时再次 `EnterLoading` 必须直接返回, 不得覆盖记录.

### 广播顺序

`Apply()` **先赋值 `Current`, 后触发 `Changed`**. 顺序不可颠倒: 处理函数会读取 `Current`, 若先广播再赋值, 每个处理函数观察到的都是上一个状态.

### 谁响应状态

`GameStateManager` 不认识任何响应者. 需要响应状态变化的组件自行订阅 `Changed`. 目前的唯一订阅者是 `AudioManager`, 它在 `OnEnable` / `OnDisable` 中订阅与退订, 据状态暂停或恢复 `AudioListener.pause`.

它**不**决定冻结期间是否放行新的播放请求 —— 那属于音频自己的入口门.

## TODO

- TODO: 没有可点击的测试入口. `GameStateManager` 是静态类, `NaughtyAttributes` 的 `Button` 只出现在 Component 的 Inspector 上, 没有可挂载的面. 未实现原因同 `SettingBootstrap`; 验证需由玩法代码调用 `Freeze` / `Resume` / `EnterLoading`.
