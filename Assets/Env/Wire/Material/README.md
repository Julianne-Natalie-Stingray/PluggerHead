# 电线材质

`LiveWire.mat`、`NeutralWire.mat`、`GroundWire.mat` 由 Wire 极性选择，当前均使用 `../Sprite/WireSprite.alt.png`。原 WireSprite.png 保留为源资源，不再被生产线材质引用。

材质保持白色，沿用内置 Shader、PixelSnap 和现有 LineRenderer 参数，不额外染色。材质不决定电性或绕线路径；未来三种极性专用美术就绪后，可分别更换主贴图。
