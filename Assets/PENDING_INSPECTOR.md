# PENDING INSPECTOR

记录**尚未由你完成**的操作 —— 这是本文件的**单一事实源**职责：凡是只有你能做的事（Inspector 赋值、资产 / GUI 操作、
以及删除文件这类 DSH 无权限执行的动作），都登记在这里，不再重复写进 `Assets/CHANGELOG.md`。

本文件是 §5.1.3 的**硬闸门**：只要下列任一项未勾选，DSH 不得开始任何**依赖它**的新工作，必须停下并提醒你。

解闸只能由你**用语言显式确认**触发（例如"已配好"）。勾选状态在 git 中可见，但"我配好了"这句话必须由你说出，
DSH 不得自行推断闸门已解除。

## 未完成项

### 1. A2 的前置：在 AudioMixer 里暴露总线音量参数 —— 阻塞"音量接线"交付

已核实：`Assets/Audios/Mixers/Master.mixer` 的 `m_ExposedParameters` 仍是**空数组**（第 32 行），三条总线为 `Master` / `SFX` / `OST`。
`.mixer` 是 Unity 资产，属 §2.1 / §3.2 的禁区，DSH 不得写入，因此这一步只能由你在 GUI 完成。

- [x] 在 AudioMixer 窗口为三条总线各暴露一个音量参数。建议命名 `MasterVolume` / `SfxVolume` / `OstVolume`；
  
      **若你用了别的名字，请把实际名字告诉我** —— 代码必须按真实参数名绑定，我不得猜。

- [x] 用一句话明确确认"已配好"。

> **DSH 复核 2（2026-10-05，你给出截图之后）**：截图确认 `Exposed Parameters (3)` 为
> `MasterVolume`（Volume of Master）、`OstVolume`（Volume of OST）、`SfxVolume`（Volume of SFX），
> 与我建议的命名一致 ⇒ **"真实参数名"这一条已满足，A2 不再因此受阻**。
> 
> **复核 3 —— 本条已闭合（2026-10-05）**：`Master.mixer` 的 `mtime` 现为 **15:22:27**（同批还有 `GenericSfx.asset` /
> `GenericOst.asset`，是典型的 `Save Project`），且第 32–38 行已是三项列表：
> 
> ```yaml
> m_ExposedParameters:
> - {guid: 7ae899885fd6f1641baf5811c29e52a1, name: MasterVolume}   # = Master 分组的 m_Volume
> - {guid: 63693a9670c0b7f49890301a3bf41a33, name: OstVolume}      # = OST 分组的 m_Volume
> - {guid: 657e96c9c055b154f8eb82155ff5c428, name: SfxVolume}      # = SFX 分组的 m_Volume
> ```
> 
> 三条 GUID 与三个分组自身的 `m_Volume` 逐一对应 ⇒ 参数确实挂在正确总线上。**本条闭合，A2 可以进行。**
> 
> **DSH 的两次误判，记录以免重犯**：① 我在你保存**之前**读了文件，把"尚未落盘"当成了"你没有做"；
> ② 参数一旦暴露，YAML 会从单行 `[]` 变成**多行列表**，而我只打印匹配行的首行，于是把非空误读成空。
> 取证姿势已修正：**读区域，不读首行**。

> **DSH 复核 1（2026-10-05）**：工作区里**唯一**的 mixer 文件
> `Assets/Audios/Mixers/Master.mixer` 第 32 行仍是 `m_ExposedParameters: []`，且该文件 `mtime = 12:33:08`
> （等于工作区检出时间，今天从未被写过）。
> 
> 因此**形式条件已满足，事实条件尚未满足**：闸门形式上已由你打开，但 DSH 仍不得开始 A2 ——
> 代码必须按**真实参数名**调用 `AudioMixer.SetFloat`，而工作区里没有任何参数名可读；
> 对未暴露的参数调用 `SetFloat` 会**静默失败**，那等于交付一个假装生效的音量设置（§1：不得猜）。
> 
> 请对号入座其一：
> 
> 1. 你是在**本工作区的 Unity** 里暴露了参数但**资产没保存** ⇒ 在 Unity 里按 `Ctrl+S`（或 `File > Save Project`），
>    回一句"已保存"，我立刻重读该文件；
> 2. 你是在**本体**的 mixer 上做的 ⇒ 按 §1.3 我读不到、也不该去读；请把这次资产改动合进本工作区后告诉我；
> 3. 参数名与我建议的不同 ⇒ 直接把**实际名字**告诉我。
> 
> 在你给出**可核实的产物**（或真实参数名）之前，A2 保持不开始；A1 不受影响，已交付并已编译通过。

