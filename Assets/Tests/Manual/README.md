# 手工测试脚本

`DebugScript.cs` 用于手工验证设置：Start 打印三路音量，Inspector 按钮随机修改内存音量或恢复默认设置。脚本及 `.meta` 从 `Scripts/Debug` 一起迁入，GUID、类型名和预定义 `Assembly-CSharp` 编译归属保持不变。

在 Play Mode 且 SettingBootstrap 初始化后使用。按钮不立即保存，也不调用 AudioManager.ApplyAudioSettings；退出钩子可能将临时值写入个人设置文件。它不是隔离的 Test Runner 用例。设置存储行为见 [Setting](../../Scripts/Game/Setting/README.md)，自动化入口见 [DebugSettingsTests](../EditMode/DebugSettingsTests.cs)。
