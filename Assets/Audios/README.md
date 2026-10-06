# 音频资源

逐文件核查日期：2026-10-06。这里保存原始 clip、单条播放数据及 Mixer；Manager 容量和注册表配置位于 `SO/Audio/`。运行时契约见 [Core/Audio](../Scripts/Core/Audio/README.md)。

## 文件与配置

| 文件 | 当前配置 |
| --- | --- |
| `SO/GenericOst.asset` | AudioId 1，引用开发音乐，路由 OST；循环、默认忽略监听器暂停、最多 1 声部；音量/音高均 1，淡入/淡出 0.15/0.3 秒。 |
| `SO/GenericSfx.asset` | AudioId 0，引用 Default_SFX，路由 SFX；不循环、不忽略暂停、最多 5 声部；音量/音高及渐变同上。 |
| `SO/MouseClick.asset` | AudioId 2，引用 click，路由 SFX；不循环、最多 10 声部、音量/音高均 1。YAML 未写默认冻结与渐变字段，当前源码默认分别为 false、0、0。 |
| `Mixers/Master.mixer` | Master 下有 OST/SFX，两者无子组；三组均含 Attenuation，未 mute/solo/bypass。暴露 MasterVolume/OstVolume/SfxVolume，与默认 Manager 配置对应。 |
| `Clips/` | 三个 MP3 及用途说明已逐项核查，详见[文件清单](Clips/README.md)。 |

三个 AudioClipData 都是 `spatialBlend=0` 的 2D 声音，距离字段为 1/20；这些值不表示当前有空间距离衰减。Mixer 只有一个起始 Snapshot，显式保存的浮点项为 SfxVolume=0，不能据此称三个音量初值均显式写入。运行时 AudioManager 应用玩家设置后还会改变总线音量。

已核对三个 clip GUID、AudioClipData 脚本、Mixer 分组 fileID、暴露参数及默认注册表引用。资源存在不代表会自动播放：MouseClick 并未因此自动接入所有 UI 按钮。OST 忽略监听器暂停也不代表冻结期间新请求自动被允许，仍需相应请求选项；循环声占满其单声部上限时不被普通同 ID 请求抢占。

素材使用约定见 [Clips/DISCLAIMER](Clips/DISCLAIMER.md)。当前引用链仍连接到构建场景，未配置公开版本自动排除。静态引用检查不等于听感、授权或实际构建验证。本轮只更新说明，保留全部资源 GUID 与配置。
