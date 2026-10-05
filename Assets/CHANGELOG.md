# CHANGELOG

本文件记录**已发生**的改动。每条记录以两条强制清单结尾（§5.1.2）：

- 「需要你手动完成的操作」——**只给指针**，指向 `Assets/PENDING_INSPECTOR.md`（单一事实源）；
- 「新增文件及其待提交的 `.meta`」——本次新增的每个 `.cs` 与其它需提交的新文件，及其**待 Unity 生成**的 `.meta`。

> 本文件在 2026-10-05 被整体重置：旧 `Assets/Scripts/Jill/AGENT-DOCs/CHANGELOG.md` 的历史条目按用户裁决作废，
> 原因是它记录的是迁移前的路径（`Assets/Scripts/Core/...`、`Assets/SO/`、`SampleScene`）与已不成立的结论。

## 2026-10-05 — 交付 A2: 玩家音量接线到 AudioMixer 总线

> **结账规则声明**：A1 与文档重置在 `Assets/PENDING_INSPECTOR.md` 中的清单尚未全部勾完，
> 因此本条目只记录**本交付自身**已发生的事实（§5.1.2 结账规则）。

### 范围

把玩家持久化的三个音量应用到已暴露的 mixer 参数上。只动 `Core/Audio`（`AudioManagerConfigs` + `AudioManager`）与文档，
**不改 `Game/Setting`**。

### 前置（你完成，DSH 已核实产物，不看回声）

- 你在 AudioMixer 里为 `Master` / `OST` / `SFX` 各暴露了一个音量参数，名为 `MasterVolume` / `OstVolume` / `SfxVolume`。
  磁盘证据：`Master.mixer` 的 `m_ExposedParameters` 为三项，且三条 GUID 与三个分组自身的 `m_Volume` **逐一对应**；
  文件 `mtime` = 15:22:27（`Save Project`）。
- 你把 `Master.mixer` 赋给了 `DefaultAudioManagerConfigs.asset` 的 `Mixer` 字段。
  磁盘证据：`mixer: {fileID: 24100000, guid: a6b573f691acdc44a9421d10d0de8b7a}`，
  与 `Assets/Audios/Mixers/Master.mixer.meta` 的 `guid` 完全一致，`fileID` 也确为该 mixer 的 `AudioMixerController`。

### 改动

- `AudioManagerConfigs` 新增 **`Bus Volume`** 组：`Mixer`（`AudioMixer` 引用，资产无法 `GetComponent<>()`，属 §5.2.1 允许的"确有需要"）
  + 三个参数名字符串（字段初值即上面三个名字，因此**既有资产自动获得正确值，不需要你手打**，且日后改名无需改代码）。
  四个只读属性对外暴露；`OnValidate` 在 `Mixer` 未赋值时记一条 warning。
- `AudioManager.ApplyAudioSettings()`：读 `SettingBootstrap.Settings.Audio`（该数据在任何场景对象 `Awake` 之前就已加载），
  把三个**线性 0–1** 值换算为分贝写入 mixer 参数：`v <= 0.0001 ⇒ -80 dB`（mixer 自身的静音阈值），否则 `20 * log10(v)`。
  默认配置 `1 / .5 / .5` ⇒ `0 / -6 / -6 dB`。初始化时自动调用一次；`Mixer` 未赋值或参数名为空时记错误并跳过，**不抛异常**。
- `AudioManager` 新增 `Debug` 区按钮 **`Apply Audio Settings`**（仅在播放模式生效），便于手测。
- 依赖方向**单向**：音频读 `Setting`，而 `Setting` 对音频一无所知 —— 与 `GameState` 那条同向。

### 明确不做（本次边界）

- **音量改动自动生效**：需要 `Setting` 侧提供变更通知（`AudioSettings` / `SettingStore` 上的事件）并由音频订阅，
  而那是 `Game/Setting` 这个**另一个交付单元**，按 §4.2 先问再写。已记入 `Audio/README.md` 的 TODO。
- 单条总线的静音 / 独奏：无玩法需求，且同样需要 mixer 侧先暴露对应参数。

### 文档

