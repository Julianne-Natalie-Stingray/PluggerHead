# 本次运行的关卡进度

GameProgress.Store 持有内存中的 LevelProgressStore，不读取或写入 LevelProgress.json。旧进度文件不参与启动。Unity SubsystemRegistration 在每次开始运行时重置静态存储，因此退出后重新启动没有通关进度；返回大厅和重开关卡不会清空。音量偏好仍由独立的 GameSettings.json 管理。

LevelProgressStore 记录已连续通关的关数。初始仅第一关解锁；CompleteLevel 只接受已解锁的 1–3 关，通关解锁下一关。重复通关不会越级或倒退，非法编号和未解锁关卡不会改变状态。

LevelProgressTracker 挂在实际关卡的 EnvironmentFacade 上，订阅 LevelCleared；禁用时退订，重新启用时检查已有通关状态。进入关卡、死亡或重开不算通关。Core 不再挂载进度追踪组件。Level0 环境配置 levelNumber=1，通关界面的“返回大厅”进入 HubScene。

HubScene 的 Portal 从左到右对应 Level1、Level2、Level3，三门均配置真实目的地；后两关初始锁定，按通关顺序解锁。三关已同步 SceneId、切换配置和 Build Settings，各自环境的 LevelProgressTracker 使用编号 1、2、3；通关按钮显示“返回大厅”，拒绝切换显示“返回大厅失败，请重试。”。Level0 保留旧注册、布局和回归用途，不再作为大厅第一门目标。

三关流程测试通过真实 J 交互进门，在测试自有运行时场景构造最小有效电路验证实际通关事件、顺序解锁和返回；不保存测试电路，也不代表原关卡布局已通过完整通关验收。

用户明确保留 Level0 当前电路布局：1 个电源插座与 2 个双极插座按现行占用/换极规则不能全部接通；本次未修改布局。通关解锁集成测试仅在自有运行时场景中构造最小有效电路，验证真实通关事件、返回大厅和进度保持，不表示该原始布局已可通关。

验证入口：LevelProgressTests 检查顺序解锁、重复通关、非法请求和运行重置；MainMenuTests 检查大厅及真实场景切换。测试隔离静态 Store，并在异步清理完成后恢复原状态；临时目录仅用于清理故障测试，不用于生产进度保存。
