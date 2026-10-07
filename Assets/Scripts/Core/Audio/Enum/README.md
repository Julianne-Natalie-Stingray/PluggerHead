# 音频标识

本目录的 `AudioId.cs` 定义项目音频请求使用的枚举；对应 meta 保留脚本 GUID，没有默认资源引用。枚举不是音频资产 GUID，也不负责加载音频或保证配置条目唯一。

## 当前映射

| 枚举 | 当前整数值 | 默认配置引用的 AudioClipData |
| --- | --- | --- |
| DefaultSfx | 0 | `Assets/Audios/SO/GenericSfx.asset` |
| DefaultOst | 1 | `Assets/Audios/SO/GenericOst.asset` |
| MouseClick | 2 | `Assets/Audios/SO/MouseClick.asset` |
| None | -1 | 可选动画音效的空配置；由 PlayerAnimationAudio 拦截，不提交播放请求。 |

源码使用隐式递增值。上述资产序列化的是整数；重排枚举或在中间插入成员会改变已有值的含义。新增标识应保留已有数值，并同步 AudioClipData 和 `Assets/SO/Audio/DefaultAudioManagerConfigs.asset` 的 audios 列表。源码中的自定义 Identifier 包 TODO 只是未来设想，当前不存在这套实现。

## 查询边界

AudioBuilder.Play 把标识交给 AudioManager，经 AudioManagerConfigs.TryGetClip 查找首个非空且 AudioId 匹配的条目。多个资产可以填同一个枚举值，编译器不会检测该冲突；配置的 Remove Duplicates 按钮才会手动移除空项及重复 ID，保留首项。

未配置的标识（包括显式强转得到的未知整数）不会自动回退到 DefaultSfx：查找失败记录错误，正常管理器播放入口返回 null。AudioEmitter 在尚无 data 时的 AudioId 查询属性确实返回 DefaultSfx，但那只是该属性的默认返回值，不是播放请求的回退策略。AudioRegistry 按此标识统计声部与选择同类抢占候选；不同标识可引用同一实际 AudioClip，不会因此合并计数。

## 核查与验证（2026-10-06）

逐一检查枚举及 meta，核对三个 AudioClipData 资产的整数、默认配置引用，以及 Builder、Manager、Emitter、Registry 消费路径。三个现有枚举值与默认配置一致；未修改枚举数值或资源 GUID。

本轮只新增文档及其 meta，未运行额外 Unity 测试。最近音频所在 PlayMode 程序集结果为 14/14（job `cb4f5995eafe45c3aaad666f473cb1cf`），它不等于专门验证了枚举重排、未知整数或重复配置，也不覆盖其后并行实现改动。
