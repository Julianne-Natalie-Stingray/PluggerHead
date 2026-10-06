# 玩家设置与暂停面板

本目录保存玩家偏好并提供暂停设置面板。存储层负责读写，`SettingsScreen` 负责 UI 和暂停/返回菜单；实际混音器音量由 AudioManager 应用。关卡进度在独立的 [Progress](../Progress/README.md) 目录，不写入设置文件。

## 逐文件职责

| 文件 | 当前行为与边界 |
| --- | --- |
| `ISettingData.cs` | 定义 ResetToDefault，泛型存储在初始化、重置和解析失败时通过此契约恢复默认值。 |
| `SettingStore.cs` | 泛型基类，持有 Data/FilePath，负责文件 I/O、加载/保存/重置顺序及日志；序列化和 Configure 由子类实现。 |
| `GameSettings.cs` | 序列化 audio 字段，对外只读暴露 Audio；ResetToDefault 新建 AudioSettings 默认对象。直接 new 后尚需重置才能取得 Audio。 |
| `FileSettingStore.cs` | 选用 JsonUtility.ToJson 与 FromJsonOverwrite；捕获解析异常返回 false，Configure 为空，不应用音量。 |
| `SettingBootstrap.cs` | BeforeSceneLoad 创建并 Load 一份存储，暴露 Settings、Save、ResetToDefault；退出时尝试保存，初始化时先退订再订阅退出回调。 |
| `SettingsScreen.cs` | 读取音量到三个滑块，显式保存，管理自己的暂停所有权及返回主菜单。 |
| `README.md` | 本目录总览及行为核查记录；对应 .meta 仅维护 Unity 资源标识。 |

六个脚本和 README 的 `.meta` 成对，脚本无 Inspector 默认引用或自定义执行顺序。SettingsScreen 的 GUID `a44e8a1743f82ae41a79f953c0d46fad` 被 MainMenuScene 和 GameplayIntegration 引用。

## 数据和生命周期

运行时文件为 `Application.persistentDataPath/GameSettings.json`，路径由数据类型名决定。当前 JSON 形态为 `{"audio":{"masterVolume":1.0,"ostVolume":0.5,"sfxVolume":0.5}}`，实际输出有缩进；设计默认值定义在 `Scripts/Core/Audio/Setting/AudioSettings.cs`。

Settings 直接返回 `store.Data`，没有延迟初始化或空值保护；应在 BeforeSceneLoad 初始化结束后使用，例如场景 Awake/Start。EditMode 和其他更早或同阶段回调不能假定可读。独立创建 FileSettingStore 后，Data 和 FilePath 在 Load 前为 null；先 Load 再操作。每次 Load 都新建数据实例，旧引用不会跟着替换；ResetToDefault 保留顶层实例但替换 Audio。

修改内存不立即落盘。`Save()` 返回是否成功；`ResetToDefault()` 只重置内存，不立即保存，也不应用实际音量。退出钩子会尝试保存当时的内存值（包括重置后的值），但忽略返回值，仅保留存储层错误日志。

## 读取和保存

Load 先 new 数据并 ResetToDefault，再读取文件并覆盖默认实例，最后调用 Configure。

| 输入/操作 | 实际处理 |
| --- | --- |
| 文件不存在 | 使用默认值并记录 Info；Load 本身不会创建文件。 |
| 读取抛异常或文件空白 | 保留默认值并记录 Warning。 |
| JsonUtility 抛异常 | TryDeserialize 返回 false；清除可能发生的部分覆盖，整体重置并记录 Warning。 |
| `{}` 或缺少顶层字段且解析成功 | 保留未覆盖的默认值，不因缺字段记录 Warning。 |
| 语法合法但数值不合理 | 没有业务校验；不保证回到默认值。 |
| Save | 先序列化，再直接 WriteAllText 覆盖；异常转为 false 和 Error 日志，成功返回 true 和 Info 日志。 |

这里没有版本迁移、备份、临时文件替换、自动建目录或写失败后的磁盘回滚。UI 在失败时回滚内存不能保证旧文件仍完整。不要把关卡进度存储的临时文件方案套用到本目录。

覆盖反序列化直接写序列化字段，不走 AudioSettings 属性 setter；setter 对普通输入的范围限制不能替代读盘校验。嵌套字段缺失、null、越界数值应按实际 Unity 版本验证，不能笼统声称“所有坏文件都会恢复默认”。泛型基类也不捕获子类 ResetToDefault、TryDeserialize 或 Configure 抛出的异常；当前 FileSettingStore 自行捕获解析异常。