### 1b. A2 第二步：把 `Master` mixer 拖进 `AudioManagerConfigs`（字段已由 DSH 加好）

- [x] 让 Unity 编译一次，使 `AudioManagerConfigs` 新增的 `Bus Volume` 组出现。

- [x] 打开 `Assets/SO/Jill/DefaultAudioManagerConfigs.asset`，在该组的 `Mixer` 字段里拖入 `Assets/Audios/Mixers/Master.mixer`。
  
      （三个参数名已按你暴露的 `MasterVolume` / `OstVolume` / `SfxVolume` 预填，如无改名无需动。）

- [x] 回一句"已拖入" ⇒ **DSH 才会开始写** `AudioManager` 里的应用逻辑（§5.1.3 硬闸门：未赋值前不得开工依赖它的代码）。

> **DSH 复核（2026-10-05）—— 本条已闭合**：`Assets/SO/Jill/DefaultAudioManagerConfigs.asset` 第 19 行现为
> `mixer: {fileID: 24100000, guid: a6b573f691acdc44a9421d10d0de8b7a, type: 2}`，
> 与 `Assets/Audios/Mixers/Master.mixer.meta` 的 `guid: a6b573f691acdc44a9421d10d0de8b7a` **完全一致**，
> `fileID 24100000` 也正是该 mixer 的 `AudioMixerController`；第 20–22 行为三个参数名。
> ⇒ 引用精确指向该 mixer，A2 的应用逻辑已据此写好。

### 2. 文档重置的收尾 —— 阻塞"交付形式完整性"，不阻塞 A1 编码

- [x] 把 DSH 给出的整块文本粘贴为 `Assets/AGENTS.md`。

- [x] 删除旧 `Assets/Scripts/Jill/AGENT-DOCs/AGENTS.md`。

- [x] 删除 `Assets/Scripts/Jill/AGENT-DOCs/CHANGELOG.md` 与 `Assets/Scripts/Jill/AGENT-DOCs/PENDING_INSPECTOR.md`
  
      （DSH 执行删除被沙箱拒绝：见 `Assets/CHANGELOG.md` 的「诚实记录（第一次）」）。

- [x] 让 Unity 获得焦点完成导入，使下列 `.meta` 生成、并使已删文件的孤儿 `.meta` 与空目录 `AGENT-DOCs/` 被 Unity 清理：
  
      `Assets/AGENTS.md.meta`、`Assets/CHANGELOG.md.meta`、`Assets/PENDING_INSPECTOR.md.meta`。

- [x] 复核 `git status`：预期只剩 `Assets/Scripts/Jill/Infra/InputSystem/PlayerControls.cs` 这一条 Unity 重写生成脚本所致的**预期** diff，
  
      外加本次文档的增删。

### 3. A1 的边界裁决：淡出是否扩展到"抢占" —— 阻塞该项后续工作

A1 交付了"起播淡入 / 非循环 clip 的尾部淡出 / 优雅停止的淡出"，但**抢占仍是硬切**。
原因: 抢占必须在**同一帧**释放池槽位（`GameObjectPool.CanReuse` 只认空闲实例数），而淡出会让该 emitter 在渐变期间继续被占用，
于是池满时会把原本的"抢占并服务"变成"抢占后仍被拒绝"。要做得对，需要一种"正在淡出的 emitter 不占池槽位"的记账模型，
而 `GameObjectPool` 属于另一个交付单元（`Core/GameObjectPool`），按 §4.2 DSH 必须先问再写。

- [x] 裁决: **(甲)** 保持现状(抢占硬切)，接受"淡出只在起播 / 尾部 / 优雅停止三处生效"；
  
      或 **(乙)** 开一次跨交付，把 `GameObjectPool` 的记账改成"可回收的退役实例"，再由 DSH 实现抢占淡出。

### 4. A1 交付的落地：让 Unity 看一眼（导入 + 编译） —— 阻塞"A1 验收"

