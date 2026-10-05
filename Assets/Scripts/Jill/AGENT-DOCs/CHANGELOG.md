# CHANGELOG

## 2026-10-05 — 交付: SceneSwitch 子系统 (切换入口 + Loading 状态 + 跨场景访问点)

### 共识 (grill 结论)

经四轮拷问定下的六条, 以及它们各自否决了什么:

| # | 决策 | 否决项与理由 |
| --- | --- | --- |
| 1 | 职责**仅入口**: 校验 + 启动一次异步加载, 返回 `AsyncOperation` | 否决自定义 `ISceneSwitchHandle`: `AsyncOperation` 就是 Unity 为这件事提供的句柄, 再包一层不增加表达力 |
| 2 | 新增 `Loading` 状态, 由 `SceneSwitchManager` 驱动 | 否决复用 `Freezed`: 那会把 `Time.timeScale = 0` 与音频暂停一起锁死在"加载"上 |
| 3 | 目标场景用 `SceneId` 枚举 + `SceneSwitchConfigs` 白名单校验 | 否决纯字符串: Unity 对不存在的场景名抛异常或静默停在当前场景, 与 GameLog 体系相悖 |
| 4 | `CoreFacade` 成为单例, 提供静态 `Instance` | 这是对 §5.2.1"少用单例"的**唯一一次刻意破例**, 理由见下 |
| 5 | 依赖方向为**拉**: 场景内容在自己的 `Awake` 里取回 Core | 否决**推**(Core 主动查找场景类型): 那样每加一个场景子系统都要改 Core |
| 6 | 运行时**确实**会多次切换场景 | 若答"只在启动时加载一次", 则本子系统整个不该存在 |

### 场景架构: 为什么 Core 与内容同场景

选定"Core 留在内容场景内 + `DontDestroyOnLoad` + `LoadSceneMode.Single`", 否决了"Core 独占常驻场景 + 加法加载".

**决定性理由是用户的测试成本**: 在加法方案下每个内容场景里都没有 Core, 因此单独播放某个内容场景时它不工作 —— 要么先播 Bootstrap 场景(于是测不了单场景), 要么手动把 Core 加回去, 而"测试时的手工接线"正是本次要消灭的成本. 单人开发的模版里, 这个成本高于"场景图结构性排除两个 Core"的收益.

代价: 需要重复实例守卫, 而它是单例标配, 约 6 行.

### 新增

- `SceneId`: 可切换场景的键. 初版**只含 `SampleScene`**, 因为它是工程里唯一存在的场景 —— 先加键会引用一个尚不存在的场景, 从而破坏编译边界.
- `SceneSwitchConfigs`: `SceneId` -> 场景名的白名单, 含 `OnValidate` 的重复项清除与空名检查(§5.2.2.5).
- `SceneSwitchManager`: 唯一切换入口. `RequestSwitch` 过四道校验(白名单已赋值 / 无切换在途 / 键有映射 / 场景已注册 Build Settings), 全过才加载.
- `CoreFacade.Instance`: 跨场景访问点.
- `CoreFacade.SceneSwitch`: 暴露切换总线.
- `GameState.Loading` 与 `GameStateManager.EnterLoading` / `ExitLoading`.

### 关键实现决策与理由

**`Loading` 不施加任何机制.** 不设 `Time.timeScale`, 不暂停音频. 理由与音频侧把 `SurviveFreeze` 从 `Loop` 上剥离是同一判据: `Freezed`(游玩被挂起)与 `Loading`(正在换成另一个场景)是两件不同的事, 合并会得到一个无法表达"只想要其中一个"的模型.

**由此产生的必然后果**: 音频的冻结入口门**不认** `Loading`, 加载期间所有音频请求照常通过. 这是有意为之而非遗漏 —— 让 `Loading` 悄悄变成第二个 `Freezed` 才是错误.

**`Loading` 记忆并还原前一个状态**(`stateBeforeLoading`), 而不是假定回到 `Playing`. 否则"在冻结中触发了一次切换"会**静默解冻**, 属于"不报错但行为错了"的缺陷. 与 `Freeze` 的记忆时间缩放同构.

**激活被显式推迟**: `allowSceneActivation = false`, 等 `progress >= 0.9` 再放行, 放行后**立刻**退出 `Loading`. Unity 的 `progress` 在 0.9 处停住, 不放行则加载永不完成; 而放行与退出必须相邻, 中间不留"既无 Loading 也未激活"的空档.

**切换目标是参数, 不是字段.** "接下来去哪个场景"是玩法决定(打完关卡 -> 下一关), 不是总线配置; 若放成 `NextScene` 字段, 它就成了第二个真相来源, 且每次测试换场景都要改 Inspector.

**同一场景再次请求也走同一条拒绝路径** —— 不引入"是否已在目标场景"的额外状态.

**已否决**: 取消进行中的 `LoadSceneAsync`(它没有 Cancel, 只能标记忽略, 于是留下一个仍在加载的场景); 排队下一次切换(顺序自动切两个场景几乎总是发起方的 bug); 为 `Loading` 单独加音频入口门(会把 `Loading` 变成第二种 `Freezed`).

### 为什么这里必须破例使用单例

场景切换会销毁**所有**场景内引用. 切换前场景对象可以靠 Inspector 拿到 `CoreFacade`, 切换后那个引用指向的实例早已随旧场景销毁 —— 它变成悬空引用. 因此"某个不属于任何场景的东西必须能在没有场景的情况下被访问到"是**硬约束**, 不是偏好.

已否决的其他形态: 每个新场景重新拖拽引用(漏接表现为运行时 null 而非编译错误); 服务定位器(为恰好一个消费者引入注册表); 静态门面(仍要持有实例, 只是把单例藏起来).

### 顺带修正的文档与代码不符

`GameStateManager` 的类注解此前声称"由 Unity 通过 `RuntimeInitializeOnLoadMethod` 在第一个场景加载之前建立", 但**该文件里没有这个属性**. 它靠 `Current` 的默认值 `Playing` 恰好正确, 而非有意的初始化. 注解已改为如实描述.

同时清理了两处过期陈述: `GameState/README.md` 称"目前没有任何订阅者"(音频已是订阅者); 其"退出时 timeScale 残留"的 TODO 经用户确认可移除, 已删除并记录理由.

### 一处我自己犯的错, 记录以免重犯

`SceneSwitchManager` 首次编译失败:

```
SceneSwitchManager.cs(51,22): error CS0246: The type or namespace name 'ExpandableAttribute' could not be found
```

`[Expandable]` 属于 NaughtyAttributes, 而我漏了 `using NaughtyAttributes;`. 这已是**同一个 API 家族上的第四次**同类失误(`BoxGroup` 是第一次). 另外, 我在写 `CoreFacade` 时还**推断**出一个 `LogAction.DestroyGameObject`, 而它不存在 —— 我随后提取 GameLog DLL 的元数据字符串核对了真实成员(`Abort` / `ClampValue` / `DisableComponent` / `DisableGameObject` / `Ignore` / `RemoveInvalid` / `Return` / `UseFallback`)并改为 `Ignore`.

两次都说明同一件事: **本工程依赖三个自研包(GameLog / MenuTool / Timer), 它们的 API 不能靠推断, 必须查实**.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `03:39:39`, 体积 `67584` -> `70144` 字节, Bee 的 `noderesult` 为 `exitcode: 0`.
- 逐项核对入产物: `SceneSwitchManager` / `SceneSwitchConfigs` / `SceneId` / `RequestSwitch` / `IsSwitching` / `TryGetSceneName` / `RunSwitch` / `get_Instance` / `get_SceneSwitch` / `Loading` / `EnterLoading` / `ExitLoading` 全部存在.
- **已 Play 模式实测**: 见下方"实测结果". 八项全部通过.

### 实测结果 (2026-10-05, 用户运行)

**已实测通过三项**, 证据全部来自运行日志:

| 项 | 证据 |
| --- | --- |
| 切换成功路径 | `Switching to SampleScene (SampleScene).` 与 `Switched to SampleScene (SampleScene).` 两条均出现 |
| `Loading` 进出时机 | `Game state is now Loading.` 在两条之间; 其后的 `Game state is now Playing.` 由 `ExitLoading` 触发 |
| 重复实例守卫 | `A second Core was found; its GameObject is being destroyed.` + `Another TimerRunner already exists. Component disabled.` |

**八项全部通过**. 其中后五项是"证明某事**没有**发生"(音乐未被暂停、请求未被拒绝、加载未开始、第二次未返回句柄、状态未变成 `Playing`), 这类性质在日志里**天然不留证据**, 因此以用户的实测确认为依据. 清单见 `PENDING_INSPECTOR.md` 9c.

> **一处验收形态的反思**: 我最初把这五项写成了与"证明存在"相同的验收标准, 还要求逐条对照一个只有作者能判定的表. 那是**无效的验收设计** —— 一个只有作者能判定的验收标准, 对使用者不可读. 正确形态是让每项打印一行可读断言(如 `###ASSERT Loading 期间 AudioListener.pause = False (期望 False)`), 使其与前三项具备同一种证据形式. 记此以免重犯.

**过程中的一次状态更正, 值得记录**: 用户第一次勾选本节时, 五项尚无测试证据. 我依据 §5.1.3 把它们**改回未勾选**, 并说明原因 —— 让一次没做过的验证看起来做过, 比留一个未勾选项危险得多. 用户随后完成实测并再次确认, 此时才全部勾上. 即: **勾选状态最终由实测确认驱动, 而非由"代码写完了"驱动**.

### 这轮实测暴露并修掉的两个真实缺陷 (在测试脚本侧)

用户的测试脚本连续撞到同一条规律的两种形态, 二者都源于**切换会销毁场景内对象, 而 Timer 跑在持久的 `TimerRunner` 上**:

1. `NullReferenceException`: 序列化字段 `public CoreFacade core` 随旧场景置空, 而 Timer 回调仍在读它. 修法: 在 `Test()` 开头解析一次 `CoreFacade.Instance` 到局部变量.
2. `MissingReferenceException: The object of type 'DebugScript' has been destroyed ... StartCoroutine`: 回调里的 `StartCoroutine` 把**宿主**绑回了场景内的 `DebugScript`. 修法: 宿主改为持久的 Core.

