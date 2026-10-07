using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class VoltageDisplayIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<Scene> scenes = new List<Scene>();
    private static readonly List<Tile> tiles = new List<Tile>();
    private static Scene previousScene;
    private static EnvironmentFacade previousEnvironment;
    private static bool initialized;

    public static IEnumerator CheckReduction()
    {
        Begin();
        var fixture = new Fixture(12.5f);
        yield return null;
        yield return null;
        CheckText(fixture.Text, "电压: 0/12.5");
        fixture.Interact(fixture.FirstReducer);
        yield return null;
        yield return null;
        CheckText(fixture.Text, "电压: 4.25/12.5");
        Require(fixture.Environment.ReducedVoltage == 4.25d, "ReducedVoltage must report connected reduction.");
        fixture.Interact(fixture.FirstReducer);
        yield return null;
        yield return null;
        CheckText(fixture.Text, "电压: 4.25/12.5");
        fixture.Interact(fixture.SecondReducer);
        yield return null;
        yield return null;
        CheckText(fixture.Text, "电压: 14.25/12.5");
        Require(fixture.Environment.ReducedVoltage == 14.25d, "Excess reduction must not be clamped.");
        fixture.Restart();
        yield return null;
        yield return null;
        CheckText(fixture.Text, "电压: 0/12.5");
        Require(fixture.Environment.ReducedVoltage == 0d, "Restart must reset reduction.");
    }

    public static IEnumerator CheckVisibility()
    {
        Begin();
        var fixture = new Fixture(0f);
        Scene other = NewScene();
        TMPro.TMP_Text otherText = CreateHud(other);
        NewObject("MissingTextReference", other).AddComponent<VoltageDisplay>();
        yield return null;
        yield return null;
        CheckHidden(fixture.Text);
        CheckHidden(otherText);
        Set(fixture.Environment, "neededVoltage", 12.5f);
        fixture.Environment.EvaluateCircuit();
        yield return null;
        yield return null;
        CheckText(fixture.Text, "电压: 0/12.5");
        CheckHidden(otherText);
        var second = NewObject("OtherEnvironment", other).AddComponent<EnvironmentFacade>();
        second.enabled = false;
        Set(second, "neededVoltage", 7.5f);
        second.EvaluateCircuit();
        yield return null;
        yield return null;
        CheckText(otherText, "电压: 0/7.5");
        CheckText(fixture.Text, "电压: 0/12.5");
        Set(fixture.Environment, "neededVoltage", 0f);
        fixture.Environment.EvaluateCircuit();
        yield return null;
        yield return null;
        CheckHidden(fixture.Text);
        CheckText(otherText, "电压: 0/7.5");
    }

    private static void Begin()
    {
        Require(!initialized, "Previous fixture cleanup must complete.");
        previousScene = SceneManager.GetActiveScene();
        previousEnvironment = EnvironmentFacade.Current;
        initialized = true;
    }

    private static Scene NewScene()
    {
        Scene scene = SceneManager.CreateScene("VoltageDisplayFixture-" + Guid.NewGuid());
        scenes.Add(scene);
        return scene;
    }

    private static GameObject NewObject(string name, Scene scene)
    {
        var result = new GameObject(name);
        SceneManager.MoveGameObjectToScene(result, scene);
        return result;
    }

    private static TMPro.TMP_Text CreateHud(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Env/ScenePrefab/GlobalUI.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(component is VoltageDisplay) && !(component is WireLengthDisplay) &&
                !(component is TMPro.TMP_Text) && !(component is UnityEngine.EventSystems.UIBehaviour))
            {
                component.enabled = false;
            }
        }
        Transform hud = root.transform.Find("GameplayHUD");
        Require(hud.GetComponent<VoltageDisplay>() != null && hud.gameObject.activeSelf,
            "Controller must remain on active HUD, separate from hidden text.");
        Require(hud.Find("ScoreText").GetComponent<TMPro.TMP_Text>().text == string.Empty, "ScoreText remains reserved.");
        var text = hud.Find("VoltageText").GetComponent<TMPro.TMP_Text>();
        Require(text.font.HasCharacters("电压: /0123456789.-"), "Shipped font must include all voltage glyphs.");
        return text;
    }

    private static void CheckText(TMPro.TMP_Text text, string expected)
    {
        Require(text.gameObject.activeInHierarchy && text.text == expected, "Expected " + expected + ", actual " + text.text);
        Canvas.ForceUpdateCanvases();
        text.ForceMeshUpdate();
        Require(!text.isTextOverflowing, "Voltage must fit its display.");
        var length = text.transform.parent.Find("RemainingLengthText").GetComponent<TMPro.TMP_Text>();
        length.ForceMeshUpdate();
        Require(!length.isTextOverflowing, "Wire length layout must remain readable.");
    }

    private static void CheckHidden(TMPro.TMP_Text text)
    {
        Require(!text.gameObject.activeSelf && text.text == string.Empty, "Hidden voltage must be cleared.");
        Require(text.transform.parent.GetComponent<VoltageDisplay>().isActiveAndEnabled, "Hidden text must not disable its controller.");
    }

    public static IEnumerator Cleanup()
    {
        if (!initialized)
        {
            yield break;
        }
        if (previousScene.IsValid() && previousScene.isLoaded)
        {
            SceneManager.SetActiveScene(previousScene);
        }
        foreach (Scene scene in scenes)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return IntegrationSceneWait.Operation(SceneManager.UnloadSceneAsync(scene), "unloading voltage fixture");
            }
        }
        scenes.Clear();
        foreach (Tile tile in tiles)
        {
            if (tile)
            {
                Object.DestroyImmediate(tile);
            }
        }
        tiles.Clear();
        typeof(EnvironmentFacade).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, previousEnvironment);
        initialized = false;
    }

    private sealed class Fixture
    {
        public readonly EnvironmentFacade Environment;
        public readonly GameObject Player;
        public readonly PowerSocket Outlet;
        public readonly VoltageReducer FirstReducer;
        public readonly VoltageReducer SecondReducer;
        public readonly TMPro.TMP_Text Text;

        public Fixture(float needed)
        {
            Scene scene = NewScene();
            var grid = NewObject("Grid", scene);
            grid.AddComponent<Grid>();
            var mapRoot = NewObject("Map", scene);
            mapRoot.transform.SetParent(grid.transform);
            var map = mapRoot.AddComponent<Tilemap>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tiles.Add(tile);
            for (int x = -1; x <= 4; x++)
            {
                for (int y = -1; y <= 2; y++)
                {
                    map.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
            Player = NewObject("Player", scene);
            Player.tag = "Player";
            var outletRoot = NewObject("Outlet", scene);
            outletRoot.transform.position = new Vector3(0.5f, 0.5f);
            outletRoot.AddComponent<BoxCollider2D>();
            Outlet = outletRoot.AddComponent<PowerSocket>();
            var wire = NewObject("Wire", scene).AddComponent<Wire>();
            Set(wire, "polarity", WirePolarity.Live);
            Set(Outlet, "wires", new List<Wire> { wire });
            FirstReducer = AddReducer(scene, 1, 4.25f);
            SecondReducer = AddReducer(scene, 2, 10f);
            Environment = NewObject("Environment", scene).AddComponent<EnvironmentFacade>();
            Environment.enabled = false;
            Set(Environment, "routingTilemap", map);
            Set(Environment, "neededVoltage", needed);
            Set(Environment, "playerTransform", Player.transform);
            Restart();
            Text = CreateHud(scene);
        }

        private static VoltageReducer AddReducer(Scene scene, int x, float drop)
        {
            var root = NewObject("Reducer", scene);
            root.transform.position = new Vector3(x + 0.5f, 0.5f);
            root.AddComponent<BoxCollider2D>();
            var reducer = root.AddComponent<VoltageReducer>();
            Set(reducer, "voltageDrop", drop);
            return reducer;
        }

        public void Restart()
        {
            Player.transform.position = Outlet.transform.position;
            Environment.RefreshNodes();
            typeof(EnvironmentFacade).GetMethod("BeginRun", PrivateInstance).Invoke(Environment, null);
        }

        public void Interact(VoltageReducer reducer)
        {
            Player.transform.position = reducer.transform.position;
            Environment.SamplePlayerPath(Player.transform.position);
            reducer.Interact(new InteractionDetails(Player, reducer.gameObject));
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
