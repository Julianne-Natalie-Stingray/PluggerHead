# 电线材质

核查日期：2026-10-07。`LiveWire.mat`、`NeutralWire.mat`、`GroundWire.mat` 由 Wire 极性选择，使用内置 Shader，材质色为白色，PixelSnap 关闭。目前三者仍共用 `../Sprite/WireSprite.png` 的红色贴图。

用户已确认三种 Wire 专用美术尚未制作，本次保留现有三材质选择机制，不通过改色冒充新素材。`WireSprite.alt.png` 是未接入的备选资源；未来素材完成后替换对应材质贴图，不手动修改 LineRenderer 的渲染参数。材质不决定电性或绕线路径。

本次只更新说明，未修改电线材质、Shader、宽度、排序或 GUID。Portal 专用美术同样尚未制作，保留现有资源，另行接入。
