using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Repeatable injected failures exercise the actual fixture cleanup paths without stalling Unity.</summary>
public static class SceneCleanupFailureChecks
{
    private static Func<IEnumerator> cleanup;
    private static IntegrationSceneState baseline;
    private static GameObject priorEnvironmentObject;
    private static bool isolatedProgress;

    public static void CheckIteratorFailure(string kind)
    {
        int parentFinally = 0;
        int restored = 0;
        var original = new InvalidOperationException("Injected operation failure");
        var disposal = new InvalidOperationException("Injected disposal failure");
        var restoreFailure = new InvalidOperationException("Injected restoration failure");
        IEnumerator child = kind == "Timeout"
            ? IntegrationSceneWait.Until(() => false, "injected unfinished operation", 0f)
            : new FaultIterator(kind == "MoveNext" || kind == "Aggregate" ? original : null,
                kind == "Dispose" ? disposal : null);
        IEnumerator routine = IntegrationSceneWait.Finally(Parent(child, () => parentFinally++), () =>
        {
            restored++;
            if (kind == "Aggregate")
            {
                throw restoreFailure;
            }
        });
        Exception observed = null;
        try
        {
            if (kind == "Cancel")
            {
                Require(routine.MoveNext(), "Cancellation fixture must first yield.");
                ((IDisposable)routine).Dispose();
            }
            else
            {
                while (routine.MoveNext()) { }
            }
        }
        catch (Exception exception)
        {
            observed = exception;
        }
        finally
        {
            ((IDisposable)routine).Dispose();
        }
        Require(parentFinally == 1 && restored == 1, "Every failure and disposal must unwind the parent and restore exactly once.");
        if (kind == "Timeout")
        {
            Require(observed is TimeoutException, "An unfinished operation must produce timeout evidence.");
        }
        else if (kind == "Aggregate")
        {
            var aggregate = observed as AggregateException;
            Require(aggregate != null && aggregate.InnerExceptions.Contains(original) &&
                aggregate.InnerExceptions.Contains(restoreFailure), "Both operation and restoration failures must remain observable.");
        }
        else
        {
            Require(kind == "Cancel" ? observed == null : ReferenceEquals(observed, kind == "Dispose" ? disposal : original),
                "The original operation or disposal exception must be preserved.");
        }
    }

