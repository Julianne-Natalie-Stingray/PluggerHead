# GizmosTools

## 本项目接入与核查（2026-10-06）

本目录为 Assets 内源码副本，项目根 manifest 未安装同名 UPM 包；下方 URL 是原分发说明，不要求重复安装。package.json 版本1.0.0、Unity 2022.2，与 [CHANGELOG](CHANGELOG.md) 初版记录一致。

已逐文件检查 Runtime/GizmosTools.cs、asmdef、元数据及现有文档。唯一接口为 `DrawCircle(Vector3 center, float radius, Vector3 upAxis, int segments = 48)`：

- radius≤0 或 segments<3 时直接返回。
- upAxis 是圆平面法向，接近零时回退 Vector3.up，再归一化；使用 segments 条 Gizmos.DrawLine 近似闭合圆。
- 使用调用时的 Gizmos.color 和 Gizmos.matrix，不设置或恢复它们。因此 center 不能无条件解释为不受矩阵影响的世界坐标。
- 不创建 Mesh、碰撞体或普通游戏画面。应在 Gizmos 绘制入口使用；程序集本身没有 Editor 平台限制，并非 Editor-only asmdef。
- 未校验 NaN/Infinity，也没有 segments 上限；极大数值会增加绘制成本，不能声称任意输入安全。

当前 Scripts/Tests 没有本工具调用或专项测试；其他 Gizmos 截图不等于此工具已实测。本轮只更新说明，未更改绘制逻辑或验证远端地址。

Small reusable Gizmos drawing helpers for Unity.

## Installation

- Add with url "https://github.com/Julianne-Natalie-Stingray/GizmosTools.git".

## Example

```csharp
private void OnDrawGizmos()
{
    GizmosTools.DrawCircle(transform.position, 2f, Vector3.forward);
}
```

## Requirements

- Unity 2022.2 or newer.
