# NaughtyAttributes 本项目接入

2026-10-06 核查。本地 package.json 版本2.1.6、Unity 2022.3；当前以 Assets 内源码使用，不需按[随附 HTML 手册](README.html)的安装步骤重复安装。HTML 中“Unity 2018.3 或更高”的旧说明不替代本地包声明或当前版本验证。

| 文件或目录 | 内容与边界 |
| --- | --- |
| `README.html` | 原始属性说明、示例和分发入口，已阅读；保留原文。自定义 Inspector 若需完整支持，应继承 NaughtyInspector 并使用 NaughtyEditorGUI 绘制入口，与本地实现一致。 |
| `package.json` | 版本、Unity 要求、作者与 Demo Scene 元数据。 |
| `Scripts/Core` | 属性声明及辅助类型，全平台程序集；这些声明不等于运行时自动执行 Inspector 校验。 |
| `Scripts/Editor` | 属性绘制、分组、条件与校验以及 NaughtyInspector，Editor-only 程序集。 |
| `Scripts/Test` | 普通演示程序集，无 TestAssemblies 标记，不计入 Unity Test Runner 回归。 |
| `Samples/DemoScene` | 演示场景与素材；MockPlayer prefab 实际引用其中 icon-github.png，不能整体当作未使用资源删除。 |

业务代码主要使用 BoxGroup 与 Button。Button 默认 Always，并非自动限制 Play Mode；支持无参数或参数全有默认值的方法，协程按钮需要 Play Mode。点击可能对多个选中目标执行并标记资源/场景 dirty，不自动保存，也不是无副作用的显示操作。具体按钮是否可在 EditMode 使用，应检查业务方法自己的保护。

HTML 的 ReadOnly、Required、MinValue、OnValueChanged 等说明属于 Inspector 功能，不是对任意脚本赋值提供运行时保护；OnValueChanged 也不是普通业务事件。静态/非序列化字段的显示更新仍依赖实际对象与域重载生命周期，不应从旧示例推导任何编辑器设置下均会自动重置。

本轮核查文档、程序集结构、主要接入及资源引用，未逐个交互演示所有属性、重新导入样例或验证旧 Unity 版本。项目集成回归不代表这个包全部绘制器已覆盖。
