using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Exercises real corner routing and collider geometry in owned scenes.
/// 独立场景中驱动真实绕线路径；仅实际帧用例启用自动 LateUpdate。
/// </summary>
public static class CornerIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<Scene> ownedScenes = new List<Scene>();
    private static EnvironmentFacade previousEnvironment;

    public static void CheckEntryAndReverse()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        Corner corner = fixture.AddCorner(Vector2.zero);
        fixture.Move(new Vector2(3f, 2f));
        Require(!corner.GetAnchor(fixture.Wire), "The first endpoint sample must not invent a movement.");
        fixture.Move(new Vector2(3f, 0f));
        Anchor anchor = corner.GetAnchor(fixture.Wire);
        Require(anchor && anchor.EngagedBy == fixture.Wire, "The entering wire segment must create and hook an Anchor.");
        Require(!corner.DetectionCollider.OverlapPoint(fixture.Wire.FreeEndPosition),
            "The endpoint remains outside: this test must detect the line rather than the player.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, corner.Center, fixture.Wire.FreeEndPosition);
        fixture.Move(new Vector2(3f, -2f));
        Require(corner.GetAnchor(fixture.Wire) == anchor,
            "Continuing around the opposite side must retain the same Anchor.");
        fixture.Tick();
        Require(corner.GetComponentsInChildren<Anchor>().Length == 1,
            "A stationary hooked tail must not create duplicate anchors.");
        fixture.Move(new Vector2(3f, 2f));
        Require(!corner.GetAnchor(fixture.Wire) && !anchor.IsEngaged && !anchor.gameObject.activeSelf,
            "Reversing onto the entry side must immediately detach and deactivate the generated Anchor.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, fixture.Wire.FreeEndPosition);
    }

    public static void CheckSweptEntryAndMultipleCorners()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        // Deliberately register the later contact first: hierarchy order must not control the path.
        Corner second = fixture.AddCorner(new Vector2(1f, -0.5f));
        Corner first = fixture.AddCorner(Vector2.zero);
        fixture.Move(new Vector2(3f, 3f));
        Physics2D.SyncTransforms();
        Require(!first.DetectionCollider.Distance(fixture.Wire.PathCollider).isOverlapped,
            "The initial wire must be outside the first circle.");
        fixture.Move(new Vector2(3f, -3f));
        Anchor firstAnchor = first.GetAnchor(fixture.Wire);
        Anchor secondAnchor = second.GetAnchor(fixture.Wire);
        Require(firstAnchor && secondAnchor && firstAnchor.EngagementSequence < secondAnchor.EngagementSequence,
            "A single fast movement must hook multiple corners in geometric contact order.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, first.Center, second.Center, fixture.Wire.FreeEndPosition);
        fixture.Move(new Vector2(3f, 3f));
        Require(!first.GetAnchor(fixture.Wire) && !second.GetAnchor(fixture.Wire),
            "One reverse sweep must unwind both corners from the tail backwards.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, fixture.Wire.FreeEndPosition);

        Fixture single = new Fixture();
        Corner swept = single.AddCorner(Vector2.zero);
        single.Move(new Vector2(3f, 3f));
        single.Move(new Vector2(3f, -3f));
        Require(swept.GetAnchor(single.Wire),
            "A fast crossing with both unhooked endpoint segments outside must still produce a hook.");
    }

    public static void CheckStationaryAndMovingAway()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        Corner corner = fixture.AddCorner(Vector2.zero);
        fixture.Move(new Vector2(3f, 0f));
        fixture.Tick();
        fixture.Tick();
        Require(!corner.GetAnchor(fixture.Wire), "An initially intersecting stationary line must not create an anchor.");
        fixture.Move(new Vector2(3f, 2f));
        Require(!corner.GetAnchor(fixture.Wire), "Movement away from initial overlap must not be classified as entry.");
        fixture.Move(new Vector2(3f, 0f));
        Require(corner.GetAnchor(fixture.Wire), "The subsequent inward movement must engage.");
        corner.DetectionCollider.enabled = false;
        fixture.Tick();
        Require(!corner.GetAnchor(fixture.Wire), "Disabling detection must release the owned anchor.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, fixture.Wire.FreeEndPosition);
    }

    public static void CheckAccumulatedSmallMovements()
    {
        BeginChecks();
        Fixture colliderFixture = new Fixture();
        Vector2 start = new Vector2(3f, 2f);
        colliderFixture.Move(start);
        for (int i = 1; i <= 100; i++)
        {
            colliderFixture.Move(start + Vector2.up * (0.000005f * i));
        }
        AssertPath(colliderFixture.Wire, colliderFixture.Wire.FixedEndPosition, colliderFixture.Wire.FreeEndPosition);

        Fixture routingFixture = new Fixture();
        Corner corner = routingFixture.AddCorner(Vector2.zero);
        // P=(-3,0), C=(0,0), E=(3,y): distance(C, PE)=3y/sqrt(36+y*y).
        // Start just above the true circle tangent, then approach in sub-tolerance steps.
        float radius = corner.DetectionCollider.radius;
        float tangentHeight = 6f * radius / Mathf.Sqrt(9f - radius * radius);
        Vector2 outside = new Vector2(3f, tangentHeight + 0.002f);
        routingFixture.Move(outside);
        // Box2D's edge skin can report contact outside the authored circle radius.
        // Validate the centerline used for entry geometry without increasing the movement steps.
        float centerlineDistance = 3f * outside.y / Mathf.Sqrt(36f + outside.y * outside.y);
        Require(centerlineDistance > radius,
            "The small-motion routing test must begin with its centerline outside the authored circle tangent.");
        for (int i = 1; i <= 100; i++)
        {
            routingFixture.Move(outside + Vector2.down * (0.00005f * i));
        }
        Require(corner.GetAnchor(routingFixture.Wire),
            "Repeated endpoint movements smaller than the per-sample tolerance must accumulate and hook.");
        AssertPath(routingFixture.Wire, routingFixture.Wire.FixedEndPosition, corner.Center,
            routingFixture.Wire.FreeEndPosition);
        for (int i = 99; i >= 0; i--)
        {
            routingFixture.Move(outside + Vector2.down * (0.00005f * i));
        }
        Require(!corner.GetAnchor(routingFixture.Wire),
            "Accumulated small reverse movements must also release the corner.");
        AssertPath(routingFixture.Wire, routingFixture.Wire.FixedEndPosition, routingFixture.Wire.FreeEndPosition);
    }

    public static void CheckWireSwapAndCleanup()
    {
        BeginChecks();
        Fixture fixture = new Fixture(true);
        Corner corner = fixture.AddCorner(Vector2.zero);
        fixture.Move(new Vector2(3f, 2f));
        fixture.Move(new Vector2(3f, -2f));
        Anchor oldAnchor = corner.GetAnchor(fixture.Wire);
        Require(oldAnchor, "The first wire must be hooked before switching.");
        fixture.Dual.Interact(new InteractionDetails(fixture.Player, fixture.Dual.gameObject));
        Require(fixture.Environment.HeldWire == fixture.SecondWire, "The real dual socket must switch the carried wire.");
        fixture.Tick();
        fixture.Move(new Vector2(3f, 2f));
        Anchor newAnchor = corner.GetAnchor(fixture.SecondWire);
        Require(newAnchor && newAnchor != oldAnchor && corner.GetAnchor(fixture.Wire) == oldAnchor,
            "One corner must own independent anchors for both wires after a switch.");
        fixture.Outlet.Interact(new InteractionDetails(fixture.Player, fixture.Outlet.gameObject));
        fixture.Move(new Vector2(3f, -2f));
        Require(fixture.SecondWire.IsClosed && fixture.Environment.HeldWire == null &&
            corner.GetAnchor(fixture.Wire) == oldAnchor && corner.GetAnchor(fixture.SecondWire) == newAnchor,
            "Plugged and closed wires must retain their routing when the player moves without a wire.");
        fixture.Restart();
        Require(!corner.GetAnchor(fixture.Wire) && !corner.GetAnchor(fixture.SecondWire) &&
            !oldAnchor.IsEngaged && !newAnchor.IsEngaged,
            "Restart must detach all generated anchors, including anchors on inactive wires.");
        fixture.Move(new Vector2(3f, 2f));
        fixture.Move(new Vector2(3f, -2f));
        Require(corner.GetAnchor(fixture.Wire), "Routing must work again after restarting.");
        corner.enabled = false;
        Require(!corner.GetAnchor(fixture.Wire), "Disabling the Corner component must immediately release its anchor.");
        fixture.Tick();
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, fixture.Wire.FreeEndPosition);
    }

    public static void CheckColliderCoordinatesAndInteractionRange()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Wire.transform.localScale = new Vector3(2f, 0.5f, 1f);
        fixture.Wire.transform.rotation = Quaternion.Euler(0f, 0f, 31f);
        Corner corner = fixture.AddCorner(new Vector2(0.5f, -0.2f));
        corner.DetectionCollider.offset = new Vector2(-0.5f, 0.2f);
        fixture.Move(new Vector2(3f, 2f));
        fixture.Move(new Vector2(3f, 0f));
        Require(corner.GetAnchor(fixture.Wire), "A circle's local offset must be applied to its detection center.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, corner.Center, fixture.Wire.FreeEndPosition);
        Physics2D.SyncTransforms();
        ColliderDistance2D contact = corner.DetectionCollider.Distance(fixture.Wire.PathCollider);
        Require(contact.isValid && contact.isOverlapped,
            "The real transformed EdgeCollider must overlap the same corner as the rendered path.");

        Fixture remote = new Fixture();
        remote.Move(new Vector2(3f, 2f));
        PlayerInteraction interaction = remote.Player.AddComponent<PlayerInteraction>();
        Set(interaction, "interactionRadius", 0.3f);
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        List<Collider2D> overlaps = new List<Collider2D>();
        remote.Scene.GetPhysicsScene2D().OverlapCircle(remote.Player.transform.position, 0.3f, filter, overlaps);
        Require(overlaps.Contains(remote.Wire.PathCollider),
            "The interaction query must actually hit the extended wire collider in the remote-interaction scenario.");
        Require(!interaction.TryPerformOperation() && remote.Environment.HeldWire == remote.Wire,
            "Touching a wire child collider must not allow interaction with its remote parent PowerSocket.");
        interaction.enabled = false;
    }

    public static IEnumerator CheckAutomaticLateUpdateAndDestroy()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Env/Corner.prefab");
        Require(prefab && prefab.GetComponent<Corner>() && prefab.GetComponent<CircleCollider2D>(),
            "The authored Corner prefab must have both the behavior and CircleCollider2D.");
        GameObject instance = UnityEngine.Object.Instantiate(prefab, fixture.Environment.transform);
        instance.transform.position = Vector3.zero;
        Corner corner = instance.GetComponent<Corner>();
        fixture.Environment.RefreshNodes();
        fixture.Player.transform.position = new Vector2(3f, 2f);
        fixture.Environment.enabled = true;
        // These yields let Unity call the real LateUpdate without a reflection-driven tick.
        yield return null;
        yield return null;
        Require(!corner.GetAnchor(fixture.Wire), "The initial real frame must only establish the free-end sample.");
        fixture.Player.transform.position = new Vector2(3f, 0f);
        yield return null;
        yield return null;
        Anchor anchor = corner.GetAnchor(fixture.Wire);
        Require(anchor && anchor.IsEngaged, "Unity's actual LateUpdate must hook the authored prefab.");
        Physics2D.SyncTransforms();
        ColliderDistance2D contact = corner.DetectionCollider.Distance(fixture.Wire.PathCollider);
        Require(contact.isValid && contact.isOverlapped, "Real wire and circle colliders must overlap after hooking.");
        fixture.Player.transform.position = new Vector2(3f, 2f);
        yield return null;
        yield return null;
        Require(!corner.GetAnchor(fixture.Wire) && !anchor,
            "After reversing, the generated Anchor must be detached and actually destroyed by Unity.");
        AssertPath(fixture.Wire, fixture.Wire.FixedEndPosition, fixture.Wire.FreeEndPosition);
    }

    public static IEnumerator Cleanup()
    {
        foreach (Scene scene in ownedScenes)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }
            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
            Require(operation != null, "Could not unload the owned corner test scene.");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!operation.isDone)
            {
                Require(Time.realtimeSinceStartup < deadline, "Timed out unloading the owned corner test scene.");
                yield return null;
            }
        }
        ownedScenes.Clear();
        typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, previousEnvironment);
        previousEnvironment = null;
    }

    private static void BeginChecks()
    {
        Require(Application.isPlaying && Time.timeScale > 0f, "Corner integration checks require unpaused Play Mode.");
        Require(ownedScenes.Count == 0, "A previous corner check still needs cleanup.");
        previousEnvironment = EnvironmentFacade.Current;
    }

    private static void AssertPath(Wire wire, params Vector3[] expected)
    {
        LineRenderer line = wire.GetComponent<LineRenderer>();
        Vector2[] colliderPoints = wire.PathCollider.points;
        Require(line.positionCount == expected.Length && colliderPoints.Length == expected.Length &&
            wire.PathCollider.enabled && wire.PathCollider.isTrigger,
            "The rendered and trigger collider paths must contain the same expected vertices.");
        for (int i = 0; i < expected.Length; i++)
        {
            Require(Vector3.Distance(line.GetPosition(i), expected[i]) < 0.0001f,
                "The renderer vertex must follow the circuit's ordered route.");
            Require(Vector2.Distance(wire.transform.TransformPoint(colliderPoints[i]), expected[i]) < 0.0001f,
                "The local collider vertex must map back onto the rendered world-space vertex.");
        }
    }

    private sealed class Fixture
    {
        public Scene Scene { get; }
        public GameObject Player { get; }
        public EnvironmentFacade Environment { get; }
        public Wire Wire { get; }
        public Wire SecondWire { get; }
        public PowerSocket Outlet { get; }
        public PolaritySocket Dual { get; }

        public Fixture(bool secondWire = false)
        {
            Scene = SceneManager.CreateScene("CornerCheck_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            ownedScenes.Add(Scene);
            Player = Create("Player", new Vector2(3f, 2f));
            Player.tag = "Player";
            PlayerInventory inventory = Player.AddComponent<PlayerInventory>();
            GameObject attach = Create("WireAttach", Player.transform.position);
            attach.tag = "WireAttach";
            attach.transform.SetParent(Player.transform, true);
            GameObject outlet = Create("Outlet", new Vector2(-3f, 0f));
            outlet.AddComponent<BoxCollider2D>().isTrigger = true;
            Outlet = outlet.AddComponent<PowerSocket>();
            GameObject wireObject = Create("LiveWire", outlet.transform.position);
            wireObject.transform.SetParent(outlet.transform, true);
            Wire = wireObject.AddComponent<Wire>();
            List<Wire> wires = new List<Wire> { Wire };
            if (secondWire)
            {
                GameObject next = Create("NeutralWire", outlet.transform.position);
                next.transform.SetParent(outlet.transform, true);
                SecondWire = next.AddComponent<Wire>();
                Set(SecondWire, "polarity", WirePolarity.Neutral);
                wires.Add(SecondWire);
                GameObject dual = Create("DualSocket", new Vector2(3f, -2f));
                dual.AddComponent<BoxCollider2D>().isTrigger = true;
                Dual = dual.AddComponent<PolaritySocket>();
            }
            Set(Outlet, "wires", wires);
            GameObject environment = Create("Environment", Vector2.zero);
            environment.SetActive(false);
            Environment = environment.AddComponent<EnvironmentFacade>();
            Set(inventory, "environment", Environment);
            environment.SetActive(true);
            Environment.enabled = false;
            Require(Environment.HeldWire == Wire, "The fixture must start with its real outlet wire held.");
        }

        public Corner AddCorner(Vector2 position)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Env/Anchor.prefab");
            Anchor prefab = asset ? asset.GetComponent<Anchor>() : null;
            Require(prefab, "The authored Anchor prefab must be available.");
            GameObject instance = Create("Corner", position);
            instance.SetActive(false);
            Corner corner = instance.AddComponent<Corner>();
            corner.DetectionCollider.radius = 0.25f;
            Set(corner, "anchorPrefab", prefab);
            instance.SetActive(true);
            return corner;
        }

        public void Move(Vector2 end)
        {
            Player.transform.position = end;
            Tick();
        }

        public void Tick()
        {
            typeof(EnvironmentFacade).GetMethod("LateUpdate", PrivateInstance).Invoke(Environment, null);
        }

        public void Restart()
        {
            typeof(EnvironmentFacade).GetMethod("DebugRestartRun", PrivateInstance).Invoke(Environment, null);
        }

        private GameObject Create(string name, Vector2 position)
        {
            GameObject instance = new GameObject(name);
            SceneManager.MoveGameObjectToScene(instance, Scene);
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