规律: **Timer 的回调里不能出现任何场景内的东西 —— 既不能是引用, 也不能是宿主.** 这是本子系统引入的固有后果.

### 一处我对用户提议的否决, 以及由此发现的真实缺陷

用户提议把 Timer 的 `callback.Invoke()` 改为 `callback?.Invoke()`. **我不同意并给出证据**: `?.` 只检查委托引用是否为 null, 而"已销毁的 MonoBehaviour"**不是 null 委托** —— `Invoke()` 会正常进入方法体, 在访问 `this` 时抛 `MissingReferenceException`. 日志本身即证明: 栈中出现 `StartCoroutine` 帧, 说明 `Invoke()` 确实被调用过. 且 `At()` 已在入口处拒绝 null 回调, 该列表不可能含 null. 故该改动无效且冗余.

但由此发现了一个**真实缺陷**: 一个回调抛异常会顺着 `ProcessTimeStamps` 传进 `Run()` 协程, **把整个 Timer 连同所有未触发的回调一起杀掉**, 且 `IsRunning` 永远停在 `true`. 用户日志中 `5s` 的 `Freeze` 与 `7s` / `8s` 的回调确实全部未执行. 建议的加固(由用户裁决, 因涉及其自有包): 在 `ProcessTimeStamps` 中逐个回调 `try/catch` 记日志并继续, 并在 `Run()` 用 `finally` 保证状态归位. 代价是异常从"崩溃"变为"一条日志".

### 用户的两处自主改动 (我同意, 理由成立)

- `DebugScript.Test()` 的测试链被删除. 理由: 切到**当前所在场景**是人为构造的边界, 不是实际用法. 该删除不撤销上述三项已验证的证据.
- `SceneSwitchConfigs.RemoveDuplicates` 由 `OnValidate` 自动调用改为**手动 `[Button]`**, `OnValidate` 只报告空名. 理由: Unity 界面上新增条目会默认复制上一项, 因此编辑过程中必然短暂出现重复, 自动去重会在填写完成前删掉它. 这与既有的 `AudioManagerConfigs` 完全同构. **副作用**: 去重不再是自动的, 需要手动点一次按钮.

### 我在本轮犯的一个错误, 记录以免重犯

我读用户的 `SceneSwitchConfigs.cs` 时**截断了文件头**(只看到前两行就判定缺少 `using NaughtyAttributes;`), 基于该错误印象去"修"它, 结果把 `using UnityEngine;` 与 `/// <summary>` **合并到了同一行**.

**关键在于: 合并后编译仍然通过** (`exitcode: 0`). 受损的是格式, 不是语法. 是逐行打印才发现该行被拼接.

教训: **编译绿不等于文件完好**; 编辑他人刚改过的文件之前必须完整读取而非抽样, 且 `write` / `edit` 之后要回读**被改动的那几行**确认形状.

### 需要你手动完成的操作

#### A. Inspector / 资产 (本次交付的落地)

- [ ] 在 `Assets/Scripts/Core/` 下**新建目录** `SceneSwitch` 已由 DSH 创建, 无需操作(此条仅备忘).
- [ ] 创建 `SceneSwitchConfigs` 资产并命名(建议放在 `Assets/SO/` 下, 与 `DefaultAudioManagerConfigs.asset` 同级).
- [ ] 在该资产里加一条映射: `SampleScene` -> `SampleScene`(名字必须与场景文件名逐字一致, **不含** `.unity`).
- [ ] 把 `SceneSwitchManager` 组件加到 `Assets/Prefabs/Core.prefab` 的 `Core` 对象上.
- [ ] 在该组件的 `Configs` 字段里拖入上一步创建的资产. **未赋值时组件会在 `Awake` 自行禁用并记一条错误**.
- [ ] 确认 `ProjectSettings/EditorBuildSettings.asset` 里 `SampleScene` 仍为启用状态(它已在列).
- [ ] Play 一次, 确认 Console 无 `configs` 未赋值的错误.
- [ ] 实测: 在任意脚本里调用 `CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.SampleScene)`, 确认日志出现"Switching to"与"Switched to"两条, 且 `Loading` 在其中间.

#### B. `.menutool` 往返 (§5.2.3.2) — 需要你的操作

`SceneSwitchConfigs` 的 `[CreateAssetMenu]` 属性**已被我注解掉**, 因为它依赖 `MenuTool` 包. 按约定分工:

- [ ] 你修改 `MenuTool` 的 `.menutool` 资产, 加入 `Game/Basic Settings/SceneSwitchConfigs` 节点(仿 `AudioManagerConfigs` 的结构).
- [ ] 你**取消注释** `SceneSwitchConfigs.cs` 里那三行被注释掉的 `[CreateAssetMenu]` 属性(文件顶部, 带 `TODO:` 标记).
- [ ] 你确认 `Game.MenuTool.g.cs` 已重新生成并包含 `SceneSwitchConfigs` 节点.

**注意**: 在此之前, 该资产**无法通过右键菜单创建**. 若你希望先创建资产, 可临时手动 `Create > ScriptableObject` 或先完成 B.

#### C. 本次新增的文件与待生成的 `.meta` (§2.1.1)

以下三个 `.cs` 已由 DSH 创建, `.meta` 已由 Unity 生成(DSH 未触碰 `GUID`). **提交时必须与 `.cs` 一起提交**:

- [ ] `Assets/Scripts/Core/SceneSwitch.meta` (目录)
- [ ] `Assets/Scripts/Core/SceneSwitch/SceneId.cs` + `SceneId.cs.meta`
- [ ] `Assets/Scripts/Core/SceneSwitch/SceneSwitchConfigs.cs` + `SceneSwitchConfigs.cs.meta`
- [ ] `Assets/Scripts/Core/SceneSwitch/SceneSwitchManager.cs` + `SceneSwitchManager.cs.meta`

另有一个**孤儿 `.meta` 待 Unity 删除**(DSH 删除了临时诊断探针 `Assets/Scripts/DshSceneSwitchProbe.cs`, 其 `.meta` 按 §2.1.1 由 Unity 清除, DSH 不得代为删除):

- [ ] 确认 `Assets/Scripts/DshSceneSwitchProbe.cs.meta` 已被 Unity 删除.

#### D. 结转自上一份交付且仍未闭合的项

- [ ] 冻结期间 `AudioSource.isPlaying` 的值仍未观测(见 `PENDING_INSPECTOR.md` 第 6b 节末项). 本次切换交付**不依赖**该观测.
---

## 2026-10-04 — 修正: 冻结期入口门必须同时看到 SurviveFreeze

### 触发 (用户的 Play 模式验收)

用户按 `DebugScript` 跑了一次典型测试, 报告: 冻结期间 `WithAllowWhileFrozen(true)` 且 `SurviveFreeze = false`
的手动请求, 日志出现 `is pooled` 与 `played`, 但 **Hierarchy 中看不到对应 emitter, 且直到 `Application.Quit()` 都没有 release**.

附带的日志把复现固定了下来:

```
 1: ###Freeze Program.        -> is now Freezed
53: ###Play when froze.       -> refused (allowWhileFrozen 未设置, 门正确拒绝)
70: ###Play when froze with allow while frozen. -> is pooled -> played, 无 release
103: ###Resume Program.       -> is now Playing
120: Settings saved            (Application.Quit)
```

### 根因

原门只检查了"是否允许进入":

```csharp
if (GameStateManager.Current == GameState.Freezed && !(allowWhileFrozen ?? false)) ...
```

于是"允许进入但不会熬过冻结"的请求**照样取走了池化 emitter**. 而这个播放被 `AudioListener.pause` 静音,
对自己对使用者都没有价值, 却在冻结期间占着池槽 —— 它的结束判定依赖 `source.isPlaying` 转为 false,
而冻结期间该判定无法推进, 于是直到解冻才可能归还. 用户因此既看不到 emitter(未被注入任何可听行为),
也看不到 release.

### 修法 (用户选定方案 a)

门改为在**解析出 clip 之后**执行, 并且**同时**要求两件事:

```csharp
bool willSurviveFreeze = surviveFreeze ?? data.DefaultSurviveFreeze;

if (!IsFrozenEntryAllowed(requested, allowWhileFrozen, willSurviveFreeze))
    return null;
```

`IsFrozenEntryAllowed` 只在"允许进入 **且** 确实会被保留"时放行, 否则记一条说明原因的日志并拒绝.

由此确立的语义, 已写入 `Play` 的注解与 README:

> **冻结期真能听到的请求 = 允许进入 且 熬过冻结.**
> 单用 `WithAllowWhileFrozen(true)` 并不足以在冻结期出声.

这也把门放在了正确的位置: 拒绝发生在**预定 emitter 之前**, 因此不占用任何池资源,
也不需要让"暂停"这层语义渗进"声音是否放完"的判定.

### 顺带的结构改动

- 抽出 `ResolveClip(AudioId)`, 把"解析失败即记错误并返回 null"收进一处, `Play` 不再内联 `TryGetClip`.
- `Play` 不再重复计算 `surviveFreeze ?? data.DefaultSurviveFreeze`, 而是复用同一变量.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `14:25:41`, 体积 `66048` -> `66560` 字节, 无任何 `CS####` 诊断.
- **尚未 Play 模式复验**. 预期: 上表第 70 行那类请求现在应在 `is pooled` **之前**就被拒绝,
  日志出现 `... would not be kept`, 且 Hierarchy 不新增 emitter.
- **一处仍未闭合的不确定**: 用户此前观察到冻结期间有 **2 次 release**; 若被暂停的音源会报告 `isPlaying == false`,
  说明"冻结期间归还"是可能的, 于是我上文"结束判定无法推进"的推断只在某些情况下成立.
  该机制无法从日志静态判定(需要读运行中的 `AudioSource.isPlaying`), 已记入 `PENDING_INSPECTOR.md` 待下一次运行确认.
  即便该推断不成立, 本次的门仍然正确: 它拒绝的是一个**无论如何都听不见**的请求.

