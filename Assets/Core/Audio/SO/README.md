# 默认音频管理器配置

`DefaultAudioManagerConfigs.asset` 的注册顺序为 GenericOst、GenericSfx、MouseClick、GameMainMenu，分别指向 `Audios/SO/` 的数据资源；Mixer 指向 `Audios/Mixers/Master.mixer`。GameMainMenu 使用独立 AudioId 3，原有标识及资源引用保持不变。

| 配置 | 当前值 |
| --- | --- |
| 暴露参数 | MasterVolume、OstVolume、SfxVolume，与 Mixer 一致 |
| collectionCheck | true |
| defaultCapacity / prewarmAmount | 10 / 10 |
| maxPoolSize / maxSoundInstance | 30 / 30 |

容量、预热量、全局声部上限是不同概念；预热不是播放请求，也不等于已经播放十个声音。每个 AudioId 还有各自上限，详见[音频资源](../../Audios/README.md)及[管理器契约](../../Scripts/Core/Managers/README.md)。

资产脚本、三个数据引用、Mixer 和 meta GUID 已核对。Core prefab 使用此配置；玩家音量存档不写入本资产，而由 GameSettings 加载后应用到 Mixer。本轮只新增说明，未修改配置或重复运行 Unity 测试。
