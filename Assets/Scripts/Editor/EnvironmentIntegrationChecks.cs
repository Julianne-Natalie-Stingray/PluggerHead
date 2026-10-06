using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Repeatable integration checks against real Player/Env components in an isolated, unsaved preview scene.
/// 在隔离预览场景中验证真实组件; 不保存、关闭或修改用户场景, 不进入播放模式.
/// </summary>
public static class EnvironmentIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/PluggerHead/Verify Player and Environment")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    /// <summary>Use -executeMethod EnvironmentIntegrationChecks.RunBatch for a batch verification.</summary>
    public static void RunBatch()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Run integration checks in Edit Mode.");
        }

        Scene previousScene = SceneManager.GetActiveScene();
        EnvironmentFacade previousEnvironment = EnvironmentFacade.Current;
        float previousTimeScale = Time.timeScale;
        Scene preview = EditorSceneManager.NewPreviewScene();
        int checks = 0;
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            Time.timeScale = 1f;
            Tilemap map = AddRoutingMap(preview, tile);
            GameObject player = Create(preview, "CheckPlayer", Vector3.zero);
            player.tag = "Player";
            PlayerMove movement = player.AddComponent<PlayerMove>();
            PlayerInteraction interaction = player.AddComponent<PlayerInteraction>();
            Invoke(movement, "Awake");
            Invoke(interaction, "Awake");
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;

            PowerSocket outlet = AddSocket(preview, "CheckOutlet", new Vector3(-10f, 0f));
            Wire live = Create(preview, "CheckLiveWire", Vector3.zero).AddComponent<Wire>();
            Wire neutral = Create(preview, "CheckNeutralWire", Vector3.zero).AddComponent<Wire>();
            Set(neutral, "polarity", WirePolarity.Neutral);
            Set(outlet, "wires", new List<Wire> { live, neutral });
            Anchor anchor = AddAnchor(preview, "CheckAnchor", Vector3.right);
            Anchor farther = AddAnchor(preview, "CheckFartherAnchor", new Vector3(1.8f, 0f));
            PolaritySocket dual = AddPolaritySocket(preview, "CheckDual", new Vector3(30f, 0f),
                WirePolarity.Live | WirePolarity.Neutral);
            PolaritySocket mismatch = AddPolaritySocket(preview, "CheckMismatch", new Vector3(40f, 0f),
                WirePolarity.Ground);
            EnvironmentFacade environment = Create(preview, "CheckEnvironment", Vector3.zero)
                .AddComponent<EnvironmentFacade>();
            Set(environment, "playerTransform", player.transform);
            Set(environment, "routingTilemap", map);
            environment.RefreshNodes();
            Invoke(environment, "BeginRun");
            Check(environment.HeldWire == live && live.IsHeld && !environment.IsCircuitClosed,
                "The outlet supplies the initial held wire", ref checks);
            Check(Get<List<Wire>>(environment, "wires").Count == 2,
                "Node discovery stays in the preview scene", ref checks);

            CheckSceneBindings(environment, player, anchor, live, ref checks);

            Anchor prefab = AssetDatabase.LoadAssetAtPath<Anchor>("Assets/Prefabs/Env/ScenePrefab/Anchor.prefab");
            Check(prefab != null, "The player anchor prefab is available", ref checks);
            Set(interaction, "anchorPrefab", prefab);
            Physics2D.SyncTransforms();
            Check(interaction.TryPerformOperation() && anchor == null && farther != null,
                "J reclaims only the nearest anchor through its child collider", ref checks);
            player.transform.position = Vector3.right;
            Check(interaction.TryPlaceAnchor(), "K places an anchor without an inventory item", ref checks);
            anchor = Get<List<Anchor>>(environment, "anchors").Find(node => node != farther);
            Check(anchor != null && anchor.transform.position == player.transform.position && anchor.EngagedBy == live,
                "A newly placed anchor is registered and routes the held wire at the player", ref checks);
            Invoke(environment, "LateUpdate");
            Check(live.TilePath.Cells[live.TilePath.Cells.Count - 1] == map.WorldToCell(anchor.transform.position),
                "The placed Anchor fixes the currently visited tile", ref checks);
            player.transform.position = new Vector3(5f, 0f);
            environment.SamplePlayerPath(player.transform.position);
            float routeLength = live.TilePath.GetLength(map);
            Set(live, "maxLength", routeLength + 1f);
            Check(environment.GetResistance(player.transform.position) == Vector2.zero,
                "A carried wire below its tile path limit applies no resistance", ref checks);
            Set(live, "maxLength", routeLength);
            Check(environment.GetResistance(player.transform.position) == Vector2.zero,
                "A wire exactly at its tile path length limit is not lethal", ref checks);
            Set(live, "maxLength", routeLength - 0.01f);
            Check(IsDeathResistance(environment.GetResistance(player.transform.position)),
                "Strictly exceeding tile path length returns the death sentinel", ref checks);
            player.transform.position = Vector3.zero;
            Set(live, "maxLength", 1f);
            Check(IsDeathResistance(environment.GetResistance(player.transform.position)),
                "A pinned route remains lethal while its earlier route is protected", ref checks);
            Set(live, "maxLength", 0f);
            Check(environment.GetResistance(player.transform.position) == Vector2.zero,
                "Unrestricted wires apply no resistance", ref checks);
            int retainedCells = live.TilePath.Cells.Count;
            Check(anchor.TryReclaim(new InteractionDetails(player, anchor.gameObject)) && anchor == null &&
                environment.HeldWire == live,
                "Reclaim destroys an Anchor without losing the held wire", ref checks);
            Invoke(environment, "LateUpdate");
            Check(live.TilePath.Cells.Count == retainedCells,
                "Reclaim releases a pin without deleting the recorded movement history", ref checks);
            const int placementCount = 16;
            for (int i = 0; i < placementCount; i++)
            {
                Check(interaction.TryPlaceAnchor(), "Repeated placement does not require inventory stock", ref checks);
            }
            Check(Get<List<Anchor>>(environment, "anchors").Count == placementCount + 1,
                "Sixteen simultaneous anchors can be placed without an inventory component", ref checks);
            foreach (Anchor placed in new List<Anchor>(Get<List<Anchor>>(environment, "anchors")))
            {
                if (placed != farther)
                {
                    Check(placed.TryReclaim(new InteractionDetails(player, placed.gameObject)),
                        "Every dynamically registered anchor can be reclaimed", ref checks);
                }
            }
            Check(Get<List<Anchor>>(environment, "anchors").Count == 1,
                "Reclaim unregisters destroyed anchors without rescanning the scene", ref checks);

            CheckLocks(interaction, movement, ref checks);
            Set(farther, "canInteract", false);
            player.transform.position = farther.transform.position;
            Physics2D.SyncTransforms();
            Check(interaction.TryPerformOperation() && farther == null,
                "J still reclaims anchors whose diagnostic routing is disabled", ref checks);
            player.transform.position = Vector3.zero;

            int clearedCount = 0;
            environment.LevelCleared += () => clearedCount++;
            mismatch.Interact(new InteractionDetails(player, mismatch.gameObject));
            Check(live.IsHeld && live.PlugTarget == null && environment.SwapCount == 0,
                "A polarity mismatch leaves the circuit unchanged", ref checks);
            dual.Interact(new InteractionDetails(player, dual.gameObject));
            Check(live.PlugTarget == dual.transform && !live.IsHeld && environment.HeldWire == neutral &&
                neutral.IsHeld && environment.SwapCount == 1 && !environment.IsCircuitClosed,
                "The dual socket plugs one wire and hands over the other", ref checks);
            environment.RefreshNodes();
            Check(live.PlugTarget == dual.transform && environment.HeldWire == neutral && environment.SwapCount == 1,
                "Refreshing nodes preserves active circuit state", ref checks);
            outlet.Interact(new InteractionDetails(player, outlet.gameObject));
            Check(neutral.IsClosed && environment.IsCircuitClosed && environment.HeldWire != null && environment.HeldWire.IsHeld && clearedCount == 1,
                "Closing the return wire completes the circuit exactly once", ref checks);
            outlet.Interact(new InteractionDetails(player, outlet.gameObject));
            environment.EvaluateCircuit();
            Check(clearedCount == 1,
                "Repeated interactions do not repeat the win event", ref checks);

            // Explicitly exercise defensive placement with a missing powered wire.
            environment.HeldWire.PlugInto(null, false);
            Set(environment, "heldWire", null);
            Check(interaction.TryPlaceAnchor(), "Placement remains safe if the powered wire is missing", ref checks);
            Anchor wireless = Get<List<Anchor>>(environment, "anchors").Find(node => node != farther);
            Check(wireless != null && !wireless.IsEngaged,
                "An anchor placed without a wire remains independently reclaimable", ref checks);
            Check(wireless.TryReclaim(new InteractionDetails(player, wireless.gameObject)),
                "A wireless anchor can be reclaimed", ref checks);

            Invoke(environment, "BeginRun");
            Check(environment.HeldWire == live && live.IsHeld && !neutral.IsClosed &&
                !environment.IsCircuitClosed && environment.SwapCount == 0,
                "Restart clears route, plug, swap, and victory state", ref checks);

            Set(live, "maxLength", 1f);
            movement.GetResistance = environment.GetResistance;
            int diedCount = 0;
            bool stoppedBeforeDeathNotification = false;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            movement.Died += () =>
            {
                diedCount++;
                stoppedBeforeDeathNotification = movement.IsDead && movement.IsInputLocked &&
                    !player.activeSelf && !body.simulated && body.velocity == Vector2.zero &&
                    body.angularVelocity == 0f;
            };
            body.velocity = Vector2.one;
            body.angularVelocity = 15f;
            Invoke(movement, "FixedUpdate");
            Check(stoppedBeforeDeathNotification && diedCount == 1,
                "Real environment overlength stops the player before notifying death", ref checks);
            Invoke(movement, "FixedUpdate");
            movement.Die();
            Check(diedCount == 1, "Repeated death processing notifies subscribers only once", ref checks);
            return $"Player/Environment integration PASS: {checks} checks; isolated preview scene cleaned up.";
        }
        finally
        {
            if (preview.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            UnityEngine.Object.DestroyImmediate(tile);
            Time.timeScale = previousTimeScale;
            typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, previousEnvironment);
            if (previousScene.IsValid() && previousScene.isLoaded && SceneManager.GetActiveScene() != previousScene)
            {
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }

    private static void CheckSceneBindings(EnvironmentFacade environment, GameObject player,
        Anchor anchor, Wire live, ref int checks)
    {
        Scene otherScene = EditorSceneManager.NewPreviewScene();
        Tile otherTile = ScriptableObject.CreateInstance<Tile>();
        try
        {
            AddRoutingMap(otherScene, otherTile);
            GameObject otherPlayer = Create(otherScene, "OtherPlayer", Vector3.zero);
            Wire otherWire = Create(otherScene, "OtherWire", Vector3.zero).AddComponent<Wire>();
            PowerSocket otherOutlet = AddSocket(otherScene, "OtherOutlet", Vector3.zero);
            Set(otherOutlet, "wires", new List<Wire> { otherWire });
            Anchor otherAnchor = AddAnchor(otherScene, "OtherAnchor", Vector3.right);
            EnvironmentFacade otherEnvironment = Create(otherScene, "OtherEnvironment", Vector3.zero)
                .AddComponent<EnvironmentFacade>();
            otherEnvironment.RefreshNodes();
            Invoke(otherEnvironment, "BeginRun");

            Check(EnvironmentFacade.Current == otherEnvironment &&
                EnvironmentFacade.HeldWireOf(new InteractionDetails(player, anchor.gameObject)) == live,
                "Actor scene selects its own wire even when Current belongs to another scene", ref checks);
            Check(EnvironmentFacade.HeldWireOf(new InteractionDetails(otherPlayer, otherAnchor.gameObject)) == otherWire,
                "A second scene resolves its independent held wire", ref checks);
            Check(EnvironmentFacade.HeldWireOf(null) == null &&
                EnvironmentFacade.HeldWireOf(new InteractionDetails(null, anchor.gameObject)) == null,
                "Missing actor details cannot acquire a wire", ref checks);
            Check(EnvironmentFacade.HeldWireOf(new InteractionDetails(player, otherAnchor.gameObject)) == null,
                "Cross-scene interaction targets cannot acquire a wire", ref checks);
            Check(!otherAnchor.TryReclaim(null) &&
                !otherAnchor.TryReclaim(new InteractionDetails(null, otherAnchor.gameObject)),
                "Missing actor details cannot reclaim an anchor", ref checks);
            Check(!otherAnchor.TryReclaim(new InteractionDetails(player, otherAnchor.gameObject)) &&
                otherAnchor.gameObject.activeSelf,
                "An actor cannot reclaim an anchor from another scene", ref checks);

            Check(EnvironmentFacade.ForScene(player.scene) == environment,
                "The player resolves its own scene environment while another scene is current", ref checks);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(otherScene);
            UnityEngine.Object.DestroyImmediate(otherTile);
            environment.RefreshNodes();
        }
    }
    private static void CheckLocks(PlayerInteraction interaction, PlayerMove movement, ref int checks)
    {
        Time.timeScale = 0f;
        Check(!interaction.TryPerformOperation() && !interaction.TryPlaceAnchor(),
            "Pause blocks reclaiming and placing anchors", ref checks);
        Time.timeScale = 1f;
        movement.LockInput();
        Check(!interaction.TryPerformOperation() && !interaction.TryPlaceAnchor(),
            "The manual input lock blocks environment operations and anchor placement", ref checks);
        movement.OnInteractionStarted();
        movement.UnlockInput();
        Check(movement.IsInputLocked && !interaction.TryPerformOperation() && !interaction.TryPlaceAnchor(),
            "Interaction animation keeps operations locked after the manual lock is released", ref checks);
        movement.LockInput();
        movement.OnInteractionEnded();
        Check(movement.IsInputLocked, "Finishing interaction preserves the manual input lock", ref checks);
        movement.UnlockInput();
        Check(!movement.IsInputLocked, "Releasing both lock sources restores input", ref checks);
    }

    private static Tilemap AddRoutingMap(Scene scene, Tile tile)
    {
        GameObject grid = Create(scene, "CheckGrid", new Vector3(-0.5f, -0.5f));
        grid.AddComponent<Grid>();
        GameObject mapObject = Create(scene, "RoutingTilemap", grid.transform.position, grid.transform);
        Tilemap map = mapObject.AddComponent<Tilemap>();
        for (int x = -12; x <= 42; x++)
        {
            for (int y = -2; y <= 3; y++)
            {
                map.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
        return map;
    }

    private static GameObject Create(Scene scene, string name, Vector3 position, Transform parent = null)
    {
        GameObject result = new GameObject(name);
        SceneManager.MoveGameObjectToScene(result, scene);
        if (parent != null)
        {
            result.transform.SetParent(parent);
        }
        result.transform.position = position;
        return result;
    }

    private static Anchor AddAnchor(Scene scene, string name, Vector3 position)
    {
        GameObject root = Create(scene, name, position);
        Anchor anchor = root.AddComponent<Anchor>();
        BoxCollider2D collider = Create(scene, name + "Collider", position, root.transform).AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.25f;
        collider.isTrigger = true;
        return anchor;
    }

    private static PowerSocket AddSocket(Scene scene, string name, Vector3 position)
    {
        GameObject root = Create(scene, name, position);
        root.AddComponent<BoxCollider2D>();
        return root.AddComponent<PowerSocket>();
    }

    private static PolaritySocket AddPolaritySocket(Scene scene, string name, Vector3 position, WirePolarity polarity)
    {
        GameObject root = Create(scene, name, position);
        root.AddComponent<BoxCollider2D>();
        PolaritySocket socket = root.AddComponent<PolaritySocket>();
        Set(socket, "accepted", polarity);
        return socket;
    }

    private static void Check(bool passed, string description, ref int count)
    {
        if (!passed)
        {
            throw new InvalidOperationException($"Player/Environment integration FAILED after {count} checks: {description}");
        }
        count++;
    }

    private static bool IsDeathResistance(Vector2 resistance)
    {
        return float.IsNegativeInfinity(resistance.x) && float.IsNegativeInfinity(resistance.y);
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }

    private static T Get<T>(object target, string field)
    {
        return (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);
    }

    private static object Invoke(object target, string method, params object[] arguments)
    {
        return target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);
    }
}
