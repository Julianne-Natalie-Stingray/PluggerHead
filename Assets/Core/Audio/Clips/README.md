# 音频素材与接入

2026-10-07 核查：下列 16 个新音效均可由 ffmpeg 完整解码，48 kHz、双声道、非循环，保留源文件与 GUID。时长由 ffprobe 读取；峰值与静音检测不等同于人工听感验收。

| 素材 | 时长（秒） | 播放用途 |
| --- | --- | --- |
| `插入（实录版）.mp3` | 0.216 | PlugIn 主片段 |
| `插入1.mp3` | 0.264 | PlugIn 随机变体 |
| `插入2.mp3` | 0.240 | PlugIn 随机变体 |
| `拔出（实录版）.mp3` | 0.408 | PlugOut 主片段 |
| `拔出.mp3` | 0.144 | PlugOut 随机变体 |
| `挂点激活（或者某种机械机关）.mp3` | 0.504 | Activated：挂点放置、降压器成功激活 |
| `按键或启动（像素）.mp3` | 1.008 | GameStart：成功开始游戏 |
| `跑步-近脚.mp3` | 0.144 | GroundStepFront：普通地面前脚 |
| `跑步-远脚.mp3` | 0.120 | GroundStepBack：普通地面后脚 |
| `近脚（金属地面）.mp3` | 0.120 | MetalStepFront：金属地面前脚 |
| `远脚（金属地面）.mp3` | 0.144 | MetalStepBack：金属地面后脚 |
| `按钮按下（开）.mp3` | 0.144 | ButtonPressOpen：一般按钮按下 |
| `按钮弹回（开）.mp3` | 0.120 | ButtonReleaseOpen：一般按钮释放/提交 |
| `按钮按下（关）.mp3` | 0.072 | ButtonPressClose：继续、返回、退出按钮按下 |
| `按钮回弹（关）.mp3` | 0.120 | ButtonReleaseClose：继续、返回、退出按钮释放/提交 |
| `开关.mp3` | 0.432 | Switch：音量滑块变化 |

这些片段通过 `../SO/DefaultAudioManagerConfigs.asset` 注册到 SFX 混音组。UI 声音允许暂停时播放，脚步和交互沿用原有成功条件与冻结规则。插拔变体每次请求只选择一个片段，不叠加播放。

启动片段约有 0.28 秒前导静音，机关片段约有 0.08 秒前导静音，开关片段中间约有 0.145 秒静音；保留作者原素材，不擅自裁剪。最大检测峰值中机关片段接近 0 dBFS，不额外放大素材。

原 `click.mp3`、`Default_SFX.mp3`、`GameMainMenu.wav` 和背景音乐保持既有用途与配置；音频用途约定见 [DISCLAIMER](DISCLAIMER.md)。