### 未改动

- `AudioEmitter` 的结束判定未改. 把"暂停"塞进"声音是否放完"的判定会让后者不再只关心音源自身.
- `DebugScript.cs` 与 `Debug/` 下的其他文件未改动(用户要求暂留 Debug 代码).
---

## 2026-10-03 — 重构: 把冻结语义从 Loop 上剥离, 拆成两个正交参数

### 动机 (用户的判断)

"是否 Loop" 与 "是否能在 Freeze 时播放" 是**两个概念**, 应当分开. 而 "是否能在 Freeze 时播放" 本身
又分成两件事: 进入冻结时**已在播放**的音是否被掐断, 以及冻结期间**新到达**的请求是否被受理.

旧实现把前者绑在 `Loop` 上(`AudioClipData.FreezeLoop`), 因此无法表达"一次性音也需要熬过冻结",
也无法表达"循环音应该被冻结掐断". 名字与语义都不成立, 故整体替换.

### 两个参数各自的机制

| 参数 | 问题 | 机制 |
| --- | --- | --- |
| `SurviveFreeze` | 进入冻结时已在播放的音是否被保留 | `AudioSource.ignoreListenerPause` |
| `AllowWhileFrozen` | 冻结期间新到达的请求是否被受理 | `AudioManager.Play` 入口检查 |

两者**不是同一个开关的正反两面**: 一个已在播放的音可以被保留, 而一个新请求仍被拒绝; 反之亦然.

### 改动

- `AudioClipData`: `FreezeLoop`(默认 `true`) -> `DefaultSurviveFreeze`(默认 `false`).
  默认值同时从 `true` 翻转为 `false`, 以**保持既有行为**: 旧逻辑下一次性音不熬过冻结, 且 `Loop == false` 时
  `FreezeLoop` 无意义, 因此默认为 `false` 是唯一不改变现状的选择. 需要熬过冻结的音效应显式开启.
- `AudioBuilder`: 新增 `WithSurviveFreeze(bool)` 与 `WithAllowWhileFrozen(bool)`, 两者都是可空覆盖 ——
  不设置即回落到 `AudioClipData` 的静态初值, 与本工程既有的静态/动态划分一致.
- `AudioEmitter`: 新增 `SurviveFreeze` 属性, 映射到 `source.ignoreListenerPause`;
  `Configure()` 只写静态初值, 覆盖由 `AudioManager` 在之后应用; `ResetEmitter()` 一并清除该状态与 `ignoreListenerPause`.
- `AudioManager.Play` 新增两个可空参数, 并在入口处加冻结门: 冻结中且未声明 `AllowWhileFrozen` 的请求被拒绝并记录 `AudioId`.

### 为什么 `WithAllowWhileFrozen` 放在 Builder 而不是 AudioManagerConfigs

它是**逐次请求**的决定, 而不是总线级策略. 放在 asset 配置里会让"这一次请求能不能通"无法表达,
而需要绕过门的请求(例如暂停菜单里的点击音)恰恰是逐次的. 若将来出现"整段时间内全部拒绝"的需求,
那才属于总线级策略, 应另加一个开关, 而不是把逐次决定升格为全局.

### 未改动

- `AudioEmitter.prefab` 未被改动: 两个决定都写在代码里, 无需在 prefab 上逐个勾选.
- `AudioSettings` 的全局音量仍未被应用(见 `Core/README.md` 的 TODO), 属于独立交付.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `12:41:48`, 体积 `64512` -> `65536` 字节, 无任何 `CS####` 诊断.
- 产物含 `WithSurviveFreeze` / `WithAllowWhileFrozen` / `DefaultSurviveFreeze` / `SurviveFreeze`.
- **尚未 Play 模式实测**两个参数的四种组合. 验收清单见 `PENDING_INSPECTOR.md` 第 6 节.

### 文档

- `Core/README.md`: 重写"音频与游戏状态"一节为零件模型与参数层级表; 同时删掉仍声称"`AudioManager` 在 `Start` 中
  校验 Timer 宿主"的过期段落(该依赖已被用户移除), 并补充音频 `Setting` 的构成行与"建池为何是 `Start` 的例外".

---
## 2026-10-03 — 修正: JSON 加载的四个缺陷 + AudioManager 注解与守卫

### 触发

用户的验收在 `.json` 一项上报告了四种异常, 并指出 `AudioManager` 的注解与代码不符. 本条逐项处理.

### 根因: 我在注解里写了一个错误的假设

`FileSettingStore.Deserialize` 的原注解写着 *"JsonUtility.FromJson returns null for input it cannot parse"*.
**这是错的**: `JsonUtility.FromJson` 对语法错误与缺项是**抛 `ArgumentException`**, 而不是返回 null.
于是 `SettingStore.Load` 里的 `if (loaded == null)` 分支永不生效, 异常直接穿出 `SettingBootstrap.Initialize`,
设置加载整体失败. 这是"注解与代码不一致"的又一例, 而且这次注解本身是错的 —— 前几次是代码漏了注解所述的行为.

### 四个症状与处理

| 报告的症状 | 实际原因 | 处理 |
| --- | --- | --- |
| 损坏 json: `ArgumentException: JSON parse error` | `FromJson` 以抛异常报错, 未被捕获 | `TryDeserialize` 捕获并返回 `false`, 回落到设计默认值 |
| 缺项 json: 同一行抛出 | 同上 | 同上 |
| 只有大括号 `{}`: 填类型默认值(int 0) | `FromJson` **新建实例**, 缺失成员取类型零值, 丢弃设计默认值 | 改用 `FromJsonOverwrite` 覆盖到一个**已用设计默认值铺底**的实例上 |
| 完全空白: 正确报警 | 原本已正确 | 保留; 另补 `IsNullOrWhiteSpace` 分支, 使"空文件"与"解析失败"在日志里可区分 |

### 契约变更

- `SettingStore.Load()` 现在**先**用设计默认值铺底, 再让子类把文件覆盖其上. 这是"缺项回落到设计默认值"的前提.
- 新增 `protected void LoadInto(TData)`, 承载文件那一半的加载与全部失败回落.
- `Deserialize(string) -> TData` 改为 `TryDeserialize(string, TData target) -> bool`.
  改成 bool 是必要的: `FromJsonOverwrite` 原地覆盖, 无法用返回值表达失败, 而让异常逃出会中断自举.
- 新增 `ReportLoadFailure`, 把"文件不可读 / 空白 / 解析失败"统一成一条 warning.

### 粒度限制 (已写入注解, 不作过度承诺)

新实现保证**顶层**缺失成员取设计默认值. 但**不保证**嵌套可序列化成员内部, 或存在但只填了一部分的数组元素内部也如此 ——
`JsonUtility` 在那里没有文档化的保证. 这条限制写在 `FileSettingStore.TryDeserialize` 的注解里.

### `AudioManager`: 注解与代码不符, 以及一个真实缺陷

用户已将 Timer 依赖整体移除(池改在 `InitializeInternal` 中随注册表一起建立并预热, 不再有 `TimerRunner` 校验).
但注解未随之更新, 出现五处陈述与代码相反的文本, 已全部改正:

- 类注解: "池在 `InitializeInternal` 中构建, 在 `Start` 中预热" -> 池与注册表同在 `InitializeInternal` 中构建, `Start` 只做预热.
- `timerRunner` 字段的 XML 注解在字段被删除后**悬空**, 连同留出的两个空行一并移除.
- `Start` 的注解写的是"校验 Timer 宿主", 已改为"预热对象池"并说明为何预热可以推迟而建池不可以.
- `BuildEmitterPool` 的注解称"由 `Start` 调用", 已改为"由 `InitializeInternal` 调用".
- `Play` 的守卫注解提到"Start 因缺少 Timer 宿主而禁用", 已改为"初始化失败, 例如 `configs` 未赋值".

**顺带修掉一个真实缺陷**: `InitializeInternal` 在 `configs` 为空时设 `enabled = false`, 但紧接着仍调用
`BuildEmitterPool()`, 而后者第一件事就是读 `configs.CollectionCheck` —— 必然 `NullReferenceException`.
已在该分支加 `return`.

### 代码风格

- 我新增的非 Unity 类型中, `GameLog` 调用由 `GameLog.Info(null)` 改为**省略 context** 的 `GameLog.Info()`,
  与 GameLog 自身 README 对"静态/非 Unity 系统"的指引一致. 共 7 处 (`SettingStore` 4, `GameStateManager` 3).
  这次编译同时证明了该无参重载确实存在.
- 删除 `AudioManager` 中 `BuildEmitterPool` 前后的多余空行与行尾空格.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `03:33:39`, 体积 `58368` -> `64512` 字节, 无任何 `CS####` 诊断.
- 产物含 `TryDeserialize` / `LoadInto` / `FromJsonOverwrite` / `ReportLoadFailure`.
- **尚未复验四个 json 场景**: 需要用户按 `PENDING_INSPECTOR.md` 重跑. 本次是"修正后待验", 不是"已验".

---
## 2026-10-03 — 交付: 音频接线 (FreezeLoop + ignoreListenerPause + 订阅 GameState)

### 范围

把 `GameState` 与音频连起来, 这是本轮三份交付中唯一跨 Subsystem 的一份.

### 改动

- `AudioClipData` 新增 `FreezeLoop`(默认 `true`), 暴露为 `FreezeLoop` 属性.
- `AudioEmitter.Configure()` 写入 `source.ignoreListenerPause = data.Loop && !data.FreezeLoop`.
  **纯代码, 无需改 `AudioEmitter.prefab`** —— 这是选它的理由: 否则每个音效都要在 Inspector 里勾选一次.
- `AudioManager` 新增 `OnEnable` / `OnDisable`, 订阅与退订 `GameStateManager.Changed`;
  新增 `HandleGameStateChanged(GameState)`, 据状态暂停或恢复 `AudioListener.pause`, 并在值未变化时提前返回.

### 三种组合的语义

| `Loop` | `FreezeLoop` | 效果 |
| --- | --- | --- |
| `false` | 无关 | 一次性音效被暂停切断 |
| `true` | `true` | 循环音随游戏暂停(默认) |
| `true` | `false` | 循环音在游戏冻结时继续播放 |

