# 电线材质

逐文件核查日期：2026-10-06。`Wire.mat` 使用内置 Shader 引用，材质色为白色，无贴图，PixelSnap 关闭，不是材质变体；对应 meta 与引用有效。

三个 Wire prefab 以及 GameplayIntegration 的两根线直接引用该材质，CircuitDiagnostics 通过 prefab 使用它。实际线色、宽度和折点由 LineRenderer 与 Env 逻辑提供，材质本身不决定电性或碰撞路径。

场景资产测试检查材质/Shader 存在且受当前编辑器支持，不等于验证全部平台、渲染管线或最终画面。本轮仅新增说明，未改材质、Shader 或 GUID。
