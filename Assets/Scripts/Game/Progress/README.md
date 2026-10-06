# 关卡进度与主菜单

`Scenes/MainMenuScene.unity` 是构建入口，New Game 从 `GameplayIntegration` 开始；Continue Game 重新加载最近进入的玩法关卡，从场景资源的默认状态开始。没有可加载的玩法关卡存档时 Continue 禁用。Settings 复用音量面板；Exit 在 Player 退出程序，在 Editor 停止播放。玩法暂停面板的 Main Menu 返回主菜单。

`GameProgress.Store` 读取 `Application.persistentDataPath/LevelProgress.json`，文件只包含一个整数 `level`（SceneId），不保存位置、背包、绕线、插接、换线次数或通关状态。音量偏好仍由独立的 `GameSettings.json` 管理。

Core prefab 上的 `LevelProgressTracker` 在玩法场景成功加载后立即记录关卡，包括直接启动玩法场景。主菜单和诊断场景不会覆盖进度；失败的加载请求也不会覆盖。写入先写临时文件再替换，失败保留旧文件并报告 warning；读取失败退回无进度状态。

新增关卡时同时更新 SceneId、场景资源、Build Settings 和 DefaultSceneSwitchConfigs，在配置条目勾选 `isGameplayLevel`。不要改动已有 SceneId 的整数值。New Game 的首关在 MainMenuScreen Inspector 配置。

验证：先运行 `PluggerHead.EditModeTests`，后运行 `PluggerHead.PlayModeTests`。存档测试使用独立临时目录，流程测试实际执行菜单按钮和 Single 场景切换。手工从主菜单开始游戏，移动/绕线后经设置面板返回，再点 Continue，角色与电路应恢复关卡默认状态；退出并重新启动后仍可 Continue。