### 依赖方向

音频认识 `GameState`, 而 `GameState` 对音频一无所知. 这是 grill 中 A13(c) 的选择 ——
"谁受影响, 谁处理", 因此 `GameStateManager` 不持有任何 Subsystem 的引用.

按 §4.2, 本次跨 Subsystem 引用要求被引用方**先交付**. 顺序已满足: `GameState` 于前一条交付, 本次才引用它.

### 未做的部分 (刻画边界)

`AudioManager` **不**决定冻结期间是否允许新的播放请求. 它只处理"已在播放的音如何受暂停影响".
是否允许暂停时发出新声音属于玩法判断, 不是音频判断.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `23:18:53`, 体积 `57344` -> `58368` 字节.
- 产物含 `get_FreezeLoop` / `HandleGameStateChanged` / `ignoreListenerPause`, 即 **Core -> Game 的跨程序集引用解析成功**.
- **尚未 Play 模式实测**: 循环音解冻后是否从原处续播, 是本次唯一无法从 Unity 文档确认的行为.
  官方文档只写明"恢复时从暂停处继续", 未写明暂停期间 `AudioSource.isPlaying` 的值. 验收项已记入 `PENDING_INSPECTOR.md` 第 6 节.

### 顺带修正

`AudioManager` 中 `timerRunner` 字段的 XML 注解被其后的空行与字段声明分离, 注解落到了空行上. 未改动它 ——
那属于既有排版瑕疵, 且注释文本本身正确; 记此以免下次被误认为本次引入.

---
## 2026-10-03 — 交付: GameState 子系统 (状态标签 + timeScale 幂等机制 + 静态事件)

### 范围

新建 `GameState` 子系统: 全局状态标签与状态变化所隐含的机制. 与 `Setting` 一样采用自举, 不依赖任何装配.

### 新增文件

| 文件 | 类型 | 职责 |
| --- | --- | --- |
| `Assets/Scripts/Game/GameState/GameState.cs` | 枚举 | 状态标签; 目前仅 `Playing` 与 `Freezed` |
| `Assets/Scripts/Game/GameState/GameStateManager.cs` | 静态类 | 持有 `Current`; `Freeze` / `Resume`; 广播 `Changed` |
| `Assets/Scripts/Game/GameState/README.md` | 文档 | 本子系统的职责, API 与内部思路 |

### 关键决策 (来自 grill 的结论)

- **机制归属**: `Freezed` 真的会 `Time.timeScale = 0f` 并 `AudioListener.pause = true`, 不只是标签. 若只发标签, 每个想暂停的调用方都得自己记住要停什么, 于是"暂停"会在各处被部分实现.
- **时间缩放记忆**: `Resume` 还原**进入 `Freeze` 时所生效的值**, 而不是写死 `1f`. 写死会让慢动作/子弹时间类玩法在暂停一次后永久丢失其缩放, 且无任何线索可查.
- **幂等**: 已处于 `Freezed` 时再次 `Freeze` 直接返回, **不得覆盖已记录的值** —— 否则会把 `0f` 记进去, 解冻后永久冻结. 这条是上一条成立的前提.
- **广播顺序**: `Apply()` 先赋值 `Current` 后触发 `Changed`. 颠倒会使每个处理函数观察到上一个状态.
- **响应者自决**: `GameStateManager` 不认识任何响应者, 需要响应的组件自行订阅 `Changed`. 目前**零订阅者**; 音频接线将加入第一个.

### 与既有子系统的相互作用 (必须记住的一条)

`Time.timeScale = 0` 会让所有走 **scaled time** 的东西停摆. 音频侧为此已把 emitter 的结束判定改为 `UseUnscaledTime()`
(见 `AudioEmitter.cs`), 否则暂停期间 `AudioEmitter` 永不归还池. 这两处改动是一对, 不能只保留其一.

### 未改动

- 未新增预制体或场景物体; 未改动 `Core` 或 `Setting` 的任何文件.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `23:17:34`, 体积 `52736` -> `57344` 字节.
- 产物含 `GameStateManager` / `timeScaleBeforeFreeze` / `Freeze` / `Resume` / `Changed`.
- 与 `Setting` 同批核实, 因此两者共享同一次编译证据.

### 未解决的设计缺口

- **无测试入口**: 静态类无法挂 `NaughtyAttributes` 的 `Button`. 已记入本子系统 `README.md` 的 `TODO`.
- **退出时恢复状态未实现**: 若在 `Freezed` 下退出编辑器, `Time.timeScale` 会保留为 `0`. 因进入播放模式时 Unity 会重置它, 实际影响限于编辑器会话内; 是否值得加钩子待定. 已记入 `TODO`.

---
## 2026-10-03 — 交付: Setting 子系统 (泛型存储 + JSON 落盘 + 自举)
> **后续更正**: 本条的"编译校验未完成"已经解决, 但**首次编译确实失败**, 原因如下.
> `SettingStore<TData>` 是普通类, 而我写了 `GameLog.Info(this)` —— `GameLog` 的 context 参数类型是
> `UnityEngine.Object`, 普通类无法传入. 编译报 5 处 `CS1503`. 同一批代码里 `GameStateManager` 用的是
> `GameLog.Info(null)`(正确), 两处写法不一致, 只有一处对.
> 该错误只用一次编译即可暴露, 本可在写入前靠"普通类不能作为 Unity 对象"这一条避免.
> 已改为 `GameLog.Info(null)`(共 5 处); 修正后 `Assembly-CSharp.dll` 由 `52736` 增至 `57344` 字节,
> 五个类型 `ISettingData` / `SettingStore` / `GameSettings` / `FileSettingStore` / `SettingBootstrap` 均进入产物.

### 范围

新建 `Setting` 子系统: 设置的数据存储与生命周期. 只做"存取", 不做"应用".

### 新增文件

| 文件 | 类型 | 职责 |
| --- | --- | --- |
| `Assets/Scripts/Game/Setting/ISettingData.cs` | 接口 | 持久化数据的契约 |
| `Assets/Scripts/Game/Setting/SettingStore.cs` | 泛型抽象类 | 加载, 持有, 保存, 重置; 拥有文件路径与全部文件 I/O |
| `Assets/Scripts/Game/Setting/GameSettings.cs` | `[Serializable]` 类 | 模版设置数据; **默认无任何字段** |
| `Assets/Scripts/Game/Setting/FileSettingStore.cs` | 具体类 | 指明持久化类型, 提供 JsonUtility 的两半实现 |
| `Assets/Scripts/Game/Setting/SettingBootstrap.cs` | 静态类 | 入口; BeforeSceneLoad 建立并加载存储 |
| `Assets/Scripts/Game/Setting/README.md` | 文档 | 本子系统的职责, API 与内部思路 |

### 关键决策 (来自 grill 的结论)

- **自举而非 Component**: 设置必须在第一个场景对象 `Awake` 之前可读; Component 要做到这点就得依赖手动装配的物体, 而模版的约定是无需手动装配. 代价是入口**隐式**, 无法在 Hierarchy 中找到.
- **存储不应用**: `FileSettingStore.Configure()` 是空的, 且这是约定而非遗漏. 若存储去应用音量, 它就必须认识 `AudioManager`; 那会让 Setting 从所有系统的上游变成下游. 设置集中存储, 由**所描述的系统自行读取**.
- **没有键值接口**: 落盘走 `JsonUtility`, 它无法序列化 `Dictionary` 或接口字段; 提供 `Get<T>(string key)` 会诱使数据进入字典, 而字典会被**静默持久化为空** —— 只在重启后才暴露. 需要动态键值对的存储应自行持久化一个键值对列表.
- **保存是显式的**: 改动值不落盘, 保持"已改动"与"已提交"为两个独立决定. `Application.quitting` 只作为兜底. `ResetToDefault()` 仅改内存, 不落盘.
- **失败即回退**: 文件缺失, 不可读, 或 JSON 无法解析, 一律回退默认值并记录原因. 损坏的设置文件不得阻止游戏启动.

### 被否决的一条路

工程引用了 `ayellowpaper.serialized-dictionary`, 但**不能**用于 JSON 落盘: 其全部变更接口位于 `#if UNITY_EDITOR` 内, 运行时 `OnAfterDeserialize` 会清空内部列表; 能否重建字典取决于 Unity 反序列化器是否在运行时调用该回调, 而这一点无法在本工作区内验证. 因此不作为设计基础.

### 未改动

- 未新增预制体或场景物体: 自举不依赖任何装配 (§3.2 禁区未触碰).
- 未改动 `Core` 的任何文件.

### 验证状态 (未完成)

**编译校验未能完成, 本轮不能声称已验证.**

- 5 个 `.cs` 已落盘 (`23:00`–`23:01`), `Assets/Scripts/Game/` 与 `.../Setting/` 两个目录均已创建.
- 但截至 `23:06`, **`.meta` 一个都未生成**, `Assembly-CSharp.dll` 仍停留在 `22:13:37`, 且不含任何新类型.
- 现象与既有模式一致: Unity 进程存活(锁被持有), 但自 `22:58:00` 起未触碰任何项目文件 —— 编辑器失焦后资产导入与脚本编译循环均不推进.
- 需要用户让 Unity 重新获得焦点, 由 DSH 复核产物后才能确认编译通过.

### 未解决的设计缺口

- **无可点击的测试入口**: `SettingBootstrap` 是静态类, `NaughtyAttributes` 的 `Button` 只出现在 Component 的 Inspector 上, 因此没有可挂载的面. 已记入该类自身的 `TODO`; 验证途径为 `Application.quitting` 或玩法代码显式调用 `Save()`.
- **数据版本与迁移未实现**: 读取失败即回退默认值, 没有"从旧版本升级"的路径. 尚无字段, 故无数据可迁移; 等出现第一个字段再决定版本方案. 已记入本子系统 `README.md` 的 `TODO`.