    public static IEnumerator CheckFixtureFailure(string kind)
    {
        Require(cleanup == null && CoreFacade.Instance == null, "Fault checks require an isolated fixture session.");
        baseline = new IntegrationSceneState();
        Scene host = SceneManager.GetActiveScene();
        priorEnvironmentObject = new GameObject("Cleanup prior environment");
        priorEnvironmentObject.SetActive(false);
        EnvironmentFacade priorEnvironment = priorEnvironmentObject.AddComponent<EnvironmentFacade>();
        Set(typeof(EnvironmentFacade), "<Current>k__BackingField", priorEnvironment);
        var expectedState = new IntegrationSceneState();
        float expectedTime = Time.timeScale;
        bool expectedPause = AudioListener.pause;
        GameState expectedGameState = GameStateManager.Current;
        bool expectedManualFreeze = (bool)Get(typeof(GameStateManager), "manualFreeze");
        bool expectedRestoreTime = (bool)Get(typeof(GameStateManager), "restoreTimeAfterLoading");
        var expectedOwners = new HashSet<object>((HashSet<object>)Get(typeof(GameStateManager), "freezeOwners"));
        MainMenuIntegrationChecks.BeginProgressIsolation();
        isolatedProgress = true;
        object isolatedStore = Get(typeof(GameProgress), "store");
        string temporaryPath = (string)Get(typeof(MainMenuIntegrationChecks), "temporaryDirectory");
        Directory.CreateDirectory(temporaryPath);
        File.WriteAllText(Path.Combine(temporaryPath, "proof.txt"), "owned temporary state");

        if (kind == "Floating")
        {
            cleanup = FloatingIntegrationChecks.Cleanup;
            UnityEngine.Random.State random = UnityEngine.Random.state;
            IntegrationSceneWait.FailureForTests = _ => new TimeoutException("Injected motion timeout");
            ExpectInjectedFailure(FloatingIntegrationChecks.CheckMotion(false));
            Require(Time.timeScale == expectedTime && JsonUtility.ToJson(UnityEngine.Random.state) == JsonUtility.ToJson(random),
                "A nested motion failure must restore time and Random before TearDown.");
            Require(GameObject.Find("FloatingTestRoot") == null, "A failed motion fixture must destroy its root immediately.");
        }
        else
        {
            Scene owned = SceneManager.CreateScene(kind == "Menu" ? "MainMenuScene" :
                kind == "Recovery" ? "SceneSwitchTarget" : "CleanupGameplayFault");
            if (kind == "Gameplay")
            {
                cleanup = SceneIntegrationChecks.CleanupGameplay;
                Set(typeof(SceneIntegrationChecks), "gameplayStarted", true);
                Set(typeof(SceneIntegrationChecks), "ownedScene", owned);
                Set(typeof(SceneIntegrationChecks), "previousScene", host);
                Set(typeof(SceneIntegrationChecks), "previousState", expectedState);
            }
            else if (kind == "Menu")
            {
                cleanup = MainMenuIntegrationChecks.CleanupMenuFlow;
                Set(typeof(MainMenuIntegrationChecks), "ownsScenes", true);
                Set(typeof(MainMenuIntegrationChecks), "testHostScene", host);
                Set(typeof(MainMenuIntegrationChecks), "previousSceneState", expectedState);
            }
            else
            {
                cleanup = SceneSwitchRecoveryChecks.Cleanup;
                Set(typeof(SceneSwitchRecoveryChecks), "ownsLoad", true);
                Set(typeof(SceneSwitchRecoveryChecks), "initialScene", host);
                Set(typeof(SceneSwitchRecoveryChecks), "previousState", expectedState);
                Set(typeof(SceneSwitchRecoveryChecks), "host", new GameObject("Recovery fault host"));
                Set(typeof(SceneSwitchRecoveryChecks), "competingHost", new GameObject("Recovery fault competitor"));
            }
            if (kind != "Recovery")
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Core.prefab");
                CoreFacade core = Object.Instantiate(prefab).GetComponent<CoreFacade>();
                if (kind == "Gameplay")
                {
                    Set(typeof(SceneIntegrationChecks), "ownedCore", core);
                }
            }
            GameStateManager.Freeze();
            GameStateManager.RequestFreeze(new object());
            Set(typeof(GameStateManager), "restoreTimeAfterLoading", !expectedRestoreTime);
            Set(typeof(EnvironmentFacade), "<Current>k__BackingField", null);
        }

