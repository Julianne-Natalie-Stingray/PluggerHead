# 关卡进度与主菜单

`Scenes/MainMenuScene.unity` 是构建入口，New Game 从 `GameplayIntegration` 开始；Continue Game 重新加载最近进入的玩法关卡，从场景资源的默认状态开始。没有可加载的玩法关卡存档时 Continue 禁用。Settings 复用音量面板；Exit 在 Player 退出程序，在 Editor 停止播放。玩法暂停面板的 Main Menu 返回主菜单。

`GameProgress.Store` 读取 `Application.persistentDataPath/LevelProgress.json`，文件只包含一个整数 `level`（SceneId），不保存位置、背包、绕线、插接、换线次数或通关状态。音量偏好仍由独立的 `GameSettings.json` 管理。

Core prefab 上的 `LevelProgressTracker` 在 `Start` 订阅 `sceneLoaded` 并检查当前活动场景，随后在加载回调中记录配置标记为玩法关卡的场景。因此直接启动玩法场景也会记录；Additive 加载的玩法场景同样会记录，不要求它成为活动场景。默认配置中的主菜单和诊断场景不会覆盖进度；被拒绝、未实际加载场景的请求也不会覆盖。

写入先写同路径的 `.tmp` 文件，再替换已有文件或移动为新文件；成功后才更新内存。捕获到 I/O、权限或参数异常时返回 `false` 并报告 warning，通常保留旧文件和内存；这不是跨平台、断电情况下的数据持久性保证。失败后可能遗留 `.tmp`，下次保存会尝试覆盖它。读取时先清空内存，文件缺失、空白、缺少 `level` 或捕获到读取/解析异常时没有有效进度；未知枚举值也不能通过查询。

## 文件职责与调用边界

| 文件 | 当前行为与接入约束 |
| --- | --- |
| `GameProgress.cs` | 首次访问 `Store` 时创建并读取存储；`SubsystemRegistration` 清空静态缓存。每帧访问不会重新读盘，外部修改文件后需显式 `Reload()`。 |
| `LevelProgressStore.cs` | `TryGetLevel` 只检查非负且属于 `SceneId`，不检查是否为玩法关卡或是否可加载。`SaveLevel` 本身不校验枚举与关卡类型，调用者负责筛选；返回值表示本次写入结果。 |
| `LevelProgressTracker.cs` | 依赖 Inspector 中的 `SceneSwitchConfigs`，配置为空时不记录。`Start` 订阅、`OnDestroy` 退订；仅禁用组件不会退订。保存失败只有存储层 warning，不重试，也不阻止已加载的关卡继续运行。 |
| `README.md` | 本目录总览；菜单控制器位于 `../MainMenu/MainMenuScreen.cs`，场景切换与玩法白名单位于 `../../Core/SceneSwitch/`。 |

Continue 还需由 `MainMenuScreen.CanLoad` 检查玩法标记、场景名映射及 Build Settings 注册；按钮还要求 Core 就绪、没有正在进行的切换且设置面板关闭。因此 `TryGetLevel` 返回 `true` 不等于 Continue 可用。New Game 不预先删除旧存档，首关实际加载且保存成功后才覆盖它。

新增关卡时同时更新 SceneId、场景资源、Build Settings 和 DefaultSceneSwitchConfigs，在配置条目勾选 `isGameplayLevel`。不要改动已有 SceneId 的整数值。New Game 的首关在 MainMenuScreen Inspector 配置。

验证：先运行 `PluggerHead.EditModeTests`，后运行 `PluggerHead.PlayModeTests`。存档测试使用独立临时目录，流程测试实际执行菜单按钮和 Single 场景切换。手工从主菜单开始游戏，移动/绕线后经设置面板返回，再点 Continue，角色与电路应恢复关卡默认状态；退出并重新启动后仍可 Continue。

## 文档核查记录（2026-10-06）

已分别检查本目录三个脚本、README 及对应 `.meta`，再核对菜单控制器、切换配置/管理器、Core prefab、主菜单场景与 Build Settings。Tracker 的脚本 GUID 与 Core 引用一致，Tracker、切换服务和菜单使用同一默认配置，首关为枚举值 3。

静态检查未发现默认主菜单与关卡保存流程的确定性缺陷；上面的无重试、Additive 记录和查询分层是当前边界。已有 `LevelProgressTests` / `MainMenuTests` 分别包装 `MainMenuIntegrationChecks.CheckProgressStorage` / `CheckMenuFlow`，覆盖基础存储、失败保留旧文件及默认状态续关；未覆盖 Additive 记录、禁用 Tracker 或所有文件系统故障。本次只修正文档，未运行 Unity Test Runner，不将测试源码存在视为运行通过。
