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

            if (scenario == "IndependentReturns")
            {
                fixture.Interact(fixture.Outlet);
                fixture.Interact(fixture.Outlet);
                Require(fixture.First.IsClosed && fixture.Second.IsClosed &&
                    !fixture.Environment.IsCircuitClosed && cleared == 0,
                    "Two wires returned independently to the source must not form a circuit.");
                return;
            }

            fixture.Interact(first);
            Require(fixture.Environment.HeldWire == fixture.Second && fixture.Second.PreviousWire == fixture.First &&
                fixture.Second.CircuitStart == first.transform && !fixture.Environment.IsCircuitClosed,
                "The first interface must join opposite wires without completing the return path.");

            if (scenario == "MissingInterface" || scenario == "WalkOnly")
            {
                if (scenario == "WalkOnly")
                {
                    fixture.Move(second.transform.position);
                }
                fixture.Interact(fixture.Outlet);
                Require(!fixture.Environment.IsCircuitClosed && cleared == 0,
                    "A closed route must not win when an interface was skipped or merely walked over.");
                return;
            }

            fixture.Interact(second);
            Require(fixture.Environment.HeldWire == fixture.Second && fixture.Second.IsHeld &&
                fixture.Second.ConnectedInterfaces.Count == 2,
                "Connecting another interface without a spare wire must retain the carried wire.");
            int connectionCount = fixture.Second.ConnectedInterfaces.Count;
            fixture.Interact(second);
            Require(fixture.Second.ConnectedInterfaces.Count == connectionCount && fixture.Environment.SwapCount == 1,
                "Repeated interaction must not duplicate a connection or a handover.");
            int pathCount = fixture.Second.TilePath.Cells.Count;
            fixture.Move(first.transform.position);
            Require(fixture.Second.TilePath.Cells.Count > pathCount,
                "Returning behind a plugged interface must preserve its path and lay a new tail.");
            fixture.Environment.RefreshNodes();
            Require(fixture.Second.ConnectedInterfaces.Count == connectionCount,
                "Refreshing nodes must preserve the connection history and occupancy.");

            if (scenario == "WrongOutlet")
            {
                PowerSocket other = fixture.AddOutlet("OtherOutlet", 0, 3);
                fixture.Environment.RefreshNodes();
                fixture.Interact(other);
                Require(fixture.Second.IsHeld && !fixture.Environment.IsCircuitClosed,
                    "Returning to another outlet must not close the source circuit.");
            }
            if (scenario == "IndependentTermination")
            {
                // Public state setters cannot manufacture the missing electrical handover.
                fixture.Second.PlugInto(second.transform, false);
                fixture.Environment.EvaluateCircuit();
                Require(!fixture.Environment.IsCircuitClosed, "Two interface endpoints alone are not a closed source circuit.");
                return;
            }

            fixture.Interact(fixture.Outlet);
            Require(fixture.Environment.IsCircuitClosed && cleared == 1 && fixture.Environment.HeldWire == null,
                "The connected live/neutral route through every interface must close at its source.");
            fixture.Environment.EvaluateCircuit();
            Require(cleared == 1, "Re-evaluation must not repeat the completion event.");
            if (scenario == "DestroyedJunction")
            {
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                fixture.Environment.RefreshNodes();
                Require(!fixture.Environment.EvaluateCircuit(), "A destroyed junction must break the circuit.");
                return;
            }
            fixture.Restart();
            Require(!fixture.Environment.IsCircuitClosed && fixture.First.ConnectedInterfaces.Count == 0 &&
                fixture.Second.ConnectedInterfaces.Count == 0 && fixture.Second.PreviousWire == null,
                "Restart must clear connections, topology, and completion state.");
            fixture.Interact(first);
            fixture.Interact(second);
            fixture.Interact(fixture.Outlet);
            Require(fixture.Environment.IsCircuitClosed && cleared == 2,
                "Restart must permit a fresh complete circuit and event.");
        }
    }

    public static void CheckMultipleWires(bool samePolarityReturn)
    {
        using (var fixture = new Fixture(false))
        {
            fixture.AddWire(WirePolarity.Live);
            if (!samePolarityReturn)
            {
                fixture.AddWire(WirePolarity.Neutral);
            }
            var interfaces = new List<PolaritySocket>
            {
                fixture.AddInterface("First", 3, 0),
                fixture.AddInterface("Second", 3, 3)
            };
            if (!samePolarityReturn)
            {
                interfaces.Add(fixture.AddInterface("Third", 0, 3));
            }
            fixture.Restart();
            foreach (PolaritySocket target in interfaces)
            {
                fixture.Interact(target);
            }
            Require(fixture.Environment.SwapCount == interfaces.Count,
                "Every available opposite wire must create one recorded handover.");
            Wire returnWire = fixture.Environment.HeldWire;
            fixture.Interact(fixture.Outlet);
            Require(returnWire.IsClosed && fixture.Environment.HeldWire == null &&
                fixture.Environment.IsCircuitClosed == !samePolarityReturn,
                "A multi-wire source loop must return on the opposite polarity, independent of the swap count.");
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
