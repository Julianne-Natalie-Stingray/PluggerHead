# PluggerHead Assets

这是 PluggerHead 的 Unity 资源目录，项目根目录在上一级。使用 Unity **2022.3.43f1c1** 打开项目；构建入口为 `Scenes/MainMenuScene.unity`。

主菜单“开始游戏”隐藏菜单并解锁大厅角色；从左到右三个传送门代表第一至三关，第一关进入 Level0，后两关尚未开放。通关按顺序解锁，允许重玩；进度仅在本次运行中保留，退出后清除。当前玩法输入为 A/D 移动、Space 跳跃、J 交互、K 放置 Anchor。Level0 原布局的已知通关限制见 [进度说明](Scripts/Game/Progress/README.md)。

| 目录 | 内容 |
| --- | --- |
| [Scripts](Scripts/README.md) | 按功能划分的运行时组件、生成集成和 Editor 检查。 |
| [Scenes](Scenes/README.md) | 主菜单、真实玩法集成及独立诊断场景。 |
| [Prefabs](Prefabs/README.md) | Core 服务与环境预制体及装配要求。 |
| [SO](SO/README.md) | 音频和场景切换配置资产。 |
| [Audios](Audios/README.md) | 音频文件、混音器及素材来源说明。 |
| [Visual](Visual/README.md) | 玩家、环境和 TextMesh Pro 视觉资源。 |
| [Packages](Packages/README.md) | 随 Assets 提交的工具依赖；区别于项目根目录的 UPM Packages。 |
| [Tests](Tests/README.md) | Editor 内 EditMode/PlayMode 测试、结果与隔离限制。 |
| [Docs](Docs/README.md) | 开发历史、旧检查清单与截图。 |

仓库修改、测试、审查和提交规则见 [AGENTS.md](AGENTS.md)。现行行为以源码和对应模块说明为准；历史文档保留当时语境。运行测试前处理未保存的场景修改，按 EditMode、PlayMode 顺序执行，并确认任务已结束。
