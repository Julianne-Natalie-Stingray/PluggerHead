using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises Timer callback reentry through a real coroutine host with bounded waits.</summary>
public static class TimerIntegrationChecks
{
    public static IEnumerator Run(string scenario)
    {
        return IntegrationSceneWait.Finally(RunBody(scenario), () => { });
    }

    private static IEnumerator RunBody(string scenario)
    {
        TimerRunner previousRunner = TimerRunner.Instance;
        GameObject ownedRunner = null;
        GameObject replacementRunner = null;
        Timer timer = null;
        float previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            if (!previousRunner)
            {
                ownedRunner = new GameObject("Timer integration runner");
                ownedRunner.AddComponent<TimerRunner>();
            }
            TimerRunner originalHost = TimerRunner.Instance;
            int first = 0;
            int later = 0;
            int completed = 0;
            bool complete = false;

            switch (scenario)
            {
                case "TimestampStop":
                    timer = new Timer(0.05f).At(0f, () => { first++; timer.Stop(); })
                        .At(0.01f, () => later++).OnComplete(() => completed++);
                    timer.Start();
                    Require(first == 1 && !timer.IsRunning && !timer.IsComplete && timer.Elapsed == 0f,
                        "At(0) Stop must prevent old first-step progress and completion.");
                    yield return null;
                    Require(later == 0, "Stopped first step must not dispatch later timestamps.");
                    timer.Start();
                    yield return Wait(() => timer.IsComplete);
                    Require(first == 1 && later == 1 && completed == 1, "Start must resume after the consumed stopped callback.");
                    break;

                case "TimestampRestart":
                    timer = new Timer(0.05f).At(0f, () =>
                    {
                        first++;
                        if (first == 1)
                        {
                            timer.Restart();
                        }
                    }).At(0.01f, () => later++).OnComplete(() => completed++);
                    timer.Start();
                    Require(first == 2 && timer.IsRunning, "At(0) Restart must leave its newer generation running.");
                    Require(Field<Coroutine>(timer, "coroutine") != null, "Old Start must not overwrite the new coroutine handle.");
                    yield return Wait(() => timer.IsComplete);
                    Require(later == 1 && completed == 1, "Only the restarted generation may run later callbacks.");
                    break;

                case "ConditionStop":
                    timer = new Timer(float.PositiveInfinity).At(1f, () => later++).CompleteWhen(() =>
                    {
                        if (first++ == 0)
                        {
                            timer.Stop();
                            return true;
                        }
                        return complete;
                    }).OnComplete(() => completed++);
                    timer.Start();
                    Require(!timer.IsRunning && !timer.IsComplete && timer.Elapsed == 0f && later == 0,
                        "A condition which stops then returns true must not finish the cancelled generation.");
                    timer.Start();
                    yield return null;
                    Require(timer.IsRunning && !timer.IsComplete && !float.IsInfinity(timer.Elapsed),
                        "An infinite conditional timer must remain a valid running timer.");
                    complete = true;
                    yield return Wait(() => timer.IsComplete);
                    Require(later == 1 && completed == 1, "Infinity+condition must forward remaining timestamps and complete exactly once.");
                    break;

                case "ConditionRestart":
                    timer = new Timer(float.PositiveInfinity).CompleteWhen(() =>
                    {
                        if (first++ == 0)
                        {
                            timer.Restart();
                            return true;
                        }
                        return complete;
                    }).OnComplete(() => completed++);
                    timer.Start();
                    Require(timer.IsRunning && !timer.IsComplete && completed == 0,
                        "An old true condition must not finish a restarted timer.");
                    complete = true;
                    yield return Wait(() => timer.IsComplete);
                    Require(completed == 1, "Only the new conditional run may complete.");
                    break;

                case "CompletionRestart":
                    timer = new Timer(0f).OnComplete(() =>
                    {
                        first++;
                        if (first == 1)
                        {
                            timer.Restart();
                        }
                    }).OnComplete(() => later++);
                    timer.Start();
                    Require(first == 2 && later == 1 && timer.IsComplete && !timer.IsRunning,
                        "Zero-duration completion Restart must skip the old generation's remaining completion callbacks.");
                    Require(Field<Coroutine>(timer, "coroutine") == null, "Synchronous completion must not retain a stale coroutine.");
                    break;

                case "LaterFrame":
                    timer = new Timer(0.08f).At(0.01f, () =>
                    {
                        first++;
                        if (first == 1)
                        {
                            timer.Stop();
                        }
                    }).At(0.03f, () =>
                    {
                        later++;
                        if (later == 1)
                        {
                            timer.Restart();
                        }
                    }).OnComplete(() => completed++);
                    timer.Start();
                    yield return Wait(() => !timer.IsRunning);
                    Require(first == 1 && later == 0 && !timer.IsComplete, "A later-frame Stop must halt that frame's dispatch.");
                    timer.Start();
                    yield return Wait(() => timer.IsComplete);
                    Require(first == 2 && later == 2 && completed == 1,
                        "A later-frame Restart must reset timestamps without completing its old run.");
                    break;

                case "TimestampException":
                    timer = new Timer(0.05f).At(0.01f, () => throw new InvalidOperationException("TimerFault timestamp"))
                        .OnComplete(() => completed++);
                    timer.Start();
                    yield return Wait(() => !timer.IsRunning);
                    Require(!timer.IsComplete, "A timestamp failure must stop without falsely completing.");
                    timer.Start();
                    yield return Wait(() => timer.IsComplete);
                    Require(completed == 1, "Resume must skip the consumed failed timestamp and complete normally.");
                    break;

                case "ConditionException":
                    timer = new Timer(float.PositiveInfinity).CompleteWhen(() =>
                    {
                        if (first++ == 0)
                        {
                            throw new InvalidOperationException("TimerFault condition");
                        }
                        return true;
                    }).OnComplete(() => completed++);
                    timer.Start();
                    Require(!timer.IsRunning && !timer.IsComplete, "A synchronous first-step condition exception must clear running state.");
                    timer.Start();
                    Require(timer.IsComplete && completed == 1, "The failed condition must be retryable on resume.");
                    break;

                case "CompletionException":
                    timer = new Timer(0.01f).OnComplete(() => throw new InvalidOperationException("TimerFault completion"))
                        .OnComplete(() => later++);
                    timer.Start();
                    yield return Wait(() => timer.IsComplete);
                    Require(!timer.IsRunning && later == 0, "Completion failure must retain completed state and the existing fail-fast callback order.");
                    break;

                case "RestartException":
                    timer = new Timer(0.05f).At(0f, () =>
                    {
                        if (first++ == 0)
                        {
                            timer.Restart();
                            throw new InvalidOperationException("TimerFault after restart");
                        }
                    }).OnComplete(() => completed++);
                    timer.Start();
                    Require(timer.IsRunning && !timer.IsComplete, "An old callback exception must not stop its restarted generation.");
                    yield return Wait(() => timer.IsComplete);
                    Require(completed == 1, "The restarted run must remain owned and complete normally.");
                    break;

                case "ZeroException":
                    timer = new Timer(0f).At(0f, () => throw new InvalidOperationException("TimerFault zero"));
                    Exception caught = null;
                    try
                    {
                        timer.Start();
                    }
                    catch (InvalidOperationException exception)
                    {
                        caught = exception;
                    }
                    Require(caught != null && !timer.IsRunning && !timer.IsComplete,
                        "A synchronous zero-duration failure must preserve its exception and clear running state.");
                    timer.Start();
                    Require(timer.IsComplete && !timer.IsRunning, "Zero-duration resume must consume the failed timestamp once.");
                    break;

                case "OriginalHost":
                    timer = new Timer(float.PositiveInfinity).CompleteWhen(() => { first++; return false; });
                    timer.Start();
                    Require(Field<TimerRunner>(timer, "ownerRunner") == originalHost, "The timer must retain its original runner.");
                    replacementRunner = new GameObject("Timer replacement runner");
                    replacementRunner.SetActive(false);
                    replacementRunner.AddComponent<TimerRunner>();
                    SetRunner(null);
                    replacementRunner.SetActive(true);
                    Require(TimerRunner.Instance != originalHost, "The test must replace the global runner.");
                    timer.Stop();
                    int stoppedCount = first;
                    yield return null;
                    yield return null;
                    Require(!timer.IsRunning && first == stoppedCount, "Stop must stop the original coroutine despite global runner replacement.");
                    break;

                default:
                    throw new ArgumentException("Unknown Timer integration scenario: " + scenario);
            }
        }
        finally
        {
            IntegrationSceneWait.RestoreAll(
                () => timer?.Stop(),
                () => SetRunner(previousRunner),
                () => { if (replacementRunner) { Object.DestroyImmediate(replacementRunner); } },
                () => { if (ownedRunner) { Object.DestroyImmediate(ownedRunner); } },
                () => Time.timeScale = previousTimeScale);
        }
    }

    private static IEnumerator Wait(Func<bool> done)
    {
        return IntegrationSceneWait.Until(done, "waiting for Timer integration state", 3f);
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetRunner(TimerRunner runner)
    {
        typeof(TimerRunner).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, runner);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
