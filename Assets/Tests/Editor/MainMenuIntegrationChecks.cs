using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks the authored menu and real scene reloads with isolated in-memory progress.</summary>
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
        StoreField.SetValue(null, new LevelProgressStore());
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
            Require(store.IsUnlocked(1) && !store.IsUnlocked(2) && !store.IsUnlocked(3),
                "Only the first level must initially be unlocked.");
            Require(!store.CompleteLevel(2) && !store.CompleteLevel(3) &&
                !store.CompleteLevel(0) && !store.CompleteLevel(4), "Invalid and locked completions must be rejected.");
            Require(store.CompleteLevel(1) && store.IsUnlocked(2) && !store.IsUnlocked(3),
                "Completing the first level unlocks only the second.");
            Require(store.CompleteLevel(1) && store.CompletedLevels == 1, "Replaying must not advance progress.");
            Require(store.CompleteLevel(2) && store.IsUnlocked(3), "Completing the second unlocks the third.");
            Require(store.CompleteLevel(3) && store.CompleteLevel(1) && store.CompletedLevels == 3,
                "Replaying earlier levels must preserve progress.");
            var restarted = new LevelProgressStore();
            Require(restarted.CompletedLevels == 0 && !restarted.IsUnlocked(2), "A new run must not recover progress.");
            typeof(GameProgress).GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Require(GameProgress.Store.CompletedLevels == 0 && !ReferenceEquals(GameProgress.Store, store),
                "Runtime initialization must reset static progress even without domain reload.");
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
        foreach (string sceneName in new[] { "MainMenuScene", "HubScene", "GameplayIntegration", "SceneSwitchTarget", "Level0" })
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
        ISoundHandle menuMusic = CheckBackgroundMusic(AudioId.GameMainMenu);
        AudioSource menuSource = Field<AudioEmitter>(menuMusic, "emitter").GetComponent<AudioSource>();
        PlayerMove player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        Require(player == null && UnityEngine.Object.FindObjectOfType<Portal>() == null &&
            UnityEngine.Object.FindObjectOfType<EnvironmentFacade>() == null, "Main menu must contain no gameplay objects.");
        Require(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1,
            "Menu must have one EventSystem.");
        Require(menu.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Menu canvas must receive pointer input.");
        foreach (string field in new[] { "newGameButton", "settingsButton", "exitButton" })
        {
            var button = Field<UnityEngine.UI.Button>(menu, field);
            Require(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == menu,
                field + " must target the authored menu controller.");
        }
        Field<UnityEngine.UI.Button>(menu, "settingsButton").onClick.Invoke();
        var settings = Field<SettingsScreen>(menu, "settingsScreen");
        Require(settings.gameObject.activeSelf && GameStateManager.Current == GameState.Freezed,
            "Settings button must open the existing settings panel.");
        yield return null;
        int pausedMenuSample = menuSource.timeSamples;
        yield return null;
        yield return null;
        Require(menuSource.timeSamples == pausedMenuSample && ReferenceEquals(menuMusic, CheckBackgroundMusic(AudioId.GameMainMenu)),
            "Menu settings must pause the same BGM voice without resetting its position.");
        CheckSettingsSave(settings);
        menu.NewGame();
        Require(menu.gameObject.activeSelf && !CoreFacade.Instance.SceneSwitch.IsSwitching, "Settings must block starting.");
        settings.ContinueGame();
        yield return IntegrationSceneWait.Until(() => menuSource.timeSamples != pausedMenuSample,
            "resuming the menu music after settings", 5f);
        Require(ReferenceEquals(menuMusic, CheckBackgroundMusic(AudioId.GameMainMenu)),
            "Continuing must preserve the same music handle.");
        GameStateManager.Freeze();
        menu.NewGame();
        Require(!CoreFacade.Instance.SceneSwitch.IsSwitching, "Manual pause must block starting.");
        GameStateManager.Resume();
        CoreFacade.Instance.SceneSwitch.enabled = false;
        try
        {
            menu.NewGame();
            Require(menu.gameObject.activeSelf && !CoreFacade.Instance.SceneSwitch.IsSwitching &&
                Field<TMPro.TMP_Text>(menu, "startStatus").text == "关卡加载失败，请重试。",
                "Rejected start must show a Chinese error and retain the menu for retry.");
            Require(ReferenceEquals(menuMusic, CheckBackgroundMusic(AudioId.GameMainMenu)),
                "Rejected start must preserve menu music.");
        }
        finally
        {
            CoreFacade.Instance.SceneSwitch.enabled = true;
        }
        Field<UnityEngine.UI.Button>(menu, "newGameButton").onClick.Invoke();
        menu.NewGame();
        yield return WaitForSwitch("HubScene");
        CheckBackgroundMusic(EnvironmentFacade.Current.BackgroundMusic);
        Require(CoreFacade.Instance.Audio.Registry.CountOf(AudioId.GameMainMenu) == 0,
            "The hub music must replace the menu track.");
        player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        Require(player != null && !player.IsInputLocked && UnityEngine.Object.FindObjectOfType<MainMenuScreen>() == null,
            "Starting must load an independently playable hub without the main menu overlay.");
        Portal[] portals = UnityEngine.Object.FindObjectsOfType<Portal>().OrderBy(p => p.transform.position.x).ToArray();
        Require(portals.Length == 3 && portals.Select(p => p.LevelNumber).SequenceEqual(new[] { 1, 2, 3 }),
            "Authored portals must represent levels one to three from left to right.");
        var hubScene = SceneManager.GetActiveScene();
        settings = Resources.FindObjectsOfTypeAll<SettingsScreen>().Single(s => s.gameObject.scene == hubScene);
        var menuButton = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().Single(b => b.name == "MenuBtn");
        Require(menuButton.onClick.GetPersistentEventCount() == 1 &&
            menuButton.onClick.GetPersistentTarget(0) == settings &&
            menuButton.onClick.GetPersistentMethodName(0) == nameof(SettingsScreen.Open) &&
            menuButton.GetComponentInChildren<TMPro.TMP_Text>().text == "菜单",
            "Hub MenuBtn must open its own settings panel and display Chinese text.");
        menuButton.onClick.Invoke();
        Require(settings.gameObject.activeInHierarchy && GameStateManager.Current == GameState.Freezed && Time.timeScale == 0f,
            "Hub MenuBtn must pause gameplay.");
        Require(Field<UnityEngine.UI.Button>(settings, "exitButton").gameObject.activeInHierarchy &&
            Field<UnityEngine.UI.Button>(settings, "exitButton").interactable,
            "Hub settings must show an interactive return-to-main-menu button.");
        portals[0].Interact(new InteractionDetails(player.gameObject, portals[0].gameObject));
        Require(!CoreFacade.Instance.SceneSwitch.IsSwitching, "Paused hub must reject portal entry.");
        settings.ContinueGame();
        Require(GameStateManager.Current == GameState.Playing && !settings.gameObject.activeSelf,
            "Continue must resume the hub.");
        Require(portals[0].CanInteract && !portals[1].CanInteract && !portals[2].CanInteract,
            "Only the implemented first portal can be used.");
        var destinationField = typeof(Portal).GetField("hasDestination", BindingFlags.Instance | BindingFlags.NonPublic);
        destinationField.SetValue(portals[1], true);
        Require(!portals[1].CanInteract, "Even a configured second portal must reject entry before the first is cleared.");
        destinationField.SetValue(portals[1], false);
        for (int i = 1; i < portals.Length; i++)
        {
            portals[i].Interact(new InteractionDetails(player.gameObject, portals[i].gameObject));
        }
        Require(!CoreFacade.Instance.SceneSwitch.IsSwitching, "Unimplemented portals must not initiate loading.");
        player.GetComponent<Rigidbody2D>().simulated = false;
        Collider2D unopenedDoor = portals[1].GetComponent<Collider2D>();
        player.transform.position = unopenedDoor.bounds.center - Vector3.right * unopenedDoor.bounds.extents.x;
        Physics2D.SyncTransforms();
        Require(!player.GetComponent<PlayerInteraction>().TryPerformOperation() && !CoreFacade.Instance.SceneSwitch.IsSwitching,
            "Interacting at the unopened door must not accidentally enter its neighbor.");
        player.transform.position = portals[0].transform.position;
        Physics2D.SyncTransforms();
        Require(player.GetComponent<PlayerInteraction>().TryPerformOperation(),
            "The player's normal J interaction path must reach the first portal.");
        yield return WaitForSwitch("Level0");
        Require(GameProgress.Store.CompletedLevels == 0, "Merely entering a level must not unlock the next.");
        Require(UnityEngine.Object.FindObjectOfType<LevelProgressTracker>() != null, "Level0 must track actual victory.");
        var environment = EnvironmentFacade.Current;
        // The authored Level0 layout is deliberately unchanged. In this owned runtime scene,
        // use a minimal valid circuit to verify victory -> progress -> hub independently of level design.
        PolaritySocket fixtureSocket = UnityEngine.Object.FindObjectsOfType<PolaritySocket>().First(s => s.IsDual);
        foreach (PolaritySocket socket in UnityEngine.Object.FindObjectsOfType<PolaritySocket>())
        {
            if (socket != fixtureSocket)
            {
                UnityEngine.Object.DestroyImmediate(socket.gameObject);
            }
        }
        environment.RefreshNodes();
        typeof(EnvironmentFacade).GetField("neededVoltage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(environment, 0f);
        foreach (Wire wire in UnityEngine.Object.FindObjectsOfType<Wire>())
        {
            typeof(Wire).GetField("maxLength", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wire, 0f);
        }
        player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        PowerSocket levelOutlet = environment.HeldWire.Socket;
        foreach (PolaritySocket socket in UnityEngine.Object.FindObjectsOfType<PolaritySocket>().Where(s => s.IsDual))
        {
            socket.Interact(new InteractionDetails(player.gameObject, socket.gameObject));
        }
        levelOutlet.Interact(new InteractionDetails(player.gameObject, levelOutlet.gameObject));
        Require(environment.IsCircuitClosed && GameProgress.Store.IsUnlocked(2) && !GameProgress.Store.IsUnlocked(3),
            "Actual Level0 victory must unlock only level two.");
        var levelPrompt = UnityEngine.Object.FindObjectOfType<NextLevelScreen>();
        Require(levelPrompt != null, "Level0 must offer a completion action.");
        levelPrompt.LoadNextLevel();
        yield return WaitForSwitch("HubScene");
        player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        Require(!player.IsInputLocked && GameProgress.Store.IsUnlocked(2), "Returning to the hub must preserve progress.");
        settings = Resources.FindObjectsOfTypeAll<SettingsScreen>().Single(s => s.gameObject.scene == SceneManager.GetActiveScene());
        UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().Single(b => b.name == "MenuBtn").onClick.Invoke();
        Field<UnityEngine.UI.Button>(settings, "exitButton").onClick.Invoke();
        yield return WaitForSwitch("MainMenuScene");
        yield return null;
        CheckBackgroundMusic(AudioId.GameMainMenu);
        Require(CoreFacade.Instance.Audio.Registry.CountOf(AudioId.DefaultOst) == 0,
            "Returning to the menu must replace the hub track.");
        Require(GameProgress.Store.IsUnlocked(2) && GameStateManager.Current == GameState.Playing,
            "Returning to main menu must release the hub pause and preserve session progress.");
        menu = UnityEngine.Object.FindObjectOfType<MainMenuScreen>();
        menu.NewGame();
        yield return WaitForSwitch("HubScene");
        CheckBackgroundMusic(EnvironmentFacade.Current.BackgroundMusic);
        Require(CoreFacade.Instance.Audio.Registry.CountOf(AudioId.GameMainMenu) == 0,
            "Starting again must replace menu music with the hub track.");
        player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        Require(GameProgress.Store.IsUnlocked(2), "Starting again must retain session progress.");
        portals = UnityEngine.Object.FindObjectsOfType<Portal>().OrderBy(p => p.LevelNumber).ToArray();
        Require(!portals[1].CanInteract && !portals[1].HasDestination,
            "Unlocked but unimplemented levels must remain unavailable.");
        destinationField.SetValue(portals[1], true);
        Require(portals[1].CanInteract, "A configured second portal must become available after the first is cleared.");
        destinationField.SetValue(portals[1], false);
        portals[0].Interact(new InteractionDetails(player.gameObject, portals[0].gameObject));
        yield return WaitForSwitch("Level0");
        Require(!EnvironmentFacade.Current.IsCircuitClosed && GameProgress.Store.IsUnlocked(2),
            "Re-entering a completed level must load its defaults and preserve unlocks.");

        // Retain completion-screen lifecycle, pause ownership and retry regression coverage.
        yield return CheckBackgroundMusicSwitch();
        NextLevelScreen nextScreen = UnityEngine.Object.FindObjectOfType<NextLevelScreen>();
        Require(nextScreen != null && !Field<GameObject>(nextScreen, "panel").activeSelf,
            "A fresh gameplay scene must have a hidden next-level prompt.");
        Scene completedScene = nextScreen.gameObject.scene;
        environment = EnvironmentFacade.ForScene(completedScene);
        // Use fixture voltages for completion UI; authored level balance can change independently.
        typeof(EnvironmentFacade).GetField("neededVoltage", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(environment, 0f);
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
        Require(GameStateManager.Current == GameState.Freezed && Time.timeScale == 0f,
            "The visible completion prompt must freeze gameplay.");
        settings = Resources.FindObjectsOfTypeAll<SettingsScreen>().Single(s => s.gameObject.scene == completedScene);
        settings.gameObject.SetActive(true);
        settings.ContinueGame();
        Require(GameStateManager.Current == GameState.Freezed && Time.timeScale == 0f,
            "Closing settings must preserve the visible completion prompt's freeze request.");
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

    public static IEnumerator CheckMenuRestart()
    {
        Require(CoreFacade.Instance == null && !ownsScenes, "Run restart checks in a fresh Runner session.");
        foreach (string sceneName in new[] { "MainMenuScene", "HubScene", "GameplayIntegration", "SceneSwitchTarget", "Level0" })
        {
            Require(!SceneManager.GetSceneByName(sceneName).isLoaded,
                "Refusing to modify a pre-existing scene: " + sceneName);
        }
        previousSceneState = new IntegrationSceneState();
        testHostScene = SceneManager.GetActiveScene();
        ownsScenes = true;
        foreach (string path in new[] { "Assets/Tests/Scenes/GameplayIntegration.unity", "Assets/Scenes/Level0.unity" })
        {
            pendingSceneOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return IntegrationSceneWait.Operation(pendingSceneOperation, "loading restart fixture", 20f);
            yield return null;
            Scene scene = SceneManager.GetActiveScene();
            var settings = Resources.FindObjectsOfTypeAll<SettingsScreen>().Single(s => s.gameObject.scene == scene);
            var death = UnityEngine.Object.FindObjectOfType<RestartLevelScreen>();
            PlayerMove player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
            Require(player != null && !player.IsDead, "Restart fixture must start with a living player.");
            Vector3 initialPosition = player.transform.position;
            var marker = new GameObject("MenuRestartRuntimeMarker");
            SceneManager.MoveGameObjectToScene(marker, scene);
            player.GetComponent<Rigidbody2D>().simulated = false;
            player.transform.position += Vector3.right * 2f;
            var menuButton = death.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "MenuButton");
            menuButton.onClick.Invoke();
            var button = Field<UnityEngine.UI.Button>(settings, "restartButton");
            Require(settings.gameObject.activeInHierarchy && Time.timeScale == 0f &&
                button != null && button.gameObject.activeInHierarchy && button.interactable,
                "Menu must open a paused panel with an enabled restart button.");
            Require(button.GetComponentInChildren<TMPro.TMP_Text>().text == "重开关卡" &&
                button.onClick.GetPersistentTarget(0) == settings &&
                button.onClick.GetPersistentMethodName(0) == "RestartLevel",
                "Authored restart label and click target must match the menu controller.");
            Require(!Field<GameObject>(death, "panel").activeSelf, "Menu restart must not show a death prompt.");
            SceneSwitchManager switcher = CoreFacade.Instance.SceneSwitch;
            switcher.enabled = false;
            try
            {
                button.onClick.Invoke();
                Require(settings.gameObject.activeInHierarchy && button.interactable && Time.timeScale == 0f &&
                    !player.IsDead && Field<TMPro.TMP_Text>(settings, "saveStatus").text == "重新开始失败，请重试。",
                    "Rejected restart must retain pause, show a Chinese error, and allow retry without killing the player.");
            }
            finally
            {
                switcher.enabled = true;
            }
            button.onClick.Invoke();
            Require(switcher.IsSwitching && !button.interactable && !settings.gameObject.activeSelf,
                "Accepted restart must close the menu and block repeated clicks.");
            AsyncOperation accepted = Field<AsyncOperation>(switcher, "operation");
            button.onClick.Invoke();
            Require(ReferenceEquals(accepted, Field<AsyncOperation>(switcher, "operation")),
                "Repeated clicks must not replace the in-flight reload.");
            yield return WaitForSwitch(scene.name);
            Scene fresh = SceneManager.GetActiveScene();
            player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
            Require(fresh.path == path && fresh.handle != scene.handle && marker == null &&
                player != null && !player.IsDead && player.GetComponent<Rigidbody2D>().simulated &&
                Mathf.Abs(player.transform.position.x - initialPosition.x) < 0.1f &&
                Time.timeScale > 0f && GameStateManager.Current == GameState.Playing && !AudioListener.pause,
                "Menu restart must reload its own level, discard runtime state and release pause.");

            // The death prompt must share the same target and still work after the menu refactor.
            death = UnityEngine.Object.FindObjectOfType<RestartLevelScreen>();
            player.Die();
            Require(Field<GameObject>(death, "panel").activeInHierarchy && Time.timeScale == 0f,
                "Death must still show and freeze its prompt.");
            Field<UnityEngine.UI.Button>(death, "restartButton").onClick.Invoke();
            yield return WaitForSwitch(fresh.name);
            Require(SceneManager.GetActiveScene().path == path &&
                !UnityEngine.Object.FindObjectOfType<PlayerMove>().IsDead && Time.timeScale > 0f,
                "Death restart must return to the same fresh, unpaused level.");
        }
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
        return scene.name == "MainMenuScene" || scene.name == "HubScene" || scene.name == "GameplayIntegration" ||
            scene.name == "SceneSwitchTarget" || scene.name == "Level0";
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

    private static IEnumerator CheckBackgroundMusicSwitch()
    {
        CoreFacade core = CoreFacade.Instance;
        EnvironmentFacade environment = EnvironmentFacade.Current;
        ISoundHandle music = Field<ISoundHandle>(environment, "musicHandle");
        Require(music != null && music.IsPlaying, "Live level must have a BGM before switching.");
        AudioSource source = Field<AudioEmitter>(music, "emitter").GetComponent<AudioSource>();
        AudioClip authoredClip = source.clip;
        // Own the clip and seek point so continuity does not depend on production music length.
        AudioClip fixtureClip = AudioClip.Create("BgmSwitchFixture", 480000, 1, 8000, false);
        try
        {
            source.clip = fixtureClip;
            source.Play();
            source.timeSamples = 80000;
            yield return new WaitForSecondsRealtime(0.1f);
            GameStateManager.Freeze();
            int beforeSwitch = source.timeSamples;
            Require(ReferenceEquals(music, core.Audio.PlayBackgroundMusic(environment.BackgroundMusic)),
                "A paused BGM request must reuse its handle, not restart or reject it.");
            yield return new WaitForSecondsRealtime(0.1f);
            Require(source.timeSamples == beforeSwitch, "Reusing paused BGM must preserve playback position.");
            GameStateManager.Resume();

            core.SceneSwitch.enabled = false;
            Require(core.SceneSwitch.RequestSwitch(SceneId.SceneSwitchTarget) == null && music.IsPlaying,
                "A rejected scene switch must leave BGM playing.");
            core.SceneSwitch.enabled = true;
            Require(core.SceneSwitch.RequestSwitch(SceneId.SceneSwitchTarget) != null && music.IsPlaying,
                "Entering Loading must not stop BGM.");
            yield return WaitForSwitch("SceneSwitchTarget");
            Require(environment == null && core == CoreFacade.Instance && music.IsPlaying &&
                source.clip == fixtureClip && source.timeSamples > beforeSwitch,
                "BGM must advance through scene unloading and a destination with no Env.");

            beforeSwitch = source.timeSamples;
            Require(core.SceneSwitch.RequestSwitch(SceneId.GameplayIntegration) != null, "Second switch must start.");
            yield return WaitForSwitch("GameplayIntegration");
            environment = EnvironmentFacade.Current;
            Require(ReferenceEquals(music, Field<ISoundHandle>(environment, "musicHandle")) && music.IsPlaying &&
                source.clip == fixtureClip && source.timeSamples > beforeSwitch,
                "The new level must adopt the same BGM source and advancing position.");
            Require(core.Audio.Registry.CountOf(music.AudioId) == 1,
                "Scene switches and duplicate Core instances must not stack BGM voices.");
        }
        finally
        {
            if (core && core.SceneSwitch)
            {
                core.SceneSwitch.enabled = true;
            }
            if (source)
            {
                source.clip = authoredClip;
                source.Play();
            }
            UnityEngine.Object.DestroyImmediate(fixtureClip);
        }
    }

    private static ISoundHandle CheckBackgroundMusic(AudioId expected)
    {
        AudioManager audio = CoreFacade.Instance.Audio;
        ISoundHandle music = Field<ISoundHandle>(audio, "backgroundMusic");
        Require(music != null && music.AudioId == expected && audio.Registry.CountOf(expected) == 1,
            "The scene must automatically select exactly one expected BGM voice.");
        var configs = Field<AudioManagerConfigs>(audio, "configs");
        Require(configs.TryGetClip(expected, out AudioClipData data) &&
            Field<AudioEmitter>(music, "emitter").GetComponent<AudioSource>().clip == data.Clip,
            "Automatic BGM must use the registered scene music clip.");
        return music;
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
