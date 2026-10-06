using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Real socket interactions in a disposable preview scene; no authored scene is saved.</summary>
public static class CircuitClosureIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void CheckCircuit(string scenario, bool neutralFirst)
    {
        using (var fixture = new Fixture(neutralFirst))
        {
            PolaritySocket first = fixture.AddInterface("First", 3, 0);
            PolaritySocket second = fixture.AddInterface("Second", 3, 3);
            fixture.Restart();
            int cleared = 0;
            fixture.Environment.LevelCleared += () => cleared++;
            fixture.Interact(first);
            Require(fixture.Environment.HeldWire == fixture.Second && fixture.Second.PreviousWire == fixture.First &&
                !fixture.Environment.IsCircuitClosed, "First socket must hand over the opposite wire; other sockets are still missing.");
            if (scenario == "MissingInterface" || scenario == "WalkOnly")
            {
                if (scenario == "WalkOnly")
                {
                    fixture.Move(second.transform.position);
                }
                fixture.Interact(fixture.Outlet);
                Require(!fixture.Environment.IsCircuitClosed && cleared == 0, "Walking past an unconnected socket must not complete it.");
                return;
            }
            fixture.Interact(second);
            Wire third = fixture.Environment.HeldWire;
            Require(third && third != fixture.Second && third.Polarity == fixture.First.Polarity &&
                third.IsHeld && fixture.Environment.SwapCount == 2, "Exhausted authored wires must still produce an opposite-polarity handover.");
            if (scenario == "NoReturnRestriction")
            {
                // Same-polarity return fills the remaining source slot through its outgoing opposite wire.
                fixture.Interact(fixture.Outlet);
                Require(fixture.Environment.IsCircuitClosed && cleared == 1 && fixture.Environment.HeldWire.IsHeld,
                    "All socket anchors are wired: no opposite-return or closed-loop restriction may remain.");
            }
            else
            {
                fixture.Interact(first);
                Require(fixture.Environment.SwapCount == 3 && fixture.Environment.HeldWire.Polarity != third.Polarity,
                    "Each repeated dual socket interaction must still switch polarity.");
                fixture.Interact(fixture.Outlet);
                Require(fixture.Environment.IsCircuitClosed && cleared == 1, "Every socket is connected and voltage is within target.");
            }
            fixture.Environment.RefreshNodes();
            Require(fixture.Environment.EvaluateCircuit() && cleared == 1, "Refresh must retain connections without repeating victory.");
            fixture.Restart();
            Require(!fixture.Environment.IsCircuitClosed && fixture.Environment.HeldWire == fixture.First &&
                fixture.Second.ConnectedInterfaces.Count == 0 && !fixture.Second.CircuitStart && !third,
                "Restart must clear authored connections and destroy generated continuations.");
        }
    }

    public static void CheckGroundAndVoltage(string scenario)
    {
        using (var fixture = new Fixture(false))
        {
            PolaritySocket groundA = fixture.AddInterface("GroundA", 3, 0);
            PolaritySocket groundB = fixture.AddInterface("GroundB", 3, 3);
            Set(groundA, "accepted", WirePolarity.Ground);
            Set(groundB, "accepted", WirePolarity.Ground);
            VoltageReducer reducer = fixture.AddReducer("Reducer", 1, 0, 30f);
            VoltageReducer unused = fixture.AddReducer("Unused", 1, 1, 100f);
            Set(fixture.Environment, "initialVoltage", 220f);
            Set(fixture.Environment, "targetVoltage", scenario == "VoltageTooHigh" ? 189f : 190f);
            fixture.Restart();
            int cleared = 0;
            fixture.Environment.LevelCleared += () => cleared++;
            fixture.Interact(groundA);
            Wire ground = fixture.Environment.HeldGroundWire;
            Require(ground && ground.Polarity == WirePolarity.Ground && ground.IsHeld &&
                fixture.Environment.HeldWire == fixture.First, "Ground pickup must coexist with the powered wire.");
            fixture.Move(groundB.transform.position);
            Require(ground.TilePath.Cells.Count > 1, "Ground wire must follow player tile movement.");
            fixture.Interact(reducer);
            fixture.Interact(reducer);
            Require(fixture.Environment.CurrentVoltage == 190d && fixture.Environment.HeldWire == fixture.First &&
                fixture.Environment.HeldGroundWire == ground, "Repeated reducer use must count once and retain both held wires.");
            if (scenario == "MissingGround")
            {
                fixture.Interact(fixture.Outlet);
                Require(!fixture.Environment.IsCircuitClosed, "Every ground socket must also be wired.");
                return;
            }
            fixture.Interact(groundB);
            Require(!fixture.Environment.HeldGroundWire && ground.PlugTarget == groundB.transform && !ground.IsHeld,
                "Second ground interaction must connect and release only the ground wire.");
            fixture.Interact(fixture.Outlet);
            bool expected = scenario != "VoltageTooHigh";
            Require(fixture.Environment.IsCircuitClosed == expected && cleared == (expected ? 1 : 0),
                "Voltage equal to target succeeds; voltage one unit above target fails.");
            fixture.Environment.RefreshNodes();
            Require(fixture.Environment.EvaluateCircuit() == expected && fixture.Environment.CurrentVoltage == 190d,
                "Refresh must preserve ground endpoints and deduplicated reducer connections.");
            if (scenario == "VoltageTooHigh")
            {
                fixture.Interact(unused);
                Require(fixture.Environment.IsCircuitClosed && fixture.Environment.CurrentVoltage == 90d && cleared == 1,
                    "A newly connected second reducer must add its drop and re-evaluate victory.");
            }
            if (scenario == "DestroyedWire")
            {
                UnityEngine.Object.DestroyImmediate(ground.gameObject);
                Require(!fixture.Environment.EvaluateCircuit(), "Destroyed ground wires must no longer satisfy socket anchors.");
            }
            fixture.Restart();
            Require(!fixture.Environment.HeldGroundWire && fixture.Environment.CurrentVoltage == 220d &&
                !fixture.Environment.IsCircuitClosed, "Restart must clear ground, voltage drops and victory.");
        }
    }

    public static void CheckRemoteInteraction(string kind)
    {
        using (var fixture = new Fixture(false))
        {
            PolaritySocket ground = fixture.AddInterface("Ground", 2, 0);
            Set(ground, "accepted", WirePolarity.Ground);
            PolaritySocket dual = fixture.AddInterface("Dual", 2, 1);
            VoltageReducer reducer = fixture.AddReducer("Reducer", 2, 1, 30f);
            fixture.Restart();
            ground.Interact(new InteractionDetails(fixture.Player, ground.gameObject));
            Wire groundWire = fixture.Environment.HeldGroundWire;
            IEnvironmentInteractable target = kind == "Reducer" ? (IEnvironmentInteractable)reducer : dual;
            target.Interact(new InteractionDetails(fixture.Player, ((Component)target).gameObject));
            fixture.Environment.SamplePlayerPath(fixture.Player.transform.position);
            AssertAdjacent(fixture.Environment.HeldWire);
            AssertAdjacent(groundWire);
            Vector3Int actorCell = fixture.Environment.RoutingTilemap.WorldToCell(fixture.Player.transform.position);
            actorCell.z = 0;
            Require(fixture.Environment.HeldWire.TilePath.Cells[fixture.Environment.HeldWire.TilePath.Cells.Count - 1] == actorCell &&
                groundWire.TilePath.Cells[groundWire.TilePath.Cells.Count - 1] == actorCell,
                "Both held wire paths must independently end at the actor after a remote interaction.");
        }
    }

    public static void CheckOutletPin()
    {
        using (var fixture = new Fixture(false))
        {
            fixture.Restart();
            fixture.Move(new Vector3(3.5f, 0.5f));
            fixture.Move(new Vector3(3.5f, 3.5f));
            fixture.Move(new Vector3(0.5f, 3.5f));
            fixture.Interact(fixture.Outlet);
            Wire next = fixture.Environment.HeldWire;
            int before = next.TilePath.Cells.Count;
            fixture.Move(new Vector3(0.5f, 1.5f));
            Require(next.TilePath.Cells.Count == before + 1,
                "PowerSocket handover must pin the inherited route; walking back lays a new tail.");
            AssertAdjacent(next);
        }
    }

    public static void CheckExamples()
    {
        GameObject ground = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Env/Sockets/GroundSocket.prefab");
        GameObject reducer = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Env/VoltageReducer.prefab");
        Require(ground && ground.GetComponent<PolaritySocket>().Accepted == WirePolarity.Ground &&
            ground.GetComponent<PolaritySocket>().IsConfigurationValid && ground.GetComponent<Collider2D>() &&
            ground.GetComponent<SpriteRenderer>().sprite,
            "Ground example must contain a valid ground socket and interaction collider.");
        Require(reducer && reducer.GetComponent<VoltageReducer>() && reducer.GetComponent<Collider2D>() &&
            reducer.GetComponent<SpriteRenderer>().sprite,
            "Reducer example must contain a voltage reducer component and interaction collider.");
    }

    private static void AssertAdjacent(Wire wire)
    {
        IReadOnlyList<Vector3Int> cells = wire.TilePath.Cells;
        for (int i = 1; i < cells.Count; i++)
        {
            Vector3Int delta = cells[i] - cells[i - 1];
            Require(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 && delta.z == 0,
                "Cross-cell interaction must preserve a four-connected wire route.");
        }
    }

    public static void CheckInvalidInitialization(int polarity)
    {
        using (var fixture = new Fixture(false))
        {
            PolaritySocket invalid = fixture.AddInterface("InvalidPole", 3, 3);
            Set(invalid, "accepted", (WirePolarity)polarity);
            typeof(PolaritySocket).GetMethod("Awake", PrivateInstance).Invoke(invalid, null);
            Require(!invalid.enabled && !invalid.CanInteract && !invalid.Initialize(),
                "Invalid poles must fail initialization and disable the component.");
            int interactions = 0;
            invalid.OnInteracted += _ => interactions++;
            invalid.enabled = true;
            invalid.Interact(new InteractionDetails(fixture.Player, invalid.gameObject));
            Require(interactions == 0, "Re-enabling an invalid component must not bypass initialization validation.");
            PolaritySocket valid = fixture.AddInterface("Valid", 3, 0);
            fixture.Restart();
            Require(!invalid.enabled, "Environment discovery must validate initialization in EditMode too.");
            fixture.Interact(valid);
            fixture.Interact(fixture.Outlet);
            Require(!fixture.Environment.IsCircuitClosed, "Disabling an invalid interface must not silently remove it from win requirements.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene scene;
        private readonly Tile tile;
        private readonly EnvironmentFacade previous = EnvironmentFacade.Current;
        public GameObject Player { get; }
        public PowerSocket Outlet { get; }
        public Wire First { get; }
        public Wire Second { get; }
        public EnvironmentFacade Environment { get; }

        public Fixture(bool neutralFirst)
        {
            scene = EditorSceneManager.NewPreviewScene();
            try
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                GameObject grid = Create("Grid", 0, 0);
                grid.AddComponent<Grid>();
                GameObject mapObject = Create("Map", 0, 0);
                mapObject.transform.SetParent(grid.transform, false);
                Tilemap map = mapObject.AddComponent<Tilemap>();
                for (int x = -1; x <= 5; x++)
                {
                    for (int y = -1; y <= 5; y++)
                    {
                        map.SetTile(new Vector3Int(x, y, 0), tile);
                    }
                }
                Player = Create("CircuitPlayer", 0, 0);
                Player.tag = "Player";
                Outlet = AddOutlet("Outlet", 0, 0);
                First = Create("FirstWire", 0, 0).AddComponent<Wire>();
                Second = Create("SecondWire", 0, 0).AddComponent<Wire>();
                Set(First, "polarity", neutralFirst ? WirePolarity.Neutral : WirePolarity.Live);
                Set(Second, "polarity", neutralFirst ? WirePolarity.Live : WirePolarity.Neutral);
                Set(Outlet, "wires", new List<Wire> { First, Second });
                Environment = Create("Environment", 0, 0).AddComponent<EnvironmentFacade>();
                Set(Environment, "routingTilemap", map);
                Set(Environment, "playerTransform", Player.transform);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private GameObject Create(string name, int x, int y)
        {
            var result = new GameObject(name);
            SceneManager.MoveGameObjectToScene(result, scene);
            result.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
            return result;
        }

        public PowerSocket AddOutlet(string name, int x, int y)
        {
            GameObject root = Create(name, x, y);
            root.AddComponent<BoxCollider2D>();
            return root.AddComponent<PowerSocket>();
        }

        public PolaritySocket AddInterface(string name, int x, int y)
        {
            GameObject root = Create(name, x, y);
            root.AddComponent<BoxCollider2D>();
            return root.AddComponent<PolaritySocket>();
        }

        public VoltageReducer AddReducer(string name, int x, int y, float drop)
        {
            GameObject root = Create(name, x, y);
            root.AddComponent<BoxCollider2D>();
            VoltageReducer reducer = root.AddComponent<VoltageReducer>();
            Set(reducer, "voltageDrop", drop);
            return reducer;
        }

        public void AddWire(WirePolarity polarity)
        {
            Wire wire = Create("AdditionalWire", 0, 0).AddComponent<Wire>();
            Set(wire, "polarity", polarity);
            var configured = new List<Wire>(Outlet.Wires) { wire };
            Set(Outlet, "wires", configured);
        }

        public void Move(Vector3 position)
        {
            Player.transform.position = position;
            Environment.SamplePlayerPath(position);
        }

        public void Interact(IEnvironmentInteractable target)
        {
            Component component = (Component)target;
            Move(component.transform.position);
            target.Interact(new InteractionDetails(Player, component.gameObject));
        }

        public void Restart()
        {
            Player.transform.position = Outlet.transform.position;
            Environment.RefreshNodes();
            typeof(EnvironmentFacade).GetMethod("BeginRun", PrivateInstance).Invoke(Environment, null);
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (tile)
            {
                UnityEngine.Object.DestroyImmediate(tile);
            }
            typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, previous);
        }
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