---
## 2026-10-03 — 结论: 输入系统的边界 (不继续开发)
> **后续更正 (2026-10-03)**: 本条记录的 `public PlayerControls Controls => controls;` **已被 revert**, 相应代码不存在.
> 使用者的否决理由是: `controls` 属于底层, 不应暴露; 输入设计改变时 wrapper 改变不可避免;
> `InputManager` 暴露的事件与 properties 应当为唯一 entry point. 本条其余事实(层级边界, 无消费者,
> 资产现状)仍然成立, 但"新增一条访问器"这一改动与结论**作废**. 详见 `Assets/Scripts/Core/README.md`.

### 结论

**输入系统不再继续实现.** 它当前的形态已经落在正确的层级上, 缺少的只有一条"可扩展通道"和一份说明. 本轮只补这两样.

### 判据 (grill 的产出)

把"输入"拆成三层后, 问题不再是"要不要做输入", 而是"停在哪一层":

| 层 | 内容 | 归属 |
| --- | --- | --- |
| L1 设备与绑定 | 物理控件, 控制方案, 重绑定, 多设备 | 属 asset, 归使用者 |
| L2 原始轴 | 设备到无名模拟轴与按钮的映射 | 模版提供 |
| L3 语义 | 哪一轴是什么, 与重力的关系, 谁拥有哪个自由度 | **刻意排除** |

排除 L3 的依据: 二维输入只有两个自由度, 而"这两个自由度是什么"是玩法信息. 横版与俯视两类玩法在**某一轴上恰好重合**,
因此 asset 里"一根已接线的轴 + 一根留空的轴"不是模版的断言, 而是当下两类玩法的重合面.
模版不对重力方向, 轴向命名, 或额外键位做任何猜测.

同时记录两条被明确搁置的:

- **非键盘输入**(手柄/触屏): 当前开发只涉及键盘; 且加入其他渠道不要求重写 wrapper, 因此先搁置.
- **控制方案(control schemes)**: asset 的 `controlSchemes` 目前是空数组, 全部绑定的 `groups` 也是空串.
  这属于 L1, 是使用者按自己的设备集合去填的内容, 模版不预设.

### 事实核查 (本轮据此调整了判断)

- `MovementInput` 与 `IsPrimaryPressed` 在改动前**零消费者**; 唯一曾使用输入的是 `DragAndDropService2D`(见下).
- `PlayerControls` 的动作类型只存在于 asset 元数据(`expectedControlType`); 生成类把它们一律暴露为 `InputAction`,
  `Vector2` / `Button` 的语义由手写的 `ReadValue<Vector2>()` / `IsPressed()` 落实.

### 改动

**1. `InputManager` 新增一条可扩展通道**

```csharp
public PlayerControls Controls => controls;
```

改动前的 wrapper 把三个 `Gameplay` 动作**逐个转写**成属性, 于是使用者要加一个跳跃键时只有两条路:
自己 `new PlayerControls()`(造成**同一 asset 两个实例**, 各自维护设备状态), 或回来改 wrapper.
新增该访问器后, 使用者往 asset 里添加任意动作都可用 `CoreFacade.Input.Controls` 读取, 无需改动 wrapper.
这是"让 wrapper 可扩展"与"不猜键位"能同时成立的唯一方式: wrapper 不再枚举动作, 只提供入口.

**2. `InputManager` 补齐 §5.1.4 要求的类注解**: 补上 Subsystem 归属, 存在位置, 职能与 ownership 澄清, 生命周期, 范式, 数据概览;
并明确写出"不负责: 哪一轴是什么, 重力与哪一轴对立, 以及游戏如何响应输入".

**3. `MovementInput` / `PointerPosition` / `IsPrimaryPressed` 标注为扩展点**, 并各附一句说明它们是词汇而非句子,
模版刻意不为它们附带消费者(§5.1.8 要求写明待实现内容与未实现原因).

**4. `Core/README.md` 增补"输入的层级边界"一节**, 记录上表的层级模型, 两条边界, 以及 `Controls` 的用意;
并把 `DragAndDropService2D` 标注为**早期代码**, 说明它目前没有任何使用者, 不在模版范围内继续演进.

### 未改动

- `Assets/Infra/InputSystem/PlayerControls.inputactions`: 属资产(§2.1 禁止 DSH 写入). 本轮**未提出**任何 asset 变更 ——
  按上述结论, asset 的键位与控制方案本就归使用者.
- `Assets/Infra/InputSystem/PlayerControls.cs`: 由 `InputActionCodeGenerator` 生成(§3.2 禁止修改).
- `Assets/Scripts/Core/Input/DragAndDropService2D.cs`: 早期代码, 未改动.

### 验证

- 编译通过: `Assembly-CSharp.dll` @ `19:06:37`, 体积 `52736` 字节, Bee/Tundra 后端同刻运行.
- 产物中出现 `get_Controls`, 该成员仅存在于本轮新增的源码中, 因此可确认编译确实纳入了本次改动.
- 说明: 该次编译后 dll 体积与上一次相同(均为 `52736`), 一度怀疑编译未生效;
  经检查 `get_Controls` 存在而排除. 记录于此以免下次又被同类巧合误导.

### 已知缺口 (沿用, 未变)

- 淡入淡出未实现: 音频抢占仍是硬切.
- 总线整体缩放未实现: `Master.mixer` 的 `m_ExposedParameters` 仍为空数组.

---
## 2026-10-03 — 交付: 音频的空间化 (SpatialBlend / MinDistance / MaxDistance + WithPosition)

### 范围

为音频补上空间化能力. 此前所有声音都是纯 2D, 且**位置对听感无影响**, 因此没有任何"从某处发声"的表达能力.

### 新增

- `AudioClipData` 新增 `SpatialBlend` / `MinDistance` / `MaxDistance` 三个字段(置于 `Spatial` 分组), 并暴露为只读 Property.
  默认值为 `0f` / `1f` / `20f`.
- `AudioEmitter.Configure()` 把三者写入 `AudioSource` 的 `spatialBlend` / `minDistance` / `maxDistance`.
- `AudioEmitter.SetPosition(Vector3)`: 只写一次世界坐标, 不触碰跟随状态.
- `AudioBuilder.WithPosition(Vector3)`: 链式入口; 用 `Vector3?` 承载, 使"未指定位置"与"位置为零"保持可区分.
  终结符 `Play` 把它转交 `AudioManager.Play`, 后者写入 `AudioEmitter.SetPosition`.

### 位置语义 (三种来源, 优先级明确)

| 来源    | 入口                            | 说明                              |
| ----- | ----------------------------- | ------------------------------- |
| 跟随目标  | `WithFollowTarget(Transform)` | 每帧同步; 目标消失后沿用既有规则               |
| 一次性坐标 | `WithPosition(Vector3)`       | 只写一次                            |
| 都不给   | ——                            | emitter 停在池留下的位置, 即 `AudioRoot` |

两者同时给出时**跟随胜出** —— 跟随每帧覆盖位置, 这是实现的自然结果, 已写入文档. 用户确认该取舍可接受:
"如需要移动 SFX, 那么 FollowTarget + Spatial 效果即可."

### 与既有 2D 行为的兼容性

`SpatialBlend == 0` 时位置**完全无影响**, 因此现有三个资产的听感不发生任何变化 —— 本次是纯增量.
反之 `SpatialBlend > 0` 时若既不跟随也不给坐标, 声音会始终从 `AudioRoot` 发出, 空间化形同无效; 二者必须一起使用.

### 已知的不精确之处 (已与用户确认接受)

场景中唯一的 `AudioListener` 挂在 `Main Camera` 上, 位于 `(0, 0, -10)`, 而 `AudioRoot` 在 `(0,0,0)`.
因此相机的 z 偏移会参与距离计算(相机移动时音量会变), 且波幅不是精确的距离衰减.
用户决定: **MainCamera 的 z 值不变**, "计算不需太精确, 目前先实现大致 spatial 效果".

### 未改动

- `AudioEmitter.prefab` 的 `AudioSource` 未改动(其 `Spatialize` 仍为 `0`; `spatialBlend` 是另一个开关, 二者都为 0 时行为一致).
- 未写入任何 `.asset`; 三个资产上的新字段由 Unity 在下次保存时序列化为上述默认值.

### 验证状态

**编译已通过并核实.**

- 首次编译**失败**, 报 `CS0246: BoxGroupAttribute / BoxGroup could not be found`:
  本文件此前只 `using UnityEngine`, 而我用上了属于 `NaughtyAttributes` 的 `BoxGroup`
  (该模式抄自 `AudioManagerConfigs.cs`, 那个文件有 `using NaughtyAttributes;`).
  已补上该 using, 与 §5.2.3.1 对 NaughtyAttributes 的既有用法一致.
  注: 同处使用的 `Min` 来自 `UnityEngine.MinAttribute`, 不需要额外 using; 只有 `BoxGroup` 需要.
- 修正后产物已更新: `Assembly-CSharp.dll` @ `17:48:01`, 体积 `51712` -> `52736` 字节,
  Unity 的 Bee/Tundra 构建后端在同刻运行.
- 产物中已确认存在 `SetPosition` / `WithPosition` / `SpatialBlend` / `MinDistance` / `MaxDistance` / `spatialBlend`,
  以及上一轮的 `willFollowTarget` / `ReleaseEmitter`.
- `AudioClipData.cs` 已出现在 Unity 生成的 MonoScript 注册表中, 说明它进入了编译并注册成功.
- 导入日志中无任何 `CS####` 诊断.

### 文档

- `Assets/Scripts/Core/README.md` 增补"音频的位置与空间化"一节, 并补充了 `willFollowTarget` 的语义、
  以及"音量写入与位置同步互相独立"这一 `LateUpdate` 结构理由.
- `Assets/PENDING_INSPECTOR.md` 加入 spatial 相关验收项 (编译项已勾除).

---

## 2026-10-03 — 修正: 无跟随目标的循环音被立刻归还 (已解决)

本轮由**用户**定位并给出修法, DSH 负责复核、收紧并记账.

### 症状

`loop = true` 且未传 `WithFollowTarget` 的 `AudioClipData`, 播放后立即被归还.

### 根因

`LateUpdate` -> `SyncPosition()` -> `HandleLostTarget()` -> 循环音被 `Stop()`.

