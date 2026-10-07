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

    public static void CheckAssetReferences()
    {
        int wires = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Env" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (Wire wire in prefab.GetComponentsInChildren<Wire>(true))
            {
                CheckAssetWire(wire);
                wires++;
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
                        wires++;
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
        Require(wires > 0, "The production asset scan must discover wires.");
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
