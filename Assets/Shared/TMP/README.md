# TextMesh Pro 随附资源

当前代码依赖项目根 Packages 的 TMP 3.0.6；本目录是导入到 Assets 的字体、材质、Shader、设置与示例资源，不是另一份 UPM 包。2026-10-06 已核对现有文档及关键资源配置，保留第三方原文和资源 GUID。

| 文件或资源组 | 当前配置与说明 |
| --- | --- |
| `Resources/TMP Settings.asset` | 默认字体 LiberationSans SDF、字号36、默认 Sprite 为 EmojiOne、默认样式表；全局 fallback 列表为空。启用换行、kerning、转义解析及 Emoji 支持。 |
| `Resources/Fonts & Materials/LiberationSans SDF.asset` | 静态图集1024×1024、padding9，字体自身回退表引用同目录 Fallback。全局回退为空不等于字体没有回退。 |
| `Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` | 动态填充模式、图集配置512×512、padding9，使用同一源字体；不保证源字体之外的字符可生成。 |
| `Resources/Fonts & Materials/` 两份 `.mat` | Outline 与 Drop Shadow 材质预设，不是独立源字体。 |
| `Resources/Style Sheets/Default Style Sheet.asset` | H1、Quote、Link、Title、H2、H3、C1、C2、C3 九种富文本样式定义。 |
| `Resources/Sprite Assets/EmojiOne.asset` | 默认精灵资产及材质数据，引用 Sprites 下的图集。 |
| `Resources/LineBreaking Leading Characters.txt`、`LineBreaking Following Characters.txt` | TMP Settings 引用的断行字符数据，运行时代码加载为字符表；不是应按叙述性文档改写的说明文字。 |
| `Shaders/` | 随附 Bitmap、SDF、Sprite、Mobile、Overlay、Masking、Surface 等 Shader 与四个 cginc；旧手册中的效果面板不适用于每个变体。 |

已有文档已分别核查：[旧 PDF 手册](Documentation/README.md)、[字体及 OFL 原文](Fonts/README.md)、[Emoji 来源说明](Sprites/README.md)。不修改许可、署名或 PDF 原文，以旁注明确版本与证据边界。

本轮没有重新生成图集、修改字体栈、验证所有字符或逐平台编译所有 Shader；现有场景测试也不提供这些保证。后续变更应基于已安装包及实际资源，而非将2016年手册的安装菜单和性能描述当作当前事实。