- `Core/Audio/README.md`：职能与公共 API 增补，新增「玩家音量到总线」一节（换算、参数来源、依赖方向），
  TODO 从"音量未被应用 / 总线未缩放"改为"改动不会自动生效"。
- `Core/README.md`：删除两个已过期的音频 TODO（改为指向 `Audio/README.md`），并在音频三节前加一行"权威文档是 `Audio/README.md`"。

### 新增文件及其待提交的 `.meta` (§2.1.1)

本次**未新增任何文件**（只修改既有文件与资产）。

### 需要你手动完成的操作

见 `Assets/PENDING_INSPECTOR.md`**第 5 项**（A2 验收：编译、Play 模式日志、两条听感验证、`git status`）。
第 1b 项（拖入 mixer）已由 DSH 核实产物后勾实。

## 2026-10-05 — 交付 A1: Core/Audio 的淡入淡出 + 循环音上限漏洞修复

> **结账规则声明**：上一份交付（文档重置）在 `Assets/PENDING_INSPECTOR.md` 中的清单**尚未勾完**，
> 因此本条目只记录**本次交付自身已发生的事实**，不对重置的收尾做任何"已完成"的断言（§5.1.2 结账规则）。

### 范围

只动 `Core/Audio` 与它的总线 `Core/Managers/AudioManager.cs`，两类改动：**淡入淡出**（新能力）与
**循环音的每-clip 上限漏洞**（真缺陷修复）。

### 新增能力：淡入淡出

- 数据落点：`AudioClipData.FadeIn` / `FadeOut`（秒，默认 `0`）。**默认 0 精确复现原先的硬起与硬切，因此既有三个 clip 的听感不变**。
- 请求级覆盖：`AudioBuilder.WithFade(fadeIn, fadeOut)`；不设置即用 clip 的静态值（与 `WithSurviveFreeze` 同一套静态/动态划分）。
- 实现：`AudioEmitter` 以 `Time.unscaledDeltaTime` 在 `LateUpdate` 推进渐变（因此 `Time.timeScale = 0` 时仍在推进），
  渐变系数**乘在**播放音量上，故渐变途中调用 `TrySetVolume` 不会被覆盖。
- 三处用法：起播淡入；非循环 clip 的**尾部**淡出（读 `AudioSource.time` 判定，不额外排程，上一次播放的残留不会影响下一次）；
  显式停止的优雅淡出（`AudioEmitter.RequestStop`，由 `SoundHandle.Stop()` 使用，且句柄仍立即失效）。

### 修复的真缺陷：循环音的每-clip 上限形同虚设

旧实现选中最旧的同类实例后，若它是循环音，`Preempt` 直接 `return` —— **既不重试也不拒绝**，请求照常播放，
于是 `CountOf` 可以无限超过 `MaxInstances`；全局上限同理。
新实现：抢占只从**非循环**候选里挑最旧的（`AudioRegistry.TryGetOldest(..., includeLooping: false)`）；
若触顶时全部实例都在循环，则**拒绝**该请求并记一条 warning（`Per-clip limit of N is reached ...`），
使两级上限重新成为硬上限。

### 明确不做（本次边界）

- **抢占时的淡出**：抢占必须在同一帧释放池槽位（`GameObjectPool.CanReuse` 只认空闲实例数），而淡出会让该 emitter 继续被占用，
  池满时会把"抢占并服务"退化成"抢占后仍被拒绝"。要做对需改 `Core/GameObjectPool` 的记账 —— 那是另一个交付单元，
  按 §4.2 先问再写。已记入 `PENDING_INSPECTOR.md` 第 3 项待裁决。
- **A2（总线缩放 / `AudioSettings` 全局音量接线）**：仍卡在"先在 AudioMixer 暴露参数"这一资产侧前置（`PENDING_INSPECTOR.md` 第 1 项）。
- **Timer 包的异常隔离**：以 §5.1.8 的 `TODO:` 写在 `AudioEmitter.Play` 的归还链处；修复位于 §2.2 禁写区，需你裁决。

### 文档

