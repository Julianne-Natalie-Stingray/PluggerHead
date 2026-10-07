> 历史 DSH 工作流记录，已不作为当前仓库规则；现行规则见 `Assets/AGENTS.md`。

# 1 工作流总述

## 1.1 分工

- DSH用于简化编程工序；凡可由 Unity Editor / Unity MCP 完成的资产、Inspector、场景、预制体、项目设置等操作，均可由 Unity MCP 直接执行。
- Unity MCP 不受资产类型、文件扩展名或 Editor 对象类别限制，可按任务需要创建、修改、移动、重命名、复制或删除 Unity 项目内容。
- 对副本进行改动后，我审批并修改，merge到主分支。
- 如果我的指令与该文件产生冲突，停止并询问。
- 本文件(`Assets/AGENTS.md`)由我维护。DSH可以指出问题并给出建议文本，但**不得改动本文件**；改动一律由我粘贴。

## 1.2 写入边界

- DSH直接文件系统写入仍受 `workspace-write` 沙箱约束，只能在本工作区内进行；若权限状态异常，应立即停止并询问。
- 对 Unity 项目内容的编辑不按资产类型设限。需要 Editor 语义的操作优先通过 Unity MCP 执行。
- Unity MCP 可修改 Unity Editor 能管理的项目内容，包括 `Assets/`、`ProjectSettings/`、通过 Package Manager 管理的项目依赖配置，以及对应的 Inspector / Importer / Build Settings 等编辑器状态。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`obj/`、`Build/`、`Builds/` 等 Unity 生成目录仍由 Unity 自行管理；允许 Unity / Unity MCP 间接更新，不应由 DSH 作为普通文本目录手工维护。

## 1.3 读取边界

- **读取权限无边界。沙箱只限制写入，不限制读取。**
- 工程本体位于本工作区之外，与副本共享同一个 `.git`。具体形式是：本工作区是它的一个 git worktree，因此工作区根下的 `.git` 是一个**指针文件**，指向工作区之外的本体 `.git`。
- DSH不得主动访问本工作区之外的任何路径，不得读取工程本体，不得读取本体的 `.git`。允许读取**本工作区自己的** `.git` 指针文件的**那一行**，以判定 git 命令会走到哪里；但**不得跟进**该目标，也不得复述其中的本体路径。
- 该约束的目的是**本体不被版本控制影响**，而不是完全隔离。因此 §3.1 允许的只读 git 命令可以穿透 worktree 链接。
- 当某项非 Unity MCP 操作看起来必须越界时，停止并询问，由我代为执行。

## 1.4 写入路径与 Unity MCP 权限

- DSH交付路径为含`Jill/`的目录名
- Unity MCP 可按任务需要操作整个 Unity 项目的 Editor 侧内容，不受“代码侧 / 资产侧”分工限制；包括场景、预制体、`.asset`、材质、动画、音频、Importer、Inspector 赋值、ScriptableObject、项目设置等。

# 2 写入权限

## 2.1 文件类型

- **不按文件扩展名限制可修改类型。** Unity 项目中凡任务需要的文件或资产类型均可修改。
- Unity MCP 可创建、修改、移动、重命名、复制、覆盖或删除 Unity 资产及其关联文件，包括但不限于 `.cs`、`.asmdef`、`.json`、`.md`、`.meta`、`.unity`、`.scene`、`.prefab`、`.asset`、`.mixer`、`.mat`、`.anim`、`.controller`、`.inputactions`、`.menutool` 等。
- 通过 DSH 直接文件系统通道进行的操作仍受实际沙箱能力约束；当某个动作在文件系统通道不可用、但 Unity MCP 可以安全完成时，优先使用 Unity MCP。
- 唯一文档权限例外：`Assets/AGENTS.md` 仍由我维护，DSH不得写入(§1.1)。

### 2.1.1 `.meta` 与 GUID 规则

- `.meta` 不再属于禁止修改类型。Unity MCP 可以随资产创建、移动、重命名、复制或删除而维护对应 `.meta`。
- 默认由 Unity 的 Asset Database 维护 `.meta` 与 GUID；如果任务确实需要直接调整 `.meta`，允许执行，但必须保持 GUID 与引用关系一致，不能无理由重建已有 GUID。
- 新增、移动、重命名或删除资产后，以 Unity 刷新后的实际 Asset Database 状态为准，并复查对应 `.meta` 是否与资产一致。
- 已存在且被引用的资产，其 GUID 应保持稳定；需要改变 GUID 时必须把所有引用更新纳入同一交付。
- `Assets/CHANGELOG.md` 的新增文件清单只需记录实际新增 / 移动 / 删除的项目内容及最终 `.meta` 状态，不再使用“等待 Unity 生成 `.meta`”作为强制人工步骤。
- 被 `.gitignore` 排除的工作文档及其 `.meta` 不要求进入仓库。

