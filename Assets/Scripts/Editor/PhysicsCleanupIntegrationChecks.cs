using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Injects unload failures into the real Corner/Ground Cleanup enumerators and then retries real unloading.</summary>
public static class PhysicsCleanupIntegrationChecks
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static Type activeOwner;
    private static EnvironmentFacade environmentBeforeCheck;

    public static IEnumerator Run(string ownerName, string failure)
    {
        Require(activeOwner == null, "Previous physics fault fixture still needs its TearDown cleanup.");
        Type owner = typeof(CornerIntegrationChecks).Assembly.GetType(ownerName, true);
        var scenes = (List<Scene>)owner.GetField("ownedScenes", PrivateStatic).GetValue(null);
        Require(scenes.Count == 0, "Fault injection requires a clean owner.");
        var cleanup = (OwnedPhysicsSceneCleanup)owner.GetField("sceneCleanup", PrivateStatic).GetValue(null);
        var pending = (Dictionary<int, Func<bool>>)typeof(OwnedPhysicsSceneCleanup)
            .GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cleanup);
        Func<Scene, Func<bool>> originalUnload = cleanup.StartUnload;
        float originalTimeout = cleanup.TimeoutSeconds;
        EnvironmentFacade originalEnvironment = EnvironmentFacade.Current;
        var sentinelObject = new GameObject("PhysicsCleanup original environment");
        sentinelObject.SetActive(false);
        EnvironmentFacade sentinel = sentinelObject.AddComponent<EnvironmentFacade>();
        GameObject core = null;
        Scene first = default;
        IEnumerator routine = null;
        bool injectedPending = false;
        Func<bool> actualInjectedOperation = null;
        try
        {
            SetEnvironment(sentinel);
            owner.GetMethod("BeginChecks", PrivateStatic).Invoke(null, null);
            activeOwner = owner;
            environmentBeforeCheck = originalEnvironment;
            first = SceneManager.CreateScene("PhysicsCleanup failed-" + Guid.NewGuid().ToString("N"));
            Scene second = SceneManager.CreateScene("PhysicsCleanup subsequent-" + Guid.NewGuid().ToString("N"));
            scenes.Add(first);
            scenes.Add(second);
            var contaminated = new GameObject("PhysicsCleanup temporary environment");
            contaminated.SetActive(false);
            SceneManager.MoveGameObjectToScene(contaminated, first);
            SetEnvironment(contaminated.AddComponent<EnvironmentFacade>());
            if (owner == typeof(GroundPolarityIntegrationChecks))
            {
                // The cleanup owns a GameObject reference; no runtime Core services are needed to test its destruction.
                core = new GameObject("PhysicsCleanup owned Core cleanup target");
                owner.GetField("ownedCore", PrivateStatic).SetValue(null, core);
            }
            int subsequentRequests = 0;
            var injectedException = new InvalidOperationException("Injected physics unload request failure.");
            cleanup.TimeoutSeconds = failure == "Timeout" ? 0f : 15f;
            cleanup.StartUnload = scene =>
            {
                if (scene.handle == first.handle)
                {
                    if (failure == "Null")
                    {
                        return null;
                    }
                    if (failure == "Exception")
                    {
                        throw injectedException;
                    }
                    if (failure == "CompletedLoaded")
                    {
                        return () => true;
                    }
                    injectedPending = true;
                    if (failure == "UnloadedPending")
                    {
                        actualInjectedOperation = originalUnload(scene);
                        Require(actualInjectedOperation != null, "The underlying real unload must start.");
                    }
                    return () => false;
                }
                subsequentRequests++;
                return originalUnload(scene);
            };

            routine = CreateCleanup(owner);
            Exception observed = null;
            if (failure == "Dispose" || failure == "UnloadedPending")
            {
                Require(routine.MoveNext(), "Cancellation must occur while a real cleanup is waiting.");
                if (failure == "UnloadedPending")
                {
                    float deadline = Time.realtimeSinceStartup + 5f;
                    while (first.isLoaded || !actualInjectedOperation())
                    {
                        Require(Time.realtimeSinceStartup < deadline, "The real injected scene unload must finish.");
                        yield return null;
                        Require(routine.MoveNext(), "An unconfirmed injected handle must keep cleanup waiting.");
                    }
                    Require(routine.MoveNext() && scenes.Contains(first) && pending.ContainsKey(first.handle),
                        "An unloaded scene with an unconfirmed handle must retain ownership and the pending handle.");
                }
                ((IDisposable)routine).Dispose();
                routine = null;
            }
            else
            {
                while (true)
                {
                    bool more;
                    try
                    {
                        more = routine.MoveNext();
                    }
                    catch (Exception exception)
                    {
                        observed = exception;
                        break;
                    }
                    if (!more)
                    {
                        break;
                    }
                    yield return routine.Current;
                }
                Require(observed is AggregateException, "Unload failures must still be reported to the caller.");
                var aggregate = (AggregateException)observed;
                if (failure == "Exception")
                {
                    Require(aggregate.InnerExceptions.Contains(injectedException), "The original unload exception must be preserved.");
                }
                else
                {
                    string expectedCause = failure == "Null" ? "Could not unload" :
                        failure == "CompletedLoaded" ? "remains loaded" : "Timed out";
                    Require(aggregate.ToString().Contains(expectedCause),
                        "The reported failure must retain the actual null/timeout/incomplete-unload cause.");
                }
            }
            Require(subsequentRequests == 1, "Failure in the first scene must not skip later unload requests.");
            Require(EnvironmentFacade.Current == sentinel, "Cleanup failure or disposal must restore the original environment.");
            Require(scenes.Contains(first) && (first.isLoaded || failure == "UnloadedPending"),
                "A scene whose unload is failed or unconfirmed must retain ownership.");
            if (injectedPending)
            {
                Require(pending.ContainsKey(first.handle), "Disposal or timeout must retain the unconfirmed pending handle.");
            }
            if (owner == typeof(GroundPolarityIntegrationChecks))
            {
                Require(core == null && owner.GetField("ownedCore", PrivateStatic).GetValue(null) == null,
                    "Ground cleanup must synchronously destroy its owned Core target despite unload failure.");
            }
            bool blocked = false;
            try
            {
                owner.GetMethod("BeginChecks", PrivateStatic).Invoke(null, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                blocked = true;
            }
            Require(blocked, "Residual ownership must prevent another fixture from starting over leaked scenes.");

            (routine as IDisposable)?.Dispose();
            routine = null;
            if (injectedPending)
            {
                // Only the injected fake has no real AsyncOperation. Preserve the second scene's actual pending handle.
                if (actualInjectedOperation == null)
                {
                    pending.Remove(first.handle);
                }
                else
                {
                    pending[first.handle] = actualInjectedOperation;
                }
                injectedPending = false;
            }
            cleanup.TimeoutSeconds = originalTimeout;
            cleanup.StartUnload = scene =>
            {
                if (scene.handle == second.handle)
                {
                    subsequentRequests++;
                }
                return originalUnload(scene);
            };
            routine = CreateCleanup(owner);
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }
            Require(scenes.Count == 0 && subsequentRequests == 1,
                "Retry must finish real unloads without starting a second unload for a still-pending scene.");
            Require(EnvironmentFacade.Current == sentinel, "Retry must not replace the already restored environment with null.");
        }
        finally
        {
            try
            {
                (routine as IDisposable)?.Dispose();
            }
            finally
            {
                cleanup.StartUnload = originalUnload;
                cleanup.TimeoutSeconds = originalTimeout;
                if (injectedPending)
                {
                    if (actualInjectedOperation == null)
                    {
                        pending.Remove(first.handle);
                    }
                    else
                    {
                        pending[first.handle] = actualInjectedOperation;
                    }
                }
                // Actual async cleanup belongs to UnityTearDown. Restore synchronous state here without
                // starting and immediately abandoning an unload if an assertion interrupted this check.
                // 实际异步卸载交给独立 TearDown; 断言失败时此处只恢复同步状态, 不发起后立即丢弃卸载.
                try
                {
                    if (core != null)
                    {
                        UnityEngine.Object.DestroyImmediate(core);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sentinelObject);
                    SetEnvironment(originalEnvironment);
                }
            }
        }
    }

    public static IEnumerator Cleanup()
    {
        Type owner = activeOwner;
        if (owner == null)
        {
            return Empty();
        }
        EnvironmentFacade original = environmentBeforeCheck;
        return IntegrationSceneWait.Finally(CreateCleanup(owner), () =>
        {
            SetEnvironment(original);
            var scenes = (List<Scene>)owner.GetField("ownedScenes", PrivateStatic).GetValue(null);
            if (scenes.Count == 0)
            {
                activeOwner = null;
                environmentBeforeCheck = null;
            }
        });
    }

    private static IEnumerator Empty()
    {
        yield break;
    }

    private static IEnumerator CreateCleanup(Type owner)
    {
        return (IEnumerator)owner.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }

    private static void SetEnvironment(EnvironmentFacade value)
    {
        typeof(EnvironmentFacade).GetField("<Current>k__BackingField", PrivateStatic).SetValue(null, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
