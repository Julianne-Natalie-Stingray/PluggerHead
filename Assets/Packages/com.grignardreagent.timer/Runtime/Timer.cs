using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lightweight coroutine-backed timer.
/// Configure it fluently before the first Start(), then Start/Stop/Restart it as needed.
/// Uses scaled game time by default, so it pauses while Time.timeScale == 0.
/// </summary>
public sealed class Timer
{
    private readonly List<TimeStamp> timeStamps = new();

    private Action onComplete;
    private Coroutine coroutine;
    private int nextTimeStampIndex;
    private bool useUnscaledTime;
    private bool hasStarted;
    private Func<bool> completeCondition;

    public float Duration { get; }
    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, Duration - Elapsed);
    public float NormalizedTime => Duration <= 0f
        ? 1f
        : Mathf.Clamp01(Elapsed / Duration);

    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }

    public Timer(float duration)
    {
        if (duration < 0f)
        {
            GameLog.Warning(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue
                .Specify($"Duration cannot be negative. Received {duration}. "))
                .Action(LogAction.ClampValue)
                .Write();
        }

        Duration = Mathf.Max(0f, duration);
    }

    /// <summary>
    /// Adds a callback to invoke once elapsed time reaches the specified time.
    /// Must be configured before the timer is started for the first time.
    /// </summary>
    public Timer At(float time, Action callback)
    {
        if (!CanConfigure())
            return this;

        if (time < 0f || time > Duration)
        {
            GameLog.Warning(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue.Specify(
                    $"Timestamp {time} is outside timer duration [0, {Duration}]. "))
                .Action(LogAction.Ignore)
                .Write();

            return this;
        }

        if (callback == null)
        {
            GameLog.Warning(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue.IsNull(nameof(callback)))
                .Action(LogAction.Ignore)
                .Write();

            return this;
        }

        timeStamps.Add(new TimeStamp(time, callback));
        return this;
    }

    /// <summary>
    /// Adds a callback to invoke after all due timestamp callbacks when the timer completes.
    /// Multiple calls append callbacks in registration order.
    /// </summary>
    public Timer OnComplete(Action callback)
    {
        if (!CanConfigure())
            return this;

        if (callback == null)
        {
            GameLog.Warning(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue.IsNull(nameof(callback)))
                .Action(LogAction.Ignore)
                .Write();

            return this;
        }

        onComplete += callback;
        return this;
    }

    /// <summary>
    /// Makes the timer use Time.unscaledDeltaTime so it continues while Time.timeScale == 0.
    /// Call before the first Start().
    /// </summary>
    public Timer UseUnscaledTime(bool useUnscaled = true)
    {
        if (!CanConfigure())
            return this;

        useUnscaledTime = useUnscaled;
        return this;
    }

    /// <summary>
    /// Starts or resumes the timer.
    /// A completed timer must be Restart()ed instead.
    /// </summary>
    public Timer Start()
    {
        if (IsRunning)
        {
            LogStateWarning("Timer is already running. ");
            return this;
        }

        if (IsComplete)
        {
            GameLog.Warning(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue.Specify("Cannot Start() a completed timer. "))
                .Action(LogAction.Specify("Use Restart() to run it again. "))
                .Write();

            return this;
        }

        TimerRunner runner = TimerRunner.Instance;
        if (!runner)
        {
            GameLog.Error(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue.MissingComponent<TimerRunner>())
                .Action(LogAction.Abort)
                .Write();

            return this;
        }

        if (!hasStarted)
        {
            timeStamps.Sort((a, b) => a.Time.CompareTo(b.Time));
            hasStarted = true;
        }

        IsRunning = true;

        if (Duration <= 0f)
        {
            ForwardToCompletion();
            return this;
        }

        coroutine = runner.StartTimer(Run());
        return this;
    }

    /// <summary>
    /// Stops the timer without resetting elapsed time.
    /// Calling Start() afterwards resumes it.
    /// </summary>
    public Timer Stop()
    {
        if (!IsRunning)
            return this;

        TimerRunner runner = TimerRunner.Instance;
        if (runner && coroutine != null)
            runner.StopTimer(coroutine);

        coroutine = null;
        IsRunning = false;
        return this;
    }

    /// <summary>
    /// Resets timer progress and immediately starts it again.
    /// Keeps all configured timestamps and completion callbacks.
    /// </summary>
    public Timer Restart()
    {
        Stop();

        Elapsed = 0f;
        nextTimeStampIndex = 0;
        IsComplete = false;

        return Start();
    }

    /// <summary>
    /// Completes the timer when the supplied condition becomes true.
    /// Remaining timestamps will fire immediately.
    /// The condition is evaluated once per frame while the timer is running.
    /// Must be configured before the timer is started for the first time.
    /// </summary>
    public Timer CompleteWhen(Func<bool> condition)
    {
        if (!CanConfigure())
            return this;

        if (condition == null)
        {
            GameLog.Warning(null)
                .Subsystem(nameof(Timer))
                .Name(LogName.None)
                .Issue(LogIssue.IsNull(nameof(condition)))
                .Action(LogAction.Ignore)
                .Write();

            return this;
        }

        completeCondition = condition;
        return this;
    }

    private IEnumerator Run()
    {
        ProcessTimeStamps();

        while (Elapsed < Duration)
        {
            if (completeCondition?.Invoke() == true)
            {
                ForwardToCompletion();
                yield break;
            }

            yield return null;

            float deltaTime = useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            Elapsed = Mathf.Min(Duration, Elapsed + deltaTime);
            ProcessTimeStamps();
        }

        Complete();
    }

    /// <summary>
    /// Jump timer to completion. All remaining timestamps fire.
    /// </summary>
    /// <returns></returns>
    private void ForwardToCompletion()
    {
        Elapsed = Duration;
        ProcessTimeStamps();
        Complete();
    }

    private void Complete()
    {
        coroutine = null;
        IsRunning = false;
        IsComplete = true;

        onComplete?.Invoke();
    }

    private void ProcessTimeStamps()
    {
        while (nextTimeStampIndex < timeStamps.Count
               && Elapsed >= timeStamps[nextTimeStampIndex].Time)
        {
            Action callback = timeStamps[nextTimeStampIndex].Callback;
            nextTimeStampIndex++;
            callback.Invoke();
        }
    }

    private bool CanConfigure()
    {
        if (!hasStarted)
            return true;

        GameLog.Warning(null)
            .Subsystem(nameof(Timer))
            .Name(LogName.None)
            .Issue(LogIssue
            .Specify("Timer configuration cannot be changed after its first Start(). "))
            .Action(LogAction.Ignore)
            .Write();

        return false;
    }

    private static void LogStateWarning(string message)
    {
        GameLog.Warning(null)
            .Subsystem(nameof(Timer))
            .Name(LogName.None)
            .Issue(LogIssue.Specify(message))
            .Action(LogAction.Ignore)
            .Write();
    }

    private sealed class TimeStamp
    {
        public float Time { get; }
        public Action Callback { get; }

        public TimeStamp(float time, Action callback)
        {
            Time = time;
            Callback = callback;
        }
    }
}
