# 字体源文件与许可原文

逐文件核查日期：2026-10-06。

- `LiberationSans.ttf`：TMP 字体资产使用的源字体，其 GUID 被 Resources 下的 SDF 与 Fallback 资产引用。
- `LiberationSans - OFL.txt`：随附 SIL Open Font License 1.1 原文，包含 Google/Red Hat 版权及保留字体名声明。保持原文，不以本说明替代许可条款。

- `NotoSansCJKsc-Regular.otf`：来自 [Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf) 的简体中文 Regular 字体，许可原文保存在 `NotoSansCJK-LICENSE.txt`。
  下载日期 2026-10-06，SHA-256：`2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`。
- `Resources/Fonts & Materials/NotoSansSC UI.asset`（相对上级目录）：48 点采样、5 像素 padding、1024×1024 静态 SDF 图集，预烘焙当前中文 UI、运行时提示及 ASCII 字符。MainMenuScene、FinalScene 和 GlobalUI 显式使用该字体及其材质。
- `NotoSansSC Fallback.asset`：同源动态回退，1024×1024，启用 Clear Dynamic Data On Build；新增文案有未烘焙字符时可从项目内源字体补充，不依赖操作系统字体。发布前应将固定新文案补入静态图集并验证覆盖。

图集、回退和默认设置见[资源总览](../README.md)。LiberationSans 仍为第三方 TMP 默认资源，项目玩家界面显式绑定上述中文字体。
