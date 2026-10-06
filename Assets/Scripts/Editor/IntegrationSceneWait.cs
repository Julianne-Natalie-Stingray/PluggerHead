using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded waits and explicitly drained cleanup iterators for Editor integration fixtures.</summary>
public static class IntegrationSceneWait
{
    internal static Func<string, Exception> FailureForTests;

    public static IEnumerator Operation(AsyncOperation operation, string description, float seconds = 15f)
    {
        if (operation == null)
        {
            throw new InvalidOperationException("Could not start " + description + ".");
        }
        return Until(() => operation.isDone, description, seconds);
    }

    public static IEnumerator Until(Func<bool> completed, string description, float seconds = 15f)
    {
        Exception injected = FailureForTests?.Invoke(description);
        if (injected != null)
        {
            throw injected;
        }
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!completed())
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                throw new TimeoutException("Timed out " + description +
                    ". Unity operations are not cancelled; stop this Runner session if cleanup remains pending.");
            }
            yield return null;
        }
    }

    // Unity owns yielded child iterators separately. Drain them here so their exceptions reach this
    // finally before control returns to Test Runner, rather than relying on parent coroutine disposal.
    public static IEnumerator Finally(IEnumerator body, Action restore)
    {
        var stack = new Stack<IEnumerator>();
        var failures = new List<Exception>();
        stack.Push(body);
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                bool next = false;
                object yielded = null;
                try
                {
                    next = current.MoveNext();
                    if (next)
                    {
                        yielded = current.Current;
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                    break;
                }
                if (!next)
                {
                    Dispose(stack.Pop(), failures);
                    if (failures.Count > 0)
                    {
                        break;
                    }
                    continue;
                }
                if (yielded is IEnumerator child)
                {
                    stack.Push(child);
                }
                else
                {
                    yield return yielded;
                }
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                Dispose(stack.Pop(), failures);
            }
            try
            {
                restore();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            if (failures.Count == 1)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }
            if (failures.Count > 1)
            {
                throw new AggregateException("Integration operation and cleanup failures.", failures);
            }
        }
    }

    private static void Dispose(IEnumerator routine, List<Exception> failures)
    {
        try
        {
            (routine as IDisposable)?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    public static void RestoreAll(params Action[] actions)
    {
        var failures = new List<Exception>();
        foreach (Action action in actions)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        if (failures.Count > 0)
        {
            throw new AggregateException("Integration fixture restoration failed.", failures);
        }
    }
}

/// <summary>Exact synchronous globals owned temporarily by a scene fixture.</summary>
internal sealed class IntegrationSceneState
{
    private readonly float timeScale = Time.timeScale;
    private readonly bool listenerPause = AudioListener.pause;
    private readonly GameState state = GameStateManager.Current;
    private readonly EnvironmentFacade environment = EnvironmentFacade.Current;
    private readonly float freezeScale = Read<float>(typeof(GameStateManager), "timeScaleBeforeFreeze");
    private readonly GameState loadingState = Read<GameState>(typeof(GameStateManager), "stateBeforeLoading");
    private readonly bool manualFreeze = Read<bool>(typeof(GameStateManager), "manualFreeze");
    private readonly bool restoreTimeAfterLoading = Read<bool>(typeof(GameStateManager), "restoreTimeAfterLoading");
    private readonly HashSet<object> freezeOwners = new HashSet<object>(
        Read<HashSet<object>>(typeof(GameStateManager), "freezeOwners"));

    public void Restore(bool loadingStillPending = false)
    {
        Time.timeScale = timeScale;
        AudioListener.pause = listenerPause;
        Write(typeof(EnvironmentFacade), "<Current>k__BackingField", environment);
        if (!loadingStillPending)
        {
            Write(typeof(GameStateManager), "<Current>k__BackingField", state);
            Write(typeof(GameStateManager), "timeScaleBeforeFreeze", freezeScale);
            Write(typeof(GameStateManager), "stateBeforeLoading", loadingState);
            Write(typeof(GameStateManager), "manualFreeze", manualFreeze);
            Write(typeof(GameStateManager), "restoreTimeAfterLoading", restoreTimeAfterLoading);
            HashSet<object> owners = Read<HashSet<object>>(typeof(GameStateManager), "freezeOwners");
            owners.Clear();
            owners.UnionWith(freezeOwners);
        }
    }

    private static T Read<T>(Type type, string name)
    {
        return (T)type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
    }

    private static void Write(Type type, string name, object value)
    {
        type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, value);
    }
}
