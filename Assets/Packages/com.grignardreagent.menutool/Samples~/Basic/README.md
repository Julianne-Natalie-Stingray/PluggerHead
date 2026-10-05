# Basic Menu Tool Sample

`Game.menutool` defines the project-specific CreateAssetMenu hierarchy.

The included generated file makes the sample compile immediately after import. Future edits to `Game.menutool` regenerate that file automatically.

`ExampleConfig.cs` demonstrates:

```csharp
[CreateAssetMenu(
    fileName = MenuTool.Game.Configs.FileName,
    menuName = MenuTool.Game.Configs.Path)]
```

Try double-clicking `Game.menutool`, changing `Configs` to another visible label, then pressing **Save**. The generated `Path` constant updates through the asset pipeline.
