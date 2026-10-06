using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks the authored menu and real scene reloads with an isolated progress file.</summary>
public static class MainMenuIntegrationChecks
{
    private static readonly FieldInfo StoreField = typeof(GameProgress).GetField("store", BindingFlags.Static | BindingFlags.NonPublic);
    private static LevelProgressStore previousStore;
    private static string temporaryDirectory;
    private static bool ownsScenes;
    private static Scene testHostScene;
    private static AsyncOperation pendingSceneOperation;
    private static AsyncOperation pendingUnload;
    private static Scene unloadingScene;
    private static bool progressReleaseRequested;
    private static IntegrationSceneState previousSceneState;
    public static bool HasPendingCleanup => ownsScenes;

    public static void BeginProgressIsolation()
    {
        Require(temporaryDirectory == null && !ownsScenes && !SceneIntegrationChecks.HasPendingCleanup,
            "Previous progress isolation or scene cleanup remains pending; stop the Runner session before retrying.");
        previousStore = (LevelProgressStore)StoreField.GetValue(null);
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "PluggerHeadMenu-" + Guid.NewGuid().ToString("N"));
        StoreField.SetValue(null, new LevelProgressStore(Path.Combine(temporaryDirectory, "progress.json")));
    }

    public static void EndProgressIsolation()
    {
        if (temporaryDirectory == null)
        {
            return;
        }
        // Preserve the original cleanup exception. A pending operation may still create a progress tracker,
        // so defer this release, keep the temporary store, and reject the next fixture's Begin call.
        if (ownsScenes || SceneIntegrationChecks.HasPendingCleanup)
        {
            progressReleaseRequested = true;
            return;
        }
        StoreField.SetValue(null, previousStore);
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, true);
        }
        temporaryDirectory = null;
        previousStore = null;
        progressReleaseRequested = false;
    }

    public static void FinishDeferredProgressRelease()
    {
        if (progressReleaseRequested && !ownsScenes && !SceneIntegrationChecks.HasPendingCleanup)
        {
            EndProgressIsolation();
        }
    }

    public static void CheckProgressStorage()
    {
        BeginProgressIsolation();
        try
        {
            var store = GameProgress.Store;
            string path = Path.Combine(temporaryDirectory, "progress.json");
            Require(!store.TryGetLevel(out _), "Missing save must not offer Continue.");
            Require(store.SaveLevel(SceneId.GameplayIntegration), "Save must succeed.");
            Require(File.ReadAllText(path) == "{\"level\":3}", "Save must contain only the level, without in-level state.");
            var reopened = new LevelProgressStore(path);
            Require(reopened.TryGetLevel(out SceneId level) && level == SceneId.GameplayIntegration,
                "A new session must recover the last level.");
            foreach (string invalid in new[] { "", "{}", "{\"level\":999}" })
            {
                File.WriteAllText(path, invalid);
                reopened.Reload();
                Require(!reopened.TryGetLevel(out _), "Empty, missing-field and unknown-level saves must be ignored.");
            }
            File.WriteAllText(path, "{broken");
            reopened.Reload();
            Require(!reopened.TryGetLevel(out _), "Corrupt save must not prevent startup.");
            Require(store.SaveLevel(SceneId.GameplayIntegration), "Restore known save.");
            Directory.CreateDirectory(path + ".tmp");
            Require(!store.SaveLevel(SceneId.CircuitDiagnostics), "I/O failure must be reported.");
            Require(new LevelProgressStore(path).TryGetLevel(out level) && level == SceneId.GameplayIntegration,
                "Failed save must preserve the previous file.");
        }
        finally
        {
            EndProgressIsolation();
        }
    }

    public static IEnumerator CheckMenuFlow()
    {
        return IntegrationSceneWait.Finally(CheckMenuFlowBody(), () => { });
    }

    private static IEnumerator CheckMenuFlowBody()
    {
        Require(CoreFacade.Instance == null, "Run in a fresh Test Runner session.");
        Require(!ownsScenes, "Previous menu scene cleanup remains pending.");
        foreach (string sceneName in new[] { "MainMenuScene", "GameplayIntegration", "SceneSwitchTarget" })
        {
            Require(!SceneManager.GetSceneByName(sceneName).isLoaded,
                "Refusing to modify a pre-existing menu flow scene: " + sceneName);
        }
        previousSceneState = new IntegrationSceneState();
        testHostScene = SceneManager.GetActiveScene();
        ownsScenes = true;
        pendingSceneOperation = SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Additive);
        yield return IntegrationSceneWait.Operation(pendingSceneOperation, "loading the owned main menu", 20f);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("MainMenuScene"));
        yield return null;
        var menu = UnityEngine.Object.FindObjectOfType<MainMenuScreen>();
        Require(menu != null, "MainMenuScene must contain its controller.");
        var continueButton = Field<UnityEngine.UI.Button>(menu, "continueGameButton");
        Require(!continueButton.interactable, "Continue must be disabled without a save.");
        Require(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1,
            "Menu must have one EventSystem.");
        Require(menu.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Menu canvas must receive pointer input.");
        foreach (string field in new[] { "newGameButton", "continueGameButton", "settingsButton", "exitButton" })
        {
            var button = Field<UnityEngine.UI.Button>(menu, field);
            Require(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == menu,
                field + " must target the authored menu controller.");
        }

        Field<UnityEngine.UI.Button>(menu, "settingsButton").onClick.Invoke();
        var settings = Field<SettingsScreen>(menu, "settingsScreen");
        Require(settings.gameObject.activeSelf && GameStateManager.Current == GameState.Freezed,
            "Settings button must open the existing settings panel.");
        CheckSettingsSave(settings);
        settings.ContinueGame();
        Require(GameStateManager.Current == GameState.Playing && Time.timeScale > 0, "Closing settings must release pause.");

        Field<UnityEngine.UI.Button>(menu, "newGameButton").onClick.Invoke();
        yield return WaitForSwitch("GameplayIntegration");
        Require(GameProgress.Store.TryGetLevel(out SceneId saved) && saved == SceneId.GameplayIntegration,
            "Successful gameplay entry must save the level.");
        PlayerMove player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        Vector3 defaultPosition = player.transform.position;
        var environment = EnvironmentFacade.Current;
        Wire heldWire = environment.HeldWire;
        Require(heldWire != null, "Authored gameplay must start with a held wire.");
        // Change scene state far beyond anything a level-only save may restore.
        player.GetComponent<Rigidbody2D>().simulated = false;
        player.transform.position = new Vector3(123, 456, 0);
        var extra = new GameObject("UnsavedRuntimeObject");
        SceneManager.MoveGameObjectToScene(extra, player.gameObject.scene);
        heldWire.gameObject.SetActive(false);
        settings = Resources.FindObjectsOfTypeAll<SettingsScreen>().Single(s => s.gameObject.scene == player.gameObject.scene);
        settings.Open();
        Field<UnityEngine.UI.Button>(settings, "exitButton").onClick.Invoke();
        yield return WaitForSwitch("MainMenuScene");
        Require(Time.timeScale > 0 && GameStateManager.Current == GameState.Playing && !AudioListener.pause,
            "Returning from pause must leave the main menu unfrozen.");
        Require(GameProgress.Store.TryGetLevel(out saved) && saved == SceneId.GameplayIntegration,
            "Main menu must not overwrite last level.");
        // Recreate the store to prove Continue works with persisted data, not cached objects.
        StoreField.SetValue(null, new LevelProgressStore(Path.Combine(temporaryDirectory, "progress.json")));
        menu = UnityEngine.Object.FindObjectOfType<MainMenuScreen>();
        yield return null;
        Require(Field<UnityEngine.UI.Button>(menu, "continueGameButton").interactable, "Persisted level must enable Continue.");
        Field<UnityEngine.UI.Button>(menu, "continueGameButton").onClick.Invoke();
        yield return WaitForSwitch("GameplayIntegration");
        player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        Require((player.transform.position - defaultPosition).sqrMagnitude < 1f,
            "Continue must use the authored spawn, not the last player position.");
        Require(GameObject.Find("UnsavedRuntimeObject") == null,
            "Continue must discard runtime objects.");
        Require(EnvironmentFacade.Current.HeldWire != null && EnvironmentFacade.Current.HeldWire.gameObject.activeSelf &&
            !EnvironmentFacade.Current.IsCircuitClosed, "Continue must restore the default circuit.");

        // Non-gameplay scene transitions must preserve the last gameplay level.
        CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.SceneSwitchTarget);
        yield return WaitForSwitch("SceneSwitchTarget");
        Require(GameProgress.Store.TryGetLevel(out saved) && saved == SceneId.GameplayIntegration,
            "Non-gameplay scenes must not overwrite progress.");
        CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.MainMenuScene);
        yield return WaitForSwitch("MainMenuScene");
        menu = UnityEngine.Object.FindObjectOfType<MainMenuScreen>();
        GameProgress.Store.SaveLevel(SceneId.SceneSwitchTarget);
        yield return null;
        Require(!Field<UnityEngine.UI.Button>(menu, "continueGameButton").interactable,
            "Non-gameplay save must not enable Continue.");
        Field<UnityEngine.UI.Button>(menu, "newGameButton").onClick.Invoke();
        yield return WaitForSwitch("GameplayIntegration");
        Require(GameProgress.Store.TryGetLevel(out saved) && saved == SceneId.GameplayIntegration,
            "New Game must replace old progress with the first level after load.");

        NextLevelScreen nextScreen = UnityEngine.Object.FindObjectOfType<NextLevelScreen>();
        Require(nextScreen != null && !Field<GameObject>(nextScreen, "panel").activeSelf,
            "A fresh gameplay scene must have a hidden next-level prompt.");
        Scene completedScene = nextScreen.gameObject.scene;
        environment = EnvironmentFacade.ForScene(completedScene);
        PlayerInteraction actor = UnityEngine.Object.FindObjectOfType<PlayerInteraction>();
        foreach (Wire wire in UnityEngine.Object.FindObjectsOfType<Wire>())
        {
            typeof(Wire).GetField("maxLength", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wire, 0f);
        }
        PowerSocket outlet = environment.HeldWire.Socket;
        PolaritySocket dual = UnityEngine.Object.FindObjectsOfType<PolaritySocket>().First(socket => socket.IsDual);
        nextScreen.enabled = false;
        dual.Interact(new InteractionDetails(actor.gameObject, dual.gameObject));
        outlet.Interact(new InteractionDetails(actor.gameObject, outlet.gameObject));
        Require(environment.IsCircuitClosed && !Field<GameObject>(nextScreen, "panel").activeSelf,
            "A disabled completion screen must unsubscribe from victory events.");
        nextScreen.enabled = true;
        Require(Field<GameObject>(nextScreen, "panel").activeInHierarchy,
            "Re-enabling after victory must recover the current completion state.");
        UnityEngine.UI.Button nextButton = Field<UnityEngine.UI.Button>(nextScreen, "nextLevelButton");
        SceneSwitchManager switcher = CoreFacade.Instance.SceneSwitch;
        switcher.enabled = false;
        try
        {
            nextButton.onClick.Invoke();
            Require(!switcher.IsSwitching && nextButton.interactable &&
                Field<TMPro.TMP_Text>(nextScreen, "congratulationsText").text == "下一关加载失败，请重试。",
                "A rejected next-level request must show a Chinese error and remain retryable.");
        }
        finally
        {
            switcher.enabled = true;
        }
        nextButton.onClick.Invoke();
        Require(switcher.IsSwitching && !nextButton.interactable,
            "An accepted next-level request must lock the button until loading completes.");
        nextScreen.LoadNextLevel();
        yield return WaitForSwitch("GameplayIntegration");
        NextLevelScreen reloadedScreen = UnityEngine.Object.FindObjectOfType<NextLevelScreen>();
        Require(reloadedScreen != null && reloadedScreen.gameObject.scene.handle != completedScene.handle &&
            !Field<GameObject>(reloadedScreen, "panel").activeSelf &&
            !EnvironmentFacade.ForScene(reloadedScreen.gameObject.scene).IsCircuitClosed,
            "The configured next level must reload GameplayIntegration with a fresh circuit and hidden prompt.");
    }

    public static IEnumerator CleanupMenuFlow()
    {
        return IntegrationSceneWait.Finally(CleanupMenuScenes(), RestoreMenuState);
    }

    private static IEnumerator CleanupMenuScenes()
    {
        if (!ownsScenes)
        {
            yield break;
        }
        CapturePendingSwitch();
        if (pendingSceneOperation != null && !pendingSceneOperation.isDone)
        {
            pendingSceneOperation.allowSceneActivation = true;
            yield return IntegrationSceneWait.Operation(pendingSceneOperation, "finishing the owned menu load", 20f);
        }
        if (!testHostScene.IsValid() || !testHostScene.isLoaded)
        {
            testHostScene = SceneManager.CreateScene("MenuTestCleanup");
        }
        SceneManager.SetActiveScene(testHostScene);
        if (pendingUnload != null)
        {
            yield return IntegrationSceneWait.Operation(pendingUnload, "finishing the owned menu unload", 20f);
            Require(!unloadingScene.IsValid() || !unloadingScene.isLoaded,
                "Menu unload completed but its owned scene remains loaded.");
            pendingUnload = null;
        }
        for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene != testHostScene && IsMenuFlowScene(scene))
            {
                unloadingScene = scene;
                pendingUnload = SceneManager.UnloadSceneAsync(scene);
                yield return IntegrationSceneWait.Operation(pendingUnload, "unloading owned menu flow scene " + scene.name, 20f);
                Require(!scene.IsValid() || !scene.isLoaded, "Menu unload completed but its owned scene remains loaded.");
                pendingUnload = null;
            }
        }
    }

    private static void RestoreMenuState()
    {
        if (!ownsScenes)
        {
            return;
        }
        CapturePendingSwitch();
        bool loadPending = pendingSceneOperation != null && !pendingSceneOperation.isDone;
        IntegrationSceneWait.RestoreAll(
            () =>
            {
                if (CoreFacade.Instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(CoreFacade.Instance.gameObject);
                }
            },
            () => previousSceneState?.Restore(loadPending),
            () =>
            {
                bool sceneRemaining = false;
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    sceneRemaining |= IsMenuFlowScene(SceneManager.GetSceneAt(index));
                }
                if (!loadPending && !sceneRemaining && (pendingUnload == null || pendingUnload.isDone))
                {
                    pendingSceneOperation = null;
                    pendingUnload = null;
                    previousSceneState = null;
                    ownsScenes = false;
                    FinishDeferredProgressRelease();
                }
            });
    }

    private static bool IsMenuFlowScene(Scene scene)
    {
        return scene.name == "MainMenuScene" || scene.name == "GameplayIntegration" || scene.name == "SceneSwitchTarget";
    }

    private static void CapturePendingSwitch()
    {
        if (CoreFacade.Instance != null && CoreFacade.Instance.SceneSwitch != null)
        {
            AsyncOperation active = Field<AsyncOperation>(CoreFacade.Instance.SceneSwitch, "operation");
            if (active != null)
            {
                pendingSceneOperation = active;
            }
        }
    }

    private static void CheckSettingsSave(SettingsScreen screen)
    {
        FieldInfo settingsStoreField = typeof(SettingBootstrap).GetField("store", BindingFlags.Static | BindingFlags.NonPublic);
        object previousSettingsStore = settingsStoreField.GetValue(null);
        var configs = Field<AudioManagerConfigs>(CoreFacade.Instance.Audio, "configs");
        string[] parameters = { configs.MasterVolumeParameter, configs.OstVolumeParameter, configs.SfxVolumeParameter };
        float[] previousVolumes = new float[3];
        for (int i = 0; i < parameters.Length; i++)
        {
            Require(configs.Mixer.GetFloat(parameters[i], out previousVolumes[i]), "Mixer volume must be exposed.");
        }
        string directory = Path.Combine(Path.GetTempPath(), "PluggerHeadSettingsUI-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            var store = SettingsIntegrationChecks.CreateStore(path);
            settingsStoreField.SetValue(null, store);
            float[] requested = { 0.25f, 0.4f, 0.6f };
            Field<UnityEngine.UI.Slider>(screen, "masterVolume").value = requested[0];
            Field<UnityEngine.UI.Slider>(screen, "ostVolume").value = requested[1];
            Field<UnityEngine.UI.Slider>(screen, "sfxVolume").value = requested[2];
            screen.SaveSettings();
            Require(File.Exists(path), "Settings UI must persist its saved values.");
            for (int i = 0; i < parameters.Length; i++)
            {
                Require(configs.Mixer.GetFloat(parameters[i], out float actual) &&
                    Mathf.Abs(actual - 20f * Mathf.Log10(requested[i])) < 0.001f,
                    "Saving settings must immediately apply each real mixer bus.");
            }
        }
        finally
        {
            settingsStoreField.SetValue(null, previousSettingsStore);
            for (int i = 0; i < parameters.Length; i++)
            {
                configs.Mixer.SetFloat(parameters[i], previousVolumes[i]);
            }
            Directory.Delete(directory, true);
        }
    }

    private static IEnumerator WaitForSwitch(string name)
    {
        CapturePendingSwitch();
        Require(pendingSceneOperation != null, "The menu switch did not expose its actual operation.");
        yield return IntegrationSceneWait.Operation(pendingSceneOperation, "switching to " + name, 20f);
        yield return IntegrationSceneWait.Until(() => SceneManager.GetActiveScene().name == name &&
            CoreFacade.Instance != null && !CoreFacade.Instance.SceneSwitch.IsSwitching,
            "waiting for the switched scene " + name, 20f);
        yield return null;
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