数据采用具体可序列化字段。当前设置没有 Dictionary；旧文档称 SerializedDictionary 的运行时变更接口全部处于 UNITY_EDITOR 内是错误的，仓库包有运行时字典接口及序列化回调。未来采用该包仍需对目标数据做 JSON 往返验证，当前代码没有承诺支持任意字典。

## 暂停面板与实际音量

MainMenuScene 和 GameplayIntegration 都包含初始 inactive 的 SettingsScreen。Inspector 需配置三条音量 Slider（当前范围 0–1）、返回主菜单 Button 和保存状态 TMP 文本。GameplayIntegration 的 MenuButton 持久事件直接调用 Open；主菜单通过 MainMenuScreen.OpenSettings 间接调用 Open。面板按钮和滑块连接 ContinueGame/SaveSettings/ReturnToMainMenu/OnVolumeChanged。

Open 在对象 activeSelf 已为 true 或全局 Loading 时直接返回，否则校验引用，使用 SetValueWithoutNotify 填滑块并清空状态文本。只有从 Playing 打开时取得暂停所有权。关闭面板只释放自己取得的暂停；原先已 Freezed 时打开不会擅自恢复别人的暂停。主菜单场景禁用返回主菜单按钮，但公开 ReturnToMainMenu 方法没有同名场景保护，外部调用仍应遵循 UI 约束。

SaveSettings 只在面板 activeInHierarchy 且引用齐全时执行：先把滑块写入设置，再 Save。成功显示提示；失败恢复先前内存音量并显示失败提示。移动滑块只清空提示，关闭未保存的面板会丢弃滑块修改。返回主菜单先关闭面板释放暂停，再请求切换；请求被拒时重新打开并恢复本面板原有的暂停。

**当前功能缺口：保存成功不会立即改变实际音量。** FileSettingStore.Configure 为空，SettingsScreen 也不调用 `CoreFacade.Instance.Audio.ApplyAudioSettings()`。AudioManager 只在 Start 自动应用一次，且 Core 跨场景保留，所以仅切场景不保证应用新值。需要即时生效的调用方必须显式调用该方法；重新启动会在 AudioManager.Start 应用读盘值。本次核查记录该缺口，不修改产品行为。

## 核查与验证（2026-10-06）

逐一检查六个脚本、README 及各自 meta 后，核对 AudioSettings/AudioManager、两场景引用与默认 inactive 状态、MainMenuScreen 和测试实现，再修订本总文档。纠正了“空数据类”“没有可点击入口”“任意时刻可读”“缺字段一定记警告”等陈旧结论，并同步修正双语 XML 注释。

现有 MainMenuTests 只覆盖面板打开/关闭、暂停恢复和返回菜单，没有调用 SaveSettings；它不能证明 JSON 健壮性、写失败回滚或实际音量生效。手工检查保存功能时应先备份个人设置：打开面板修改并保存、重开后确认滑块值、重启确认读盘；即时混音器应用另行验证。不要通过破坏个人设置文件验证错误分支，应使用隔离路径或内存对象。

本次 Unity 2022.3.43f1c1 内存探针使用真实 FileSettingStore.TryDeserialize：`{}`、`{"audio":{}}`、`{"audio":null}` 均解析成功并保留 1/0.5/0.5；`{"audio":{"masterVolume":2,"ostVolume":-1}}` 也返回成功，实际读到 2/-1/0.5，确认越界读盘未被校验；`{broken` 返回 false。这些结果仅覆盖所列载荷，不是所有目标平台或 JSON 输入的保证。

另用独立 FileSettingStore 及临时文件调用真实 Save/LoadInto，确认 MasterVolume=0.3 保存成功并读回（JSON 中有浮点表示误差）；已删除探针文件，没有改动玩家真实设置或 Bootstrap 存储。此检查覆盖成功 I/O，不覆盖磁盘写入中断或权限失败。

本次仅修改文档与 XML 注释，独立复审通过；最终脚本编译后 Console 无 error，依次运行 EditMode job `09479b2f716e406c8c0fbb3e3922d8d8`（7/7 通过）与 PlayMode job `c8bff04aa225440c9c3ee3fddb483899`（14/14 通过）。集成回归通过不代表前述未覆盖的设置缺口已修复。
