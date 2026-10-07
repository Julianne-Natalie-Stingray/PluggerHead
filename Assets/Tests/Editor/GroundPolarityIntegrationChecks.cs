using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Uses real 2D contacts in owned local physics scenes without saving authored scenes; frame checks may create an owned Core.
/// 在独立物理场景中生成真实接触，再推进 PlayerMove 的物理逻辑；不模拟输入或修改现有场景。
/// </summary>
public static class GroundPolarityIntegrationChecks
{
    public static void CheckFootstepAudio(AudioManager manager)
    {
        BeginChecks();
        var fixture = new Fixture(WirePolarity.Live, WirePolarity.Live, false);
        PlayerAnimationAudio receiver = fixture.Player.gameObject.AddComponent<PlayerAnimationAudio>();
        try
        {
            fixture.EstablishContact(Vector2.down);
            fixture.Player.enabled = true;
            fixture.Body.velocity = Vector2.right;
            Set(receiver, "useSurfaceFootsteps", true);
            receiver.PlayMoveAudio();
            Require(manager.Registry.CountOf(AudioId.GroundStepFront) == 1, "Ordinary support must play front foot first.");
            fixture.Player.LockInput();
            receiver.PlayMoveAudio();
            fixture.Player.UnlockInput();
            fixture.Body.velocity = Vector2.zero;
            receiver.PlayMoveAudio();
            fixture.Body.velocity = Vector2.right;
            CoreFacade savedCore = CoreFacade.Instance;
            typeof(CoreFacade).GetProperty("Instance").SetValue(null, null);
            try
            {
                receiver.PlayMoveAudio();
            }
            finally
            {
                typeof(CoreFacade).GetProperty("Instance").SetValue(null, savedCore);
            }
            receiver.PlayMoveAudio();
            Require(manager.Registry.CountOf(AudioId.GroundStepBack) == 1 && manager.Registry.CountOf(AudioId.GroundStepFront) == 0,
                "Locked and stationary events must not advance alternation.");
            FootstepSurface marker = fixture.Surface.gameObject.AddComponent<FootstepSurface>();
            receiver.PlayMoveAudio();
            Require(manager.Registry.CountOf(AudioId.MetalStepFront) == 1, "Metal support must change family while preserving alternation.");
            fixture.Body.velocity = Vector2.one;
            receiver.PlayMoveAudio();
            fixture.Body.velocity = Vector2.right;
            receiver.PlayMoveAudio();
            Require(manager.Registry.CountOf(AudioId.MetalStepBack) == 1, "Upward motion must not advance the sequence.");
            marker.enabled = false;
            GameStateManager.Freeze();
            receiver.PlayMoveAudio();
            GameStateManager.Resume();
            receiver.PlayMoveAudio();
            Require(manager.Registry.CountOf(AudioId.GroundStepFront) == 1, "Disabled marker uses ground; pause must not advance.");
            Set(fixture.Player, "groundLayers", (LayerMask)0);
            receiver.PlayMoveAudio();
            Set(fixture.Player, "groundLayers", (LayerMask)(~0));
            receiver.PlayMoveAudio();
            Require(manager.Registry.CountOf(AudioId.GroundStepBack) == 1, "Excluded support must not advance alternation.");
            receiver.enabled = false;
            Require(manager.Registry.Count == 0, "Disable must release the footstep voice.");
        }
        finally
        {
            receiver.enabled = false;
            fixture.Player.enabled = false;
        }
    }

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<Scene> ownedScenes = new List<Scene>();
    private static readonly List<Tile> ownedTiles = new List<Tile>();
    private static EnvironmentFacade previousEnvironment;
    private static GameObject ownedCore;
    private static bool hasEnvironmentSnapshot;
    private static readonly OwnedPhysicsSceneCleanup sceneCleanup = new OwnedPhysicsSceneCleanup();

