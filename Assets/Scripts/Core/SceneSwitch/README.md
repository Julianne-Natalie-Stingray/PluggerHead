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
| 5 | FinalScene | Scenes/FinalScene.unity | 否 |

默认配置为 SO/SceneSwitch/DefaultSceneSwitchConfigs.asset；Core 的 Manager、LevelProgressTracker 与主菜单共享它。Build Settings 顺序为 MainMenuScene、GameplayIntegration、CircuitDiagnostics、SceneSwitchTarget、FinalScene，全部启用；构建索引与枚举整数不是同一概念。退役枚举值 0 不复用。新增关卡须一起维护枚举、资源、配置和构建注册；玩法标记决定是否可由菜单进入及记录进度。

## 查询与配置校验

TryGetSceneName 返回首个同 ID 的名字，即使名字为空仍返回 true；IsGameplayLevel 检查任一同 ID 条目是否标为玩法；TryGetGameplayLevel 用精确场景名找到首个玩法条目。这些查询不校验列表为 null、不清理重复项。MainMenuScreen 每帧调用查询，不是只在切换时查询。

OnValidate 只警告空名。Inspector 的 Remove Duplicates 按钮会实际修改列表，按 ID 保留首项；不自动运行，不处理重复场景名、错误枚举值或构建注册。重复 ID 可使首项映射和任一玩法标记不一致，重复场景名可使进度记录到错误 ID；当前默认资产没有这些冲突。场景名保持与 Scene.name 一致，避免破坏 Tracker 的反向查找。

## 切换流程

Awake 缺 configs 时记录一次错误并禁用组件。RequestSwitch 先拒绝失活/禁用、缺配置、本实例忙碌、另一服务持有切换预留或已有 Loading 的请求，静默返回 null；无映射或构建索引不存在时记录错误并返回 null。通过后取得跨实例预留并调用 LoadSceneAsync(Single)。同步启动异常释放预留并向调用方传播，Unity 返回 null 时释放预留并返回 null。

加载保留默认 allowSceneActivation=true，进入 Loading 后注册 AsyncOperation.completed；即使操作已完成而注册立即触发回调，也保证先进入再退出 Loading。完成委托不依赖宿主协程，组件失活或销毁不会中断实际加载与状态清理。EnterLoading/ExitLoading 的订阅者异常会记录，但不阻止加载或 finally 中的清理；GameState 在通知前已更新标签。完成通知期间仍持有预留，拒绝回调重入请求，随后清空 operation、IsSwitching 及属于本实例的跨实例预留。

完成与第一帧画面显示不保证精确同步。加载同一个场景仍会重新加载；没有玩法标记限制，因此主菜单和诊断场景也能请求。返回值是本次实际 AsyncOperation，可用于观察进度；调用方不应修改 allowSceneActivation，否则仍可主动阻塞 Unity 加载。

本服务不直接写时间倍率或监听器暂停；GameState 的 Changed 订阅者会响应，AudioManager 在 Loading 时解除监听器暂停。冻结与加载的转换约束见 [GameState](../../Game/GameState/README.md)，以当前状态管理器实现为准。新请求要求服务激活；已启动操作即使原 Core 失活或销毁仍完成清理。调用方应把 AsyncOperation 当作观察对象。

## 已知缺口与核查（2026-10-06）

Unity Single 异步加载不支持取消，本服务不提供取消、排队或超时，也不会在 OnDestroy 假装取消进行中的加载。通过实际完成事件解决原先协程中断遗留 Loading/忙碌以及禁止激活后异常卡住的问题；外部调用方若主动设置 allowSceneActivation=false，仍须自行放行。静态预留仅协调本服务请求，不替其他 SceneManager 调用管理状态。

已逐一核对三个脚本与 meta，再比对默认配置、Core prefab、主菜单/进度调用、Build Settings 及测试。SceneAssetTests 检查当前注册表和场景资源；MainMenu PlayMode 流程实际切换并检查目标活动场景、IsSwitching 结束及返回菜单后的状态。新增 SceneSwitchRecoveryTests 使用只有切换服务的隔离持久物体和纯相机 SceneSwitchTarget，覆盖失活/禁用拒绝、加载中组件禁用/宿主失活/销毁、进入和退出状态回调抛异常、同实例及跨实例重入拒绝，以及后续同场景重载。测试不创建 Core、不读写玩家进度或设置。错误/重复配置仍未覆盖。新增测试的运行结果以本轮 Test Runner 报告为准。

独立文档复审通过。本轮只改本目录注释与文档；最终工作区同时包含同期 GameState/Setting 修复及新增测试，已核对交叉说明不再将修复前的状态转换缺陷写作当前行为。编译后 Console 无 error，EditMode job `ffde23500166454aad0d97b5b4ec39f2` 通过 10/10，随后 PlayMode job `cb4f5995eafe45c3aaad666f473cb1cf` 通过 14/14。这些结果属于修复前的文档核查，不作为本轮异常清理修复的验证结果。

## 问题修复验证（2026-10-06）

功能与新增回归经独立代码审查和复审通过。最终编译完成后无编译错误，依次运行 EditMode job `d44f3debefcc408abe77997773b0add8`（11/11）和 PlayMode job `1bef65db930a49189759caf3775d0762`（20/20），均已结束并通过。覆盖真实旋转位置保持与暂停恢复、EditMode 设置按钮保护，以及实际场景切换的失活/销毁/异常通知恢复。Gizmos 坐标转换仅作代码审查，不将运动测试视作绘制断言。

首次 PlayMode job `2f09ea5d698f4c1196b406167003fa30` 为 18/20：两条旋转恢复断言仅等两帧，尚未产生足够可测角度。测试改为等待 0.05 秒实际缩放时间（5 秒实时超时），并逐帧检查位置不变后通过，未放宽原行为要求。Test Runner 临时场景已清理，Enter Play Mode Options 恢复运行前设置。
