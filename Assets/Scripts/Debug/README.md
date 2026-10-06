# 手工诊断与漂浮实验

这里保留设置诊断和视觉实验脚本，不是正式测试程序集。运行时集成测试在 `Assets/Tests/`，Editor 检查实现位于 `Scripts/Editor/`。

## 逐文件说明

| 文件 | 行为与使用边界 |
| --- | --- |
| `DebugScript.cs` | Start 打印三路音量；两个 NaughtyAttributes 按钮分别把同一个随机值写入三路音量、恢复设计默认值，再打印结果。 |
| `FloatingLogic.cs` | Awake 记录初始局部位置和随机种子；Update 叠加周期漂移、双轴 Perlin 位移，再执行 Z 轴旋转；选中时绘制 Gizmos。 |
| 两个脚本 `.meta` | 保留 GUID，没有默认引用或特殊执行顺序。 |
| `README.md` / `.meta` | 目录说明及标识。 |

本次按脚本 GUID 搜索当前 Scenes 和 Prefabs，未找到两组件的序列化引用；这不能排除外部代码动态创建。DebugScript.Test 中读取的 CoreFacade 局部变量未被使用，不构成 Core 服务验证。原按钮旁的 `passed` 注释没有测试证据，本次已移除。

## 设置按钮

只应在 Play Mode、SettingBootstrap 初始化后使用。代码并没有 `Application.isPlaying` 防护，默认 Button 属性也允许 EditMode 点击；初始化前直接访问 Settings 可能空引用，不能把使用约束当成代码已实现的保护。

按钮只修改内存，不立即 Save，也不调用 AudioManager.ApplyAudioSettings。退出播放/程序时 SettingBootstrap 的 quitting 钩子可能把这些临时改动写入真实 GameSettings.json。因此手工诊断会影响个人设置，需自行保留原值；它不是隔离的自动化测试。默认值与保存语义见 [Setting](../Game/Setting/README.md)。

## FloatingLogic 参数及计算

漂移的 X/Y 幅度为 Inspector 值乘 0.25，周期为 drift.period；随机种子决定沿轨迹的方向。Y 使用 `sin((2π/period) × (dir × Time.time + phaseOffset))`，因此 phaseOffset 在当前公式中是时间偏移，不能当作直接加到正弦相位上的弧度值。

噪声的两个轴分别采样 PerlinNoise，再限制到 0–1 并减 0.5；有效幅度为 `oscillation.amplitude × 2 / sqrt(mass × 25)`，有效频率为 Inspector 值乘 1.2。mass 只缩放视觉噪声，不读取或设置 Rigidbody 质量。旋转速度为 Inspector 值乘 20，以 Time.deltaTime 积分；所有运动使用缩放时间。

OnValidate 在 xYSync 时同步漂移幅度：若 X 与缓存不同优先以 X 覆盖 Y，否则检查 Y；Awake/OnValidate 重新计算有效值，并把非正 mass/period 改为 0.01。它不校验非有限数，也不保护四个设置对象为 null 的情况。运行时直接改配置字段不会自动重算所有缓存。

位置每帧从 Awake 的基线重新计算，禁用后再启用不会重记初始位置，不适合与角色移动共同控制同一 Transform。`_RadialOscillate` 是未调用的径向备选，实际 Update 用的是双轴噪声。

## 行为缺陷与核查（2026-10-06）

已逐一检查脚本与 meta，再核对设置生命周期、按钮属性及场景/预制体引用。当前明确的坐标空间缺陷仍未修复：BaseRotation 把 `transform.localPosition` 作为 RotateAround 的世界支点；非恒等父级下可能额外改变世界位置。Gizmos.DrawLine/DrawWireSphere 也直接使用局部坐标，没有转换或设置 Gizmos.matrix，因此父级变换下的绘制可能错位。

本次只修正文档和注释。没有 Debug 专项自动化覆盖；项目集成回归不证明浮动公式、父级变换或 EditMode 按钮安全。需要验证行为修复时，应覆盖根对象、带平移/旋转/缩放父级、暂停恢复和非空配置，不能只观察默认根对象的漂浮效果。

本次 Unity 预览场景探针直接执行同样的 RotateAround(localPosition) 表达式：父位置 (10,0,0)、子局部位置 (1,0,0)，转 90° 后子世界位置从 (11,0,0) 变成 (1,10,0)，确认支点不是子对象自身。探针未运行完整 FloatingLogic；finally 关闭自建预览场景，没有修改或保存用户场景。

独立文档复审通过。本轮最终脚本编译后 Console 无 error，顺序运行 EditMode job `ffde23500166454aad0d97b5b4ec39f2`（10/10）和 PlayMode job `cb4f5995eafe45c3aaad666f473cb1cf`（14/14）均通过；运行工作区包含同期 GameState/Setting 修复及新增测试。本目录只改注释和文档，前述 Debug 行为缺口没有修复。
