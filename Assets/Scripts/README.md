# 项目脚本

运行时按功能组织；修改接口前阅读所属模块说明，并同步调用方和场景、预制体引用。项目入口与资源导航见[项目说明](../README.md)。

| 目录 | 职责与接入说明 |
| --- | --- |
| [Core](Core/README.md) | 输入、音频、对象池和场景切换；场景需配置 Core，不能假设任意组件的 Awake 晚于服务初始化。 |
| [Game](Game/README.md) | 游戏状态、设置持久化、关卡标识进度和主菜单。 |
| [Player](Player/README.md) | 移动、动画锁、环境交互、背包接口及槽位 UI。 |
| [Env](Env/README.md) | 真实电路、持线、绕线、Anchor、地面极性和通关判定。 |
| [Infra](Infra/README.md) | Input System 与 MenuTool 生成集成；修改源资产后通过工具生成。 |
| [Debug](Debug/README.md) | 手动设置检查和漂浮视觉行为。 |
| [Editor](Editor/README.md) | Editor 集成检查实现、夹具和清理辅助；自动化包装位于 Tests。 |

Player 通过 Env 的真实接口交互。背包保存接口实例，当前没有具体普通道具拾取实现；Anchor 使用环境交互，不进入背包。GameplayIntegration 提供已装配的真实玩法场景，CircuitDiagnostics 保留独立诊断布局。

验证方式与隔离限制见[测试说明](../Tests/README.md)。测试通过只证明对应断言，不能代替真实输入、听感和完整关卡体验检查。各子目录说明记录逐文件职责及已知边界；本导航不重复定义接口。