`followTarget == null` 被当成了"目标已丢失", 但 `null` 同时表示"从未请求跟随". 两者被混为一谈,
于是每个不跟随的声音每帧都被判定为"跟丢了", 循环音随之被立刻停止.

### 用户的修法

引入 `willFollowTarget` 作为"本次播放是否请求了跟随"的运行时标志(不序列化):

- `SetFollowTarget()` 置 `true`;
- `HandleLostTarget()` 置 `false`.

设计意图(用户明确): 跟随是**每次请求**的特性, 目标一丢失就必须重新请求.
`willFollowTarget` 与请求周期完全同步, **不允许** AudioEmitter 自己开着跟随状态等目标重新出现.

### DSH 复核时发现并修正的两处问题

**1. 守卫条件写反了.** 原提交为:

```csharp
if (!IsPlaying || willFollowTarget)   // 反了
    return;
```

按此条件: 请求了跟随的声音(`willFollowTarget == true`)会立即返回, **永远不做位置同步** ——
恰好把该标志要启用的行为抑制掉了; 而不跟随的声音仍然每帧走 `SyncPosition()` 并被判定为跟丢.
同时 `isVolumeDirty` 的写入块位于其后, 两种情况下都无法执行,
意味着 `SoundHandle.TrySetVolume` 的运行时改音量从未真正到达 `AudioSource`.

已改为: 音量对**每种**播放都应用(句柄随时可能改音量), 位置同步**只在** `willFollowTarget` 为真时进行 ——
两者互相独立, 因为"是否跟随"与"是否改音量"是无关的两件事.

**2. `ResetEmitter()` 未清 `willFollowTarget`, 存在跨播放泄漏.** 修正守卫后这一点变成真缺陷:
若某次跟随播放借出的 emitter 在目标仍存活时被归还, `willFollowTarget` 会停在 `true`,
于是**下一次**播放(可能是不跟随的循环音)会去同步一个 `null` 目标 —— 正是本次修掉的那个 bug 经池复用复活.
已在 `ResetEmitter()` 中补 `willFollowTarget = false`.

### 验证

- 编译通过: `Assembly-CSharp.dll` 于 `16:16:16` 重建, 体积 `51712` 字节.
- `willFollowTarget` 生命周期已逐行核对: 仅 `SetFollowTarget()`(255) 置真;
  `ResetEmitter()`(276) 与 `HandleLostTarget()`(328) 置假 —— 与用户所述"与请求周期同步"一致.
- 用户已实测循环 clip 行为符合预期.

### 数据与资产改动 (由用户完成)

- `AudioId` 枚举由 `Default` 拆为 `DefaultSfx` / `DefaultOst`, 并保留 `MouseClick`.
  核对结果: 三个 `AudioClipData` 的 `audioId` 分别 `0 / 1 / 2`, 与枚举值**一一对应, 无冲突**.
- `GenericAudioClipData.asset` 重命名为 `GenericSfx.asset`(GUID 保留, 故引用未断), 新增 `GenericOst.asset`.
  `DefaultAudioManagerConfigs.audios` 的三条引用全部解析成功.
- `Master.mixer` 新增 `SFX` / `OST` 分组; `Master.mixer` 亦被修改.
- 导入一首 BGM(`上海アリス幻楽団 - 運命のダークサイド.mp3`)及 `Assets/Audios/Clips/DISCLAIMER.md`.

### 设计决策记录 (用户决定, 非缺陷)

- **有意不在 Audio 系统内区分 Ost 与 Sfx**, 也未为 Ost 添加"防止多首同时播放"的逻辑.
  计划未来以类似 `OstScheme` 的机制管理, **明确超出本次开发范围**.
  注: `GenericOst.asset` 的 `maxInstances` 已被设为 `1`, 因此当前每 clip 上限恰好承担了这层保护;
  真正的通道级管理(混音总线、优先级、互斥)仍待 `OstScheme`.

### 仍然存在的缺口 (未变)

- **`Master.mixer` 的 `m_ExposedParameters` 仍为空数组**, 因此代码层面依然无法调节总线音量.
  分组(SFX / OST)已建好, 但要在代码里控制它们, 需要先在 Inspector 中暴露参数. 此前的 TODO 依然有效.
- `DISCLAIMER.md` 目前为英文, 而 §5.1.0 要求生成出的 `.md` 只用中文. 待用户确认是否改写.
- 淡入淡出仍未实现: 抢占依旧是硬切.

---

## 2026-10-03 — 修正: 归还调用与归还回调互相递归导致栈溢出

**本条记录的是上一条修复所引发的回归.** 上一条补上了缺失的 `emitterPool.Release(emitter)`,
但插入的位置使它调用了自己, 造成无限递归. 现已改为把"归还输入"与"归还输出"彻底分开.

### 症状

Play 模式下栈溢出 (`StackOverflowException`). 用户已在 `AudioManager.cs:287` 临时注释掉该行止血,
并定位到 `AudioManager.cs:287` / `GameObjectPool.cs:57` / `GameObjectPool.cs:81` 三处互相调用.

### 根因 (DSH 的实现缺陷)

`GameObjectPool` 的构造函数接收一个 `onRelease` 回调, 而传进去的正是 `AudioManager.OnEmitterRelease`:

```csharp
emitterPool = new(..., OnEmitterRelease, ...);   // onRelease == OnEmitterRelease
```

于是上一条补的那行形成了闭环:

```
OnEmitterRelease(emitter)          // AudioManager.cs:271
  -> emitterPool.Release(emitter)  // AudioManager.cs:287   <- 上一条我加的那行
    -> pool.Release(item)          // GameObjectPool.cs:81
      -> actionOnRelease(item)     // GameObjectPool.cs:57
        -> onRelease?.Invoke(item) // 就是 OnEmitterRelease 本身
          -> (回到第一行)
```

**同一个方法身兼两职**: 它既是池的"归还输出"(清理), 又因为上一条的改动成了"归还输入"(发起归还).
方法开头的 `-= OnEmitterRelease` 挡不住它, 因为递归并不经过 `onAudioFinished`.

### 修法: 拆成两个方法, 各自单向

```csharp
// 归还输出: 池归还时执行的清理, 绝不调用 Release
private void OnEmitterRelease(AudioEmitter emitter) { ...清理与日志... }

// 归还输入: 订阅到 emitter 完成回调, 只负责转发给池
private void ReleaseEmitter(AudioEmitter emitter) => emitterPool.Release(emitter);
```

订阅点:

```csharp
emitter.onAudioFinished += ReleaseEmitter;      // 先: 触发池归还
emitter.onAudioFinished += OnEmitterRelease;    // 后: 清理与日志
```

订阅顺序是**有意义的**: C# 多播委托按订阅顺序调用, 因此池的归还(含停用 GameObject)先发生,
清理中的 `ResetEmitter` 与重挂父级随后执行. 若顺序颠倒, 清理会先改动一个随后才被停用的对象.

### 由此确立的工程规则

**注册为某个池"归还回调"的方法, 自身绝不能调用该池的 Release.**
当同一个位置既需要"被通知已归还"又需要"发起归还"时, 必须是两个方法.
这条规则与上一条("开始方法必须先终结上一次使用")合起来, 构成池化对象两侧的边界:
-- 取用侧: `Play` 必须先终结上一次使用;
-- 归还侧: 归还回调不得再发起归还.

### 验证

- 编译通过: `Assembly-CSharp.dll` 于 `03:40:27` 重建.
- 代码边界已逐行核对: `OnEmitterRelease`(270-285) 内不含任何 `emitterPool.Release`;
  全文件仅 `ReleaseEmitter`(296) 调用 `emitterPool.Release`, 且其唯一订阅者是 emitter 的完成回调.
- **尚未在 Play 模式下复验.** 请见 `Assets/PENDING_INSPECTOR.md`.

### 诚实记录 (第五次, 且是第二次自我回归)

上一条我修好了一个"缺失调用", 却因此引入了"无限递归". 这两件事同源:
我每次只盯着单个方法看, 没有画出**回调图**. 这次的教训具体到可执行:

**当 A 调用 B 之前, 必须确认 B 是否会回调 A.** 本缺陷只需画一张三个节点的图就能发现, 不需要运行. 至此五次缺陷中,
有两次(第 4 与第 5)完全可以在提交前靠"注解逐句对照代码"与"回调图"抓住.

---

## 2026-10-03 — 修正: OnEmitterRelease 从未归还 emitter (池枯竭的真正原因)

> **后续**: 本条补上的那行 `emitterPool.Release(emitter)` 因插入位置错误而引发无限递归,
> 见本文件更靠前的一条. 缺调用是真的, 但那次的插入方式是错的.

**本条更正上一条的结论.** 上一条把池枯竭归因于 `Play()` 的重入守卫, 那是错的 ——
守卫确实是个缺陷(已修), 但它不是池枯竭的原因. 真正的原因在本条.

### 症状

`AudioEmitter is released` 的日志会出现, 但池从不真正回收, 用尽后开始拒绝请求.
用户在 `GameObjectPool.cs:58` 的 `actionOnRelease` 上打断点发现: **该委托从未被调用**.

### 根因 (DSH 的实现缺陷)

对比原始实现与我的改写:

```csharp
// 原始实现 (HEAD)
emitter.onAudioFinished += emitterPool.Release;   // emitter 播完 -> 直接归还池

// 我的改写
private void OnEmitterRelease(AudioEmitter emitter)
{
    emitter.onAudioFinished -= OnEmitterRelease;
    registry.Unregister(emitter);
    emitter.ResetEmitter();
    emitter.transform.SetParent(emitterRoot, false);
    ...日志...
    // ← 这里没有 emitterPool.Release(emitter), 也没有任何其他地方调用它
}
```

整个 `AudioManager` 里只有 `Prewarm` / `CanReuse` / `Get`, **没有任何一处调用 `Release`**.
emitter 被交出后, 租约永不回滚: 池一直以为它们在外面, 直到 30 个名额用尽, 然后拒绝一切.

