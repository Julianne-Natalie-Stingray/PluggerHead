using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Corner/Ground teardown only: retain owned scenes and pending unloads until Unity confirms unloading.</summary>
internal sealed class OwnedPhysicsSceneCleanup
{
    private readonly Dictionary<int, Func<bool>> pending = new Dictionary<int, Func<bool>>();
    internal Func<Scene, Func<bool>> StartUnload = BeginUnload;
    internal float TimeoutSeconds = 15f;

    internal IEnumerator Run(List<Scene> ownedScenes)
    {
        var failures = new List<Exception>();
        var waiting = new List<Scene>();
        try
        {
            // Start every owned scene before yielding so one failure or cancellation cannot skip later scenes.
            foreach (Scene scene in ownedScenes.ToArray())
            {
                if ((!scene.IsValid() || !scene.isLoaded) && !pending.ContainsKey(scene.handle))
                {
                    continue;
                }
                try
                {
                    if (!pending.ContainsKey(scene.handle))
                    {
                        Func<bool> isDone = StartUnload(scene);
                        if (isDone == null)
                        {
                            throw new InvalidOperationException("Could not unload owned physics scene " + scene.name);
                        }
                        pending.Add(scene.handle, isDone);
                    }
                    waiting.Add(scene);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (waiting.Count > 0)
            {
                for (int index = waiting.Count - 1; index >= 0; index--)
                {
                    Scene scene = waiting[index];
                    try
                    {
                        if (pending[scene.handle]())
                        {
                            pending.Remove(scene.handle);
                            if (scene.IsValid() && scene.isLoaded)
                            {
                                // A terminal operation that left its scene loaded must not be reported as clean.
                                throw new InvalidOperationException("Unload completed but owned physics scene remains loaded: " + scene.name);
                            }
                            ownedScenes.Remove(scene);
                            waiting.RemoveAt(index);
                            continue;
                        }
                        if (Time.realtimeSinceStartup >= deadline)
                        {
                            throw new TimeoutException("Timed out unloading owned physics scene " + scene.name);
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                        waiting.RemoveAt(index);
                    }
                }
                if (waiting.Count > 0)
                {
                    yield return null;
                }
            }
        }
        finally
        {
            // Dispose never hides an in-flight failure with another exception. Remaining scenes block BeginChecks.
            for (int index = ownedScenes.Count - 1; index >= 0; index--)
            {
                Scene scene = ownedScenes[index];
                if ((!scene.IsValid() || !scene.isLoaded) && !pending.ContainsKey(scene.handle))
                {
                    pending.Remove(scene.handle);
                    ownedScenes.RemoveAt(index);
                }
            }
        }
        if (failures.Count > 0)
        {
            throw new AggregateException("Owned physics scene cleanup failed; remaining ownership was retained for retry.", failures);
        }
    }

    private static Func<bool> BeginUnload(Scene scene)
    {
        AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
        return operation == null ? null : () => operation.isDone;
    }
}