## 2.2 文件目录

- DSH直接文件系统写入的默认工作区仍为本 workspace；具体能力以沙箱实际授权为准。
- Unity MCP 可按任务需要修改 `Assets/`、`ProjectSettings/`，以及 Unity / Package Manager 正常管理的 `Packages/` 项目配置或嵌入式包内容，不需要因为目录类别额外申请。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`obj/`、`Build/`、`Builds/` 等生成目录由 Unity 自行管理。Unity MCP 可以通过正常 Editor 操作触发其中内容变化，但不把它们当作手工维护的源文件目录。
- `.vs/`、`.idea/` 等 IDE 状态目录不是交付目标；如工具自行更新，不作为 Unity 资产权限违规处理。

# 3 操作权限

## 3.1 允许的操作

- DSH可在沙箱允许范围内创建、修改项目文件，不再按扩展名设定白名单。
- Unity MCP 可执行 Unity Editor 能提供的资产与编辑器操作，包括：
  - 创建、修改、复制、移动、重命名、删除任意 Unity 资产；
  - 修改 Inspector 字段、组件、Importer 与序列化数据；
  - 创建和编辑场景、GameObject、Prefab、Prefab Variant；
  - 创建和编辑 ScriptableObject、Material、Animation、Animator、AudioMixer、Timeline、Input Actions 等资产；
  - 修改 Project Settings、Build Settings、Tags / Layers、Physics、Graphics、Quality 等 Editor 管理的项目配置；
  - 使用 Package Manager 完成与任务相关的包配置；
  - 刷新、重新导入、保存资产和场景，并读取 Console / 编译结果进行自查。
- 允许修改 `Editor` 脚本，不需要另行申请。
- Unity 生成文件也不再作为“禁止修改类型”处理；但应知道它们可能在重新生成时被覆盖。若修改目的是长期改变生成结果，应优先修改其源资产、生成配置或生成器。
- 只读的 git 操作，**仅此三条**：`status`、`diff`、`git update-index --refresh`。
- `git log` 与 `git show` **禁止**：它们会把本体历史搬进DSH的上下文，而 §1.3 的目的是本体不被版本控制影响。
- `git update-index --refresh` 允许，且**仅此一条** `update-index` 形式。它只刷新索引中记录的文件状态缓存，不改变任何被跟踪文件的内容，不动 `HEAD`，不动工作区。禁止 `--add`、`--remove`、`--force-remove` 以及任何其他 `update-index` 形式。
- 允许该命令的理由：整个审批流程建立在“`git status` 不说谎”之上。幽灵 ` M` 标记会训练我忽略这个标记，而它正是唯一能看见越界写入的像素。
- 允许**只读**访问 `Library/ScriptAssemblies/` 下的编译产物(时间戳、字节数)，用于自查“交付边界处代码是否编译通过”。

## 3.2 仍然禁止的操作

- `git commit`、`push`、`merge`、`rebase`、`reset`、`checkout`、`switch`、`clean`、`add`。
- 任何对 `Assets/AGENTS.md` 的改动(§1.1)。
- 主动越过 §1.3 的工作区读取边界去访问工程本体或本体 `.git`。
- 绕开 Unity 语义直接破坏生成目录、缓存或数据库一致性的操作。

对于删除、移动、覆盖等破坏性资产操作：

- **允许 Unity MCP 执行。**
- 完成后必须通过重新读取资产、场景、Hierarchy、Inspector、Asset Database 或等价结果确认最终状态；不得只依赖工具自身打印的“成功”回声。
- 如果直接文件系统通道因沙箱机制无法执行某项破坏性操作，而 Unity MCP 能完成，则使用 Unity MCP；如果两者都无法完成，再登记到 `Assets/PENDING_INSPECTOR.md`。

## 3.3 会话前置条件

