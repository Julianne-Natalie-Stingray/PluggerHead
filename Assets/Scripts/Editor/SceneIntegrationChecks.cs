using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                Require(labels.Any(label => label.name == "CongratulationsTitle" && label.text.Contains("CONGRATULATIONS")) &&
                    labels.Any(label => label.name == "CreditsHeading" && label.text.Contains("SAMPLE")) &&
                    labels.Any(label => label.name == "SampleCredits" && label.text.Contains("Programming")),
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
                BoxCollider2D ground = FindComponents<BoxCollider2D>(scene).Single(collider => collider.name == "Ground");
                List<Corner> corners = FindComponents<Corner>(scene);
                Require(corners.Count == 4, "Gameplay Ground must have four authored Corner instances.");
                foreach (Corner corner in corners)
                {
                    Vector2 local = ground.transform.InverseTransformPoint(corner.Center);
                    Vector2 delta = local - ground.offset;
                    Require(Mathf.Abs(Mathf.Abs(delta.x) - ground.size.x * 0.5f) < 0.0001f &&
                        Mathf.Abs(Mathf.Abs(delta.y) - ground.size.y * 0.5f) < 0.0001f &&
                        (corner.transform.lossyScale - Vector3.one).sqrMagnitude < 0.0001f &&
                        corner.DetectionCollider.isTrigger &&
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(corner.gameObject) == "Assets/Prefabs/Env/Corner.prefab",
                        "Each Corner prefab must sit at a Ground collider corner with compensated scale.");
                }
                Require(FindComponents<Wire>(scene).All(wire => wire.GetComponent<EdgeCollider2D>() != null &&
                    wire.GetComponent<EdgeCollider2D>().isTrigger), "Authored wires must include trigger EdgeCollider2D.");
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
        PlayerInventory inventory = interaction.GetComponent<PlayerInventory>();
        PlayerMove movement = interaction.GetComponent<PlayerMove>();
        EnvironmentFacade environment = FindComponents<EnvironmentFacade>(ownedScene).Single();
        Require(ownedCore != null && ownedCore.Input != null && ownedCore.Audio != null,
            "Authored Core services must initialize.");
        Require(interaction.isActiveAndEnabled && movement != null && movement.isActiveAndEnabled &&
            !movement.IsDead && inventory.Environment == environment,
            "Player startup must bind to its real environment and keep both controllers enabled.");
        Wire initialWire = environment.HeldWire;
        Require(initialWire != null && initialWire.IsHeld && !environment.IsCircuitClosed,
            "The environment must start with an open circuit and a held wire.");

        Anchor anchor = FindComponents<Anchor>(ownedScene).First(node => node.CanReclaim);
        Vector3 anchorPosition = anchor.transform.position;
        Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
        // Keep the authored Player at the target during this interaction check; movement physics
        // is covered separately. Only this test-owned scene is modified.
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        body.velocity = Vector2.zero;
        body.position = anchorPosition;
        interaction.transform.position = anchorPosition;
        Physics2D.SyncTransforms();
        RaiseInput(ownedCore.Input, "HandleSecondaryPressed");
        Require(inventory.Count == 0 && !anchor.gameObject.activeSelf && !anchor.CanReclaim,
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
        Require(anchor.EngagedBy == initialWire && inventory.Count == 0 &&
            (anchor.transform.position - anchorPosition).sqrMagnitude < 0.0001f,
            "Placement must immediately route the held wire at the player position.");
        yield return null;
        LineRenderer line = initialWire.GetComponent<LineRenderer>();
        Require(line.positionCount == 3 && (line.GetPosition(1) - anchorPosition).sqrMagnitude < 0.0001f,
            "LateUpdate must render the dynamic anchor as the wire's intermediate bend.");
        yield return WaitForUnlocked(movement);
        Physics2D.SyncTransforms();
        Require(interaction.TryPerformOperation() && !anchor.gameObject.activeSelf,
            "J must reclaim a dynamically placed anchor.");
        yield return null;
        Require(anchor == null && line.positionCount == 2 && environment.HeldWire == initialWire,
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
            outlet.Interact(new InteractionDetails(interaction.gameObject, outlet.gameObject));
            Require(environment.IsCircuitClosed && environment.HeldWire == null && clearedCount == 1,
                "Returning the second wire to its outlet must clear the scene once.");
            environment.EvaluateCircuit();
            Require(clearedCount == 1, "Repeated evaluation must not emit another clear event.");
            typeof(EnvironmentFacade).GetMethod("DebugRestartRun", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(environment, null);
            Require(environment.HeldWire == initialWire && initialWire.IsHeld && !environment.IsCircuitClosed &&
                environment.SwapCount == 0 && !anchor.IsEngaged && initialWire.PlugTarget == null,
                "Restart must reset the authored circuit, held wire and routing.");
        }
        finally
        {
            environment.LevelCleared -= onCleared;
        }

        Corner groundCorner = FindComponents<Corner>(ownedScene).Single(node => node.name == "Corner Left Top");
        Vector2 handOffset = (Vector2)initialWire.FreeEndPosition - body.position;
        body.position = new Vector2(-12f, -2f) - handOffset;
        movement.transform.position = body.position;
        yield return null;
        yield return null;
        Require(!groundCorner.GetAnchor(initialWire), "Moving above Ground must not pre-hook its corner.");
        body.position = new Vector2(-12f, -4.5f) - handOffset;
        movement.transform.position = body.position;
        yield return null;
        yield return null;
        Anchor cornerAnchor = groundCorner.GetAnchor(initialWire);
        Require(cornerAnchor && cornerAnchor.EngagedBy == initialWire && line.positionCount == 3 &&
            groundCorner.DetectionCollider.Distance(initialWire.PathCollider).isOverlapped,
            "The actual scene's Ground corner must automatically hook the Player's carried wire.");
        body.position = new Vector2(-12f, -2f) - handOffset;
        movement.transform.position = body.position;
        yield return null;
        yield return null;
        Require(!cornerAnchor && !groundCorner.GetAnchor(initialWire) && line.positionCount == 2,
            "Reversing the real scene Player must unhook and destroy the generated corner Anchor.");

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
            Require(movement.IsDead && !movement.gameObject.activeSelf && !body.simulated && diedCount == 1,
                "Exceeding the real wire limit must kill the player and notify death in a physics frame.");
            yield return new WaitForFixedUpdate();
            Require(diedCount == 1, "Later physics frames must not repeat the death notification.");
        }
        finally
        {
            movement.Died -= onDied;
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
