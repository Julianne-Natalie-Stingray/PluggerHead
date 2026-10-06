# 音频玩家设置数据

本目录定义可序列化的音量数据；存取文件由 [Game/Setting](../../../Game/Setting/README.md) 负责，实际混音器应用由 AudioManager 负责。

## 文件与默认值

| 文件 | 当前行为 |
| --- | --- |
| `AudioSettings.cs` | 普通 `[Serializable]` 类，公开 MasterVolume、OstVolume、SfxVolume 三个可写属性，背后对应私有序列化字段。不是组件或 ScriptableObject。 |
| `AudioSettings.cs.meta` | 保留脚本 GUID；没有默认资源引用或特殊执行顺序。 |

`AudioSettings.Default()` 每次创建独立对象，三个值依次为 **1、0.5、0.5**。直接 `new AudioSettings()` 的字段则全为 0。GameSettings.ResetToDefault 使用 Default 并替换整个 Audio 对象；调用方缓存的旧 AudioSettings 引用不会自动指向重置后的对象，应重新取得 `SettingBootstrap.Settings.Audio`。

## 写入、保存与应用

三个属性采用相同规则：0..1（含端点）直接赋值；其他输入记录 Warning 后交给 Mathf.Clamp01。修改一个属性不会改变其他总线，不会发布事件、保存文件或应用混音器。

运行时调用方应传入有限数值。当前 setter 没有单独的 NaN 检查，不能把范围钳制当作完整的数值有效性保证。序列化直接写 `masterVolume`、`ostVolume`、`sfxVolume` 字段，不经过这些 setter；FileSettingStore 在读盘后另行检查 Audio 非空及各值有限且处于 0..1，失败由存储层回退默认值。该读盘检查不等于 Save 前也执行相同校验。

AudioManager.Start 读取设置并应用混音器；SettingsScreen 在保存成功后也显式调用现有 Core.Audio.ApplyAudioSettings。直接改属性、调用 SettingBootstrap.Save 或 ResetToDefault 的其他调用方，需要按自身业务时机显式应用音量。存储层本身没有音频服务依赖。

音量为线性值，AudioManager 使用 `20 * log10(value)` 写入配置指定的混音器参数；小于等于 0.0001 时使用 -80 dB。这些转换与参数名属于消费端，不在数据类中执行。

## 核查与验证（2026-10-06）

逐一检查源码、脚本 meta 和目录 meta，并核对 GameSettings、FileSettingStore、SettingStore、SettingsScreen、AudioManager 与设置测试。原脚本没有需要同步修改的说明，本轮仅新增目录文档及其 meta。

最近完成的回归为 EditMode 10/10、PlayMode 14/14（job：`ffde23500166454aad0d97b5b4ec39f2`、`cb4f5995eafe45c3aaad666f473cb1cf`）。其中设置检查覆盖 JSON 非有限/越界输入、有效边界及保存失败；主菜单检查覆盖 UI 保存后的实际混音器值。没有直接覆盖 setter 对 NaN 的处理、直接构造零值或 Default 对象独立性；这些不宣称已做运行时验证。该结果对应当时的代码快照，不覆盖其后并行新增的测试和修复。本目录纯文档变更无需重跑 Unity 测试。
