# TMP 运行时资源

2026-10-06 核查：本目录下的两个 `.txt` 是断行数据，不是叙述性文档；其原始字符内容和 meta 保留，不进行措辞改写。

| 文件 | 引用与作用 |
| --- | --- |
| `TMP Settings.asset` | 保存默认字体、精灵、样式表、断行表及文本默认设置。 |
| `LineBreaking Leading Characters.txt` | 由 TMP Settings 的 m_leadingCharacters 引用，本地 TMP_Settings 代码载入 leadingCharacters 字符表。 |
| `LineBreaking Following Characters.txt` | 由 m_followingCharacters 引用，载入 followingCharacters 字符表。 |
| `Fonts & Materials/LiberationSans SDF.asset` | 默认静态字体，字体自身引用动态 Fallback。 |
| `Fonts & Materials/LiberationSans SDF - Fallback.asset` | 同源字体的动态回退，不是完整多语言字体库。 |
| `Fonts & Materials/LiberationSans SDF - Outline.mat` | Outline 材质预设。 |
| `Fonts & Materials/LiberationSans SDF - Drop Shadow.mat` | Drop Shadow 材质预设。 |
| `Sprite Assets/EmojiOne.asset` | 默认 Emoji 精灵与材质数据。 |
| `Style Sheets/Default Style Sheet.asset` | 九个命名样式的富文本定义。 |

具体数值与配置边界见[上级总览](../README.md)。全局 fallback 列表为空，但默认字体自己的回退表不为空；不能由这两点推导中文或任意字符均受支持。文档核查不等于字体覆盖、断行效果或各平台渲染实测。
