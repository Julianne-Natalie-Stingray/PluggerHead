# 共享预制体

本目录按功能组织可复用对象。逐文件审计与接入说明分别见：

- [Core](Core/README.md)：共享服务根对象及池化 AudioEmitter。
- [Env](Env/README.md)：环境节点、场景组合预制体、插口和电线。

当前真实 Player 直接配置在场景中。GameplayIntegration 的 GlobalUI、Env、Environment Grid 和 EventSystem 已提取到 Env 目录；GlobalUI 不含 InventoryUI，复用时需在场景配置 RestartLevelScreen.player。Prefab 引用依赖 GUID，移动时必须保留 meta；修改独立场景对象不会反向更新 prefab。