- [x] 让 Unity 获得焦点完成导入与编译，使 `Assets/Scripts/Jill/Core/Audio/README.md.meta` 生成，并确认 Console **无编译错误**。
  
      本次改动的文件：`Core/Audio/SODefinitions/AudioClipData.cs`、`Core/Audio/AudioRegistry.cs`、`Core/Audio/AudioEmitter.cs`、
      `Core/Audio/AudioBuilder.cs`、`Core/Audio/ISoundHandle.cs`、`Core/Audio/SoundHandle.cs`、`Core/Managers/AudioManager.cs`、`Core/README.md`；
      新增文件：`Core/Audio/README.md`（以及**待 Unity 生成**的 `README.md.meta`）。

- [x] 在 `AudioClipData` 资产上按需填 `FadeIn` / `FadeOut`（默认 `0`，因此**现有三个 clip 的听感完全不变**；要听淡入淡出必须显式填值）。

- [x] 复核 `git status`：预期只有本次音频改动，外加 `PlayerControls.cs` 那一条 Unity 预期重写。

- [x] 建议实测两条：(1) 给某 clip 填 `FadeIn = 0.15` / `FadeOut = 0.3`，播一次听淡入、停一次听淡出；
  
      (2) 给某个**循环** clip 把 `MaxInstances` 设为 `1`，连点两次测试按钮，确认第二次被拒且日志出现
      `Per-clip limit of 1 is reached` —— 这正是本次修掉的真实缺陷（旧实现会让上限被无限突破）。

### 5. A2 验收：玩家音量真的作用到总线上 —— 阻塞"A2 验收"

- [x] 让 Unity 编译一次，确认 Console **无编译错误**。

- [x] 进 Play 模式，Console 应出现 `Bus volumes applied: master 1, ost 0.5, sfx 0.5`（若你改过设置，则是你保存的值）。

- [x] 听感验证 1：把 `AudioSettings.SfxVolume` 改成 `0.2` → 点 `AudioManager` 的 **`Apply Audio Settings`** 按钮 →
  
      音效应变小（`0.2 ⇒ -14 dB`）。

- [x] 听感验证 2：把 `MasterVolume` 改成 `0` → 再点一次该按钮 → **全部静音**（`-80 dB`，即 mixer 自身的静音阈值）。

- [x] 复核 `git status`：预期为本次 A2 的改动（`AudioManagerConfigs.cs`、`AudioManager.cs`、两份 README、`CHANGELOG.md`）
  
      外加 `DefaultAudioManagerConfigs.asset`（你拖入 mixer 所致）与 `PlayerControls.cs` 那条预期重写。

### 6. 移交与合并之后的收尾 —— 阻塞"下一个 Subsystem 的干净起点"

- [ ] **处置那 2 个未暂存修改**：`HeXie/Infra/InputSystem/PlayerControls.cs` 是 Unity 按新 `.inputactions` 的**重新生成结果**（正确版本），
  
      `...PlayerControls.inputactions` 只差"文件末尾换行"。建议**提交这两处**，或让 HeXie 侧重新生成后提交；
      **不要还原 `PlayerControls.cs`** —— 还原后下一次导入还会再生成，diff 会反复出现。
- [ ] **裁决跨树依赖**：`Jill/Core/CoreFacade.cs` 的 `[RequireComponent(typeof(InputManager))]` 现在指向 `HeXie` 的 `InputManager`
  
      （`Core.prefab` 已改指，GUID 可解析）。若你希望 Core 与 Input 解耦（例如让门面暴露接口而不是具体组件），请裁决 ——
      这属于跨交付单元的改动，按 §4.2 DSH 先问再写。
- [ ] **粘贴 `AGENTS.md` 的所有权补丁**（5 处文本见本次对话；§1.1 规定 DSH 不得改动该文件）。
- [ ] 复核 `git status`：预期只有那 2 个 HeXie Input 文件为未暂存修改，无 `D`/`??` 指向已移交路径。

### 7. Environment (E1) 的首次装配与验收

**状态：已通过。** 你回传的日志逐字为 `###ACCEPTANCE PASS (5 passed, 1 skipped)`（第 1/2/3/4/6 步 PASS，第 5 步 SKIPPED）。
装配由你完成；下列清单保留作记录，不需要再逐条执行。

