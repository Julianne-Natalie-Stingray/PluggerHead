# Changelog

## [1.2.0] - 2026-10-03

- Change `TimerRunner.cs`'s Singleton accessibility to "public". Currently:
```csharp
    public static TimerRunner Instance { get; private set; }
```

## [1.1.0] - 2026-10-01

- Add new API in `Timer.cs`: `CompleteWhen(Func<bool> condition)`.
```csharp
    /// <summary>
    /// Completes the timer when the supplied condition becomes true.
    /// Remaining timestamps will fire immediately.
    /// The condition is evaluated once per frame while the timer is running.
    /// Must be configured before timer.Start() is called.
    /// </summary>
    public Timer CompleteWhen(Func<bool> condition) { }
```

## [1.0.1] - 2026-09-30

- Up to date with GameLog changing from [1.0.1] to [1.1.0].



## [1.0.0] - 2026-09-29

- Initial package release.