更糟的是, 我写的注解原文是"…so registry removal, the follow target reset and **the return to the pool**
happen exactly once per playback" —— **我把自己没有实现的行为写进了文档.**

### 为什么它被掩盖了两次

1. `GameObjectPool.Prewarm` 内部会调用 `pool.Release(...)`(`GameObjectPool.cs:99`),
   因此 `actionOnRelease` 在启动时**确实会触发两次**. 这就是为什么每一份日志里 `released` 恒为 `2` ——
   那两次全部来自预热, 与真实播放无关. 恒定值看起来像"机制在工作", 掩盖了它其实只对预热生效.
2. 上一条的诊断被这个假象带偏, 去修了一个真实但无关的守卫缺陷.

### 修法

在 `OnEmitterRelease` 末尾补上归还:

```csharp
emitterPool.Release(emitter);
```

位置在最后, 理由: 池自身的归还回调(`ResetEmitter` / 停用 GameObject)应当面对一个已经干净的 emitter;
而日志放在它之前, 以免那条消息看起来是由一个已入池的对象发出的.

### 验证

- 编译通过: `Assembly-CSharp.dll` 于 `03:20:05` 重建, Bee/Tundra 后端在同刻运行.
- **尚未在 Play 模式下复验**. 请见 `Assets/PENDING_INSPECTOR.md`.

### 本条的方法论教训

前三次我都说"编译通过不代表运行时正确". 这一次的教训不同, 更具体也更尴尬:

**我文档里承诺的行为, 必须在代码里找得到对应语句.** 这条注解明确写了"return to the pool",
而代码里没有 `Release` 调用 —— 这是一次纯粹的读写不一致, 不需要运行时就能发现,
只要在提交前把注解逐句对照代码就能抓住.

另有两点: 恒定的日志计数(此处恒为 2)是**可疑信号**, 不是正常信号;
以及"改写既有实现时, 必须逐条核对被替换掉的每一行原始行为" —— 我删掉了一行 `+= emitterPool.Release`,
并用一个不含归还的处理器替代了它.

---

## 2026-10-03 — 修正: AudioEmitter.Play 的重入守卫 (结论已被更正)

> **更正**: 本条原文声称重入守卫是池枯竭的原因. 那是误判.
> 枯竭的真正原因是 `OnEmitterRelease` 从未调用 `emitterPool.Release`, 见本文件更靠前的一条.
> 守卫本身确为缺陷, 已修, 但它不是枯竭的成因.

本次修掉第三个缺陷, 也是前两次修复之后**依然存在**的那个. 三者性质各不相同, 因此分三条记录.

### 症状

播放正常, `TimerRunner is available` 正常出现, 但 emitter 几乎不归还, 池被撑到上限后开始拒绝请求.

### 证据 (来自运行日志)

| 事件                              | 次数             |
| ------------------------------- | -------------- |
| `AudioEmitter is pooled`        | 2              |
| `Audio MouseClick played`       | 30             |
| `AudioEmitter is released`      | 2 (其中一次来自预热自身) |
| `Timer/<Run>d__34:MoveNext` 帧路径 | 1              |

场景覆盖 `requestTime` 为 `10`(由 `5` 改来), 因此 30 次播放 = 点击 3 次.

**矛盾点**: 30 次请求走到了 `Play()`, 但全生命周期只存在过 **2** 个 emitter. 结论只能是那 2 个被反复复用,
而每次复用都撞上了 `Play()` 开头的守卫:

```csharp
if (isPlaying)
    return;          // 上一次播放尚未结束, 于是静默返回
```

于是: 不启动播放, 不装载 Timer, 不触发 `Complete`, 不归还池. 池里因此堆着 30 个"在外面"的 emitter,
它们的租约永远不回滚.

### 根因 (DSH 的实现缺陷)

`Play()` 被写成了**重入守卫**, 而这个方法真实的语义应当是**替换当前播放**.
池化复用的 emitter 在"上一次播放尚未结束"时被再次交给 `Play()` 是**正常且必然**的,
静默返回等于让池的单向租约断裂.

### 修法

`Play()` 的第一件事改为 `Stop()`, 用它结束仍在进行中的播放:

```csharp
public void Play()
{
    Stop();          // 先结束在飞的播放, 让完成回调执行, 池才能记账

    InitializeInternal();
    source.Stop();

    isPlaying = true;
    ...
}
```

`Stop()` 本身幂等, 因此"新 emitter 首次播放"时它只是 `return`, 无副作用.
这条改动**不会**带来重复归还: 抢占与句柄 `Stop` 仍受同一幂等守卫保护.

### 由此确立的工程规则

**池化组件的"开始"方法必须先终结上一次使用.** 池化对象只被交出一次租约,
若它在被复用时因状态残留而拒绝对新请求提供服务, 池必然枯竭. 这不是防御性编程, 是池化成立的前提.

### 验证

- 编译通过: `Assembly-CSharp.dll` 于 `03:01:48` 重建, Unity 的 Bee/Tundra 构建后端在同刻运行.
- **尚未在 Play 模式下复验**. 请见 `Assets/PENDING_INSPECTOR.md`.

### 诚实记录 (第三次)

这是 DSH 连续第三个交付后暴露出、且都能通过编译的运行时缺陷. 三次的共性非常明确:

| #   | 缺陷                 | 为什么编译抓不到       |
| --- | ------------------ | -------------- |
| 1   | 反射解析尚未加载的程序集       | 运行时字符串, 非编译期符号 |
| 2   | 同物体组件 `Awake` 顺序竞争 | 生命周期顺序, 非类型错误  |
| 3   | `Play()` 重入守卫      | 复用路径, 首次播放永远正常 |

前两次我给出的教训是"要给时序论证"; 这一次的教训更具体: **池化路径必须专门验证"复用"这一条分支**,
只在对象上调用一次的方法永远不会暴露这类缺陷. 本工程后续任何池化组件的交付, 都应在验收清单里
显式包含"同一实例被复用"的用例.

---

## 2026-10-03 — 修正: AudioManager 与 TimerRunner 的 Awake 顺序竞争

上一条把反射改成直接读取之后, 立刻暴露出第二个缺陷. 两者是不同的问题, 因此分开记录.

### 症状

```
[Core: AudioManager] "TimerRunner" component is missing. Component disabled.
AudioManager:VerifyTimerRunner () (at Assets/Scripts/Core/Managers/AudioManager.cs:318)
AudioManager:InitializeInternal () (at Assets/Scripts/Core/Managers/AudioManager.cs:278)
AudioManager:Awake () (at Assets/Scripts/Core/Managers/AudioManager.cs:60)
```

### 根因 (DSH 的实现缺陷)

`AudioManager` 与 `TimerRunner` 挂在**同一个 GameObject** 上, 而 Unity **不保证同一物体上两个组件 `Awake()` 的先后**.
`TimerRunner.Instance` 只在自己的 `Awake()` 里赋值, 因此当 `AudioManager.Awake()` 先跑时, 读到的是 `null`.

这条与上一条的关键区别: 上一条是"程序集未加载", 这一条是"同物体组件初始化顺序" —— **直接引用并不能解决它**,
它独立存在, 只是此前被反射的假阴性掩盖了.

### 修法

- Timer 宿主的校验从 `Awake` 移到 **`Start`**: `Start` 在该物体上所有 `Awake()` 之后运行, 此时宿主必然已注册.
- `InitializeInternal()` 不再建池; 池改由 `Start` 在**校验通过之后**通过 `BuildEmitterPool()` 建立,
  因此不会为一个即将自禁的管理器建池.
- `Play()` 增加 `emitterPool == null` 守卫, 同时覆盖"`Start` 尚未运行"的时间窗与该自禁情形.
- 字段 `isTimerRunnerAvailable`(bool) 换成 `timerRunner`(引用) 并暴露为只读属性:
  它持有真正需要的依赖, 不再是只能读一次的布尔快照.

### 由此确立的工程规则

**跨组件协调的初始化一律放在 `Start`, 不放 `Awake`.** `Awake` 只解析自身依赖 (`GetComponent<>()` 等);
凡是需要另一个组件已经完成初始化的判断, 都必须等到 `Start`.

### 验证

- 编译通过: `Assembly-CSharp.dll` 于 `02:54:49` 重建, Unity 的 Bee/Tundra 构建后端在同刻运行.
- 编译产物中含 `BuildEmitterPool`, 且 `GetProperty` / `BindingFlags` 已消失, 证明反射路径彻底移除.

### 诚实记录

这是 DSH 连续第二次在"编译通过"之后仍无法保证运行时行为. 两次都不是编译错误, 而是**执行时序**问题.
因此本条目起, 涉及跨组件初始化的改动必须给出时序论证, 而不只是贴编译产物.

---

## 2026-10-03 — 修正: 移除 Timer 宿主的反射检查

本次改动修掉了音频交付里由 **DSH** 引入的一个缺陷. 它不是 Timer 包的问题.

### 症状

运行日志中从不出现 `TimerRunner is available`, 但同一次运行的调用栈显示
`Timer/<Run>d__34:MoveNext -> ForwardToCompletion -> Complete -> AudioEmitter.Stop`,
即 Timer 确实完成过并归还了 emitter. 两者矛盾, 说明检查本身在说假话.

### 根因 (DSH 的实现缺陷)

原实现用字符串在运行时解析类型:

```csharp
System.Type.GetType("TimerRunner, GrignardReagent.Timer")
```

`Type.GetType` 解析程序集限定名时要求该程序集**已经加载**. `GrignardReagent.Timer` 只在首次用到 `Timer` 时才加载,
而 `AudioManager.Awake()` 发生在任何 `Timer` 被实例化之前 —— 于是解析失败, 函数直接返回,
`isTimerRunnerAvailable` 恒为 `false`, 且那条 `false` 分支**什么也不打印**.

这正是"用反射绕开 internal"这一手段的固有代价: 它把一个编译期问题变成了运行时时序问题.

### 修法

- Timer 包已将 `TimerRunner.Instance` 从 `internal` 改为 `public`(包版本 `2e596da7b1` -> `621fd79a7a`).
- `AudioManager.VerifyTimerRunner` 改为**直接读取** `TimerRunner.Instance`, 反射与 `using System.Reflection` 一并删除.
  依赖因此回到编译期可见, 并且该读取本身会加载 Timer 程序集, 时序陷阱消失.
