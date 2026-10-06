# Assets 内工具依赖

本目录不同于项目根的 UPM `Packages/`：这里是随仓导入的工具源码、DLL、示例和原始文档。各 package.json 的版本/依赖声明不能单独证明根 manifest 已安装对应包，也不要求重复安装。

2026-10-06 逐包检查现有文档、实现或二进制接口及项目调用，结果见：

| 包 | 本地版本 | 说明 |
| --- | --- | --- |
| [GameLog](com.grignardreagent.gamelog/README.md) | 1.1.0 | DLL 日志构造器；Action 描述动作，不执行动作。 |
| [Timer](com.grignardreagent.timer/README.md) | 1.2.0 | 协程计时器；Core 提供 Runner，条件完成与 elapsed 时钟分开。 |
| [MenuTool](com.grignardreagent.menutool/README.md) | 1.0.1 | Editor 定义导入和常量生成；输出路径、遗留文件与示例类型冲突需按实际配置处理。 |
| [GizmosTools](com.grignardreagent.gizmostools/README.md) | 1.0.0 | 圆形 Gizmos 绘制辅助；当前业务无调用。 |
| [SerializedDictionary](SerializedDictionary/README.md) | 1.0.0 | 字典与 Editor backing list；已修正 Confine 说明，Editor/Player 容错不同。 |
| [NaughtyAttributes](NaughtyAttributes/README.md) | 2.1.6 | Inspector 扩展；Button 可修改对象，演示程序集不是回归测试。 |

各 CHANGELOG 保留版本历史，许可证、PDF 与 HTML 原文保留，通过旁注明确版本和行为边界。外部下载地址的实时可用性未在本轮验证。示例不是统一可删除内容：MockPlayer 使用 NaughtyAttributes 示例图片，MenuTool Basic 又可能与项目生成代码冲突。

本轮以静态审计和文档修订为主，没有改变工具实现、重新安装依赖或执行包专项测试；现有项目 Test Runner 只覆盖实际调用中的部分路径，不证明包的全部功能或平台兼容性。
