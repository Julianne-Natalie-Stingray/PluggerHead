# Infra：生成式集成

## 中文

这里保存项目使用的工具源资产及生成 C#，运行时业务实现仍按功能放在 Core、Game、Player 和 Env。

| 目录 | 源文件 → 生成输出 | 接入位置 |
| --- | --- | --- |
| [InputSystem](InputSystem/README.md) | PlayerControls.inputactions → PlayerControls.cs | Core/Input/InputManager 管理生命周期并转发鼠标、Space、A/D、J/K 输入；玩法组件解释动作。 |
| [MenuTool](MenuTool/README.md) | Game.menutool → Game.MenuTool.g.cs | 为 AudioManagerConfigs、AudioClipData、SceneSwitchConfigs 的 CreateAssetMenu 提供路径和默认文件名常量。 |

先修改源资产，再由对应工具生成；不要手改 PlayerControls.cs 或 *.g.cs。输入绑定变更须先协商；本次文档核查不改变输入。修改生成 API、identifier 或类名时同步调用方，移动源/输出时保留 GUID，并检查 importer 的输出位置。现有配置类已经依赖 MenuTool 输出，不能直接删除。

本目录的直接文件是本 README 及其 meta，子目录 meta 保留文件夹标识。2026-10-06 先逐文件核对两个子目录的源、生成输出、importer 配置及调用方，再修订本总览；当前输入 JSON 与内嵌 JSON 一致，菜单三个叶子常量与引用一致。具体边界及验证见子目录文档。本次只改文档，不把静态一致性检查当成所有设备或生成器边界的运行验证。

## English

Infra holds source assets and generated C# integrations. InputManager owns the generated PlayerControls lifecycle; gameplay code interprets its events. MenuTool supplies CreateAssetMenu constants already referenced by the audio and scene configuration classes.

Edit the source asset and regenerate through its importer/tool; do not hand-edit generated code. Agree input changes first, preserve asset GUIDs, and update callers when generated APIs change. See the directory guides for the current bindings, menu paths and audit limits.
