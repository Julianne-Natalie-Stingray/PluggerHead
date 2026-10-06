using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Reusable checks for the authored integration scenes; never saves a scene.</summary>
public static class SceneIntegrationChecks
{
    private const string GameplayScenePath = "Assets/Scenes/Tests/GameplayIntegration.unity";
    private const float TimeoutSeconds = 15f;
    private static Scene ownedScene;
    private static Scene previousScene;
    private static EnvironmentFacade previousEnvironment;
    private static CoreFacade ownedCore;
    private static AsyncOperation pendingLoad;
    private static bool gameplayStarted;
    private static AsyncOperation pendingUnload;
    private static IntegrationSceneState previousState;
    public static bool HasPendingCleanup => gameplayStarted;

    public static void CheckSceneRegistry()
    {
        SceneSwitchConfigs configs = AssetDatabase.LoadAssetAtPath<SceneSwitchConfigs>(
            "Assets/SO/SceneSwitch/DefaultSceneSwitchConfigs.asset");
        Require(configs != null, "Default scene switch configuration must exist.");
        string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        SceneId[] ids = (SceneId[])Enum.GetValues(typeof(SceneId));
        Require(scenePaths.Length == ids.Length, "Every project scene must have a functional SceneId.");
        SerializedProperty entries = new SerializedObject(configs).FindProperty("scenes");
        Require(entries.arraySize == ids.Length, "Scene configuration must contain exactly one entry per SceneId.");
        HashSet<string> mappedPaths = new HashSet<string>();
        foreach (SceneId id in ids)
        {
            Require(configs.TryGetSceneName(id, out string name), $"Missing mapping for {id}.");
            string path = scenePaths.SingleOrDefault(candidate => System.IO.Path.GetFileNameWithoutExtension(candidate) == name);
            Require(mappedPaths.Add(path) && scenePaths.Contains(path), $"Invalid or duplicate scene mapping: {id}.");
            Require(EditorBuildSettings.scenes.Count(scene => scene.enabled && scene.path == path) == 1,
                $"Build Settings must enable {path} exactly once.");
        }
        Require(EditorBuildSettings.scenes.All(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null),
            "Build Settings must not reference deleted scenes.");
        Require(EditorBuildSettings.scenes.First(scene => scene.enabled).path == "Assets/Scenes/MainMenuScene.unity",
            "The build must start in the main menu.");
    }

