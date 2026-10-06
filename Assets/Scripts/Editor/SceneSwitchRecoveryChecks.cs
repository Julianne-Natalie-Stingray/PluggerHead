using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises real Single loads with isolated service objects and a camera-only target scene.</summary>
public static class SceneSwitchRecoveryChecks
{
    private static GameObject host;
    private static GameObject competingHost;
    private static AsyncOperation pending;
    private static Action<GameState> callback;
    private static Scene initialScene;
    private static bool ownsLoad;
    private static AsyncOperation pendingUnload;
    private static IntegrationSceneState previousState;

    public static IEnumerator CheckRecovery(string interruption)
    {
        return IntegrationSceneWait.Finally(CheckRecoveryBody(interruption), () => { });
    }

    private static IEnumerator CheckRecoveryBody(string interruption)
    {
        Require(!ownsLoad && previousState == null, "Previous recovery fixture still needs cleanup.");
        Require(!SceneManager.GetSceneByName("SceneSwitchTarget").isLoaded,
            "Refusing to unload a pre-existing recovery target scene.");
        Require(CoreFacade.Instance == null, "Scene switch recovery requires an isolated Test Runner session.");
        Require(GameStateManager.Current == GameState.Playing, "Scene switch recovery starts in Playing.");
        previousState = new IntegrationSceneState();
        initialScene = SceneManager.GetActiveScene();
        SceneSwitchManager manager = CreateManager("SceneSwitchRecovery", out host);
        SceneSwitchManager competitor = CreateManager("SceneSwitchCompetitor", out competingHost);

        manager.enabled = false;
        Require(manager.RequestSwitch(SceneId.SceneSwitchTarget) == null, "Disabled service must refuse loading.");
        manager.enabled = true;
        host.SetActive(false);
        Require(manager.RequestSwitch(SceneId.SceneSwitchTarget) == null, "Inactive service must refuse loading.");
        host.SetActive(true);
        Require(GameStateManager.Current == GameState.Playing && !manager.IsSwitching,
            "Refused requests must not change state or busy flags.");

        bool entered = false;
        bool exited = false;
        bool reentrantRejected = true;
        callback = state =>
        {
            if (manager != null)
            {
                reentrantRejected &= manager.RequestSwitch(SceneId.SceneSwitchTarget) == null;
            }
            reentrantRejected &= competitor.RequestSwitch(SceneId.SceneSwitchTarget) == null;
            if (state == GameState.Loading)
            {
                entered = true;
                if (interruption == "Deactivate")
                {
                    host.SetActive(false);
                }
                else if (interruption == "Destroy")
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
                else if (interruption == "Disable")
                {
                    manager.enabled = false;
                }
            }
            else
            {
                exited = true;
            }
            if (interruption == "Throw")
            {
                throw new InvalidOperationException("SceneSwitchRecovery expected " + state);
            }
        };
        GameStateManager.Changed += callback;
        ownsLoad = true;
        pending = manager.RequestSwitch(SceneId.SceneSwitchTarget);
        Require(pending != null && pending.allowSceneActivation, "Accepted load must retain automatic activation.");
        Require(entered && manager.IsSwitching && GameStateManager.Current == GameState.Loading,
            "Load must retain its reservation until Unity completes, despite interruption.");
        Require(competitor.RequestSwitch(SceneId.SceneSwitchTarget) == null,
            "A different service must not interleave another Single load.");
        yield return WaitForCompletion();
        Require(exited && reentrantRejected, "State callbacks must reject same-instance and cross-instance reentry.");
        Require(!manager.IsSwitching && GameStateManager.Current == GameState.Playing,
            "Completion must clear busy and Loading even after destruction or callback failure.");
        Require(SceneManager.GetActiveScene().name == "SceneSwitchTarget", "Real target must activate.");

        GameStateManager.Changed -= callback;
        callback = null;
        // A new owner can reload the same scene once completion has released the reservation.
        pending = competitor.RequestSwitch(SceneId.SceneSwitchTarget);
        Require(pending != null, "Completion must release the global reservation for subsequent requests.");
        yield return WaitForCompletion();
        Require(!competitor.IsSwitching && GameStateManager.Current == GameState.Playing,
            "The subsequent switch must also clear its reservation.");
    }

    public static IEnumerator Cleanup()
    {
        return IntegrationSceneWait.Finally(CleanupScene(), RestoreState);
    }

    private static IEnumerator CleanupScene()
    {
        if (callback != null)
        {
            GameStateManager.Changed -= callback;
            callback = null;
        }
        if (pending != null && !pending.isDone)
        {
            pending.allowSceneActivation = true;
            yield return WaitForCompletion();
        }
        if (ownsLoad)
        {
            if (!initialScene.IsValid() || !initialScene.isLoaded)
            {
                initialScene = SceneManager.CreateScene("SceneSwitchRecoveryCleanup");
            }
            SceneManager.SetActiveScene(initialScene);
            Scene target = SceneManager.GetSceneByName("SceneSwitchTarget");
            if (pendingUnload == null && target.IsValid() && target.isLoaded)
            {
                pendingUnload = SceneManager.UnloadSceneAsync(target);
            }
            if (pendingUnload != null || (target.IsValid() && target.isLoaded))
            {
                yield return IntegrationSceneWait.Operation(pendingUnload, "unloading the recovery target");
                Require(!target.IsValid() || !target.isLoaded, "Recovery unload completed but its scene remains loaded.");
            }
        }
    }

    private static void RestoreState()
    {
        bool loadPending = pending != null && !pending.isDone;
        IntegrationSceneWait.RestoreAll(
            () =>
            {
                if (callback != null)
                {
                    GameStateManager.Changed -= callback;
                    callback = null;
                }
            },
            () => { if (host != null) { UnityEngine.Object.DestroyImmediate(host); } host = null; },
            () => { if (competingHost != null) { UnityEngine.Object.DestroyImmediate(competingHost); } competingHost = null; },
            () => previousState?.Restore(loadPending),
            () =>
            {
                if (!loadPending && (pendingUnload == null || pendingUnload.isDone) &&
                    (!ownsLoad || !SceneManager.GetSceneByName("SceneSwitchTarget").isLoaded))
                {
                    pending = null;
                    pendingUnload = null;
                    ownsLoad = false;
                    previousState = null;
                }
            });
    }

    private static SceneSwitchManager CreateManager(string name, out GameObject gameObject)
    {
        gameObject = new GameObject(name);
        gameObject.SetActive(false);
        var manager = gameObject.AddComponent<SceneSwitchManager>();
        var configs = AssetDatabase.LoadAssetAtPath<SceneSwitchConfigs>(
            "Assets/SO/SceneSwitch/DefaultSceneSwitchConfigs.asset");
        Require(configs != null, "Scene switch fixture requires the authored scene registry.");
        typeof(SceneSwitchManager).GetField("configs", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, configs);
        gameObject.SetActive(true);
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        return manager;
    }

    private static IEnumerator WaitForCompletion()
    {
        return IntegrationSceneWait.Operation(pending, "finishing the recovery scene load");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
