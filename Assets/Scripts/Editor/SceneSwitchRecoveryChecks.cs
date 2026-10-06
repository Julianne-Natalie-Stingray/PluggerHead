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

    public static IEnumerator CheckRecovery(string interruption)
    {
        Require(CoreFacade.Instance == null, "Scene switch recovery requires an isolated Test Runner session.");
        Require(GameStateManager.Current == GameState.Playing, "Scene switch recovery starts in Playing.");
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
        pending = null;
        if (host != null)
        {
            UnityEngine.Object.Destroy(host);
        }
        if (competingHost != null)
        {
            UnityEngine.Object.Destroy(competingHost);
        }
        host = null;
        competingHost = null;
        if (ownsLoad)
        {
            if (!initialScene.IsValid() || !initialScene.isLoaded)
            {
                initialScene = SceneManager.CreateScene("SceneSwitchRecoveryCleanup");
            }
            SceneManager.SetActiveScene(initialScene);
            Scene target = SceneManager.GetSceneByName("SceneSwitchTarget");
            if (target.IsValid() && target.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(target);
            }
            ownsLoad = false;
        }
        yield return null;
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
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!pending.isDone && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Require(pending.isDone, "Scene switch did not finish within 15 seconds.");
        yield return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