        IntegrationSceneWait.FailureForTests = _ => new TimeoutException("Injected unload timeout");
        ExpectInjectedFailure(cleanup());
        Require(CoreFacade.Instance == null, "A failed unload must not skip owned persistent Core destruction.");
        Require(Time.timeScale == expectedTime && AudioListener.pause == expectedPause && GameStateManager.Current == expectedGameState,
            "A failed unload must restore synchronous game state.");
        Require((bool)Get(typeof(GameStateManager), "manualFreeze") == expectedManualFreeze &&
            (bool)Get(typeof(GameStateManager), "restoreTimeAfterLoading") == expectedRestoreTime &&
            expectedOwners.SetEquals((HashSet<object>)Get(typeof(GameStateManager), "freezeOwners")),
            "A failed unload must restore manual pause, pending time restoration and freeze ownership, not only the visible state.");
        Require(EnvironmentFacade.Current == priorEnvironment, "A failed unload must restore the previous environment reference.");
        if (kind == "Recovery")
        {
            Require(GameObject.Find("Recovery fault host") == null && GameObject.Find("Recovery fault competitor") == null,
                "Recovery unload failure must still destroy both owned service hosts.");
        }
        MainMenuIntegrationChecks.EndProgressIsolation();
        if (MainMenuIntegrationChecks.HasPendingCleanup || SceneIntegrationChecks.HasPendingCleanup)
        {
            Require(ReferenceEquals(Get(typeof(GameProgress), "store"), isolatedStore) && Directory.Exists(temporaryPath),
                "Pending scene cleanup must retain the temporary store and its files.");
        }
        IntegrationSceneWait.FailureForTests = null;
        yield return cleanup();
        MainMenuIntegrationChecks.EndProgressIsolation();
        Require(!Directory.Exists(temporaryPath), "Successful cleanup retry must finish deleting temporary progress files.");
    }

    public static IEnumerator CheckPendingLoadIsolation()
    {
        Require(cleanup == null && CoreFacade.Instance == null && !SceneManager.GetSceneByName("SceneSwitchTarget").isLoaded,
            "Pending-load fault check requires a fresh scene session.");
        baseline = new IntegrationSceneState();
        object originalStore = Get(typeof(GameProgress), "store");
        MainMenuIntegrationChecks.BeginProgressIsolation();
        isolatedProgress = true;
        object temporaryStore = Get(typeof(GameProgress), "store");
        string directory = (string)Get(typeof(MainMenuIntegrationChecks), "temporaryDirectory");
        Directory.CreateDirectory(directory);
        cleanup = MainMenuIntegrationChecks.CleanupMenuFlow;
        Set(typeof(MainMenuIntegrationChecks), "ownsScenes", true);
        Set(typeof(MainMenuIntegrationChecks), "testHostScene", SceneManager.GetActiveScene());
        Set(typeof(MainMenuIntegrationChecks), "previousSceneState", new IntegrationSceneState());
        AsyncOperation operation = SceneManager.LoadSceneAsync("SceneSwitchTarget", LoadSceneMode.Additive);
        operation.allowSceneActivation = false;
        Set(typeof(MainMenuIntegrationChecks), "pendingSceneOperation", operation);
        IntegrationSceneWait.FailureForTests = _ => new TimeoutException("Injected pending load timeout");
        ExpectInjectedFailure(cleanup());
        Require(!operation.isDone && ReferenceEquals(operation, Get(typeof(MainMenuIntegrationChecks), "pendingSceneOperation")),
            "Timeout must preserve the original unfinished Unity operation.");
        MainMenuIntegrationChecks.EndProgressIsolation();
        Require(ReferenceEquals(temporaryStore, Get(typeof(GameProgress), "store")) && Directory.Exists(directory),
            "An unfinished load must never restore the personal progress store.");
        bool rejected = false;
        try
        {
            MainMenuIntegrationChecks.BeginProgressIsolation();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, "The next fixture must not overwrite pending progress isolation.");
        IntegrationSceneWait.FailureForTests = null;
        yield return cleanup();
        Require(ReferenceEquals(originalStore, Get(typeof(GameProgress), "store")) && !Directory.Exists(directory),
            "Cleanup retry after the original load completes must honor the deferred store release automatically.");
    }

    public static IEnumerator Cleanup()
    {
        IntegrationSceneWait.FailureForTests = null;
        return IntegrationSceneWait.Finally(CleanupOwnedFixture(), () =>
        {
            IntegrationSceneWait.RestoreAll(
                () => { if (isolatedProgress) { MainMenuIntegrationChecks.EndProgressIsolation(); } },
                () => { if (priorEnvironmentObject) { Object.DestroyImmediate(priorEnvironmentObject); } },
                () => baseline?.Restore(MainMenuIntegrationChecks.HasPendingCleanup || SceneIntegrationChecks.HasPendingCleanup));
            priorEnvironmentObject = null;
            cleanup = null;
            baseline = null;
            isolatedProgress = false;
        });
    }

    private static IEnumerator CleanupOwnedFixture()
    {
        if (cleanup != null)
        {
            yield return cleanup();
        }
    }

    private static IEnumerator Parent(IEnumerator child, Action restored)
    {
        try
        {
            yield return child;
        }
        finally
        {
            restored();
        }
    }

    private sealed class FaultIterator : IEnumerator, IDisposable
    {
        private readonly Exception moveFailure;
        private readonly Exception disposeFailure;
        private bool yielded;
        public FaultIterator(Exception moveFailure, Exception disposeFailure)
        {
            this.moveFailure = moveFailure;
            this.disposeFailure = disposeFailure;
        }
        public object Current => null;
        public bool MoveNext()
        {
            if (moveFailure != null)
            {
                throw moveFailure;
            }
            if (yielded)
            {
                return false;
            }
            yielded = true;
            return true;
        }
        public void Reset()
        {
            throw new NotSupportedException();
        }
        public void Dispose()
        {
            if (disposeFailure != null)
            {
                throw disposeFailure;
            }
        }
    }

    private static void ExpectInjectedFailure(IEnumerator routine)
    {
        Exception observed = null;
        try
        {
            routine.MoveNext();
            throw new InvalidOperationException("Fault injection did not fail before yielding.");
        }
        catch (Exception exception)
        {
            observed = exception;
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
        }
        Require(observed is TimeoutException && observed.Message.StartsWith("Injected"), "The injected timeout must propagate unchanged.");
    }

    private static object Get(Type type, string field)
    {
        return type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    }
    private static void Set(Type type, string field, object value)
    {
        type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
