# 音频标识

本目录的 `AudioId.cs` 定义项目音频请求使用的枚举；对应 meta 保留脚本 GUID，没有默认资源引用。枚举不是音频资产 GUID，也不负责加载音频或保证配置条目唯一。

## 当前映射

| 枚举 | 当前整数值 | 默认配置引用的 AudioClipData |
| --- | --- | --- |
| DefaultSfx | 0 | `Assets/Core/Audio/SO/GenericSfx.asset` |
| DefaultOst | 1 | `Assets/Core/Audio/SO/GenericOst.asset` |
| MouseClick | 2 | `Assets/Core/Audio/SO/MouseClick.asset` |
| GameMainMenu | 3 | `Assets/Core/Audio/SO/GameMainMenu.asset` |
| PlugIn / PlugOut | 4 / 5 | `Core/Audio/SO/plugin.asset` / `plugout.asset` |
| GameStart / Activated | 6 / 7 | `Core/Audio/SO/gamestart.asset` / `activated.asset` |
| GroundStepFront / GroundStepBack | 8 / 9 | 普通地面前后脚 SO |
| MetalStepFront / MetalStepBack | 10 / 11 | 金属地面前后脚 SO |
| ButtonPressOpen / ButtonReleaseOpen | 12 / 13 | 一般按钮按下/释放 SO |
| ButtonPressClose / ButtonReleaseClose | 14 / 15 | 关闭按钮按下/释放 SO |
| Switch | 16 | 音量滑块变化 SO |
| None | -1 | 可选动画音效的空配置；由 PlayerAnimationAudio 拦截，不提交播放请求。 |

源码显式固定整数值，保护已序列化的 AudioId。新增标识应保留已有数值，并同步 AudioClipData 和 `Assets/Core/Audio/SO/DefaultAudioManagerConfigs.asset` 的 audios 列表。源码中的自定义 Identifier 包 TODO 只是未来设想，当前不存在这套实现。

## 查询边界

上述十七个非 None 标识均已注册于 `Assets/Core/Audio/SO/DefaultAudioManagerConfigs.asset`，资源按功能迁移到 `Assets/Core/Audio/`。真实主/地线接入播放 PlugIn，独立拾起地线和收回 Anchor 播放 PlugOut；放置 Anchor、降压器首次接入播放 Activated，重复接入包括跨线不重复发声，重开后可再次激活。主菜单成功请求进入大厅时播放 GameStart，失败或重复请求不播放。成功交互音在通关冻结前发出并允许该声部继续；上述玩法交互请求在暂停中不发声；UI 按钮和滑块反馈允许在暂停中播放。

AudioBuilder.Play 把标识交给 AudioManager，经 AudioManagerConfigs.TryGetClip 查找首个非空且 AudioId 匹配的条目。多个资产可以填同一个枚举值，编译器不会检测该冲突；配置的 Remove Duplicates 按钮才会手动移除空项及重复 ID，保留首项。

未配置的标识（包括显式强转得到的未知整数）不会自动回退到 DefaultSfx：查找失败记录错误，正常管理器播放入口返回 null。AudioEmitter 在尚无 data 时的 AudioId 查询属性确实返回 DefaultSfx，但那只是该属性的默认返回值，不是播放请求的回退策略。AudioRegistry 按此标识统计声部与选择同类抢占候选；不同标识可引用同一实际 AudioClip，不会因此合并计数。

## 核查与验证（2026-10-06）

逐一检查枚举及 meta，核对三个 AudioClipData 资产的整数、默认配置引用，以及 Builder、Manager、Emitter、Registry 消费路径。三个现有枚举值与默认配置一致；未修改枚举数值或资源 GUID。

本轮只新增文档及其 meta，未运行额外 Unity 测试。最近音频所在 PlayMode 程序集结果为 14/14（job `cb4f5995eafe45c3aaad666f473cb1cf`），它不等于专门验证了枚举重排、未知整数或重复配置，也不覆盖其后并行实现改动。
