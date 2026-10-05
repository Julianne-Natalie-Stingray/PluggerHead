# Timer

A lightweight coroutine-backed fluent timer for Unity.

## Dependency

This package depends on:

- `com.grignardreagent.gamelog` `1.1.0`

When using local tarballs, install the GameLog package first.

## Install

- Add from url "https://github.com/Julianne-Natalie-Stingray/Timer.git"

## Setup

Add `TimerRunner` to a persistent GameObject (for example your `GameCore` object).

## Example

```csharp
new Timer(5f)
    .At(1f, WarnPlayer)
    .At(3f, FlashSomething)
    .OnComplete(Explode)
    .Start();
```

Timers use scaled time by default and therefore pause when `Time.timeScale == 0`.

Use:

```csharp
new Timer(2f)
    .UseUnscaledTime()
    .OnComplete(HideMessage)
    .Start();
```

to continue during a time-scale pause.

## Requirements

- Unity 2022.2 or newer.
- `com.grignardreagent.gamelog` `1.1.0`.