    public static void CheckSceneAsset(string path)
    {
        Require(!Application.isPlaying, "Scene asset checks require Edit Mode.");
        Require(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path),
            $"Build Settings must enable {path}.");
        Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, $"Scene asset is missing: {path}.");

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool ownsScene = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (ownsScene)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                        $"{path}: missing script on {child.name}.");
                    Require(!PrefabUtility.IsPrefabAssetMissing(child.gameObject),
                        $"{path}: missing prefab on {child.name}.");
                }
                foreach (Wire wire in root.GetComponentsInChildren<Wire>(true))
                {
                    LineRenderer renderer = wire.GetComponent<LineRenderer>();
                    Require(renderer != null && renderer.sharedMaterial != null &&
                        renderer.sharedMaterial.shader != null && renderer.sharedMaterial.shader.isSupported,
                        $"{path}: {wire.name} needs a supported wire material.");
                }
                foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    Require(label.font != null && label.font.material != null,
                        $"{path}: {label.name} needs a font and material.");
                    string characters = new string(label.text.Where(character => !char.IsControl(character)).ToArray());
                    Require(label.font.HasCharacters(characters),
                        $"{path}: {label.name} must have baked glyphs for its authored text.");
                    Require(label.font.HasCharacters("剩余线长：不限--0123456789.重新开始失败，请重试。关卡加载失败，请重试。返回主菜单失败，请重试。设置已保存保存失败，请重试。下一关加载失败，请重试。"),
                        $"{path}: {label.name} must support Chinese runtime status and error messages.");
                }
            }

            Require(FindComponents<Camera>(scene).Count == 1 && FindComponents<AudioListener>(scene).Count == 1,
                $"{path}: each scene must provide one camera and listener.");
            bool switchTarget = path.EndsWith("/SceneSwitchTarget.unity", StringComparison.Ordinal);
            Require(FindComponents<CoreFacade>(scene).Count == (switchTarget ? 0 : 1),
                $"{path}: content scenes need one Core; the switch target must rely on the persistent Core.");
            if (path.EndsWith("/CircuitDiagnostics.unity", StringComparison.Ordinal))
            {
                Require(FindComponents<EnvironmentFacade>(scene).Count == 1 &&
                    FindComponents<PlayerInteraction>(scene).Count == 0 &&
                    FindComponents<Transform>(scene).Count(item => item.CompareTag("Player")) == 1 &&
                    FindComponents<PowerSocket>(scene).Any(socket => socket.StartingWire != null && socket.Wires.Count >= 2),
                    "Circuit diagnostics must retain its mock player and configured circuit, independent of real Player controls.");
            }

            if (path.EndsWith("/FinalScene.unity", StringComparison.Ordinal))
            {
                FinalSceneScreen screen = FindComponents<FinalSceneScreen>(scene).Single();
                UnityEngine.UI.Button exit = FindComponents<UnityEngine.UI.Button>(scene).Single();
                Require(exit.interactable && exit.targetGraphic != null && exit.targetGraphic.raycastTarget &&
                    exit.onClick.GetPersistentEventCount() == 1 &&
                    exit.onClick.GetPersistentTarget(0) == screen &&
                    exit.onClick.GetPersistentMethodName(0) == nameof(FinalSceneScreen.ExitGame),
                    "FinalScene Exit must be interactive and wired to its exit action.");
                Require(FindComponents<UnityEngine.EventSystems.EventSystem>(scene).Count == 1 &&
                    FindComponents<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(scene).Count == 1 &&
                    FindComponents<UnityEngine.UI.GraphicRaycaster>(scene).Count == 1,
                    "FinalScene must receive UI input.");
                List<TMPro.TMP_Text> labels = FindComponents<TMPro.TMP_Text>(scene);
                Require(labels.Any(label => label.name == "CongratulationsTitle" && label.text.Contains("恭喜通关")) &&
                    labels.Any(label => label.name == "CreditsHeading" && label.text.Contains("示例")) &&
                    labels.Any(label => label.name == "SampleCredits" && label.text.Contains("程序")),
                    "FinalScene must contain congratulations and clearly identified sample credits.");
                Require(labels.All(label => label.font != null && label.gameObject.activeInHierarchy &&
                    label.rectTransform.rect.width > 0 && label.rectTransform.rect.height > 0),
                    "FinalScene text must have fonts and visible dimensions.");
                SceneSwitchConfigs finalConfigs = AssetDatabase.LoadAssetAtPath<SceneSwitchConfigs>(
                    "Assets/SO/SceneSwitch/DefaultSceneSwitchConfigs.asset");
                Require(!finalConfigs.IsGameplayLevel(SceneId.FinalScene), "Credits must not replace saved gameplay progress.");
            }

            if (path == GameplayScenePath)
            {
                Require(FindComponents<CoreFacade>(scene).Count == 1, "Gameplay scene must contain one configured Core.");
                Require(FindComponents<EnvironmentFacade>(scene).Count == 1, "Gameplay scene must contain one environment.");
                Require(FindComponents<PlayerInteraction>(scene).Count == 1, "Gameplay scene must contain the real Player.");
                PlayerInteraction player = FindComponents<PlayerInteraction>(scene).Single();
                Anchor anchorPrefab = (Anchor)typeof(PlayerInteraction)
                    .GetField("anchorPrefab", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                Require(anchorPrefab != null && AssetDatabase.Contains(anchorPrefab) &&
                    anchorPrefab.GetComponentInChildren<Collider2D>(true) != null,
                    "Gameplay Player must reference a reclaimable anchor prefab with a collider.");
                Require(FindComponents<PowerSocket>(scene).Any(socket => socket.StartingWire != null &&
                    socket.Wires.Count >= 2), "Gameplay outlet must reference its authored wires.");
                Require(FindComponents<UnityEngine.Tilemaps.TilemapCollider2D>(scene).Count == 1,
                    "Gameplay ground must use an authored Tilemap collider.");
            }
            if (path == GameplayScenePath || path.EndsWith("/CircuitDiagnostics.unity", StringComparison.Ordinal))
            {
                EnvironmentFacade environment = FindComponents<EnvironmentFacade>(scene).Single();
                Require(environment.RoutingTilemap != null && environment.RoutingTilemap.GetUsedTilesCount() > 0,
                    "Both circuit scenes must bind an authored, painted routing Tilemap.");
                foreach (Component node in FindComponents<PowerSocket>(scene).Cast<Component>()
                    .Concat(FindComponents<PolaritySocket>(scene)).Concat(FindComponents<Anchor>(scene)))
                {
                    Require(environment.TryGetTilePosition(node.transform.position, out Vector3 center) &&
                        Vector3.Distance(node.transform.position, center) < 0.0001f,
                        "Every socket and Anchor must occupy a painted tile center.");
                }
            }

        }
        finally
        {
            if (ownsScene && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
            }
        }
    }

    public static IEnumerator CheckGameplay()
    {
        return IntegrationSceneWait.Finally(CheckGameplayBody(), () => { });
    }

    private static IEnumerator CheckGameplayBody()
    {
        Require(Application.isPlaying, "Gameplay checks require Play Mode.");
        Require(!gameplayStarted, "A previous scene check still needs cleanup.");
        Require(CoreFacade.Instance == null, "Run in a fresh Test Runner session without an existing Core.");
        Require(!SceneManager.GetSceneByPath(GameplayScenePath).isLoaded,
            "The gameplay scene is already loaded; refusing to modify an unowned scene.");
        Require(Time.timeScale > 0f && GameStateManager.Current == GameState.Playing,
            "Run gameplay checks in an unpaused Playing state.");

        previousState = new IntegrationSceneState();
        previousScene = SceneManager.GetActiveScene();
        previousEnvironment = EnvironmentFacade.Current;
        gameplayStarted = true;
        // The authored scene may deliberately start beyond its wire limit. Keep the interaction
        // scenario unrestricted in this test-owned copy, then test lethal length after restart.
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> configureWireLimits = (scene, mode) =>
        {
            if (scene.path == GameplayScenePath)
            {
                foreach (Wire wire in FindComponents<Wire>(scene))
                {
                    typeof(Wire).GetField("maxLength", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(wire, 0f);
                }
            }
        };
        SceneManager.sceneLoaded += configureWireLimits;
        try
        {
            pendingLoad = SceneManager.LoadSceneAsync(GameplayScenePath, LoadSceneMode.Additive);
            yield return WaitForOperation(pendingLoad, "loading the gameplay scene");
        }
        finally
        {
            SceneManager.sceneLoaded -= configureWireLimits;
        }
        ownedScene = SceneManager.GetSceneByPath(GameplayScenePath);
        ownedCore = CoreFacade.Instance;
        Require(ownedScene.IsValid() && ownedScene.isLoaded, "Gameplay scene did not load.");
        SceneManager.SetActiveScene(ownedScene);

        int initialFrame = Time.frameCount;
        yield return null;
        yield return null;
        Require(Time.frameCount > initialFrame, "Gameplay frames must advance.");
        PlayerInteraction interaction = FindComponents<PlayerInteraction>(ownedScene).Single();
        PlayerMove movement = interaction.GetComponent<PlayerMove>();
        EnvironmentFacade environment = FindComponents<EnvironmentFacade>(ownedScene).Single();
        RestartLevelScreen restartScreen = FindComponents<RestartLevelScreen>(ownedScene).Single();
        Transform restartPanel = restartScreen.transform.Find("RestartLevelScreen");
        restartScreen.enabled = false;
        typeof(RestartLevelScreen).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(restartScreen, null);
        restartScreen.enabled = true;
        restartScreen.FindPlayer();
        Require((PlayerMove)typeof(RestartLevelScreen).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(restartScreen) == movement,
            "GlobalUI must automatically bind the Player in its own scene when no reference is assigned.");
        NextLevelScreen nextScreen = FindComponents<NextLevelScreen>(ownedScene).Single();
        Transform nextPanel = nextScreen.transform.Find("NextLevelScreen");
        Require(nextPanel != null && !nextPanel.gameObject.activeSelf,
            "The next-level prompt must be hidden before the environment declares victory.");
        nextScreen.LoadNextLevel();
        Require(!ownedCore.SceneSwitch.IsSwitching, "The next-level action must reject calls before victory.");
        Require(movement.GetComponents<MonoBehaviour>().All(component => component.GetType().Name != "PlayerInventory") &&
            restartScreen.transform.Find("InventoryUI") == null,
            "The master gameplay scene must run without inventory components or inventory UI.");
        Require(restartPanel != null && !restartPanel.gameObject.activeSelf,
            "The restart prompt must remain hidden while the player is alive.");
        Require(ownedCore != null && ownedCore.Input != null && ownedCore.Audio != null,
            "Authored Core services must initialize.");
        Require(interaction.isActiveAndEnabled && movement != null && movement.isActiveAndEnabled &&
            !movement.IsDead && EnvironmentFacade.ForScene(movement.gameObject.scene) == environment,
            "Player startup must bind to its real environment and keep both controllers enabled.");
        Wire initialWire = environment.HeldWire;
        yield return CheckVisualFacing(movement);
        Require(initialWire != null && initialWire.IsHeld && !environment.IsCircuitClosed,
            "The environment must start with an open circuit and a held wire.");

        WireLengthDisplay lengthDisplay = FindComponents<WireLengthDisplay>(ownedScene).Single();
        TMPro.TMP_Text lengthText = lengthDisplay.transform.Find("RemainingLengthText").GetComponent<TMPro.TMP_Text>();
        TMPro.TMP_Text scoreText = lengthDisplay.transform.Find("ScoreText").GetComponent<TMPro.TMP_Text>();
        Require(lengthText.text == "剩余线长：不限",
            "An unrestricted carried wire must display the Chinese unlimited label.");
        Require(scoreText.text == string.Empty && scoreText.rectTransform.rect.width >= 300f &&
            !scoreText.raycastTarget && !lengthText.raycastTarget,
            "The HUD must reserve an empty score column without intercepting input.");

        Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
        UnityEngine.Tilemaps.TilemapCollider2D ground =
            FindComponents<UnityEngine.Tilemaps.TilemapCollider2D>(ownedScene).Single();
        float landingDeadline = Time.realtimeSinceStartup + 4f;
        while (!body.IsTouching(ground) && Time.realtimeSinceStartup < landingDeadline)
        {
            yield return new WaitForFixedUpdate();
        }
        Require(body.IsTouching(ground) && !movement.IsDead,
            "The real Player must land on the migrated Tilemap ground without falling through.");

        Anchor anchor = FindComponents<Anchor>(ownedScene).First(node => node.CanReclaim);
        Vector3 anchorPosition = anchor.transform.position;
        // Keep the authored Player at the target during this interaction check; movement physics
        // is covered separately. Only this test-owned scene is modified.
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        body.velocity = Vector2.zero;
        body.position = anchorPosition;
        interaction.transform.position = anchorPosition;
        Physics2D.SyncTransforms();
        RaiseInput(ownedCore.Input, "HandleSecondaryPressed");
        Require(!anchor.gameObject.activeSelf && !anchor.CanReclaim,
            "Reclaim must immediately deactivate the anchor without storing an inventory item.");
        Require(!anchor.TryReclaim(new InteractionDetails(interaction.gameObject, anchor.gameObject)),
            "A reclaimed anchor must reject a second request before deferred destruction.");
        yield return null;
        Require(anchor == null, "Play Mode reclaim must destroy the anchor at the end of the frame.");
        yield return WaitForUnlocked(movement);
        List<Anchor> existingAnchors = FindComponents<Anchor>(ownedScene);
        RaiseInput(ownedCore.Input, "HandleTertiaryPressed");
        Require(FindComponents<Anchor>(ownedScene).Count == existingAnchors.Count + 1,
            "The K input event must create one anchor without inventory stock.");
        anchor = FindComponents<Anchor>(ownedScene).Single(node => !existingAnchors.Contains(node));
        Require(anchor.EngagedBy == initialWire &&
            (anchor.transform.position - anchorPosition).sqrMagnitude < 0.0001f,
            "Placement must immediately route the held wire at the player position.");
        yield return null;
        LineRenderer line = initialWire.GetComponent<LineRenderer>();
        Require(line.positionCount == initialWire.TilePath.Cells.Count &&
            (line.GetPosition(line.positionCount - 1) - anchorPosition).sqrMagnitude < 0.0001f,
            "LateUpdate must render the visited tile centers through the placed Anchor.");
        yield return WaitForUnlocked(movement);
        Physics2D.SyncTransforms();
        Require(interaction.TryPerformOperation() && !anchor.gameObject.activeSelf,
            "J must reclaim a dynamically placed anchor.");
        yield return null;
        Require(anchor == null && line.positionCount == initialWire.TilePath.Cells.Count && environment.HeldWire == initialWire,
            "Reclaiming a routed anchor must update the wire path and preserve the held wire.");
        yield return WaitForUnlocked(movement);
        existingAnchors = FindComponents<Anchor>(ownedScene);
        Require(interaction.TryPlaceAnchor(), "Placement remains available after reclaiming an anchor.");
        anchor = FindComponents<Anchor>(ownedScene).Single(node => !existingAnchors.Contains(node));
        yield return WaitForUnlocked(movement);

        PolaritySocket dual = FindComponents<PolaritySocket>(ownedScene).First(node => node.IsDual && node.CanInteract);
        PowerSocket outlet = initialWire.Socket;
        int clearedCount = 0;
        Action onCleared = () => clearedCount++;
        environment.LevelCleared += onCleared;
        try
        {
            dual.Interact(new InteractionDetails(interaction.gameObject, dual.gameObject));
            Require(initialWire.PlugTarget == dual.transform && environment.HeldWire != null &&
                environment.HeldWire != initialWire && environment.SwapCount == 1,
                "The authored dual socket must hand over the second wire and count one swap.");
            typeof(Wire).GetField("maxLength", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(environment.HeldWire, 1000f);
            yield return null;
            yield return null;
            Require(lengthText.text.StartsWith("剩余线长：") && lengthText.text != "剩余线长：不限" &&
                lengthText.text != "剩余线长：--",
                "Swapping to a finite wire must refresh the carried wire display.");
            outlet.Interact(new InteractionDetails(interaction.gameObject, outlet.gameObject));
            Require(environment.IsCircuitClosed && environment.HeldWire == null && clearedCount == 1,
                "Returning the second wire to its outlet must clear the scene once.");
            Require(nextPanel.gameObject.activeInHierarchy &&
                nextPanel.GetComponentInChildren<UnityEngine.UI.Button>().interactable &&
                nextPanel.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("恭喜通关"),
                "The environment victory event must immediately show the Chinese completion prompt.");
            yield return null;
            yield return null;
            Require(lengthText.text == "剩余线长：--", "A completed circuit with no held wire must clear the length.");
            environment.EvaluateCircuit();
            Require(clearedCount == 1, "Repeated evaluation must not emit another clear event.");
            typeof(EnvironmentFacade).GetMethod("DebugRestartRun", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(environment, null);
            Require(environment.HeldWire == initialWire && initialWire.IsHeld && !environment.IsCircuitClosed &&
                environment.SwapCount == 0 && !anchor.IsEngaged && initialWire.PlugTarget == null,
                "Restart must reset the authored circuit, held wire and routing.");
            yield return null;
            Require(!nextPanel.gameObject.activeSelf, "An environment debug restart must clear the completion prompt.");
        }
        finally
        {
            environment.LevelCleared -= onCleared;
        }

        UnityEngine.Tilemaps.Tilemap map = environment.RoutingTilemap;
        typeof(Wire).GetField("maxLength", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(initialWire, 500f);
        Vector3 routeStart = map.GetCellCenterWorld(map.WorldToCell(interaction.transform.position));
        body.position = routeStart;
        movement.transform.position = routeStart;
        yield return null;
        int startCount = initialWire.TilePath.Cells.Count;
        float remainingAtStart = 500f - initialWire.TilePath.GetLength(map);
        Vector3Int startCell = map.WorldToCell(routeStart);
        Vector3 farTile = map.GetCellCenterWorld(startCell + Vector3Int.up * 4);
        body.position = farTile;
        movement.transform.position = farTile;
        yield return null;
        yield return null;
        Require(initialWire.TilePath.Cells.Count >= startCount + 4,
            "The real scene Player must extend the wire along every crossed tile.");
        Require(lengthText.text == "剩余线长：" + (remainingAtStart - 4f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            "Moving four unit tiles must reduce the displayed remaining length by four.");
        body.position = routeStart;
        movement.transform.position = routeStart;
        yield return null;
        yield return null;
        Require(initialWire.TilePath.Cells.Count == startCount,
            "Returning over the real scene Player's tile path must retract the same cells.");
        Require(lengthText.text == "剩余线长：" + remainingAtStart.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            "Backtracking must restore the displayed remaining length.");

        int diedCount = 0;
        Action onDied = () => diedCount++;
        movement.Died += onDied;
        try
        {
            typeof(Wire).GetField("maxLength", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(initialWire, 1f);
            body.position = (Vector2)initialWire.FixedEndPosition + Vector2.right * 100f;
            movement.transform.position = body.position;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Require(movement.IsDead && movement.IsInputLocked && movement.gameObject.activeSelf && !body.simulated && diedCount == 1,
                "Exceeding the real wire limit must kill the player and notify death in a physics frame.");
            Require(restartPanel.gameObject.activeInHierarchy &&
                restartPanel.GetComponentInChildren<UnityEngine.UI.Button>().interactable,
                "Player death must show an actionable restart prompt.");
            Require(GameStateManager.Current == GameState.Freezed && Time.timeScale == 0f,
                "The visible restart prompt must freeze gameplay.");
            restartScreen.enabled = false;
            Require(!restartPanel.gameObject.activeSelf, "Disabling the restart screen must hide its prompt.");
            Require(GameStateManager.Current == GameState.Playing && Time.timeScale > 0f,
                "Hiding the only visible prompt must release its freeze request.");
            typeof(RestartLevelScreen).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(restartScreen, null);
            restartScreen.enabled = true;
            Require(restartPanel.gameObject.activeInHierarchy,
                "Re-enabling GlobalUI must find the dead Player and restore its prompt.");
            yield return null;
            Require(diedCount == 1, "Later frames must not repeat the death notification.");
            yield return null;
            yield return null;
            Require(lengthText.text == "剩余线长：0.0", "Exceeding the wire limit must display zero rather than a negative length.");
        }
        finally
        {
            movement.Died -= onDied;
        }
    }

    private static IEnumerator CheckVisualFacing(PlayerMove movement)
    {
        PlayerVisual visual = movement.GetComponentInChildren<PlayerVisual>();
        Require(visual != null && visual.name == "Visual", "The Player prefab must configure PlayerVisual on Visual.");
        SpriteRenderer sprite = visual.GetComponent<SpriteRenderer>();
        Transform facingMarker = visual.transform.Find("FacingMarker");
        Require(facingMarker != null, "The symmetric placeholder sprite needs a visible facing marker.");
        Vector3 rootScale = movement.transform.localScale;
        Transform attachment = movement.transform.Find("PlayerAnchor");
        Vector3 attachmentPosition = attachment.localPosition;
        Quaternion attachmentRotation = attachment.localRotation;
        PlayerControls controls = (PlayerControls)typeof(InputManager)
            .GetField("controls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(CoreFacade.Instance.Input);
        var previousDevices = controls.asset.devices;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        float previousTimeScale = Time.timeScale;
        try
        {
            controls.asset.devices = new InputDevice[] { keyboard };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Require(sprite.flipX && facingMarker.localPosition.x < 0f,
                "Left movement input must face the sprite and marker left.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Require(sprite.flipX, "Releasing input must preserve the last facing direction.");

            movement.LockInput();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Require(sprite.flipX, "Locked movement must not change sprite facing.");
            movement.UnlockInput();
            Time.timeScale = 0f;
            yield return null;
            yield return null;
            Require(sprite.flipX, "Paused movement must preserve sprite facing.");
            Time.timeScale = previousTimeScale;
            yield return new WaitForFixedUpdate();
            Require(!sprite.flipX && facingMarker.localPosition.x > 0f,
                "Right movement input must face the sprite and marker right after unlocking and resuming.");

            movement.enabled = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Require(!sprite.flipX, "A disabled movement component must not drive sprite facing.");
            movement.enabled = true;
            yield return new WaitForFixedUpdate();
            Require(sprite.flipX, "Re-enabled movement must respond to held input.");
            Require(movement.transform.localScale == rootScale && attachment.localPosition == attachmentPosition &&
                attachment.localRotation == attachmentRotation,
                "Facing changes must preserve the physics root and attachment transforms.");
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            movement.UnlockInput();
            movement.enabled = true;
            controls.asset.devices = previousDevices;
            InputSystem.RemoveDevice(keyboard);
        }
    }

    /// <summary>Called by UnityTearDown even if a runtime assertion fails.</summary>
    public static IEnumerator CleanupGameplay()
    {
        return IntegrationSceneWait.Finally(CleanupGameplayScene(), RestoreGameplayState);
    }

    private static IEnumerator CleanupGameplayScene()
    {
        if (!gameplayStarted)
        {
            yield break;
        }
        if (pendingLoad != null && !pendingLoad.isDone)
        {
            pendingLoad.allowSceneActivation = true;
            yield return WaitForOperation(pendingLoad, "finishing the owned scene load for cleanup");
        }
        if (!ownedScene.IsValid())
        {
            ownedScene = SceneManager.GetSceneByPath(GameplayScenePath);
        }
        if (previousScene.IsValid() && previousScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousScene);
        }
        if (pendingUnload == null && ownedScene.IsValid() && ownedScene.isLoaded)
        {
            pendingUnload = SceneManager.UnloadSceneAsync(ownedScene);
        }
        if (pendingUnload != null || (ownedScene.IsValid() && ownedScene.isLoaded))
        {
            yield return WaitForOperation(pendingUnload, "unloading the owned gameplay scene");
            Require(!ownedScene.IsValid() || !ownedScene.isLoaded, "Gameplay unload completed but its scene remains loaded.");
        }
    }

    private static void RestoreGameplayState()
    {
        if (!gameplayStarted)
        {
            return;
        }
        bool loadPending = pendingLoad != null && !pendingLoad.isDone;
        IntegrationSceneWait.RestoreAll(
            () =>
            {
                if (ownedCore == null && !loadPending)
                {
                    ownedCore = CoreFacade.Instance;
                }
                if (ownedCore != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedCore.gameObject);
                }
                ownedCore = null;
            },
            () => previousState?.Restore(loadPending),
            () =>
            {
                if (!loadPending && (pendingUnload == null || pendingUnload.isDone) &&
                    (!ownedScene.IsValid() || !ownedScene.isLoaded))
                {
                    ownedCore = null;
                    pendingLoad = null;
                    pendingUnload = null;
                    ownedScene = default;
                    previousState = null;
                    gameplayStarted = false;
                    MainMenuIntegrationChecks.FinishDeferredProgressRelease();
                }
            });
    }

    private static void RaiseInput(InputManager input, string handler)
    {
        typeof(InputManager).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(input, new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });
    }

    private static IEnumerator WaitForUnlocked(PlayerMove movement)
    {
        // Let the Animator process its interaction trigger before checking the lock.
        yield return null;
        yield return null;
        float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (movement.IsInputLocked)
        {
            Require(Time.realtimeSinceStartup < deadline, "Player interaction animation did not release its input lock.");
            yield return null;
        }
    }

    private static IEnumerator WaitForOperation(AsyncOperation operation, string description)
    {
        return IntegrationSceneWait.Operation(operation, description, TimeoutSeconds);
    }

    private static List<T> FindComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            components.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return components;
    }

    private static void Require(bool passed, string description)
    {
        if (!passed)
        {
            throw new InvalidOperationException(description);
        }
    }
}
