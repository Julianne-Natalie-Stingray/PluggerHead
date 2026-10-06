# 场景切换服务

生产玩法通过 `CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId)` 请求异步 Single 加载。目标由调用方决定；配置还供主菜单判断可加载关卡、进度追踪反向查询场景。Editor 测试为了隔离会直接使用 SceneManager，不能把本服务描述为整个工程唯一的加载调用。

## 逐文件职责

| 文件 | 职责 |
| --- | --- |
| SceneId.cs | 类型化场景键，保持显式整数值；编译器不验证资源或构建注册。 |
| SceneSwitchConfigs.cs | ScriptableObject，存储 ID、无扩展名场景名、isGameplayLevel；提供三个线性查询和编辑器空名警告/手动去重。 |
| SceneSwitchManager.cs | Core 上的 MonoBehaviour，校验请求、持有 AsyncOperation 和 IsSwitching、驱动 Loading 前后状态。 |
| 各脚本 .meta | GUID 与 Core/配置引用一致；没有默认引用或自定义执行顺序。 |
| README.md / .meta | 目录说明及资源标识。 |

## 默认注册表

| SceneId 整数 | 名称 | 路径（相对 Assets） | 玩法关卡 |
| --- | --- | --- | --- |
| 1 | SceneSwitchTarget | Scenes/Tests/SceneSwitchTarget.unity | 否 |
| 2 | CircuitDiagnostics | Scenes/Tests/CircuitDiagnostics.unity | 否 |
| 3 | GameplayIntegration | Scenes/Tests/GameplayIntegration.unity | 是 |
| 4 | MainMenuScene | Scenes/MainMenuScene.unity | 否 |

默认配置为 SO/SceneSwitch/DefaultSceneSwitchConfigs.asset；Core 的 Manager、LevelProgressTracker 与主菜单共享它。Build Settings 顺序为 MainMenuScene、GameplayIntegration、CircuitDiagnostics、SceneSwitchTarget，全部启用；构建索引与枚举整数不是同一概念。退役枚举值 0 不复用。新增关卡须一起维护枚举、资源、配置和构建注册；玩法标记决定是否可由菜单进入及记录进度。

## 查询与配置校验

TryGetSceneName 返回首个同 ID 的名字，即使名字为空仍返回 true；IsGameplayLevel 检查任一同 ID 条目是否标为玩法；TryGetGameplayLevel 用精确场景名找到首个玩法条目。这些查询不校验列表为 null、不清理重复项。MainMenuScreen 每帧调用查询，不是只在切换时查询。

OnValidate 只警告空名。Inspector 的 Remove Duplicates 按钮会实际修改列表，按 ID 保留首项；不自动运行，不处理重复场景名、错误枚举值或构建注册。重复 ID 可使首项映射和任一玩法标记不一致，重复场景名可使进度记录到错误 ID；当前默认资产没有这些冲突。场景名保持与 Scene.name 一致，避免破坏 Tracker 的反向查找。

## 切换流程

Awake 缺 configs 时记录一次错误并禁用组件。RequestSwitch 的拒绝顺序为：缺配置/本实例忙碌时静默返回 null；无映射时记录错误；构建索引不存在时记录错误。后两者也返回 null。通过后调用 LoadSceneAsync(Single)、设 isSwitching=true、启动协程并返回原始 AsyncOperation。方法未检查组件 enabled 或 activeInHierarchy，也未捕获加载调用异常。

协程先禁用自动激活并 EnterLoading，等 progress>=0.9 后放行激活，再等 isDone 后 ExitLoading、清空本地标记。这不保证与第一帧画面显示精确同步。加载同一个场景不会自动拒绝，而是重新加载；没有玩法标记限制，因此主菜单和诊断场景也能请求。全局单次切换依赖 CoreFacade 去重，IsSwitching 本身是实例字段。

本服务不直接写时间倍率或监听器暂停；GameState 的 Changed 订阅者会响应，AudioManager 在 Loading 时解除监听器暂停。冻结与加载的转换约束见 [GameState](../../Game/GameState/README.md)，以当前状态管理器实现为准。调用方应保持 Core 存活且激活，并把 AsyncOperation 当作观察对象，不与管理器争夺 allowSceneActivation。

## 已知缺口与核查（2026-10-06）

没有取消、排队、超时或 try/finally/OnDestroy 状态恢复。Core 失活/销毁或 Changed 回调抛异常可使协程无法到达尾部，遗留 Loading/忙碌标记；EnterLoading 在禁止激活之后执行，若该回调抛异常，加载还可能停在等待放行激活。这些是当前实现风险，本次文档检查不修改行为。

已逐一核对三个脚本与 meta，再比对默认配置、Core prefab、主菜单/进度调用、Build Settings 及测试。SceneAssetTests 检查当前注册表和场景资源；MainMenu PlayMode 流程实际切换并检查目标活动场景、IsSwitching 结束及返回菜单后的状态。未覆盖错误/重复配置、忙碌拒绝、同场景重载、回调异常或中断清理，不能用正常流程通过证明这些路径正确。

独立文档复审通过。本轮只改本目录注释与文档；最终工作区同时包含同期 GameState/Setting 修复及新增测试，已核对交叉说明不再将修复前的状态转换缺陷写作当前行为。编译后 Console 无 error，EditMode job `ffde23500166454aad0d97b5b4ec39f2` 通过 10/10，随后 PlayMode job `cb4f5995eafe45c3aaad666f473cb1cf` 通过 14/14。本目录异常清理风险仍存在。
