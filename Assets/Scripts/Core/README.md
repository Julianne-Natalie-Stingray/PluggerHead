# Core 共享服务总览

Core 提供输入、音频请求和场景切换服务，并使用通用对象池。它不决定玩家交互规则或下一个关卡；这些由 Player、Env 与 Game 层负责。当前采用内容场景内配置 Core、由 DontDestroyOnLoad 保活、以 Single 模式切换内容的结构。

## 目录与入口

| 路径 | 职责与文档 |
| --- | --- |
| CoreFacade.cs | 挂在 Core GameObject 上，缓存同物体的 Input、Audio、SceneSwitch，提供静态 Instance，处理持久化与重复实例。 |
| [Input](Input/README.md) | InputManager 适配生成的 PlayerControls，提供状态与事件；DragAndDropService2D 是需要宿主驱动的普通辅助类。 |
| [Managers](Managers/README.md) | AudioManager 管理音频请求、池、注册表、限流、状态通知和混音器应用。 |
| [Audio](Audio/README.md) | 请求 Builder、句柄、池化声部、注册表，以及标识、开发者配置、玩家音量数据。 |
| [SceneSwitch](SceneSwitch/README.md) | 类型化 SceneId、场景映射与玩法标记、异步切换和 Loading 生命周期。 |
| [GameObjectPool](GameObjectPool/README.md) | Unity ObjectPool 包装，支持借还、预热、回调及容量查询；Get 本身不强制总创建上限。 |
| CoreFacade.cs.meta、README.md.meta、各子目录 meta | 保留资源身份；CoreFacade 没有特殊执行顺序或默认引用。 |

生成的输入绑定与菜单常量属于 [Infra](../Infra/README.md)。Configs 表示开发者维护的工程配置，Settings 表示玩家可调整的数据；不要从命名推断它们一定在所有组件初始化前或之后可用。

## CoreFacade 的初始化与生命周期

Core 必须由场景或调用方创建，Instance 访问不会自动生成它。Awake 在已有其他存活实例时记录 Warning 并 Destroy 自己的整个 GameObject；否则先赋 Instance，再通过 GetComponent 缓存三个服务，最后对 GameObject 调用 DontDestroyOnLoad。四个 RequireComponent 分别声明 InputManager、AudioManager、SceneSwitchManager、TimerRunner。

门面只缓存组件，不负责完成各服务的 Awake，也不检查服务启用状态或资源配置。RequireComponent 不能代替配置检查；AudioManager 的 emitterPrefab/configs 和 SceneSwitchManager 的 configs 仍需装配。其他组件的 Awake 没有必然晚于 Core 的顺序，建议沿用现有 Player 的 Start 或明确初始化流程获取服务；Instance 非空本身不是所有服务就绪的信号。

当前 prefab 是根对象，满足持久化使用要求，AudioRoot 子物体随它保留。切换不会使指向存活 Core 的引用自动失效；被卸载的场景对象才需要重新获取。新场景中的 Core 副本会被去重，指向该副本的序列化引用不可当作最终存活的全局服务引用，跨场景业务应从当前 Instance 取得服务。

尚需注意的边界：Destroy 是延迟操作，重复 Core 的其他组件可能已经执行 Awake/OnEnable；本类没有阻止这些初始化副作用。没有 OnDestroy 或 SubsystemRegistration 清空静态引用的钩子，销毁后的 Instance 可能保留 Unity 已销毁对象包装，按 Unity 对象有效性判断，不能把普通 C# 的 ?. 当作同等检查。运行时把 Core 放到其他物体下面也没有根对象检查。这些不是当前常规 prefab 流程已覆盖的保证。

## 已装配资源与场景

Assets/Prefabs/Core/Core.prefab 包含上述四个依赖组件和 CoreFacade，AudioRoot 是其子物体。AudioManager 引用 AudioEmitter prefab 与 DefaultAudioManagerConfigs；SceneSwitchManager 使用默认场景配置。LevelProgressTracker 位于实际关卡的环境对象，按通关事件记录内存进度。

MainMenuScene、GameplayIntegration、CircuitDiagnostics、FinalScene 含 Core 实例；SceneSwitchTarget 不含 Core，依赖切换后保留的实例。直接运行不含 Core 的场景不会由门面自动补齐服务。构建入口和场景映射需与 [场景切换说明](SceneSwitch/README.md) 保持一致。

## 接入契约

- 输入由 PlayerMove、PlayerInteraction 等解释为玩法行为；不是“尚无消费者”的模板。InputManager 管理生成 Controls 的生命周期，但不自动按 GameState 禁用输入，也不提供 UI 遮挡仲裁。DragAndDropService2D 当前没有业务宿主，调用约束见其目录文档。
- 音频请求使用新的 CreateBuilder，通过 Play 返回 ISoundHandle 或 null。Builder 是可变 struct，消费状态只属于当前副本。Registry 属性没有 setter，但返回对象具有公开修改方法，不是只读集合。
- 音频正常完成先归还池再通知句柄；带淡出的 Stop 则先使句柄失效，声部继续占槽。实例限制、回调重入、外部销毁、冻结与渐变等边界集中记录在 Managers/Audio 文档，不能把 Finished 通用地解释为“已经释放池槽”。
- SceneSwitch.RequestSwitch 校验组件活动状态、配置、本实例/跨实例占用、Loading 和目标映射/构建注册。满足条件时可以重载当前场景，没有专门的同场景拒绝规则。返回操作供观察，不修改其 allowSceneActivation。
- 切换以默认允许激活的异步 Single 加载推进，通过 completed 委托恢复 Loading 并清理占用；不依赖宿主协程继续活动，不承诺与首个显示帧精确同步，也不提供取消或排队。配置缺失、并发请求等并非全部输出日志。
- AudioManager 在 Start 应用当前玩家音量；SettingsScreen 保存成功后显式应用。存储层 Save/ResetToDefault 本身不应用 Mixer。输入、音频及场景服务都不自动保存完整玩法进度，进度边界见 [Game](../Game/README.md)。

服务可以读取 GameState、玩家设置和通用 Transform 等依赖，但不应承担具体关卡或玩家交互规则。实际功能和实现边界以各子目录文档及当前源码为准。

## 核查与验证（2026-10-06）

先逐一核查 CoreFacade.cs、现有 README、对应 meta 与子目录 meta，再结合已完成的子目录审查核对 prefab、场景和消费者。总览删除了“所有引用随切换销毁”“任意 Awake 即可安全访问”“输入无消费者”“同场景总拒绝”“没有坐标就不空间化”等失实说明；保留结构与接入约束，把详细生命周期归到对应子目录。

现有主菜单与场景测试覆盖实际跨场景流程；音频与设置测试覆盖正常播放和 Mixer 应用。没有专门覆盖嵌套 Core、重复实例的初始化副作用、销毁后的静态包装或禁用 Domain Reload 的跨会话生命周期。

本轮仅修改门面和管理器注释以及目录文档；去除注释/空白后源码与原版本一致，独立复审通过，引用与链接已检查。编译完成且 Console 无错误；依次运行 EditMode 17/17、PlayMode 27/27 通过（job `1d96f6f716264310af0df0e75726f731`、`21336b3daa794474982d80666cea66c0`）。测试包含当时并行新增的音频修复与检查，不宣称覆盖上述尚缺专项验证的生命周期组合。
