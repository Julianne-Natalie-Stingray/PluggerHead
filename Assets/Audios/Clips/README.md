# 开发音频文件

逐文件核查日期：2026-10-06。用途约定及授权证据边界见 [DISCLAIMER](DISCLAIMER.md)。本轮保留原素材与 GUID。

| 文件 | 源文件信息（ffprobe） | 当前引用 |
| --- | --- | --- |
| `上海アリス幻樂団 - 運命のダークサイド.mp3` | MP3，44.1 kHz，双声道，约 228.623 秒；标签记录同名标题与作者，并带封面图流。标签不构成授权证明。 | `Audios/SO/GenericOst.asset` |
| `Default_SFX.mp3` | MP3，48 kHz，单声道，约 1.248 秒。 | `Audios/SO/GenericSfx.asset` |
| `click.mp3` | MP3，44.1 kHz，双声道，约 0.392 秒。 | `Audios/SO/MouseClick.asset` |
| `DISCLAIMER.md` | 记录学习/开发用途约定；已区分用途承诺与当前引用事实。 | 人工阅读，不参与构建过滤。 |

三个 AudioImporter 的 meta 设置相同：无平台覆盖、未强制单声道、未后台加载、关闭 preloadAudioData；源采样率不同，不应将 `sampleRateOverride: 44100` 字段单独解释为所有素材已被强制重采样。实际播放参数由 AudioClipData 与请求决定，见[音频资源总览](../README.md)。

本轮检查文件格式、时长、导入配置及引用，不评价听感，不证明所有平台导入或发布包内容。仅更新文档，无需重跑 Unity 测试。