- 需要使用 Unity MCP 或执行 Editor 侧资产操作前，必须确认副本正被一个 Unity GUI 实例打开。可依据 `Temp/UnityLockfile` 与 Unity 进程状态判断。
- 该前提用于确保 Unity MCP 有有效的 Editor 会话，并让 Asset Database、序列化、导入和 `.meta` / GUID 管理由 Unity 正常完成。
- 若本次工作纯属不依赖 Unity Editor 的代码 / 文档修改，可在沙箱允许范围内进行；一旦需要资产导入、场景 / Prefab / Inspector / Project Settings 操作，就必须满足上述 Unity 会话前提。
- 同一工程目录不允许被两个 Unity 进程同时打开。因此不得为了执行任务再启动第二个 Unity 实例或命令行 Unity 进程去抢占同一工程。
- 资产刷新优先由 Unity MCP 主动执行 `Refresh` / `Reimport` / `Save` 等等价操作，不再把“我看一眼 Unity”作为必经的人工作业。
- 若某项 Unity 侧变化确实无法通过 Unity MCP 可靠触发，再把需要人工确认的内容写入 `Assets/PENDING_INSPECTOR.md`。

# 4 交付粒度

## 4.1 交付单元

- 一份交付的单位是**一个Subsystem**，不是单个文件，也不是整个工程。
- 一份交付应包含：该Subsystem的全部代码和资产改动、该Subsystem的 `README.md`，以及 `Assets/CHANGELOG.md` 与 `Assets/PENDING_INSPECTOR.md` 的必要增量。
- **“完整”指编译与资产引用意义上的完整，不指设计意义上的完整。** 交付边界处代码必须能编译，场景 / Prefab / ScriptableObject 等引用必须保持有效。设计仍可迭代，交付之后继续对同一Subsystem迭代是允许且预期的。

## 4.2 跨Subsystem接口

- 若一份交付需要引用另一个Subsystem尚未交付的接口，该接口属于**设计上的不完整**，会使编译边界不成立。
- 此时**先问再写**：停止，说明所缺接口，等我裁决。不得先写半个接口。
- 若交付依赖资产侧前置，优先由 Unity MCP 在同一交付内直接完成，不再默认视为“只有我能完成”的事项。
- 只有 Unity MCP 无法可靠完成、需要外部账号 / 人工判断 / 第三方工具或我本人明确确认的前置，才进入 `Assets/PENDING_INSPECTOR.md` 并形成硬闸门。
- 该规则同时满足 §5.2.1 “Subsystem间低耦合”：跨Subsystem的引用点应当少而显式。

# 5 工作流规范

## 5.1 日志 / 开发文档 / 注解规范

- 中英双语**只适用于代码内的日志、文档注解与注释**。三条都写时英文在前。
- 生成出来的 `.md` 文档不用双语，只用中文，见 §5.1.0。

### 5.1.0 生成的文档只用中文

本节的 `.md` 是交付物，不是代码，读者只有中文母语的你，因此只用中文撰写。代码里的日志与注解仍按 §5.1 保持中英双语。

- 适用：每个 Subsystem 的 `README.md`、`Assets/CHANGELOG.md`、`Assets/PENDING_INSPECTOR.md`。
- 不适用：`.cs` 内的 XML注解、行内注释，以及 `GameLog` 日志文本——这些是代码，仍为双语英文在前。
- 本文件(`Assets/AGENTS.md`)也只使用中文说明；技术名词、API、类型名、代码标识、路径和固定产品名可保留英文。

### 5.1.1 README编写

Subsystem的划分以**交付单元**为准。已确立的独立交付单元包括 `Core/Audio`、`Core/SceneSwitch`、`Game/Setting`、`Game/GameState`、`Infra`。

在每个Subsystem下存有一份 `README.md`。
需要写明：

- Subsystem职能；
- 公共API；
- 内部实现思路概述；
- 职能改变，README同步改动；
- 架构层面的**设计决定**(例如“退出时恢复状态归 session 负责”)必须写进对应 README，不能只存在于历史CHANGELOG里；
- `Core/README.md` 覆盖 `Core/` 层面的协调内容(如 `CoreFacade`)，不替代下层Subsystem的README。尚未建立README的交付单元，在首次触及它的交付中补齐，不单独开交付；
- 撰写语言按 §5.1.0：只用中文。

### 5.1.2 CHANGELOG编写

