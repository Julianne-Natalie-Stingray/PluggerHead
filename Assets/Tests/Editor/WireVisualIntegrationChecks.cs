using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WireVisualIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void CheckSelection(int polarity)
    {
        using (var fixture = new Fixture())
        {
            Wire wire = fixture.CreateWire("Selected", (WirePolarity)polarity);
            wire.Initialize();
            Require(wire.GetComponent<LineRenderer>().sharedMaterial == fixture.Configs.GetMaterial((WirePolarity)polarity),
                "Initialization must select the exact polarity material.");
        }
    }

    public static void CheckContinuation(int polarity)
    {
        using (var fixture = new Fixture())
        {
            Wire source = fixture.CreateWire("Source", WirePolarity.Live);
            source.Initialize();
            LineRenderer original = source.GetComponent<LineRenderer>();
            original.startColor = new Color(0.3f, 0.4f, 0.6f, 0.7f);
            original.endColor = new Color(0.8f, 0.2f, 0.1f, 0.9f);
            Gradient before = original.colorGradient;
            Material originalMaterial = original.sharedMaterial;
            Wire continuation = fixture.CreateWire("Continuation", WirePolarity.Live);
            Set(continuation, "visualConfigs", null);
            LineRenderer continuationLine = continuation.GetComponent<LineRenderer>();
            continuationLine.startColor = new Color(0.2f, 0.7f, 0.4f, 0.6f);
            continuationLine.endColor = new Color(0.6f, 0.3f, 0.8f, 0.5f);
            Gradient continuationGradient = continuationLine.colorGradient;
            typeof(Wire).GetMethod("ConfigureContinuation", PrivateInstance)
                .Invoke(continuation, new object[] { source, (WirePolarity)polarity });
            Require(continuation.GetComponent<LineRenderer>().sharedMaterial == fixture.Configs.GetMaterial((WirePolarity)polarity),
                "Continuation must select the new polarity from the inherited configuration.");
            Require(original.sharedMaterial == originalMaterial, "Continuation must not alter the source material.");
            Require(SameGradient(original.colorGradient, before), "Continuation must not alter the source gradient.");
            Require(SameGradient(continuationLine.colorGradient, continuationGradient),
                "Continuation must not overwrite its authored gradient with a polarity tint.");
        }
    }

    public static void CheckMissingConfiguration()
    {
        using (var fixture = new Fixture())
        {
            Wire wire = fixture.CreateWire("Unconfigured", WirePolarity.Neutral);
            LineRenderer line = wire.GetComponent<LineRenderer>();
            line.sharedMaterial = fixture.Configs.GetMaterial(WirePolarity.Live);
            Set(wire, "visualConfigs", null);
            wire.Initialize();
            Require(line.sharedMaterial == fixture.Configs.GetMaterial(WirePolarity.Live),
                "Absent configuration must preserve the authored material.");
            Set(wire, "visualConfigs", fixture.Configs);
            Set(fixture.Configs, "neutralMaterial", null);
            wire.Initialize();
            Require(line.sharedMaterial == null, "An empty slot must clear a stale material.");
        }
    }

    public static void CheckPathAndOrder()
    {
        using (var fixture = new Fixture())
        {
            Wire original = fixture.CreateWire("Original", WirePolarity.Live);
            Wire wire = fixture.CreateWire("Suffix", WirePolarity.Neutral);
            original.TilePath.Reset(Vector3Int.zero);
            original.TilePath.Visit(Vector3Int.right);
            wire.TilePath.CopyFrom(original.TilePath);
            wire.TilePath.Visit(new Vector3Int(2, 0, 0));
            wire.Initialize();
            LineRenderer line = wire.GetComponent<LineRenderer>();
            Material material = line.sharedMaterial;
            wire.RenderPath(new[] { Vector3.zero, Vector3.right, Vector3.right * 2f });
            Require(line.positionCount == 2 && line.GetPosition(0) == Vector3.right &&
                line.GetPosition(1) == Vector3.right * 2f, "Only the suffix after inherited edges must be rendered.");
            wire.SetRenderOrder(0, 7);
            Require(line.sortingLayerID == 0 && line.sortingOrder == 7 && line.sharedMaterial == material,
                "Handover sorting must preserve material selection.");
        }
    }

    public static void CheckSocketConnections(string socketName, int polarity)
    {
        using (var fixture = new Fixture())
        {
            Wire wire = fixture.CreateWire("Socket connection", (WirePolarity)polarity);
            wire.Initialize();
            GameObject wireAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Env/Wire/LiveWire.prefab");
            GameObject socketAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Env/Socket/{socketName}.prefab");
            Require(wireAsset && socketAsset, "Production wire and socket prefabs must exist.");
            GameObject socket = (GameObject)PrefabUtility.InstantiatePrefab(socketAsset, wire.gameObject.scene);
            SpriteRenderer socketRenderer = socket.GetComponent<SpriteRenderer>();
            LineRenderer authoredLine = wireAsset.GetComponent<LineRenderer>();
            LineRenderer line = wire.GetComponent<LineRenderer>();
            wire.SetRenderOrder(authoredLine.sortingLayerID, authoredLine.sortingOrder);
            Transform anchor = socket.TryGetComponent(out PowerSocket outlet)
                ? outlet.GetWireAnchor((WirePolarity)polarity)
                : socket.GetComponent<PolaritySocket>().GetWireAnchor((WirePolarity)polarity);
            Require(anchor && anchor.IsChildOf(socket.transform), "Each polarity must reference its own socket anchor.");
            Vector3[] path = { anchor.position + Vector3.right * 0.1f, anchor.position + Vector3.left * 4f,
                anchor.position + Vector3.up * 3f, anchor.position };
            typeof(Wire).GetMethod("BeginConnection", PrivateInstance)
                .Invoke(wire, new object[] { socket.transform, null });
            wire.PlugInto(socket.transform, false);
            // Exercise both authored layers and the same-layer order case without fixing production values.
            foreach (bool sameLayer in new[] { false, true })
            {
                if (sameLayer)
                {
                    socketRenderer.sortingLayerID = line.sortingLayerID;
                    socketRenderer.sortingOrder = line.sortingOrder + 5;
                }
                wire.RenderPath(path);
                foreach (string name in new[] { "StartConnection", "EndConnection" })
                {
                    Transform child = wire.transform.Find(name);
                    Require(child, "Both electrical endpoints must create a connection renderer.");
                    LineRenderer connection = child.GetComponent<LineRenderer>();
                    Require(connection.enabled && connection.positionCount == 2 &&
                        connection.GetPosition(0) == (name == "StartConnection" ? path[1] : path[2]) &&
                        connection.GetPosition(1) == anchor.position,
                        "Terminal connections must use the second vertex from their own path end, regardless of distance.");
                    int connectionLayer = SortingLayer.GetLayerValueFromID(connection.sortingLayerID);
                    int socketLayer = SortingLayer.GetLayerValueFromID(socketRenderer.sortingLayerID);
                    Require(connectionLayer > socketLayer ||
                        (connectionLayer == socketLayer && connection.sortingOrder > socketRenderer.sortingOrder),
                        "Socket artwork must not cover the terminal connection.");
                }
                Require(line.sortingLayerID == authoredLine.sortingLayerID &&
                    line.sortingOrder == authoredLine.sortingOrder && line.positionCount == path.Length - 2 &&
                    line.GetPosition(0) == path[1] && line.GetPosition(1) == path[2],
                    "The main renderer must omit both replaced terminal edges and preserve its sorting.");
                Require(wire.PathCollider.pointCount == path.Length, "Rendering must preserve collision vertices.");
                Vector2[] collision = wire.PathCollider.points;
                for (int i = 0; i < path.Length; i++)
                {
                    Require(
                        Vector2.Distance(wire.transform.TransformPoint(collision[i] + wire.PathCollider.offset), path[i]) < 0.00001f,
                        "Replacing rendered terminal edges must not move collision vertices.");
                }
            }
            TileWirePath previous = new TileWirePath();
            previous.Reset(Vector3Int.zero);
            previous.Visit(Vector3Int.right);
            wire.TilePath.CopyFrom(previous);
            wire.Hold();
            wire.RenderPath(path);
            LineRenderer startLine = wire.transform.Find("StartConnection").GetComponent<LineRenderer>();
            LineRenderer endLine = wire.transform.Find("EndConnection").GetComponent<LineRenderer>();
            Require(startLine.GetPosition(0) == path[2] && !endLine.enabled &&
                line.positionCount == 2 && line.GetPosition(0) == path[2] && line.GetPosition(1) == path[3],
                "A continuation must count from its inherited boundary and only replace its own outgoing edge.");

            wire.TilePath.Reset(Vector3Int.zero);
            typeof(Wire).GetMethod("BeginConnection", PrivateInstance)
                .Invoke(wire, new object[] { null, null });
            wire.PlugInto(socket.transform, false);
            wire.RenderPath(path);
            Require(!startLine.enabled && endLine.GetPosition(0) == path[2] && line.positionCount == 3 &&
                line.GetPosition(0) == path[0] && line.GetPosition(2) == path[2],
                "Without a starting anchor only the incoming final edge is replaced.");

            typeof(Wire).GetMethod("BeginConnection", PrivateInstance)
                .Invoke(wire, new object[] { socket.transform, null });
            GameObject otherSocket = (GameObject)PrefabUtility.InstantiatePrefab(socketAsset, wire.gameObject.scene);
            otherSocket.transform.position += Vector3.right * 2f;
            Transform otherAnchor = otherSocket.TryGetComponent(out PowerSocket otherOutlet)
                ? otherOutlet.GetWireAnchor((WirePolarity)polarity)
                : otherSocket.GetComponent<PolaritySocket>().GetWireAnchor((WirePolarity)polarity);
            wire.PlugInto(otherSocket.transform, false);
            wire.RenderPath(new[] { path[0], path[1] });
            Require(line.positionCount == 0 && startLine.enabled && !endLine.enabled &&
                startLine.GetPosition(0) == otherAnchor.position && startLine.GetPosition(1) == anchor.position,
                "A single edge with two socket endpoints must be replaced by a direct anchor connection.");
            wire.RenderPath(new[] { path[0] });
            Require(startLine.GetPosition(0) == path[0] && endLine.GetPosition(0) == path[0],
                "A one-point route must use its only point without indexing outside the list.");
            wire.RenderPath(Array.Empty<Vector3>());
            Require(line.positionCount == 0 && !startLine.enabled && !endLine.enabled && !wire.PathCollider.enabled,
                "An empty route must clear all rendered connections and disable collision.");
        }
    }

    public static void CheckAssetReferences()
    {
        Require(AssetDatabase.IsValidFolder("Assets/Env"), "The production environment asset directory must exist.");
        int prefabWires = 0;
        int sceneWires = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Env" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (Wire wire in prefab.GetComponentsInChildren<Wire>(true))
            {
                CheckAssetWire(wire);
                prefabWires++;
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes", "Assets/Tests/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
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
                    foreach (Wire wire in root.GetComponentsInChildren<Wire>(true))
                    {
                        CheckAssetWire(wire);
                        sceneWires++;
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
        Require(prefabWires > 0, "The production prefab scan must discover wires.");
        Require(sceneWires > 0, "The production scene scan must discover wires.");
    }

    private static void CheckAssetWire(Wire wire)
    {
        var serialized = new SerializedObject(wire);
        var configs = serialized.FindProperty("visualConfigs").objectReferenceValue as WireVisualConfigs;
        Require(configs != null, $"{wire.name}: missing wire visual configuration.");
        Require(configs.GetMaterial(WirePolarity.Live) && configs.GetMaterial(WirePolarity.Neutral) &&
            configs.GetMaterial(WirePolarity.Ground), $"{wire.name}: incomplete type material references.");
        Material material = configs.GetMaterial(wire.Polarity);
        Require(material && material.shader && material.shader.isSupported,
            $"{wire.name}: unsupported or missing polarity material.");
        Require(wire.GetComponent<LineRenderer>().sharedMaterial == material,
            $"{wire.name}: authored material does not match its polarity configuration.");
    }

    private static bool SameGradient(Gradient first, Gradient second)
    {
        if (first.mode != second.mode || first.colorKeys.Length != second.colorKeys.Length ||
            first.alphaKeys.Length != second.alphaKeys.Length)
        {
            return false;
        }
        for (int i = 0; i < first.colorKeys.Length; i++)
        {
            if (first.colorKeys[i].color != second.colorKeys[i].color || first.colorKeys[i].time != second.colorKeys[i].time)
            {
                return false;
            }
        }
        for (int i = 0; i < first.alphaKeys.Length; i++)
        {
            if (first.alphaKeys[i].alpha != second.alphaKeys[i].alpha || first.alphaKeys[i].time != second.alphaKeys[i].time)
            {
                return false;
            }
        }
        return true;
    }

    public sealed class Fixture : IDisposable
    {
        private readonly Scene scene;
        private readonly List<Material> materials = new List<Material>();
        public WireVisualConfigs Configs { get; }

        public Fixture()
        {
            scene = EditorSceneManager.NewPreviewScene();
            Configs = ScriptableObject.CreateInstance<WireVisualConfigs>();
            try
            {
                Shader shader = Shader.Find("Sprites/Default");
                Require(shader != null, "The explicit test material shader must be available.");
                foreach (string field in new[] { "liveMaterial", "neutralMaterial", "groundMaterial" })
                {
                    var material = new Material(shader);
                    materials.Add(material);
                    Set(Configs, field, material);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public Wire CreateWire(string name, WirePolarity polarity)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            Wire wire = gameObject.AddComponent<Wire>();
            Set(wire, "polarity", polarity);
            Set(wire, "visualConfigs", Configs);
            return wire;
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (Material material in materials)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
            UnityEngine.Object.DestroyImmediate(Configs);
        }
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
