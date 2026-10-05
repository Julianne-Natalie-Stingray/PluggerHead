# GameLog

A lightweight fluent logging utility for Unity.

## Installation

- Install `com.grignardreagent.gamelog` through Unity Package Manager.
- Or add with url "https://github.com/Julianne-Natalie-Stingray/GameLog.git".

## Example

```csharp
GameLog.Warning(this)
    .Subsystem("Movement")
    .Name(LogName.ClassAndGameObject)
    .Issue(LogIssue.Invalid("speed"))
    .Action(LogAction.ClampValue)
    .Write();
```

`context` is optional. Passing a Unity object lets Unity associate the console entry with that object; omit it for static/non-Unity systems.

## Requirements

- Unity 2022.2 or newer.
