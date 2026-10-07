# 主菜单与选关大厅

`Assets/Scenes/MainMenuScene.unity` 是构建入口，包含 Core、Player、环境、相机、三个传送门和 uGUI 菜单。MainMenuCanvas 默认启用；MainMenuScreen.OnEnable 锁定序列化引用的 Player。点击“开始游戏”后隐藏该 Canvas 并解锁角色，仍留在大厅。设置面板打开或场景切换期间不能开始。返回大厅会重新显示菜单，但不重置本次运行的通关进度。

Inspector 配置 Player、开始/设置/退出三个 Button 和 SettingsScreen。按钮持久事件分别调用 NewGame、OpenSettings、ExitGame；不再提供存档续关按钮。设置沿用现有音量保存和暂停逻辑。退出在 Editor 停止播放，在 Player 退出程序。

Portal 实现 IEnvironmentInteractable，复用 Player 的 J 交互路径。从左到右为第一、二、三关。第一关进入 Level0；第二、三关尚未制作，显示“尚未开放”，即使进度解锁也不能进入。传送门要求目的关卡已配置、已解锁、构建注册有效、服务可用，且交互角色存活、未锁定并属于同一场景。失败请求保留重试。

`FinalSceneScreen.cs` 仍单独驱动结尾场景的退出按钮。进度和后续关卡接入见 [Progress](../Progress/README.md)。

验证覆盖初始角色锁、设置暂停、原地开始、正常 Player 交互进入 Level0、通关事件解锁、返回与重玩、未实现传送门拒绝进入，以及每次运行重置。菜单测试在自有运行时场景构造有效电路，不将 Level0 当前关卡设计作为菜单逻辑的验收条件。
