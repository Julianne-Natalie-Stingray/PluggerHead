# 主菜单与选关大厅

`Assets/Scenes/MainMenuScene.unity` 是纯主菜单构建入口，包含 Core、相机、EventSystem 和 uGUI 菜单/设置，不含 Player、环境或传送门。点击“开始游戏”通过 SceneSwitchManager 加载独立 `Assets/Scenes/HubScene.unity`，设置打开、暂停或 Loading 时不能开始。请求被拒绝显示中文提示并保留重试。

Inspector 配置开始/设置/退出三个 Button、SettingsScreen 和 startStatus 文本。按钮持久事件分别调用 NewGame、OpenSettings、ExitGame；不引用 Player，也不提供存档续关按钮。退出在 Editor 停止播放，在 Player 退出程序。

主菜单启用后等待 Core 音频服务可用且游戏处于 Playing，再通过 `PlayBackgroundMusic(AudioId.GameMainMenu)` 请求菜单音乐；每次启用只尝试一次，拒绝时不逐帧重试。设置暂停后从原位置续播，重新启用时同 ID 复用已有声部。菜单关闭或卸载不主动停止音乐，后续环境按既有跨场景 BGM 规则接管。

HubScene 保留原大厅的 Player、环境、地板和传送门布局，配置 Core 以支持独立播放。右上角 `MenuBtn` 显示“菜单”，持久事件调用同场景 SettingsScreen.Open，打开暂停面板，可继续游戏或返回主菜单。设置面板位于 MenuBtn 上层。返回主菜单后再次开始不会重置本次运行进度。

Portal 实现 IEnvironmentInteractable，复用 Player 的 J 交互路径。从左到右对应 Level1、Level2、Level3，三门均配置真实目的地，按连续通关进度解锁。传送门要求目的关卡已配置、已解锁、构建注册有效、服务可用，且交互角色存活、未锁定并属于同一场景。失败请求保留重试。

`FinalSceneScreen.cs` 仍单独驱动结尾场景的退出按钮。进度和后续关卡接入见 [Progress](../../Game/Script/Progress/README.md)。

验证覆盖纯主菜单、开始加载大厅、失败重试、MenuBtn 暂停继续和返回主菜单、正常 Player 交互进入关卡、通关事件解锁、返回与重玩、未解锁传送门拒绝进入，以及每次运行重置。菜单测试在自有运行时场景构造有效电路，不将 Level0 当前关卡设计作为菜单逻辑的验收条件。

## 字体与交互音效

场景文本、GlobalUI 与 Portal 使用 Shared/Fonts 下的 Fusion Pixel 简体中文静态 TMP 字体，TMP 默认字体同步设置；字体许可保留在 FusionPixel-LICENSE-OFL.txt。

SelectableAudio 通过 Core 的 SFX 总线播放按钮按下/释放与滑块变化音效。关闭、继续和退出按钮使用“关”音色，其余按钮使用“开”音色；滑块使用开关音效，并以非缩放时间限制连续播放频率。初始化音量仍使用 SetValueWithoutNotify，不触发音效。暂停面板的反馈允许在冻结时播放；不可交互控件、Loading 和非左键按下不播放。缺少音频服务时保持静音，不影响 UI 功能。