    public static void CheckConfiguration(int value, bool valid)
    {
        var root = new GameObject("GroundConfigurationCheck");
        GroundPolarity ground = root.AddComponent<GroundPolarity>();
        try
        {
            Set(ground, "polarity", (WirePolarity)value);
            Action[] checks =
            {
                () => { WirePolarity ignored = ground.Polarity; },
                () => ground.CanSupport(null),
                () => typeof(GroundPolarity).GetMethod("Awake", PrivateInstance).Invoke(ground, null),
                () => typeof(GroundPolarity).GetMethod("OnValidate", PrivateInstance).Invoke(ground, null)
            };
            foreach (Action check in checks)
            {
                Exception failure = null;
                try
                {
                    check();
                }
                catch (TargetInvocationException exception)
                {
                    failure = exception.InnerException;
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                Require(valid ? failure == null : failure is InvalidOperationException &&
                    failure.Message.Contains("Live or Neutral") && failure.Message.Contains($"({value})"),
                    "All configuration entry points must accept Live/Neutral and throw a diagnostic exception for invalid values.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static void CheckInspectorOptions()
    {
        var root = new GameObject("GroundInspectorCheck");
        try
        {
            GroundPolarity ground = root.AddComponent<GroundPolarity>();
            FieldInfo field = typeof(GroundPolarity).GetField("polarity", PrivateInstance);
            var dropdown = (NaughtyAttributes.DropdownAttribute)Attribute.GetCustomAttribute(field, typeof(NaughtyAttributes.DropdownAttribute));
            Require(dropdown != null, "The serialized polarity must use the restricted Inspector dropdown.");
            var options = (WirePolarity[])typeof(GroundPolarity).GetProperty(dropdown.ValuesName, PrivateInstance).GetValue(ground);
            Require(options.Length == 2 && options[0] == WirePolarity.Live && options[1] == WirePolarity.Neutral,
                "The Inspector must offer only Live and Neutral, never None, Ground or combinations.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static void CheckNonMatchingPolarities()
    {
        BeginChecks();
        CheckLethal(WirePolarity.Live, WirePolarity.Neutral, false);
        CheckLethal(WirePolarity.Neutral, WirePolarity.Live, true);
        CheckLethal(WirePolarity.Live, WirePolarity.Ground, false);
        CheckLethal(WirePolarity.Live, WirePolarity.None, false);
        CheckLethal(WirePolarity.Live, WirePolarity.Live | WirePolarity.Neutral, false);
    }

    public static void CheckSafeContacts()
    {
        BeginChecks();
        CheckSafe(WirePolarity.Live, WirePolarity.Live);
        CheckSafe(WirePolarity.Neutral, WirePolarity.Neutral);

        Fixture ordinary = new Fixture(WirePolarity.Neutral, WirePolarity.Live, false);
        ordinary.EstablishContact(Vector2.down);
        ordinary.Tick();
        Require(!ordinary.Player.IsDead, "Ordinary ground without the component must remain safe.");

        Fixture disabled = new Fixture(WirePolarity.Neutral, WirePolarity.Live);
        disabled.Ground.enabled = false;
        disabled.EstablishContact(Vector2.down);
        disabled.Tick();
        Require(!disabled.Player.IsDead, "A disabled ground component must be harmless.");

        Fixture excludedLayer = new Fixture(WirePolarity.Neutral, WirePolarity.Live);
        Set(excludedLayer.Player, "groundLayers", (LayerMask)0);
        excludedLayer.EstablishContact(Vector2.down);
        excludedLayer.Tick();
        Require(!excludedLayer.Player.IsDead, "A contact excluded by Ground Layers must be harmless.");

        CheckNonSupportingContact(Vector2.left);
        CheckNonSupportingContact(Vector2.up);

        Fixture trigger = new Fixture(WirePolarity.Neutral, WirePolarity.Live);
        trigger.Surface.isTrigger = true;
        trigger.Body.position = trigger.Surface.bounds.center;
        Physics2D.SyncTransforms();
        trigger.Physics.Simulate(0.02f);
        Require(trigger.Surface.bounds.Intersects(trigger.PlayerCollider.bounds),
            "The trigger scenario must actually overlap the player.");
        trigger.Tick();
        Require(!trigger.Player.IsDead, "A trigger volume does not count as standing on ground.");
    }

    public static void CheckMissingOrReleasedWire()
    {
        BeginChecks();
        Fixture noWire = new Fixture(WirePolarity.Live, WirePolarity.Live);
        noWire.Wire.PlugInto(null, false);
        Set(noWire.Environment, "heldWire", null);
        noWire.EstablishContact(Vector2.down);
        noWire.Tick();
        Require(noWire.Player.IsDead, "A player without a carried wire must die on polarized ground.");

        Fixture released = new Fixture(WirePolarity.Live, WirePolarity.Live);
        released.Wire.PlugInto(released.Outlet.transform, false);
        released.EstablishContact(Vector2.down);
        released.Tick();
        Require(released.Player.IsDead, "A stale reference to a matching but released wire must not protect the player.");

        Fixture foreign = new Fixture(WirePolarity.Live, WirePolarity.Live);
        Fixture missingEnvironment = new Fixture(WirePolarity.Live, WirePolarity.Live);
        UnityEngine.Object.DestroyImmediate(missingEnvironment.Environment.gameObject);
        typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, foreign.Environment);
        missingEnvironment.EstablishContact(Vector2.down);
        missingEnvironment.Tick();
        Require(missingEnvironment.Player.IsDead, "A player without its own environment must not borrow a matching wire from another scene.");
    }

    public static void CheckCarriedGroundWire(bool keepPoweredWire, bool releaseGroundWire)
    {
        BeginChecks();
        Fixture fixture = new Fixture(WirePolarity.Live, WirePolarity.Live, true, true);
        Wire ground = fixture.PickUpGroundWire();
        Require(ground && ground.IsHeld && fixture.Environment.HeldWire == fixture.Wire,
            "A real ground socket must provide a separate carried ground wire.");
        if (!keepPoweredWire)
        {
            fixture.Dual.Interact(new InteractionDetails(fixture.Player.gameObject, fixture.Dual.gameObject));
            fixture.Outlet.Interact(new InteractionDetails(fixture.Player.gameObject, fixture.Outlet.gameObject));
            Require(!fixture.Environment.HeldWire && fixture.Environment.HeldGroundWire == ground,
                "Returning the main wire must preserve independent ground carrying.");
        }
        if (releaseGroundWire)
        {
            // Retain the stale facade reference deliberately: only a held free end offers protection.
            ground.PlugInto(fixture.Outlet.transform, false);
        }
        fixture.EstablishContact(Vector2.down);
        fixture.Tick();
        Require(fixture.Player.IsDead == !keepPoweredWire,
            "Only the matching main wire protects on Live terrain; carrying or releasing ground must not change that.");

        Fixture mismatch = new Fixture(WirePolarity.Neutral, WirePolarity.Live);
        mismatch.PickUpGroundWire();
        mismatch.EstablishContact(Vector2.down);
        mismatch.Tick();
        Require(mismatch.Player.IsDead, "Carrying a ground wire must not mask a mismatching powered wire on neutral terrain.");
    }

    public static void CheckReleaseWhileStanding()
    {
        BeginChecks();
        Fixture fixture = new Fixture(WirePolarity.Neutral, WirePolarity.Live, true, true);
        fixture.Dual.Interact(new InteractionDetails(fixture.Player.gameObject, fixture.Dual.gameObject));
        fixture.EstablishContact(Vector2.down);
        fixture.Tick();
        Require(!fixture.Player.IsDead, "The matching return wire must protect the standing player.");
        fixture.Player.LockInput();
        fixture.Outlet.Interact(new InteractionDetails(fixture.Player.gameObject, fixture.Outlet.gameObject));
        Require(!fixture.Environment.HeldWire, "Connecting the return wire must leave the player empty-handed.");
        fixture.Tick();
        Require(fixture.Player.IsDead, "Releasing the matching wire while standing must kill on the next physics check despite input lock.");
    }

    public static void CheckWireSwapWhileStanding()
    {
        BeginChecks();
        Fixture fixture = new Fixture(WirePolarity.Live, WirePolarity.Live, true, true);
        fixture.EstablishContact(Vector2.down);
        fixture.Tick();
        Require(!fixture.Player.IsDead, "Standing with the original matching wire must be safe.");

        fixture.Player.LockInput();
        fixture.Dual.Interact(new InteractionDetails(fixture.Player.gameObject, fixture.Dual.gameObject));
        Require(fixture.Environment.HeldWire != fixture.Wire && fixture.Environment.HeldWire.IsHeld &&
            fixture.Environment.HeldWire.Polarity == WirePolarity.Neutral,
            "The real dual socket must hand over the opposite wire.");
        fixture.Tick();
        Require(fixture.Player.IsDead,
            "Changing the held wire while already standing must kill even while input is locked.");
    }

    public static IEnumerator CheckAutomaticPhysicsCallback()
    {
        BeginChecks();
        Require(Time.timeScale > 0f, "Automatic physics callbacks require an unpaused game.");
        if (CoreFacade.Instance == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Core.prefab");
            Require(prefab != null, "The authored Core prefab must be available for runtime startup.");
            ownedCore = UnityEngine.Object.Instantiate(prefab);
        }
        Require(CoreFacade.Instance != null && CoreFacade.Instance.Input != null,
            "Player startup needs the real Core input service.");

        Fixture fixture = new Fixture(WirePolarity.Neutral, WirePolarity.Neutral);
        fixture.Wire.PlugInto(fixture.Outlet.transform, false);
        Set(fixture.Environment, "heldWire", null);
        int deaths = 0;
        fixture.Player.Died += () => deaths++;
        fixture.EstablishContact(Vector2.down);
        fixture.Player.LockInput();
        fixture.Player.enabled = true;
        // No reflection call here: Unity runs Start, then the component's own FixedUpdate.
        yield return new WaitForFixedUpdate();
        Require(fixture.Player.IsDead && !fixture.Player.gameObject.activeSelf && !fixture.Body.simulated && deaths == 1,
            "Unity's automatic physics callback must kill an empty-handed player on polarized ground even with input locked.");
        yield return new WaitForFixedUpdate();
        Require(deaths == 1, "Later real physics frames must not repeat the death notification.");
    }

    public static IEnumerator Cleanup()
    {
        IEnumerator routine = sceneCleanup.Run(ownedScenes);
        try
        {
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }
        }
        finally
        {
            try
            {
                (routine as IDisposable)?.Dispose();
            }
            finally
            {
                try
                {
                    if (ownedCore != null)
                    {
                        UnityEngine.Object.DestroyImmediate(ownedCore);
                        ownedCore = null;
                    }
                }
                finally
                {
                    if (ownedScenes.Count == 0)
                    {
                        foreach (Tile tile in ownedTiles)
                        {
                            UnityEngine.Object.DestroyImmediate(tile);
                        }
                        ownedTiles.Clear();
                    }
                    if (hasEnvironmentSnapshot)
                    {
                        typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                            .SetValue(null, previousEnvironment);
                        previousEnvironment = null;
                        hasEnvironmentSnapshot = false;
                    }
                }
            }
        }
    }

    private static void BeginChecks()
    {
        Require(Application.isPlaying, "Ground physics checks require Play Mode.");
        Require(ownedScenes.Count == 0, "A previous ground check still needs cleanup.");
        previousEnvironment = EnvironmentFacade.Current;
        hasEnvironmentSnapshot = true;
    }

    private static void CheckLethal(WirePolarity ground, WirePolarity wire, bool childCollider)
    {
        Fixture fixture = new Fixture(ground, wire, true, false, childCollider);
        int deaths = 0;
        fixture.Player.Died += () => deaths++;
        fixture.EstablishContact(Vector2.down);
        fixture.Tick();
        Require(fixture.Player.IsDead && !fixture.Player.gameObject.activeSelf && !fixture.Body.simulated && deaths == 1,
            $"Standing on {ground} ground with {wire} wire must disable the player and emit death once.");
        fixture.Tick();
        fixture.Player.Die();
        Require(deaths == 1, "Further physics logic and explicit death requests must not repeat Died.");
    }

    private static void CheckSafe(WirePolarity ground, WirePolarity wire)
    {
        Fixture fixture = new Fixture(ground, wire);
        fixture.EstablishContact(Vector2.down);
        fixture.Tick();
        Require(!fixture.Player.IsDead, $"Standing on {ground} ground with {wire} wire must be safe.");
    }

    private static void CheckNonSupportingContact(Vector2 direction)
    {
        Fixture fixture = new Fixture(WirePolarity.Neutral, WirePolarity.Live);
        fixture.EstablishContact(direction);
        fixture.Tick();
        Require(!fixture.Player.IsDead, "Touching a polarized wall or ceiling must not count as standing on it.");
    }

    private sealed class Fixture
    {
        public PlayerMove Player { get; }
        public Rigidbody2D Body { get; }
        public BoxCollider2D PlayerCollider { get; }
        public BoxCollider2D Surface { get; }
        public GroundPolarity Ground { get; }
        public EnvironmentFacade Environment { get; }
        public Wire Wire { get; }
        public PowerSocket Outlet { get; }
        public PolaritySocket Dual { get; }
        public PhysicsScene2D Physics { get; }
        private readonly Scene scene;

        public Fixture(WirePolarity groundPolarity, WirePolarity wirePolarity, bool addGround = true,
            bool secondWire = false, bool childCollider = false)
        {
            scene = SceneManager.CreateScene("GroundPolarityCheck_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            ownedScenes.Add(scene);
            Physics = scene.GetPhysicsScene2D();

            GameObject playerObject = Create("Player", Vector2.zero);
            playerObject.tag = "Player";
            Player = playerObject.AddComponent<PlayerMove>();
            // Drive only the physics logic explicitly; no Core, input bindings or automatic Start is needed.
            Player.enabled = false;
            Player.GetResistance = position => Vector2.zero;
            Body = playerObject.GetComponent<Rigidbody2D>();
            Body.gravityScale = 0f;
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            PlayerCollider = playerObject.AddComponent<BoxCollider2D>();

            GameObject groundObject = Create("Ground", Vector2.down);
            if (addGround)
            {
                Ground = groundObject.AddComponent<GroundPolarity>();
                Set(Ground, "polarity", groundPolarity);
            }
            GameObject surfaceObject = groundObject;
            if (childCollider)
            {
                surfaceObject = Create("GroundCollider", Vector2.down);
                surfaceObject.transform.SetParent(groundObject.transform, true);
            }
            Surface = surfaceObject.AddComponent<BoxCollider2D>();

            GameObject outletObject = Create("Outlet", new Vector2(20f, 0f));
            outletObject.AddComponent<BoxCollider2D>().isTrigger = true;
            Outlet = outletObject.AddComponent<PowerSocket>();
            Wire = Create("Wire", new Vector2(20f, 0f)).AddComponent<Wire>();
            Set(Wire, "polarity", wirePolarity == WirePolarity.Neutral ? WirePolarity.Neutral : WirePolarity.Live);
            List<Wire> wires = new List<Wire> { Wire };
            if (secondWire)
            {
                Wire next = Create("NextWire", new Vector2(20f, 0f)).AddComponent<Wire>();
                Set(next, "polarity", WirePolarity.Neutral);
                wires.Add(next);
                GameObject dualObject = Create("DualSocket", new Vector2(30f, 0f));
                dualObject.AddComponent<BoxCollider2D>().isTrigger = true;
                Dual = dualObject.AddComponent<PolaritySocket>();
            }
            Set(Outlet, "wires", wires);

            GameObject gridObject = Create("Grid", new Vector2(-0.5f, -0.5f));
            gridObject.AddComponent<Grid>();
            GameObject mapObject = Create("RoutingTilemap", gridObject.transform.position);
            mapObject.transform.SetParent(gridObject.transform, true);
            Tilemap map = mapObject.AddComponent<Tilemap>();
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            ownedTiles.Add(tile);
            for (int x = -3; x <= 32; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    map.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
            GameObject environmentObject = Create("Environment", Vector2.zero);
            environmentObject.SetActive(false);
            Environment = environmentObject.AddComponent<EnvironmentFacade>();
            Set(Environment, "routingTilemap", map);
            environmentObject.SetActive(true);
            Require(Environment.HeldWire == Wire && Wire.IsHeld,
                "The owned environment must initialize with its real outlet wire held.");
            // Defensive tests inject unsupported carried polarities only after valid level initialization.
            Set(Wire, "polarity", wirePolarity);
        }

        public Wire PickUpGroundWire()
        {
            GameObject groundSocket = Create("GroundSocket", new Vector2(25f, 0f));
            groundSocket.AddComponent<BoxCollider2D>().isTrigger = true;
            PolaritySocket socket = groundSocket.AddComponent<PolaritySocket>();
            Set(socket, "accepted", WirePolarity.Ground);
            Environment.RefreshNodes();
            socket.Interact(new InteractionDetails(Player.gameObject, groundSocket));
            return Environment.HeldGroundWire;
        }

        public void EstablishContact(Vector2 towardSurface)
        {
            Surface.transform.position = towardSurface * 0.95f;
            Body.position = Vector2.zero;
            Body.velocity = towardSurface;
            Physics2D.SyncTransforms();
            Require(Physics.Simulate(0.02f), "The local physics scene must simulate.");
            List<ContactPoint2D> contacts = new List<ContactPoint2D>();
            Body.GetContacts(contacts);
            bool expectedContact = false;
            foreach (ContactPoint2D contact in contacts)
            {
                if ((contact.collider == Surface || contact.otherCollider == Surface) &&
                    Vector2.Dot(contact.normal, -towardSurface) > 0.9f)
                {
                    expectedContact = true;
                }
            }
            Require(expectedContact, "The scenario must have a real contact with the intended surface normal.");
        }

        public void Tick()
        {
            typeof(PlayerMove).GetMethod("FixedUpdate", PrivateInstance).Invoke(Player, null);
        }

        private GameObject Create(string name, Vector2 position)
        {
            GameObject instance = new GameObject(name);
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.position = position;
            return instance;
        }
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }

    private static void Require(bool passed, string description)
    {
        if (!passed)
        {
            throw new InvalidOperationException(description);
        }
    }
}
