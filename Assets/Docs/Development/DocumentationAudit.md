# 文档审计记录（2026-10-06）

## 范围与证据

按用户确认的范围，检查现有文档目录及主要功能目录，不为每个贴图、音频或预制体叶目录单独创建说明。先核对文件、实现和引用，再更新或创建目录 README，并按目录提交。当前模块导航见[项目入口](../../README.md)和[脚本入口](../../Scripts/README.md)。

本记录是审计过程快照，不代替各模块契约，也不表示后续工作区改动已经验证。下表列出目录审计的可追溯提交；同目录后续修正可通过 Git 历史查看。

| 范围 | 已核查内容与目录提交 |
| --- | --- |
| Core | 输入 `d0efd86`、池 `9f800b9`、切场景 `503ad40`、Managers `ffbf244`、总览 `b59e180`。 |
| Core/Audio | Enum `1ace705`、配置 `807d330`、Setting `a0a8126`、总览 `b81380f`；覆盖边界修正 `250433a`。 |
| Game | Progress `9d9c7c2`、MainMenu `e41c37f`、GameState `81c3641`、Setting `8b1b0f8`、总览 `5cc563d`。 |
| Player、Env、Debug | 分别为 `47e2e14`、`9cb6b30`、`c086f60`；核对真实交互、场景装配及手动检查边界。 |
| Infra | InputSystem `69be778`、MenuTool `2cf16aa`、总览 `d1ccfad`；保留生成文件与源资产的关系。 |
| Editor、Tests | Editor `9d2937f`；Shared `bf92473`、EditMode `046888c`、PlayMode `6c797a0`、总览 `cc46595`。后续清理说明修正 `0e46d7d`、`09f404c`、`019a01e`。 |
| Scenes | Tests `07ff639`、总览 `57a97ba`；核对四个构建场景、SceneId、配置及场景用途。 |
| Prefabs | Core `9ffd224`、Env `5ff03dc`、总览 `37dece3`；核对组件、GUID 和实例装配要求。 |
| SO | Audio `8cf686e`、SceneSwitch `d5bdc02`、总览 `317659c`。 |
| Audios | Clips `d69ceab`、总览 `5b39613`；检查音频元数据、混音器及素材声明，未把用途声明当授权凭证。 |
| Visual | Player `dd1285d`、Env `2a3bd6f`、总览 `5bb1436`。 |
| TextMesh Pro | Documentation `060bc8d`、Fonts `f72b9cd`、Sprites `3752e5c`、Resources `1b2230a`、总览 `9f9f5aa`。 |
| 工具依赖 | Timer `387fc1e`、GameLog `7b13e3b`、Gizmos `35c39de`、MenuTool Basic `9886e2b`、MenuTool `31196b9`、SerializedDictionary `436bc35`、NaughtyAttributes `ce9ec10`、总览 `a582508`。 |
| 历史与入口 | Development `83e859f`、Docs `d444894`、Scripts 导航 `bd917de`、根导航及 AGENTS 事实摘要 `03f8c3e`。 |

原有 Markdown、HTML、PDF、许可和归属文本均纳入核查。第三方 PDF、许可正文及历史记录保留原文，通过相邻 README 或定位说明解释版本差异和过时示例。TMP 两份断行字符文本作为运行数据检查，不作为说明文档改写。开发截图逐张查看，未把静态图当作当前运行结果。`Samples~` 是工具样例目录，其 README 没有 Unity meta；不因常规资源规则而补造样例 meta。

## 验证与后续修复复核

截至根导航提交，66 份 Markdown 的本地链接检查没有发现失效目标；除上述 Samples~ 文档外，对应 Markdown meta 均存在。独立审查发现的音频覆盖、在途加载状态恢复、清理错误聚合和 PhysicsCleanup fallback 边界已修正文档。仓库权限规则及历史 MCP 快照保持原义。

已直接查询确认的清理修复回归为 EditMode **17/17**（job `3eb8b79ef39f415abbed40cead84b714`）及 PlayMode **57/57**（job `e648646396f14d7ab119a800823a87c8`），均已结束且通过。测试范围与失败清理限制见[测试总说明](../../Tests/README.md)，不扩展为真实输入、听感或完整游戏体验已验证。

后续逐文件复核了 Timer、MenuTool 修复及新增测试，以及 PhysicsCleanup 的失败清理和 CompletedLoaded 用例。对应文档同步提交为 Timer `90c712c`、MenuTool `509e11d`、Editor `8c4142b`、EditMode `fcabf85`、PlayMode `5584502`、Tests 总览 `ff0e22e`；生成输出迁移说明 `6c5a077`。生成输出仅新增与源 meta 一致的 GUID 注释，未改变菜单常量或输出 meta GUID。

最终覆盖复查统计为 **67 份 Markdown、55 个文档目录、55 份 README**，新增迁移说明后全量检查的 **165 个本地链接目标**均存在。主要功能目录及全部现有文档目录均有总说明。第三方 HTML 的 Grip 站点样式依赖未随仓提供，已在相邻说明标明离线展示限制（`e5f6fc9`），保留原 HTML。

本轮测试负责方记录 EditMode **21/21**（job `21b6cbceec95472ca70ce3a965876b63`）通过。首次 PlayMode 会话中断后，服务已无法查询旧 job；重连后的新 PlayMode job `a8881108238d4eb5860ef817658ccfdf` 已由文档审计方直接查询，终态 succeeded，**71/71** 通过、0 失败、0 跳过。新增覆盖包括 MenuTool 4 项、Timer 12 项及 Physics 2 项；具体覆盖与未验证边界见测试目录说明。仅文档变化不重复运行 Unity 测试。

测试与代码提交由另一项工作负责，最终修复提交为 `d43b5fd`，包含上述工具、生成输出、测试及最终结果说明。提交后检查确认测试临时场景与 Editor 设置修改已清理，除本审计记录的收尾修改外没有剩余工作区差异。文档审计按目录独立提交说明，没有代为提交其他工作的代码。
