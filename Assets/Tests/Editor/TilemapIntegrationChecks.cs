using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Exercises real Tilemap routing in owned scenes, including actual LateUpdate frames.</summary>
public static class TilemapIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<Scene> ownedScenes = new List<Scene>();
    private static readonly List<Tile> ownedTiles = new List<Tile>();
    private static EnvironmentFacade previousEnvironment;
    private static bool hasEnvironmentSnapshot;
    private static readonly OwnedPhysicsSceneCleanup sceneCleanup = new OwnedPhysicsSceneCleanup();

    public static void CheckPathAndLength()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Move(3, 0);
        fixture.Move(3, 2);
        AssertCells(fixture.Wire, new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0),
            new Vector3Int(3, 0, 0), new Vector3Int(3, 1, 0), new Vector3Int(3, 2, 0));
        Require(Mathf.Approximately(fixture.Wire.TilePath.GetLength(fixture.Map), 5f),
            "Wire length must include every visited tile edge, rather than endpoint distance.");
        Set(fixture.Wire, "maxLength", 5f);
        Require(fixture.Environment.GetResistance(fixture.Player.transform.position) == Vector2.zero,
            "A wire exactly at the tile-path length limit is safe.");
        Set(fixture.Wire, "maxLength", 4.99f);
        Require(float.IsNegativeInfinity(fixture.Environment.GetResistance(fixture.Player.transform.position).x),
            "A tile path strictly beyond its limit is lethal.");
        fixture.Move(3, 0);
        fixture.Move(0, 0);
        AssertCells(fixture.Wire, Vector3Int.zero);
        Require(!fixture.Wire.PathCollider.enabled, "A fully retracted zero-length wire has no query edge.");
    }

    public static void CheckFastAndDiagonalRetrace()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Move(8, 0);
        Require(fixture.Wire.TilePath.Cells.Count == 9, "Fast movement must include all eight crossed tile edges.");
        fixture.Move(0, 0);
        AssertCells(fixture.Wire, Vector3Int.zero);
        foreach (Vector3Int destination in new[] { new Vector3Int(4, 4, 0), new Vector3Int(-4, 4, 0),
            new Vector3Int(-4, -4, 0), new Vector3Int(4, -4, 0), new Vector3Int(7, 3, 0) })
        {
            fixture.Move(destination.x, destination.y);
            Require(fixture.Wire.TilePath.Cells.Count == Mathf.Abs(destination.x) + Mathf.Abs(destination.y) + 1,
                "A diagonal sweep must generate a contiguous Manhattan path.");
            AssertContiguous(fixture.Wire);
            fixture.Move(0, 0);
            AssertCells(fixture.Wire, Vector3Int.zero);
        }
        foreach (Vector3 end in new[] { new Vector3(1.4999998f, 1.5f), new Vector3(1.5f, 1.4999998f),
            new Vector3(-0.4999998f, 1.5f), new Vector3(1.5f, -0.4999998f), new Vector3(7.499999f, 3.5f) })
        {
            fixture.Player.transform.position = end;
            fixture.Tick();
            AssertContiguous(fixture.Wire);
            fixture.Move(0, 0);
            AssertCells(fixture.Wire, Vector3Int.zero);
        }
    }

    public static void CheckSameCellAndMissingTile()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Move(2, 0);
        for (int i = 0; i < 100; i++)
        {
            fixture.Player.transform.position = fixture.Center(2, 0) + Vector3.up * (i * 0.001f);
            fixture.Tick();
        }
        Require(fixture.Wire.TilePath.Cells.Count == 3 &&
            Mathf.Approximately(fixture.Wire.TilePath.GetLength(fixture.Map), 2f),
            "Sub-cell movement must neither duplicate cells nor change tile-based length.");
        PlayerInteraction interaction = fixture.Player.AddComponent<PlayerInteraction>();
        Anchor anchorPrefab = AssetDatabase.LoadAssetAtPath<Anchor>("Assets/Scenes/Prefab/Anchor.prefab");
        Require(anchorPrefab != null, "The anchor prefab must be available before checking tile rejection.");
        Set(interaction, "anchorPrefab", anchorPrefab);
        fixture.Map.SetTile(new Vector3Int(2, 0, 0), null);
        Require(!interaction.TryPlaceAnchor(), "Anchor placement must reject a cell without a tile.");
        interaction.enabled = false;
    }

    public static void CheckAnchorPinAndRelease()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Move(3, 0);
        Anchor anchor = fixture.AddAnchor(3, 0);
        anchor.Interact(new InteractionDetails(fixture.Player, anchor.gameObject));
        Require(anchor.IsEngaged, "The current tile must accept a manual Anchor pin.");
        fixture.Move(5, 0);
        fixture.Move(3, 0);
        Require(fixture.Wire.TilePath.Cells.Count == 4, "The unpinned tail must retract to the Anchor.");
        fixture.Move(2, 0);
        Require(fixture.Wire.TilePath.Cells.Count == 5 &&
            Mathf.Approximately(fixture.Wire.TilePath.GetLength(fixture.Map), 4f),
            "Crossing behind an Anchor must extend a new tail while preserving its earlier route.");
        fixture.Move(3, 0);
        Require(anchor.TryReclaim(new InteractionDetails(fixture.Player, anchor.gameObject)),
            "Reclaim must release the pin without adding inventory.");
        fixture.Move(0, 0);
        AssertCells(fixture.Wire, Vector3Int.zero);
    }

    public static void CheckWireSwapAndRestart()
    {
        BeginChecks();
        Fixture fixture = new Fixture(true);
        fixture.Move(3, 0);
        Anchor anchor = fixture.AddAnchor(3, 0);
        anchor.Interact(new InteractionDetails(fixture.Player, anchor.gameObject));
        fixture.Move(3, 2);
        fixture.Dual.Interact(new InteractionDetails(fixture.Player, fixture.Dual.gameObject));
        Require(fixture.Environment.HeldWire == fixture.SecondWire, "The dual socket must switch to the second wire.");
        Vector3Int[] firstPath = new List<Vector3Int>(fixture.Wire.TilePath.Cells).ToArray();
        fixture.Move(3, 0);
        fixture.Move(0, 0);
        Require(fixture.SecondWire.TilePath.Cells.Count > firstPath.Length &&
            fixture.SecondWire.TilePath.Cells[fixture.SecondWire.TilePath.Cells.Count - 1] == Vector3Int.zero,
            "Returning from a connected interface must lay a return path without retracting the connection.");
        AssertCells(fixture.Wire, firstPath);
        Require(anchor.EngagedBy == fixture.Wire, "A swap must keep the first wire's Anchor ownership.");
        fixture.Outlet.Interact(new InteractionDetails(fixture.Player, fixture.Outlet.gameObject));
        Require(fixture.SecondWire.IsClosed && !fixture.Environment.HeldWire,
            "Closing the return wire must preserve the first wire's independent path.");
        fixture.Restart();
        Require(!anchor.IsEngaged && fixture.Environment.HeldWire == fixture.Wire && !fixture.SecondWire.IsClosed,
            "Restart must release every pin and restore initial circuit state.");
        AssertCells(fixture.Wire, Vector3Int.zero);
        AssertCells(fixture.SecondWire, Vector3Int.zero);
        typeof(EnvironmentFacade).GetMethod("DebugRunAcceptance", PrivateInstance).Invoke(fixture.Environment, null);
        Require((int)typeof(EnvironmentFacade).GetField("failedSteps", PrivateInstance).GetValue(fixture.Environment) == 0 &&
            (int)typeof(EnvironmentFacade).GetField("acceptedSteps", PrivateInstance).GetValue(fixture.Environment) >= 5,
            "Scripted acceptance must visit the Anchor tile before attempting to pin it.");
    }

    public static void CheckTransformedGeometryAndInteractionRange()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Map.transform.parent.localScale = new Vector3(2f, 0.5f, 1f);
        fixture.Player.transform.position = fixture.Center(0, 0);
        fixture.Outlet.transform.position = fixture.Center(0, 0);
        fixture.Restart();
        fixture.Wire.transform.localScale = new Vector3(2f, 0.5f, 1f);
        fixture.Wire.transform.rotation = Quaternion.Euler(0f, 0f, 31f);
        fixture.Wire.PathCollider.offset = new Vector2(1.2f, -0.7f);
        fixture.Move(3, 0);
        fixture.Move(3, 2);
        Require(Mathf.Approximately(fixture.Wire.TilePath.GetLength(fixture.Map), 7f),
            "Length must use world-space cell edges, including nonuniform grid scale.");
        AssertRenderedPath(fixture);
        fixture.Wire.PathCollider.offset = new Vector2(-1.6f, 0.9f);
        fixture.Tick();
        AssertRenderedPath(fixture);
        PlayerInteraction interaction = fixture.Player.AddComponent<PlayerInteraction>();
        Set(interaction, "interactionRadius", 0.3f);
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        List<Collider2D> overlaps = new List<Collider2D>();
        fixture.Scene.GetPhysicsScene2D().OverlapCircle(fixture.Player.transform.position, 0.3f, filter, overlaps);
        Require(overlaps.Contains(fixture.Wire.PathCollider), "The remote interaction query must really hit the wire.");
        Require(!interaction.TryPerformOperation() && fixture.Environment.HeldWire == fixture.Wire,
            "A wire collider must not make its distant parent outlet interactable.");
        interaction.enabled = false;
    }

    public static IEnumerator CheckAutomaticLateUpdate()
    {
        BeginChecks();
        Fixture fixture = new Fixture();
        fixture.Environment.enabled = true;
        fixture.Player.transform.position = fixture.Center(4, 0);
        yield return null;
        yield return null;
        Require(fixture.Wire.TilePath.Cells.Count == 5, "Real LateUpdate must record all crossed player cells.");
        Anchor anchor = fixture.AddAnchor(4, 0);
        anchor.Interact(new InteractionDetails(fixture.Player, anchor.gameObject));
        fixture.Player.transform.position = fixture.Center(2, 0);
        yield return null;
        yield return null;
        Require(fixture.Wire.TilePath.Cells.Count == 7, "Real frames must preserve the route pinned by an Anchor.");
        fixture.Player.transform.position = fixture.Center(4, 0);
        yield return null;
        yield return null;
        anchor.TryReclaim(new InteractionDetails(fixture.Player, anchor.gameObject));
        fixture.Player.transform.position = fixture.Center(0, 0);
        yield return null;
        yield return null;
        Require(!anchor, "Unity must destroy the reclaimed Anchor after frame advancement.");
        AssertCells(fixture.Wire, Vector3Int.zero);
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

    private static void BeginChecks()
    {
        Require(Application.isPlaying && Time.timeScale > 0f, "Tilemap checks require unpaused Play Mode.");
        Require(ownedScenes.Count == 0, "A previous Tilemap check still needs cleanup.");
        previousEnvironment = EnvironmentFacade.Current;
        hasEnvironmentSnapshot = true;
    }

    private static void AssertCells(Wire wire, params Vector3Int[] expected)
    {
        Require(wire.TilePath.Cells.Count == expected.Length, "The route must have the expected cell count.");
        for (int i = 0; i < expected.Length; i++)
        {
            Require(wire.TilePath.Cells[i] == expected[i], "The route must preserve cell order at index " + i);
        }
        AssertContiguous(wire);
    }

    private static void AssertContiguous(Wire wire)
    {
        for (int i = 1; i < wire.TilePath.Cells.Count; i++)
        {
            Vector3Int delta = wire.TilePath.Cells[i] - wire.TilePath.Cells[i - 1];
            Require(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 && delta.z == 0,
                "Every pair of successive path cells must share an edge.");
        }
    }

    private static void AssertRenderedPath(Fixture fixture)
    {
        LineRenderer line = fixture.Wire.GetComponent<LineRenderer>();
        Vector2[] points = fixture.Wire.PathCollider.points;
        Require(line.positionCount == fixture.Wire.TilePath.Cells.Count && points.Length == line.positionCount,
            "Rendering and collision must use every tile path vertex.");
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 expected = fixture.Map.GetCellCenterWorld(fixture.Wire.TilePath.Cells[i]);
            Require(Vector3.Distance(line.GetPosition(i), expected) < 0.0001f,
                "Rendered vertices must use world-space tile centers.");
            Require(Vector3.Distance(fixture.Wire.transform.TransformPoint(points[i] + fixture.Wire.PathCollider.offset), expected) < 0.0001f,
                "Collider local coordinates and offset must map onto the rendered tile path.");
        }
    }

    private sealed class Fixture
    {
        public Scene Scene { get; }
        public Tilemap Map { get; }
        public GameObject Player { get; }
        public EnvironmentFacade Environment { get; }
        public Wire Wire { get; }
        public Wire SecondWire { get; }
        public PowerSocket Outlet { get; }
        public PolaritySocket Dual { get; }

        public Fixture(bool secondWire = false)
        {
            Scene = SceneManager.CreateScene("TilemapCheck_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            ownedScenes.Add(Scene);
            GameObject grid = Create("Grid", Vector3.zero);
            grid.AddComponent<Grid>();
            GameObject map = Create("RoutingTilemap", Vector3.zero);
            map.transform.SetParent(grid.transform, false);
            Map = map.AddComponent<Tilemap>();
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            ownedTiles.Add(tile);
            for (int x = -10; x <= 10; x++)
            {
                for (int y = -10; y <= 10; y++)
                {
                    Map.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
            Player = Create("Player", Center(0, 0));
            Player.tag = "Player";
            GameObject outlet = Create("Outlet", Center(0, 0));
            outlet.AddComponent<BoxCollider2D>().isTrigger = true;
            Outlet = outlet.AddComponent<PowerSocket>();
            GameObject wire = Create("LiveWire", outlet.transform.position);
            wire.transform.SetParent(outlet.transform, true);
            Wire = wire.AddComponent<Wire>();
            List<Wire> wires = new List<Wire> { Wire };
            if (secondWire)
            {
                SecondWire = CreateWire("NeutralWire", WirePolarity.Neutral);
                wires.Add(SecondWire);
                GameObject dual = Create("DualSocket", Center(3, 2));
                dual.AddComponent<BoxCollider2D>().isTrigger = true;
                Dual = dual.AddComponent<PolaritySocket>();
            }
            Set(Outlet, "wires", wires);
            GameObject environment = Create("Environment", Vector3.zero);
            environment.SetActive(false);
            Environment = environment.AddComponent<EnvironmentFacade>();
            Set(Environment, "routingTilemap", Map);
            // Explicit fixture voltages keep wiring checks independent of level tuning.
            Set(Environment, "neededVoltage", 0f);
            environment.SetActive(true);
            Environment.enabled = false;
            Require(Environment.HeldWire == Wire, "The real outlet must initialize the fixture's held wire.");
        }

        private Wire CreateWire(string name, WirePolarity polarity)
        {
            GameObject instance = Create(name, Outlet.transform.position);
            instance.transform.SetParent(Outlet.transform, true);
            Wire wire = instance.AddComponent<Wire>();
            Set(wire, "polarity", polarity);
            return wire;
        }

        public Vector3 Center(int x, int y) => Map.GetCellCenterWorld(new Vector3Int(x, y, 0));

        public Anchor AddAnchor(int x, int y)
        {
            GameObject instance = Create("Anchor", Center(x, y));
            Anchor anchor = instance.AddComponent<Anchor>();
            Environment.RegisterAnchor(anchor);
            return anchor;
        }

        public void Move(int x, int y)
        {
            Player.transform.position = Center(x, y);
            Tick();
        }

        public void Tick() => typeof(EnvironmentFacade).GetMethod("LateUpdate", PrivateInstance).Invoke(Environment, null);
        public void Restart() => typeof(EnvironmentFacade).GetMethod("DebugRestartRun", PrivateInstance).Invoke(Environment, null);

        private GameObject Create(string name, Vector3 position)
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
