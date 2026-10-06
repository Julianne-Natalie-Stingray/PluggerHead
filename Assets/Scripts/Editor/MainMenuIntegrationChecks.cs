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

    public static void BeginProgressIsolation()
    {
        Require(temporaryDirectory == null, "Previous progress isolation was not cleaned up.");
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
        StoreField.SetValue(null, previousStore);
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, true);
        }
        temporaryDirectory = null;
        previousStore = null;
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
        Require(CoreFacade.Instance == null, "Run in a fresh Test Runner session.");
        testHostScene = SceneManager.GetActiveScene();
        ownsScenes = true;
        yield return SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Additive);
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
        Require(GameObject.Find("UnsavedRuntimeObject") == null && player.GetComponent<PlayerInventory>().Count == 0,
            "Continue must discard runtime objects and inventory.");
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
    }

    public static IEnumerator CleanupMenuFlow()
    {
        if (!ownsScenes)
        {
            yield break;
        }
        if (CoreFacade.Instance != null)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (CoreFacade.Instance.SceneSwitch.IsSwitching && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            UnityEngine.Object.Destroy(CoreFacade.Instance.gameObject);
        }
        if (!testHostScene.IsValid() || !testHostScene.isLoaded)
        {
            testHostScene = SceneManager.CreateScene("MenuTestCleanup");
        }
        SceneManager.SetActiveScene(testHostScene);
        for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene != testHostScene && (scene.name == "MainMenuScene" || scene.name == "GameplayIntegration" || scene.name == "SceneSwitchTarget"))
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
        if (GameStateManager.Current == GameState.Loading)
        {
            GameStateManager.ExitLoading();
        }
        if (GameStateManager.Current == GameState.Freezed)
        {
            GameStateManager.Resume();
        }
        ownsScenes = false;
        yield return null;
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
        float deadline = Time.realtimeSinceStartup + 20f;
        while (SceneManager.GetActiveScene().name != name || CoreFacade.Instance.SceneSwitch.IsSwitching)
        {
            Require(Time.realtimeSinceStartup < deadline, "Timed out switching to " + name);
            yield return null;
        }
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