- 每次修改在 `Assets/` 根下编写 `Assets/CHANGELOG.md`，记录修改。新条目写在文件最前。
- 每条记录必须以下述两条强制清单结尾。
- 清单之一，**本次资产 / 文件变化**：列出本次新增、移动、重命名或删除的重要项目内容；需要纳入版本控制的项目同时确认其最终 `.meta` / GUID 状态。用checkbox形式。
- 清单之二，**需要我手动完成的操作**：**只给指针**，指向 `Assets/PENDING_INSPECTOR.md`(单一事实源)。CHANGELOG不重复列举这些操作；若本次确实没有，指针照写并注明“本次无需操作”。
- Unity MCP 已成功完成并经复查确认的 Inspector、场景、Prefab、资产或项目设置操作属于“已发生的改动”，直接写入 CHANGELOG，不进入 PENDING。
- 结账规则：若我尚未勾完上一份交付的人工清单，DSH不得把“已完成”当作既成事实写进新的CHANGELOG条目。
- 撰写语言按 §5.1.0：只用中文。

### 5.1.3 PENDING_INSPECTOR编写

- `Assets/PENDING_INSPECTOR.md` 与 `Assets/CHANGELOG.md` 同级，是**独立文件**，不是CHANGELOG的一个小节。
- `CHANGELOG.md` 记录**已发生**的改动；`PENDING_INSPECTOR.md` 只记录**Unity MCP / DSH 无法可靠完成，仍需我本人或外部工具处理 / 确认**的事项，并且是这类事项的**唯一事实源**。
- 尽管文件名保留 `PENDING_INSPECTOR`，其范围不再表示“所有 Inspector 操作都必须人工完成”。Inspector、资产、场景、Prefab、项目设置等凡 Unity MCP 可可靠完成的操作，应直接由 Unity MCP 执行。
- **硬闸门**：只要其中存在未勾选项，DSH不得开始任何依赖这些未完成前置的新工作。必须停下并提醒我。
- 解闸只能由我**显式确认**触发(例如“已配好”)。**本文件已不再纳入版本控制(见 `.gitignore`)，因此勾选状态不再对 git 可见**；闸门以本机文件的实际内容与我口头确认两者为准。DSH不得仅凭文件内容自行推断闸门已解除。
- 撰写语言按 §5.1.0：只用中文。

### 5.1.4 类注解编写

需要写明：

- Subsystem归属：它属于什么Subsystem？
- 存在位置：例如在哪个GameObject上(MonoBehaviour适用)；
- 职能概览和ownership澄清：它负责什么？哪些相关职能它不负责？
- 生命周期：什么时候创建？什么时候销毁？(动态添加的MonoBehaviour和非Persistent实例适用)；
- 使用的范式：如Singleton、Fluent API等；
- 数据概览：它的载荷包含哪些方面的数据(用于ScriptableObject定义等数据持久化脚本)。

### 5.1.5 接口注解编写

- 谁应该继承这个契约？
- 契约赋予了什么特性？

### 5.1.6 静态类注解编写

- 提供什么服务？
- 谁需要调用？

### 5.1.7 方法注解编写

- 职能，例如“某功能的单一入口”(适用于公共方法)；
- 实现思路，例如“使用Timer包在结束时触发callback”。

### 5.1.8 TODO编写

- 我明确要求暂缓实现，或等待设计决定的实现，应当用 `TODO: ` 注明。
- 需要写明：待实现内容；未实现原因。
- 已知的真实缺陷若不能立即修复，同样用 `TODO: ` 记录，并写明阻塞原因。
- 只允许在XML或常规注解中使用 `TODO: `。

### 5.1.9 被注释的代码块

- 为通过编译 / 存在多种实现但未选型时，可以将代码注释掉。
- 必须附上 `TODO: ` 解释原因。

## 5.2 代码规范

### 5.2.1 基本原则

- Subsystem内高内聚，Subsystem间低耦合。
- 组合大于继承，避免使用简单class继承，尽量少用abstract class继承，但interface继承不在此列。
- 谨慎做interface抽象，除非有 ≥3 个类型拥有相同特性，或者需要划清ownership / scope。
- 尽可能避免magic string match。
- 少用单例范式。
- 最小化暴露字段 / 方法；公共字段除非确有写入需要，全部使用Property + backing field暴露。
- **解决依赖**不使用序列化字段 + Inspector赋值，改用 `GetComponent<>()` + 缓存，除非依赖与场景高度相关必须手动赋值，或确有需要。
- 这里的“依赖”指**子系统之间与子系统内部的依赖解析**。**持久化参数不是依赖**：`configs` 一类ScriptableObject数据通过序列化字段 + Inspector拖入，是允许且首选的(见 §5.2.3.1)，不受上一条限制。
- 数据持久化优于直接序列化，多使用ScriptableObject。
- 相互耦合的多个脚本共用同一份数据时，使用property映射，减少inspector赋值操作。

