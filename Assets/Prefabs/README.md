# 共享预制体

本目录按功能组织可复用对象。逐文件审计与接入说明分别见：

- [Core](Core/README.md)：共享服务根对象及池化 AudioEmitter。
- [Env](Env/README.md)：环境节点、诊断用 MockPlayer、插口和电线。

当前真实 Player 与菜单 UI 主要直接配置在场景中，不应假设这里存在完整玩家或菜单 prefab。Prefab 引用依赖 GUID，移动时必须保留 meta；修改独立场景对象不会反向更新 prefab。2026-10-06 已核对上述两组全部资源，未改变 GUID 或资源配置。