- 检查现在**两种结果都记录**: 可用时 `GameLog.Info`, 不可用时 `GameLog.Error` + `LogIssue.MissingComponent<TimerRunner>()` + 禁用组件.
  此前那条静默的 `false` 分支本身就是坏设计 —— 它让"永不归还 emitter"看起来像一个安静运行的系统.

### 验证

- 编译通过: `Assembly-CSharp.dll` 于 `02:47:11` 重建(Unity 的 Bee/Tundra 构建后端在同刻运行),
  体积由 `50176` 降为 `49664` 字节 —— 减少的正是被删掉的反射代码.
- 直接引用能编译, 本身即证明 `TimerRunner.Instance` 现在跨程序集可见.
- **尚未在 Play 模式下复验** `TimerRunner is available` 是否出现; 见 `Assets/PENDING_INSPECTOR.md`.

### 同时更正的一处文档错误

音频交付条目里把"没有 Pool exhausted 警告"写成了验收条件. 那是错的: 以 `Test Audio Request` 的测试强度
(每次点击 10 次请求, 池上限 30), 第 31 个请求被拒绝并告警**才是**设计生效的证据.
Unity 的 `ObjectPool` 在池空时只会新建实例而从不拒绝, 因此该硬上限必须由代码强制. 详见 `Assets/PENDING_INSPECTOR.md` 第 3 节.

---

## 2026-10-03 — 探针移除记录

本次改动是**记账**, 不是代码改动. 交付 (C) 的探针已在 `f14b94c` 之后的提交中移除, 但当时没有留下记录.

- `Assets/Scripts/Core/Diagnostics/` 整个目录已删除, 其中包含 `DshWriteProbe.cs` 与其 `.cs.meta`.
- 因为 `.cs` 与它的 `.meta` 当初是**成对提交**的, git 把两者记为同一次删除, 没有留下孤儿 `.meta`.
- 交付 (C) 建立的链路 (`DSH 写盘 -> Unity 生成 .meta -> 编译通过 -> git 可见 -> 提交`) 已在此前被端到端验证通过, 因此探针没有继续保留的价值.

无 Inspector 操作, 无新增文件.

---

## 2026-10-03 — 交付: Core 音频协调层 (AudioBuilder / SoundHandle / AudioRegistry)

### 范围

补齐 Core 音频子系统缺失的"协调"层: 单次播放的链式参数, 播放句柄, 以及两级实例上限与抢占.
参考实现 `adammyhre/Unity-Audio-Pooling` 只提供了链式位置与随机音高两项能力, 本交付按本工程的规范重新设计, 并修掉了它的若干缺陷 (见下).

### 被测/新增行为

- **逐次播放的参数**: 新增 `AudioBuilder` (struct), 提供 `WithVolume` / `WithPitch` / `WithRandomPitch` / `WithFollowTarget`, 终结符 `Play(AudioId)` 返回 `ISoundHandle`.
- **播放句柄**: 新增 `ISoundHandle` 与唯一实现 `SoundHandle`. 句柄在本次播放结束时**失效**, 因此池化 emitter 被下一次播放复用时, 旧句柄不会误报新播放的状态. 写入类操作是 `TrySetVolume` / `TrySetPitch`, 失败由返回值表达, 不静默吞掉.
- **两级实例上限与抢占**: 每 clip 上限来自 `AudioClipData.MaxInstances`, 全局上限来自 `AudioManagerConfigs.MaxSoundInstance`. 触顶时抢占**最旧且非循环**的声音. 循环音永不被抢占.
- **活跃注册表**: 新增 `AudioRegistry`, 负责注册, 注销, 按 `AudioId` 计数, 以及按序号回答"谁最旧". 顺序用序号而不是时间戳, 因为 `Time.time` 在同一帧内会重复.
- **延迟移除**: 注销只把槽位标记为空, 压实发生在下一次注册. 这是因为停止一个声音会同步触发注销, 若直接移除就会在抢占遍历中改动列表 —— 参考实现正是在这里崩过一次 (`StopAll` 的 `InvalidOperationException`).
- **位置跟随**: `AudioEmitter` 在 `LateUpdate` 中同步到 `WithFollowTarget` 传入的 `Transform`, 且**不改变父子关系**. 因此跟随的声音不随目标销毁而消失: 一次性音在目标最后位置播完, 循环音在目标消失时停止.
- **幂等 Stop**: `AudioEmitter.Stop()` 现在幂等, 使"句柄 Stop"与"被抢占"这两条会竞争的路径无法把同一次播放报告两次 (参考实现依赖 Editor-only 的 `collectionCheck` 掩盖此问题).
- **启动自检**: `AudioManager` 在初始化时确认 Timer 包的协程宿主存在. 该宿主缺失时 `onAudioFinished` 永不触发, emitter 会静默永久滞留在池外. (注: 本条的**初版**用反射读取该宿主, 那是 DSH 的实现缺陷, 已在本文件顶部"移除 Timer 宿主的反射检查"一条中修正.)
- **池上限硬约束**: `GameObjectPool` 新增 `CanReuse(maxSize)`. Unity 的 `ObjectPool` 在池空时会新建实例而**不会拒绝**, 因此"池达到 `MaxPoolSize` 后拒绝新请求并记警告"必须由我们自己判定.

### 修掉的两个既有缺陷

- `AudioManager.RequestAudio` 在 `TryGetClip` 失败时**缺少 return**: 它会继续 `emitter.Configure(null)` 并访问 `data.AudioId`, 必然抛 `NullReferenceException`. 新实现直接拒绝并返回 `null`.
- `AudioManagerConfigs.MaxSoundInstance` 与 `AudioClipData.MaxInstances` 此前**写入但从未被读取**. 现在两者都生效.

### 与参考实现的已知差异 (刻意为之)

- 参考实现的 `maxPoolSize` 只管空闲实例, 因此并发数不受它约束, 非 frequent 音效完全无上限. 本交付把"池大小"与"并发上限"显式分离, 并要求 `MaxPoolSize >= MaxSoundInstance`.
- 参考实现没有每 clip 上限, 没有句柄, 没有移动跟随, 也没有淡入淡出. 本交付补了前三项, **淡入淡出明确不在本次范围**.

### 本次不做 (已知缺口)

- **淡入淡出**: 抢占是硬切. `SoundHandle.TrySetVolume` 已经提供了驱动器, 但淡入淡出属于一次独立交付.
- **总线整体缩放**: `Master.mixer` 未暴露任何参数, 因此代码层面无法调总线音量. 正确落点是 AudioMixer 的 exposed parameter, 属于一次需要你在 Inspector 配置的独立交付.
- **每 clip 上限对循环音无效**: 循环音既不被抢占, 也不能靠上限回收. 若某循环 clip 的 `MaxInstances` 被设为 1 而它已在播放, 后续请求仍会启动第二个实例 (因为唯一候选受害者是循环音, 被跳过). 这是策略的直接后果, 记录在此以免被当作 bug.

### 需要你在 Inspector 里手动做的操作

- [x] 把 `Assets/SO/DefaultAudioManagerConfigs.asset` 的 `MaxPoolSize` 从 `10` 改为不小于 `MaxSoundInstance`(当前 `30`)的值. 当前 `10 < 30`, 会让第 11 个并发 emitter 播放后被销毁而不是回收.
- [x] 建议同时核对 `PrewarmAmount <= DefaultCapacity <= MaxPoolSize`; 当前三者都是 `10`.
- [x] 为每个 `AudioClipData` 资产填写新增的 `Volume` 与 `Pitch` 字段 (默认 `1` / `1`, 保持原听感即可).
- [x] 打开 Unity Console 确认没有编译错误, 尤其确认 `AudioBuilder` / `SoundHandle` / `AudioRegistry` / `ISoundHandle` 已成功导入 (它们的 `.meta` 是否已生成).
- [x] 进入 Play 模式, 用 `AudioManager` 上的 `Test Audio Request` 按钮验证播放与回收, 确认日志中没有 "Pool exhausted" 或缺失 TimerRunner 的报错.
- [x] 确认 `Assets/Prefabs/Core.prefab` 上 `AudioManager` 的 `emitterPrefab` / `emitterRoot` / `configs` 三个引用未被本次改动破坏 (它们没有动, 但值得复核一次).

### 新增文件及其待提交的 `.meta`

DSH 不得写入这些 `.meta` (§2.1.1); 它们由 Unity 生成, 且必须与所属文件**一起提交**.

- [x] `Assets/Scripts/Core/Audio/ISoundHandle.cs` + `ISoundHandle.cs.meta` (GUID `a2eb9ceb01910184fb16974c3ba1700b`)
- [x] `Assets/Scripts/Core/Audio/SoundHandle.cs` + `SoundHandle.cs.meta`
- [x] `Assets/Scripts/Core/Audio/AudioRegistry.cs` + `AudioRegistry.cs.meta`
- [x] `Assets/Scripts/Core/Audio/AudioBuilder.cs` + `AudioBuilder.cs.meta`

### 修改的文件

- [x] `Assets/Scripts/Core/Audio/AudioEmitter.cs` (跟随, 幂等 Stop, 实时音量音高, 类注解)
- [x] `Assets/Scripts/Core/Managers/AudioManager.cs` (两级上限, 抢占, Builder 工厂, 启动自检, 修 return 缺陷)
- [x] `Assets/Scripts/Core/Audio/SODefinitions/AudioClipData.cs` (新增 `Volume` / `Pitch`)
- [x] `Assets/Scripts/Core/Audio/SODefinitions/AudioManagerConfigs.cs` (暴露 `MaxSoundInstance`)
- [x] `Assets/Scripts/Core/GameObjectPool/GameObjectPool.cs` (新增 `CanReuse`)
- [x] `Assets/Scripts/Core/README.md` (纯中文重写 + 音频限流章节)
- [x] `Assets/CHANGELOG.md` (本文件)
- [x] `Assets/PENDING_INSPECTOR.md`
