# 音频玩家设置数据

本目录定义可序列化的音量数据；存取文件由 [Game/Setting](../../../Game/Setting/README.md) 负责，实际混音器应用由 AudioManager 负责。

## 文件与默认值

| 文件 | 当前行为 |
| --- | --- |
| `AudioSettings.cs` | 普通 `[Serializable]` 类，公开 MasterVolume、OstVolume、SfxVolume 三个可写属性，背后对应私有序列化字段。不是组件或 ScriptableObject。 |
| `AudioSettings.cs.meta` | 保留脚本 GUID；没有默认资源引用或特殊执行顺序。 |

`AudioSettings.Default()` 每次创建独立对象，三个值依次为 **1、0.5、0.5**。直接 `new AudioSettings()` 的字段则全为 0。GameSettings.ResetToDefault 使用 Default 并替换整个 Audio 对象；调用方缓存的旧 AudioSettings 引用不会自动指向重置后的对象，应重新取得 `SettingBootstrap.Settings.Audio`。

## 写入、保存与应用

三个属性采用相同规则：NaN 记录 Warning（Action.Ignore）并保留该总线原值；0..1（含端点）直接赋值；有限越界值与正负 Infinity 记录 Warning 后交给 Mathf.Clamp01，分别钳制到 0 或 1。修改一个属性不会改变其他总线，不会发布事件、保存文件或应用混音器。

运行时调用方应传入有限数值；setter 的 NaN 防护不会修复绕过属性写入的无效字段。序列化直接写 `masterVolume`、`ostVolume`、`sfxVolume` 字段，不经过这些 setter；FileSettingStore 在读盘后另行检查 Audio 非空及各值有限且处于 0..1，失败由存储层回退默认值。该读盘检查不等于 Save 前也执行相同校验。

AudioManager.Start 读取设置并应用混音器；SettingsScreen 在保存成功后也显式调用现有 Core.Audio.ApplyAudioSettings。直接改属性、调用 SettingBootstrap.Save 或 ResetToDefault 的其他调用方，需要按自身业务时机显式应用音量。存储层本身没有音频服务依赖。

音量为线性值，AudioManager 使用 `20 * log10(value)` 写入配置指定的混音器参数；小于等于 0.0001 时使用 -80 dB。这些转换与参数名属于消费端，不在数据类中执行。

## 核查与验证（2026-10-06）

修复前的独立内存探针向 `AudioSettings.Default()` 的 MasterVolume 写入 `float.NaN`，读回 `float.IsNaN(...) == true`，确认仅调用 Clamp01 不能拒绝 NaN。本轮在三条 setter 中增加 NaN 拒绝分支，保留原值并准确记录 Ignore；其余输入和默认值语义保持不变。

新增 EditMode 用例 `AudioSettings_RejectNaNClampOtherInputsAndKeepIndependentDefaults`，通过 `SettingsIntegrationChecks.CheckAudioSetters` 使用独立内存对象，覆盖三路 NaN 保留、其他路不变、0/1 边界、有限越界、正负 Infinity、有效区间值、直接构造零值及 Default 对象独立性。不访问玩家设置、Bootstrap 或混音器。新增测试的实际运行结果由本轮主任务收尾记录，下方结果为修复前历史快照。

### 修复前文档核查

逐一检查源码、脚本 meta 和目录 meta，并核对 GameSettings、FileSettingStore、SettingStore、SettingsScreen、AudioManager 与设置测试。原脚本没有需要同步修改的说明，本轮仅新增目录文档及其 meta。

最近完成的回归为 EditMode 10/10、PlayMode 14/14（job：`ffde23500166454aad0d97b5b4ec39f2`、`cb4f5995eafe45c3aaad666f473cb1cf`）。其中设置检查覆盖 JSON 非有限/越界输入、有效边界及保存失败；主菜单检查覆盖 UI 保存后的实际混音器值。没有直接覆盖 setter 对 NaN 的处理、直接构造零值或 Default 对象独立性；这些不宣称已做运行时验证。该结果对应当时的代码快照，不覆盖其后并行新增的测试和修复。本目录纯文档变更无需重跑 Unity 测试。

### 本轮修复验证

独立代码审查通过，编译后无编译错误。按顺序完成 EditMode job `c3468d5a72d04450a61b07a208126637`（12/12）和 PlayMode job `b7dd30129c8d42ff9d559c8657bee76f`（20/20），均已结束并通过。新增 setter 回归验证三路 NaN 不再污染原音量，其他有效输入与钳制行为保持原契约。
