# Basic Menu Tool Sample

`Game.menutool` defines the project-specific CreateAssetMenu hierarchy.

The included generated file supplies the sample API when no conflicting generated types exist. Future edits to a valid `Game.menutool` regenerate that file when generation is enabled.

2026-10-06 本项目核查：这里的 `Game.menutool`、预生成 `.g.cs` 与 `ExampleConfig.cs` 是一组示例，不是项目现用定义。PluggerHead 已有 `MenuTool.Game`，原样导入会重复定义生成成员。需要在隔离项目体验，或先协调 namespace/root、生成文件与调用方；不要将“包含预生成代码”解释为在任何项目导入即能编译。当前 Samples~ 内容没有作为项目普通脚本使用。详见[包说明](../../README.md)。

`ExampleConfig.cs` demonstrates:

```csharp
[CreateAssetMenu(
    fileName = MenuTool.Game.Configs.FileName,
    menuName = MenuTool.Game.Configs.Path)]
```

Try double-clicking `Game.menutool`, changing the **Menu Label** of the `Configs` node while retaining its **Identifier**, then pressing **Save**. The generated `Path` constant updates through the asset pipeline when generation is enabled and the definition is valid.
