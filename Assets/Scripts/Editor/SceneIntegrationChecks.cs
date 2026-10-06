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
    private const string GameplayScenePath = "Assets/Scenes/Tests/HeXieTestScene.unity";
    private const float TimeoutSeconds = 15f;
    private static Scene ownedScene;
    private static Scene previousScene;
    private static EnvironmentFacade previousEnvironment;
    private static CoreFacade ownedCore;
    private static AsyncOperation pendingLoad;
    private static bool gameplayStarted;

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

            if (path == GameplayScenePath)
            {
                Require(FindComponents<CoreFacade>(scene).Count == 1, "Gameplay scene must contain one configured Core.");
                Require(FindComponents<EnvironmentFacade>(scene).Count == 1, "Gameplay scene must contain one environment.");
                Require(FindComponents<PlayerInteraction>(scene).Count == 1, "Gameplay scene must contain the real Player.");
                Require(FindComponents<PowerSocket>(scene).Any(socket => socket.StartingWire != null &&
                    socket.Wires.Count >= 2), "Gameplay outlet must reference its authored wires.");
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
        Require(Application.isPlaying, "Gameplay checks require Play Mode.");
        Require(!gameplayStarted, "A previous scene check still needs cleanup.");
        Require(CoreFacade.Instance == null, "Run in a fresh Test Runner session without an existing Core.");
        Require(!SceneManager.GetSceneByPath(GameplayScenePath).isLoaded,
            "The gameplay scene is already loaded; refusing to modify an unowned scene.");
        Require(Time.timeScale > 0f && GameStateManager.Current == GameState.Playing,
            "Run gameplay checks in an unpaused Playing state.");

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

        Anchor anchor = FindComponents<Anchor>(ownedScene).First(node => node.CanPickup && node.CanInteract);
        Vector3 anchorPosition = anchor.transform.position;
        Rigidbody2D body = movement.GetComponent<Rigidbody2D>();
        // Keep the authored Player at the target during this interaction check; movement physics
        // is covered separately. Only this test-owned scene is modified.
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        body.velocity = Vector2.zero;
        body.position = anchorPosition;
        interaction.transform.position = anchorPosition;
        Physics2D.SyncTransforms();
        Require(interaction.TryPerformOperation(), "J must find the nearby anchor through scene colliders.");
        Require(inventory.Count == 1 && inventory.Items[0].SourceObject == anchor.gameObject &&
            !anchor.gameObject.activeSelf, "Pickup must store the original nearest anchor instance.");
        IPickupInstance carried = inventory.Items[0];
        yield return WaitForUnlocked(movement);
        Require(inventory.DropItem(carried, anchorPosition) && inventory.Count == 0 && anchor.gameObject.activeSelf,
            "Drop must restore the original scene anchor.");
        Physics2D.SyncTransforms();
        interaction.ToggleOperationMode();
        Require(interaction.CurrentMode == PlayerInteraction.OperationMode.Select &&
            interaction.TryPerformOperation() && anchor.EngagedBy == initialWire,
            "K followed by J must route the held wire around the restored anchor.");
        yield return null;
        LineRenderer line = initialWire.GetComponent<LineRenderer>();
        Require(line.positionCount == 3 && (line.GetPosition(1) - anchorPosition).sqrMagnitude < 0.0001f,
            "LateUpdate must render the anchor as the wire's intermediate bend.");

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
        if (!gameplayStarted)
        {
            yield break;
        }
        // A timed-out load is still owned by this fixture; wait for it before unloading.
        if (pendingLoad != null && !pendingLoad.isDone)
        {
            yield return WaitForOperation(pendingLoad, "finishing the owned scene load for cleanup");
        }
        ownedScene = SceneManager.GetSceneByPath(GameplayScenePath);
        if (ownedCore == null)
        {
            ownedCore = CoreFacade.Instance;
        }
        if (previousScene.IsValid() && previousScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousScene);
        }
        if (ownedScene.IsValid() && ownedScene.isLoaded)
        {
            yield return WaitForOperation(SceneManager.UnloadSceneAsync(ownedScene), "unloading the owned gameplay scene");
        }
        if (ownedCore != null)
        {
            UnityEngine.Object.Destroy(ownedCore.gameObject);
            yield return null;
        }
        typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, previousEnvironment);
        ownedCore = null;
        pendingLoad = null;
        gameplayStarted = false;
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
        Require(operation != null, $"Could not start {description}.");
        float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (!operation.isDone)
        {
            Require(Time.realtimeSinceStartup < deadline, $"Timed out {description}.");
            yield return null;
        }
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