- [x] **新增 Tag**：`Project Settings > Tags and Layers` 里加 `WireAttach`（挂线点）；确认 `Player` Tag 存在。门面按这两个 Tag 找玩家与挂点，缺失时只会记一条 warning。
- [x] **SceneRoot**：关卡里唯一的 Root GO，挂 `EnvironmentFacade`（每关一个，不跨场景）。
- [ ] **插座孔**：GO + `PowerSocket` + `Collider2D`；在 `Wires` 列表里拖入从它引出的线。
- [ ] **线**：GO + `Wire`（自动带 `LineRenderer`）；线上的端点/折点做成**子物体**并各挂 `WirePoint`；`Wire.Polarity` 设火/零/地。
- [ ] **带电接口**：GO + `PolaritySocket` + `Collider2D`；`Accepted` 选零 / 火 / 双性（双性 = 换线点）。
- [ ] **锚点**：GO + `Anchor`；不可动的留空 `Can Pickup`，可绕线的勾选 `Can Interact`。
- [ ] **让 Unity 编译一次**，确认 Console **无编译错误**；`Assets/Scripts/Jill/Env/` 的 `.meta` 会随之生成（清单见 CHANGELOG 的 E1 条目）。
- [ ] 在门面上点 **`Refresh nodes and evaluate`**，确认 Console 出现 `###ASSERT circuit closed=...` 一行；选中门面应能看到折线 Gizmos（火=红 / 零=蓝 / 地=绿）。
- [ ] **裁决那一处规则放宽**：条件 3 被实现为"终止"（插回插座孔 **或** 停在双性接口）而非"两个自由端都插回插座孔"，理由见 `Env/README.md`。
  
      若你要严格字面语义，说一句即可 —— 删掉 `IsTerminated` 里的双性分支，一行改动（代价是关卡不可通关）。
- [ ] **向 HeXie 侧提出跨树接口需求**（DSH 不能替他们改）：目标发现改为接口发现；以及 `PlayerFacade.OnPickupUsed(PickupUsageDetails)` 目前**不存在**。

### 8. E1 验收之后的收尾 —— 阻塞"E2（拾取 / 携带）与 E1 结账"

- [ ] **修正 Tag 归属**：门面的警告逐字为
      `The object tagged WireAttach (Anchor) has no ancestor tagged Player (the player found was MockPlayer).`
      也就是名为 `Anchor` 的物体带着 `WireAttach` Tag。请在 Inspector 顶部核对并改正：`MockPlayer > WireAttach` 应为 **WireAttach**、`Anchor` 应为 **Untagged**；
      改完 `Ctrl+S`，再点 `Refresh nodes and evaluate`，警告应消失。
      **影响**：Tag 落错处时，线被携带后自由端会跟随 `Anchor` 而不是玩家的手（验收步骤本身不受影响）。
      磁盘证据：`Anchor.prefab` 是 `Untagged`、`MockPlayer.prefab` 是 `Player` + `WireAttach`，场景文件里也没有 Tag 覆盖项 —— 以 Inspector 实际显示为准。
- [ ] **裁决条件 3 的"终止"放宽**：E1 现按"终止 = 插回插座孔 **或** 停在双性接口"实现，验收第 6 步据此 PASS。
      若你要严格字面语义（两个自由端都必须插回插座孔），说一句即改；代价是关卡不可通关。
- [ ] **可选：让验收第 5 步从 SKIPPED 变 PASS** —— 再放一个 `PolaritySocket`，`Accepted` 只勾 `Live`（或只勾 `Neutral`），即可验证"电性不匹配被拒绝"。
- [ ] **提交 E1**：13 个 `.cs` 与其 `.meta`（清单见 `CHANGELOG.md` 的 E1 条目；13/13 `.cs.meta` 已由 Unity 生成），加上目录 `Assets/Scripts/Jill/Env.meta`。
      `Env/README.md` 已 ignore，无需提交。
- [ ] **向 HeXie 侧提出跨树接口需求**（DSH 不能替他们改）：目标发现改为 `GetComponentInParent<IEnvironmentPickup>()` / `IEnvironmentInteractable>()`；
      以及 `PlayerFacade.OnPickupUsed(PickupUsageDetails)` 目前**不存在** —— 它正是 E2（拾取 / 携带）的前置。

## 历史

（本文件随 2026-10-05 的文档重置新建，暂无历史条目。）
