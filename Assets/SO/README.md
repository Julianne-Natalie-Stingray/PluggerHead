# 共享配置资产

本目录按服务保存开发者维护的 ScriptableObject 配置，区别于玩家持久化 Settings。逐文件核查日期：2026-10-06。

- [Audio](Audio/README.md)：默认音频注册表、Mixer 参数及池/声部容量。
- [SceneSwitch](SceneSwitch/README.md)：场景键到名称的映射与玩法关卡标记。

当前各子目录只有一份配置资产，均由 Core prefab 引用。单条 AudioClipData 位于 `Audios/SO/`，没有归入本目录；见[音频资源总览](../Audios/README.md)。各资产及 meta 已核对，未改变 GUID、配置或运行时行为。
