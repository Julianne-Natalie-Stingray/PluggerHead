# Setting 子系统

> **本子系统已于 2026-10-05 移交 `HeXie/` 树**：代码现在位于 `Assets/Scripts/HeXie/Game/Setting/`，所有权归 HeXie 一方，DSH 不再改动它。
> 本文件保留为**移交时的实现说明**（已逐字节核对：与 `Jill/` 下的旧版本完全相同，属纯移动）。若两者不一致，以 `HeXie/` 下的代码为准。
> 受影响的一处：`AudioManager.ApplyAudioSettings()` 读取的 `SettingBootstrap.Settings` 现在指向 `HeXie/` 里的那个类型。

## 职能

设置的数据存储与生命周期管理. 它只做"存取", 不做"应用".

- 拥有一个泛型存储基类 `SettingStore<TData>`, 负责加载, 持有, 保存与重置一份数据.
- 拥有模版的空壳数据类 `GameSettings`.
- **不负责**把设置值应用到任何系统. 应用由拥有该系统的代码完成.
- **不负责**决定何时保存. 保存是显式调用.
- 不知道 `Core` 或任何其他 Subsystem 的存在.

## 构成

| 路径 | 类型 | 职责 |
| --- | --- | --- |
| `ISettingData.cs` | 接口 | 持久化数据的契约; 由每个数据类实现 |
| `SettingStore.cs` | 泛型抽象类 | 加载, 持有, 保存, 重置; 拥有文件路径与全部文件 I/O |
| `GameSettings.cs` | `[Serializable]` 类 | 模版的设置数据; **默认没有任何字段** |
| `FileSettingStore.cs` | 具体类 | 指明持久化类型, 提供 JsonUtility 的两半实现 |
| `SettingBootstrap.cs` | 静态类 | 入口; 在场景加载前建立并加载存储 |

## 公共API

- `SettingBootstrap.Settings` -> `GameSettings`: 当前设置. 从第一个场景加载之前到进程结束均有效, 任何代码随时可读.
- `SettingBootstrap.Save()` -> `bool`: 显式持久化. 返回是否成功; 失败不抛异常.
- `SettingBootstrap.ResetToDefault()`: 仅在内存中恢复默认值, **不落盘**; 需随后调用 `Save()`.
- `SettingStore<TData>.Load()` / `.Save()` / `.ResetToDefault()`: 存储自身的方法, 供需要独立存储的 Subsystem 使用.

## 内部实现思路

### 为什么是自举而不是 Component

设置必须在**第一个场景对象 `Awake` 之前**即可读. Component 要做到这一点, 就得依赖一个必须由你手动放置在场景或预制体上的物体 —— 而模版的约定是"无需手动装配". 因此 `SettingBootstrap` 是静态类, 通过 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 由 Unity 自身调用.

**代价**: 这个对象是**隐式**的, 无法在 Hierarchy 中找到它. 如果你更看重"常驻物体肉眼可见", 这个选型需要重新考虑.

### 边界: 存储不应用

`FileSettingStore.Configure()` 是**空的**, 且这是约定而非遗漏.

原因是依赖方向: 若存储去应用音量, 它就必须认识 `AudioManager`; 若将来还要应用分辨率与语言, 它就必须认识更多系统. 那样 Setting 会变成所有系统的下游, 而它本应是所有系统的上游. 因此:

- 设置集中存储, 由**它所描述的系统自行读取**;
- 谁需要设置, 谁负责读, 谁负责把自己的改动写回 `Settings` 并调用 `Save()`.

### 为什么没有键值接口

`TData` 必须是**普通可序列化类**, 因为落盘走 `JsonUtility`, 而它无法序列化 `Dictionary` 或接口类型字段.

因此 `SettingStore` 刻意不提供 `Get<T>(string key)` 这类接口: 那会诱使数据进入字典, 而字典会被**静默地持久化为空** —— 一个只在重启后才暴露的错误. 需要动态键值对的存储, 应在自己的数据类里持久化一个可序列化的键值对列表.

### 工程里已被否决的一条路

工程引用了 `ayellowpaper.serialized-dictionary`, 但**不能**用它承载 JSON 落盘: 它的全部变更接口都包在 `#if UNITY_EDITOR` 内, 且运行时 `OnAfterDeserialize` 会清空内部列表. 能否重建字典取决于 Unity 的反序列化器是否在运行时调用该回调 —— 这一点无法在本工作区内验证, 因此不作为设计基础.

### 损坏的 JSON 如何处理

损坏是**预期情况而非异常情况**: 损坏的设置文件绝不应阻止游戏启动. 契约如下:

| 文件状态 | 结果 |
| --- | --- |
| 不存在 | 使用设计默认值, 记一条 info |
| 不可读(权限等) | 使用设计默认值, 记一条 warning |
| 完全空白 | 使用设计默认值, 记一条 warning(与"解析失败"可区分) |
| 语法损坏 | 使用设计默认值, 记一条 warning, **不抛异常** |
| 缺少部分成员 | 文件中给出的值生效, 其余取**设计默认值** |
| 只有 `{}` | 全部取设计默认值 |

实现要点: 加载**先用设计默认值铺底**, 再用 `JsonUtility.FromJsonOverwrite` 把文件覆盖其上.
这里必须用 `FromJsonOverwrite` 而不是 `FromJson`: 后者会**新建实例**, 于是文件中缺失的成员会取**类型零值**,
而把刚刚铺好的设计默认值丢掉 —— 表现为"空文件把音量重置成 0".

另一处必须记住的是: `JsonUtility.FromJson` / `FromJsonOverwrite` 对畸形载荷是**抛 `ArgumentException`**,
不是返回 null. 因此 `TryDeserialize` 内部捕获异常并返回 `false`; 若让异常逃出, 自举会被中断, 游戏将完全没有设置可用.

**粒度限制**: 上述"缺失成员取设计默认值"只保证**顶层**. 嵌套可序列化成员内部, 或存在但只填了一部分的数组元素内部,
`JsonUtility` 没有给出文档化的保证.

## 验证方式

因为 `SettingBootstrap` 是静态类, `NaughtyAttributes` 的 `Button` 在 Inspector 上没有可挂载的面, 因此**没有可点击的测试入口**. 验证途径是 `Application.quitting`(退出时自动保存)或由玩法代码显式调用 `Save()`. 这一点记在 `SettingBootstrap` 的 `TODO` 中.

不依赖 UI 的验收路径:

1. 运行一次, 确认日志出现"没有已保存的设置, 使用默认值", 并记录日志里的文件路径;
2. 修改 `SettingBootstrap.Settings.Audio` 的某个音量, 调用 `SettingBootstrap.Save()`;
3. 确认该路径下出现 JSON 文件, 且内容含改动后的值;
4. 重新运行, 确认日志不再报"没有已保存的设置", 且读到的是改动后的值;
5. 把文件内容改坏(或改成 `{}`), 重新运行, 确认游戏仍能启动, 且回落到**设计默认值**而非类型零值(记录为 warning);
6. 删除该文件, 重新运行, 确认按默认值重建并再次写出.

## TODO

- TODO: 数据版本与迁移未实现. 当前读取失败即**整体**回退设计默认值, 没有"把旧版本数据逐项升级"的路径. 未实现原因: 目前只有 `Audio` 一个成员, 改一个音量字段名不会造成值得写迁移的损失; 等出现"改名或改语义会造成用户可见损失"的字段时再引入版本方案, 过早做会是为想象中的 case 设计.