- **新增** `Assets/Scripts/Jill/Core/Audio/README.md`：按 §5.1.1，`Core/Audio` 作为独立交付单元需要自己的 README，
  承载其职能 / 公共API / 内部实现思路（含新的渐变与上限语义）。
- `Core/README.md` 的构成表新增一行指向该 README。
- 上一份交付遗留的 `Core/SceneSwitch/README.md` 欠账**不属于**本次范围（一份交付一个 Subsystem），仍待首次触及 SceneSwitch 时补齐。

### 过程记录（诚实记录，第二次）

写入期间，工程内**已存在**的文件被外部进程反复以"不允许删除共享"的方式持有，`ReplaceFileW` 返回
`Win32 32`(共享冲突)；**新建**文件不受影响。你处理一次后恢复，随后转为按文件、按时间窗的间歇性封锁，
DSH 以"最小粒度 + 等待重试"推进。

这期间工程**曾两度短暂不可编译**：先是 `SoundHandle` 引用了尚未写入的 `RequestStop`；随后是 `AudioBuilder`
的结构体构造函数未给新字段赋值（CS0171）。最后一行 `fadeOut = null;` 由**你手动补上**（DSH 对该文件的写入
被持续拒绝）；DSH 随后复核确认，并补完 `WithFade` 到 `AudioManager.Play` 的传参、`AudioManager` 内两处注解
（孤儿注解块、过时的 `Preempt` 注解）以及抢占淡出的 `TODO:`。以上均已在交付内闭合。

### 新增文件及其待提交的 `.meta` (§2.1.1)

- [ ] `Assets/Scripts/Jill/Core/Audio/README.md` + `Assets/Scripts/Jill/Core/Audio/README.md.meta`（待 Unity 生成）

本次**未新增任何 `.cs` 文件**：全部改动都在既有文件内。

### 需要你手动完成的操作

一律见 `Assets/PENDING_INSPECTOR.md`（单一事实源）**第 4 项**：让 Unity 导入并编译、按需为 clip 填 `FadeIn` / `FadeOut`、
复核 `git status`、以及两条建议实测。本次**无需其它操作**。

## 2026-10-05 — 重置: Agent 文档重建 (fresh start)

### 范围

- 作废旧 `Assets/Scripts/Jill/AGENT-DOCs/CHANGELOG.md` 与 `.../PENDING_INSPECTOR.md`，改为 `Assets/` 根下的两份新文档（本文件与 `Assets/PENDING_INSPECTOR.md`）。
- 规则文本 `AGENTS.md` 迁到 `Assets/AGENTS.md`，并按当前真实路径与本次 grill 的结论改写。**DSH 只产文本，不写入该文件**（新增的 §2.1 例外）。
- 本次**不含任何 `.cs` 改动**；代码交付（A1）在此之后单独进行。

### 为什么重置

核对后发现旧文档描述的不是本工作区的树，且其结论已多处不可靠（例如它把"总线缩放未实现"与"已全部完成"两种说法并存）。
用户裁决：旧文档整体 stale，历史条目清零，只把**仍然成立的规则差异**改写进新版 `AGENTS.md`。

### 本次确立的规则差异（已写入新版 AGENTS.md）

1. 文档落点：三份文档都在 `Assets/` 根。
2. `AGENTS.md` 不由 DSH 写入，只提议文本。
3. 写入范围：`Assets/Scripts/Jill/**` 下允许类型全可写（含 `Jill/Core/**`）；`ProjectSettings/`、Editor 脚本、越界写入仍需先申请。
4. §5.2.1 的"不用序列化字段 + Inspector"只约束**依赖解析**；`configs` 属**参数**，序列化字段 + Inspector 拖入是首选。
5. 类型粒度：一文件一个**对外可见**类型；`private` 嵌套类型不拆（判据：外界是否需要知道它存在）。
6. Inspector 待办单一事实源改为 `Assets/PENDING_INSPECTOR.md`；CHANGELOG 不再重复承载。
7. git 边界收窄：仅 `status`/`diff`/`update-index --refresh`；禁 `log`/`show` 与一切其他 `update-index` 形式；可读工作区自己的 `.git` 指针行、不得跟进。目的是"本体不被版本控制改动"，不是完全隔离。
8. 焦点问题**不实现**：用户以分屏监视替代。因此每次交付必须列出"需要你看一眼 Unity"的文件清单。

