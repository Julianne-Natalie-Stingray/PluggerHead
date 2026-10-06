# 输入动作源与生成包装

本目录维护 Input System 的输入定义和生成的 C# 包装。设备输入由 `Scripts/Core/Input/InputManager.cs` 管理生命周期并向玩法层转发；本目录不决定拾取、绕线、暂停等业务行为。

## 逐文件说明

| 文件 | 职责与修改方式 |
| --- | --- |
| `PlayerControls.inputactions` | 输入动作源 JSON，包含一个 Gameplay map、六个 action；输入变更先协商，再修改此源资产。 |
| `PlayerControls.inputactions.meta` | InputActionImporter 配置，启用 Generate C#，类名 PlayerControls，输出路径和命名空间为空（默认同目录、全局命名空间）。保留 GUID。 |
| `PlayerControls.cs` | Input System 1.7.0 自动生成；构造时从内嵌 JSON 创建 InputActionAsset，提供动作访问、启用/禁用、回调配对及 Dispose；不要手改。 |
| `PlayerControls.cs.meta` | 生成脚本 GUID，无默认引用和执行顺序覆盖。 |
| `README.md` / `.meta` | 本目录总文档与资源标识。 |

## 当前绑定

| Action | 类型 / 值 | 绑定 |
| --- | --- | --- |
| PrimaryPress | Button | 鼠标左键 |
| PointerPosition | PassThrough / Vector2 | 鼠标位置（屏幕坐标） |
| Up | Button | Space |
| MovementInput | Value / Vector2 | 2DVector 的 left=A、right=D；up/down 路径为空，当前只输出横向输入 |
| SecondaryPress | Button | J |
| TertiaryPress | Button | K |

没有 controlSchemes、手柄或触控绑定，没有自定义 interactions/processors。MovementInput 的 initialStateCheck 为 true，其余为 false。名称 Up 表示独立按钮，并未绑定到 MovementInput 的 Y 轴。

InputManager 在 Awake 创建包装，OnEnable 订阅并启用 Gameplay，OnDisable 退订并禁用，OnDestroy Dispose。公开位置/移动向量、按住状态及按钮事件，不公开完整 controls。玩家组件解释 J/K 等含义；暂停与输入锁由上层限制，生成包装本身不读取 GameState。

## 生成与核查（2026-10-06）

输入源变更获授权后，通过 Input Actions 编辑器保存并应用 importer 的 Generate C# 配置；提交源资产、生成输出及必要 meta，不手工补生成类。源文件与生成输出必须同步，改动作名还须同步 InputManager 等调用方。

已逐一核查上述源文件、生成代码和 meta；生成类内嵌 JSON 与源文件解析后的结构化内容完全一致，六个动作的查找、属性、接口及三阶段回调注册/退订齐全。生成头的版本 1.7.0 与项目 manifest 一致，importer GUID 可在已安装 Input System 包中解析。未发现本目录源/生成引用缺陷。

本次仅新增文档，没有更改输入或重新生成脚本，不重复运行 Unity 测试。此前最终脚本回归已通过 EditMode 7/7、PlayMode 14/14（Setting 文档记录对应 job）；这些测试不证明所有输入设备和实际键盘/鼠标交互均已覆盖。
