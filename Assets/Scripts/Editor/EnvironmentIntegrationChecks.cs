using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        Sprite sprite = null;
        Texture2D texture = null;
        int checks = 0;
        try
        {
            Time.timeScale = 1f;
            GameObject player = Create(preview, "CheckPlayer", Vector3.zero);
            player.tag = "Player";
            PlayerInventory inventory = player.AddComponent<PlayerInventory>();
            PlayerMove movement = player.AddComponent<PlayerMove>();
            PlayerInteraction interaction = player.AddComponent<PlayerInteraction>();
            Invoke(movement, "Awake");
            Invoke(interaction, "Awake");
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;

            GameObject hand = Create(preview, "CheckHand", Vector3.zero, player.transform);
            PowerSocket outlet = AddSocket(preview, "CheckOutlet", new Vector3(100f, 0f));
            Wire live = Create(preview, "CheckLiveWire", Vector3.zero).AddComponent<Wire>();
            Wire neutral = Create(preview, "CheckNeutralWire", Vector3.zero).AddComponent<Wire>();
            Set(neutral, "polarity", WirePolarity.Neutral);
            Set(outlet, "wires", new List<Wire> { live, neutral });
            Anchor anchor = AddAnchor(preview, "CheckAnchor", Vector3.right, true);
            Anchor farther = AddAnchor(preview, "CheckFartherAnchor", new Vector3(1.8f, 0f), true);
            Anchor fixedAnchor = AddAnchor(preview, "CheckFixedAnchor", new Vector3(20f, 0f), false);
            PolaritySocket dual = AddPolaritySocket(preview, "CheckDual", new Vector3(30f, 0f),
                WirePolarity.Live | WirePolarity.Neutral);
            PolaritySocket mismatch = AddPolaritySocket(preview, "CheckMismatch", new Vector3(40f, 0f),
                WirePolarity.Ground);
            WirePoint point = Create(preview, "CheckWirePoint", new Vector3(0f, 2f), live.transform)
                .AddComponent<WirePoint>();
            Invoke(point, "Awake");
            EnvironmentFacade environment = Create(preview, "CheckEnvironment", Vector3.zero)
                .AddComponent<EnvironmentFacade>();
            Set(inventory, "environment", environment);
            Set(environment, "attachPoint", hand.transform);
            environment.RefreshNodes();
            Invoke(environment, "BeginRun");
            Check(environment.HeldWire == live && live.IsHeld && !environment.IsCircuitClosed,
                "The outlet supplies the initial held wire", ref checks);
            Check(Get<List<Wire>>(environment, "wires").Count == 2,
                "Node discovery stays in the preview scene", ref checks);

            CheckSceneBindings(environment, player, inventory, anchor, live, ref checks);

            texture = new Texture2D(2, 2);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            anchor.gameObject.AddComponent<SpriteRenderer>().sprite = sprite;
            InventoryUI ui = Create(preview, "CheckInventoryUI", Vector3.zero).AddComponent<InventoryUI>();
            int inventoryChanges = 0;
            inventory.Changed += () => inventoryChanges++;

            Physics2D.SyncTransforms();
            Check(interaction.TryPerformOperation(), "J finds a pickup through its child collider", ref checks);
            Check(inventory.Count == 1 && inventory.Items[0] is AnchorInstance &&
                inventory.Items[0].SourceObject == anchor.gameObject && !anchor.gameObject.activeSelf &&
                farther.gameObject.activeSelf, "Only the nearest anchor is stowed, with its original identity", ref checks);
            IPickupInstance carried = inventory.Items[0];
            Check((Sprite)Invoke(ui, "GetItemSprite", carried) == sprite,
                "Inventory icons still read the inactive source sprite", ref checks);
            Check(!inventory.PickUpItem(anchor), "Repeated pickup cannot duplicate an instance", ref checks);
            Check(!inventory.PickUpItem(fixedAnchor), "A fixed anchor cannot be picked up", ref checks);
            Check(!inventory.DropItem(carried, new Vector3(float.NaN, 0f, 0f)) && inventory.Count == 1,
                "Invalid placement preserves the carried instance", ref checks);
            Check(inventory.DropItem(carried, Vector3.right) && inventory.Count == 0 &&
                anchor.gameObject.activeSelf && anchor.transform.position == Vector3.right &&
                carried.SourceObject == null && carried.CurrentStackAmount == 0,
                "Drop restores the same anchor and consumes the instance", ref checks);
            Check(!inventory.DropItem(carried, Vector3.zero) && !carried.TryDrop(Vector3.zero),
                "A consumed instance cannot drop twice", ref checks);
            Check(inventoryChanges == 2, "Only successful membership changes notify the UI", ref checks);

            Physics2D.SyncTransforms();
            interaction.ToggleOperationMode();
            Check(interaction.CurrentMode == PlayerInteraction.OperationMode.Select &&
                interaction.TryPerformOperation() && anchor.EngagedBy == live && inventory.Count == 0,
                "K switches a dual-purpose anchor from pickup to routing", ref checks);
            Invoke(environment, "LateUpdate");
            Check(live.GetComponent<LineRenderer>().positionCount == 3 &&
                live.GetComponent<LineRenderer>().GetPosition(1) == anchor.transform.position,
                "Routing inserts the real anchor into the rendered wire path", ref checks);

            hand.transform.position = new Vector3(5f, 0f);
            Set(live, "maxLength", 6f);
            Check(environment.GetResistance(player.transform.position) == Vector2.zero,
                "A carried wire below its length limit applies no resistance", ref checks);
            Set(live, "maxLength", 5f);
            Check(environment.GetResistance(player.transform.position) == Vector2.zero,
                "A carried wire exactly at its length limit is not lethal", ref checks);
            Set(live, "maxLength", 4.99f);
            Check(IsDeathResistance(environment.GetResistance(player.transform.position)),
                "Exceeding the length limit at the actual hand returns the death sentinel", ref checks);
            hand.transform.position = Vector3.zero;
            Set(live, "maxLength", 1f);
            Set(live, "pullStrength", 0f);
            Check(IsDeathResistance(environment.GetResistance(player.transform.position)),
                "Routing through an anchor can exceed the limit even at the fixed end with zero pull strength", ref checks);
            hand.transform.position = new Vector3(5f, 0f);
            Set(live, "maxLength", 0f);
            Check(environment.GetResistance(player.transform.position) == Vector2.zero,
                "Unrestricted wires apply no resistance", ref checks);

            Check(inventory.PickUpItem(anchor) && !anchor.IsEngaged && environment.HeldWire == live,
                "Picking up a routed anchor detaches it without losing the held wire", ref checks);
            Invoke(environment, "LateUpdate");
            Check(live.GetComponent<LineRenderer>().positionCount == 2,
                "Picking up an anchor removes its bend from the path", ref checks);
            carried = inventory.Items[0];
            Check(inventory.DropItem(carried, Vector3.right), "A routed anchor can be placed again", ref checks);
            Check(interaction.TryPerformOperation() && anchor.IsEngaged,
                "A dropped anchor remains subscribed and can route again", ref checks);
            anchor.Interact(new InteractionDetails(player, anchor.gameObject));

            point.Interact(new WirePointDetails(player, point.gameObject));
            Invoke(environment, "LateUpdate");
            Check(point.IsEngaged && live.GetComponent<LineRenderer>().GetPosition(1) == point.transform.position,
                "WirePointDetails routes an authored wire point", ref checks);
            point.Interact(new WirePointDetails(player, point.gameObject));
            Check(!point.IsEngaged, "A second wire-point interaction releases the bend", ref checks);

            CheckLocks(interaction, inventory, movement, anchor, ref checks);
            Check(inventory.PickUpItem(anchor), "Pickup works after input locks are released", ref checks);
            carried = inventory.Items[0];
            Time.timeScale = 0f;
            Check(!inventory.DropItem(carried, Vector3.right) && inventory.Count == 1,
                "Pause prevents dropping without losing inventory", ref checks);
            Time.timeScale = 1f;
            UnityEngine.Object.DestroyImmediate(anchor.gameObject);
            Check(inventory.Count == 0, "Destroyed source objects are removed from inventory", ref checks);

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
            Check(neutral.IsClosed && environment.IsCircuitClosed && environment.HeldWire == null && clearedCount == 1,
                "Closing the return wire completes the circuit exactly once", ref checks);
            outlet.Interact(new InteractionDetails(player, outlet.gameObject));
            environment.EvaluateCircuit();
            Check(clearedCount == 1 && environment.GetResistance(Vector2.right * 100f) == Vector2.zero,
                "Completed circuits neither repeat the win event nor pull the player", ref checks);

            Invoke(environment, "BeginRun");
            Check(environment.HeldWire == live && live.IsHeld && !neutral.IsClosed &&
                !environment.IsCircuitClosed && environment.SwapCount == 0 && !point.IsEngaged,
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
            if (sprite != null)
            {
                UnityEngine.Object.DestroyImmediate(sprite);
            }
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
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
        PlayerInventory inventory, Anchor anchor, Wire live, ref int checks)
    {
        Scene otherScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject otherPlayer = Create(otherScene, "OtherPlayer", Vector3.zero);
            Wire otherWire = Create(otherScene, "OtherWire", Vector3.zero).AddComponent<Wire>();
            PowerSocket otherOutlet = AddSocket(otherScene, "OtherOutlet", Vector3.zero);
            Set(otherOutlet, "wires", new List<Wire> { otherWire });
            Anchor otherAnchor = AddAnchor(otherScene, "OtherAnchor", Vector3.right, true);
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
            Check(!inventory.PickUpItem(otherAnchor) && otherAnchor.gameObject.activeSelf && inventory.Count == 0,
                "Inventory cannot take an anchor from another scene", ref checks);

            Check(inventory.Environment == environment,
                "A valid explicit environment is preserved while another scene is current", ref checks);
            Set(inventory, "environment", null);
            Check(inventory.Environment == environment && Get<EnvironmentFacade>(inventory, "environment") == environment,
                "An unassigned inventory resolves and caches its own scene environment", ref checks);
            Set(inventory, "environment", otherEnvironment);
            Check(inventory.Environment == environment,
                "An environment reference from another scene is replaced with the local environment", ref checks);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(otherScene);
            environment.RefreshNodes();
        }
    }
    private static void CheckLocks(PlayerInteraction interaction, PlayerInventory inventory,
        PlayerMove movement, Anchor anchor, ref int checks)
    {
        Time.timeScale = 0f;
        PlayerInteraction.OperationMode mode = interaction.CurrentMode;
        interaction.ToggleOperationMode();
        Check(!interaction.TryPerformOperation() && !inventory.PickUpItem(anchor) && interaction.CurrentMode == mode,
            "Pause blocks pickup, interaction, and mode changes", ref checks);
        Time.timeScale = 1f;
        movement.LockInput();
        Check(!interaction.TryPerformOperation() && !inventory.PickUpItem(anchor),
            "The manual input lock blocks environment operations", ref checks);
        movement.UnlockInput();
        movement.OnDashStarted();
        movement.OnInteractionStarted();
        movement.OnDashEnded();
        Check(movement.IsInputLocked && !interaction.TryPerformOperation() && !inventory.PickUpItem(anchor),
            "Overlapping animation locks remain active until both animations finish", ref checks);
        movement.OnInteractionEnded();
        Check(!movement.IsInputLocked, "Finishing the final animation releases input", ref checks);
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

    private static Anchor AddAnchor(Scene scene, string name, Vector3 position, bool movable)
    {
        GameObject root = Create(scene, name, position);
        Anchor anchor = root.AddComponent<Anchor>();
        Set(anchor, "canPickup", movable);
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