### 旧文档条目的去向（范围定调）

- **做**：音频淡入淡出；总线整体缩放（需用户先在 `Master.mixer` 暴露参数）；`AudioSettings` 全局音量接线。
- **不做**：Setting 数据版本/迁移；GameState 退出恢复（改为在设计注解与 `GameState/README.md` 写明"归 session 负责"）。
- **记为 `TODO:`**：`com.grignardreagent.timer` 零异常隔离（真实缺陷，但 `Assets/Packages/` 属 §2.2 禁写区，只能由用户处置）。
- **待交付中修复**：循环音每-clip 上限漏洞（见 A1）。

### 结转的一次性欠账

- `Assets/Scripts/Jill/Core/SceneSwitch/README.md` 缺失（§5.1.1 要求每个 Subsystem 一份）。用户裁决：**在首次触及 SceneSwitch 子系统的交付中补上**，不单独开一份交付。

### 观测记录（预期 diff，不是越界写入）

- 用户于 13:59 首次以 Unity GUI 打开本工作区（冷启动，此前无 `Library/`）。Unity 自行创建了 `Library/`、`Temp/`、`Logs/`、`UserSettings/`，四者均被 `.gitignore` 忽略。
- `git status` 出现 ` M Assets/Scripts/Jill/Infra/InputSystem/PlayerControls.cs`：这是 Unity 导入时重写生成脚本所致，§3.2 已写明**属预期**，不计入越界。
- 编译产物证据：`Library/ScriptAssemblies/Assembly-CSharp.dll` @ 14:00:48，68096 字节；同批包程序集生成于 14:00:05–14:00:08。

### 诚实记录（第一次）

`Assets/Scripts/Jill/AGENT-DOCs/` 下的两份旧文档，DSH **执行删除失败**：`Remove-Item` 报"对路径的访问被拒绝"。
更严重的是当次脚本把 `DELETED ...` 无条件打印了出来——那行是**假的**，（复查目录确认）两个文件当时仍在。
教训：删除类操作必须以**复查目录**收尾，不得以自身回声为证据。

**根因（已取证）**：不是权限损坏，而是沙箱的既定设计。工作区根上存在两条**非继承**的授权条目：

- `Everyone` → **拒绝**「删除子目录与文件」；
- DSH 自身的 `S-1-4-*` 条目 → 只授予「写 / 删权限位 / 同步」，不含删除能力。

因此**DSH 能写文件、不能删文件**。这条已写入新版 `AGENTS.md` §3.2：需要删除时，DSH 只能把动作登记到
`Assets/PENDING_INSPECTOR.md` 并请你执行。

先前的权限修复（工作区根补全用户完全控制权限）仍然有效且必要——它修的是"沙箱无法为工作区授权"这个前置故障，
与本条是两件事。

### 需要你手动完成的操作

见 `Assets/PENDING_INSPECTOR.md`（单一事实源）：

- [ ] 粘贴 `Assets/AGENTS.md`（DSH 给出的整块文本），并删除旧 `AGENT-DOCs/AGENTS.md`；
- [ ] 删除 `AGENT-DOCs/CHANGELOG.md` 与 `AGENT-DOCs/PENDING_INSPECTOR.md`（DSH 无删除权限）；
- [ ] 回到 Unity 完成导入，使上述 `.meta` 生成、空目录被清理。

### 新增文件及其待提交的 `.meta` (§2.1.1)

- [ ] `Assets/CHANGELOG.md` + `Assets/CHANGELOG.md.meta`（待 Unity 生成）
- [ ] `Assets/PENDING_INSPECTOR.md` + `Assets/PENDING_INSPECTOR.md.meta`（待 Unity 生成）
- [ ] `Assets/AGENTS.md` + `Assets/AGENTS.md.meta`（由你创建，同样待 Unity 生成）

本次**未新增任何 `.cs` 文件**，故本清单不含脚本条目。