### 5.2.2 代码模版

#### 5.2.2.1 Region / If 分区

- 暴露的Properties超过5个，划入 `APIs` 区域。
- `OnValidate()` 等划入 `if UNITY_EDITOR`，放在代码最后。
- 测试用方法划入 `Debug` 区域，放在代码最后(与 `if UNITY_EDITOR` 分区的先后则无关紧要)。

#### 5.2.2.2 代码排版

- 一个 `.cs` 文件内只能有一个**对外可见**的 class / enum / struct / etc.。Unity自行生成的脚本不受此限。
- 仅供本文件使用的 `private` 嵌套类型**不拆**：判据是“外界到底需不需要知道它的存在”。
- XML注解先于 Attributes。
- 代码块间空一行，代码块内不空行。
- 每个方法占一个代码块，方法内根据代码逻辑分块。
- 公共Properties占一个代码块(并可能划分region)，放在最前。
- 序列化私有字段占一个代码块，放在第二块。
- 运行时变量不序列化，占一个代码块，放在第三块。
- 常量占一个代码块，放在第四块。
- Debug相关字段和Debug方法一起放在Debug分区内。
- Unity生命周期方法相邻，放在方法最前面。
- 一个方法的多个重载相邻。
- 工具方法、Gizmos方法和 `InitializeInternal` / `ResolveDependencies` 方法放在除 `Debug` 之外的方法后。

#### 5.2.2.3 方法名规范

- `InitializeInternal` 方法解决Subsystem内部依赖。
- `ResolveDependencies` 方法解决Subsystem间依赖。
- `Configure` 方法应用持久化参数。

#### 5.2.2.4 字段 / 变量名规范

- Property：PascalCase。
- local variable：camelCase。
- private field：camelCase。
- constant：camelCase。

#### 5.2.2.5 ScriptableObject规范

- 命名传统为 `configs`。
- 所有数据全部序列化，需要暴露的数据创建公共property。
- 可以提供检索 / 查找等数据相关公有方法，但不应该包含其他逻辑。
- 如有需要，应支持数据 `Validate`、`Clamp` 和 `Remove Duplicate`。

#### 5.2.2.6 访问限制词

声明时从不省略访问限制词。

- `public`
- `[SerializeField] private`
- `private`

#### 5.2.2.7 方法调用规范

- 初始化使用 `Awake()` 而非 `Start()`。
- 持续整个生命周期的订阅使用 `OnEnable()` / `OnDisable()`。
- 禁止在高发方法中使用 `GetComponent<>()` / `Find` 和 `System.Linq` 系列方法。

#### 5.2.2.8 Attribute规范

- 加入 `using System;`，使用 `[Serializable]`。
- 容易产生歧义的序列化字段，可以添加 `[ToolTip]`。
- 详见Unity Package部分。

### 5.2.3 Unity Package

#### 5.2.3.1 NaughtyAttributes

- 序列化ScriptableObject时使用 `[Expandable]`。

#### 5.2.3.2 MenuTool

- `[CreateAssetMenu]` 属性涉及 `MenuTool` 包时，允许 Unity MCP 直接修改 `.menutool` 资产、相关 ScriptableObject 定义及所需 Editor 状态，在同一交付内完成整个往返。
- `Game.MenuTool.g.cs` 属于生成输出，**允许修改**，但应预期重新生成时可能被覆盖。若目的是长期改变生成结果，优先修改 `.menutool`、生成配置或生成器后再让 Unity 重新生成。
- Unity MCP 已完成并复查确认的 MenuTool 改动直接记入 `Assets/CHANGELOG.md`；只有 Unity MCP 无法完成、确需我本人处理的事项才进入 `Assets/PENDING_INSPECTOR.md`。

#### 5.2.3.3 GameLog

- 正常Logging使用 `GameLog.Info`。
- Editor中错误使用 `GameLog.Warning`。
- 运行时错误使用 `GameLog.Error`。
- 这之后，应执行与日志内容一致的操作。
