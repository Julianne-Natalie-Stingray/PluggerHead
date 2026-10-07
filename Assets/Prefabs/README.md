# 共享预制体

本目录保存可复用对象。逐文件审计与接入说明分别见：

- [Core](Core/README.md)：共享服务根对象及池化 AudioEmitter。
- [Env](Env/README.md)：环境节点、场景组合预制体、插口和电线。
- [Player](Player/Player.prefab)：真实玩家预制体；碰撞材质位于 `Physics/Material/Player/`。

真实 Player 使用 `Player/Player.prefab`。GameplayIntegration 的 GlobalUI、Env、Environment Grid 和 EventSystem 已提取到 Env 目录；GlobalUI 不含 InventoryUI，复用时需在场景配置 RestartLevelScreen.player。Prefab 引用依赖 GUID，移动时必须保留 meta；修改独立场景对象不会反向更新 prefab。
