using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Uses the real Player prefab and keyboard input in an isolated preview physics scene.</summary>
public static class PlayerWallSlideIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void CheckWall(int direction, bool ascending)
    {
        using (var fixture = new Fixture())
        {
            BoxCollider2D wall = fixture.Surface(new Vector2(direction, 0f), new Vector2(1f, 100f));
            fixture.Body.velocity = ascending ? Vector2.up * 8f : Vector2.zero;
            fixture.Press(direction > 0 ? Key.D : Key.A);
            fixture.Step(10);
            Require(fixture.Body.IsTouching(wall), "The player must actually be touching the wall.");
            Require(!fixture.IsGrounded(), "A wall must not count as ground or permit a wall jump.");
            if (ascending)
            {
                Require(fixture.Body.position.y > 1f && fixture.Body.velocity.y > 5f,
                    "Pressing into a wall must not consume the upward jump velocity.");
            }
            fixture.Step(90);
            Require(fixture.Body.IsTouching(wall) && Mathf.Abs(fixture.Body.position.x) < 0.1f,
                "Sliding must retain solid wall collision without penetrating the wall.");
            Require(fixture.Body.position.y < -2f && fixture.Body.velocity.y < -8f,
                $"Holding into a wall must fall under gravity: y={fixture.Body.position.y}, vy={fixture.Body.velocity.y}.");

            float fallingVelocity = fixture.Body.velocity.y;
            fixture.RequestJump();
            fixture.Step(1);
            Require(fixture.Body.velocity.y < fallingVelocity,
                "Jump input while only touching a wall must not reset falling velocity.");
            fixture.Press(direction > 0 ? Key.A : Key.D);
            fixture.Step(5);
            Require(fixture.Body.position.x * direction < -0.3f,
                "Opposite input must immediately move the player away from the wall.");
        }
    }

    public static void CheckGroundAndJump()
    {
        using (var fixture = new Fixture())
        {
            BoxCollider2D ground = fixture.Surface(Vector2.down, new Vector2(20f, 1f));
            fixture.Step(50);
            Require(fixture.Body.IsTouching(ground) && fixture.IsGrounded() &&
                Mathf.Abs(fixture.Body.position.y) < 0.05f && Mathf.Abs(fixture.Body.velocity.y) < 0.01f,
                "Zero friction must preserve stable floor contact and grounded detection.");
            fixture.Press(Key.D);
            fixture.Step(10);
            Require(fixture.Body.position.x > 0.8f, "Ground movement must retain its configured speed.");
            fixture.Press();
            fixture.Step(1);
            Require(Mathf.Abs(fixture.Body.velocity.x) < 0.01f,
                "Releasing movement must still stop horizontal motion without material friction.");
            fixture.RequestJump();
            fixture.Step(5);
            Require(fixture.Body.position.y > 0.5f && fixture.Body.velocity.y > 6f,
                "Jump input from the floor must retain the normal jump height and velocity.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene scene;
        private readonly Keyboard keyboard;
        private readonly PlayerControls controls;
        private readonly PlayerMove player;
        private readonly float previousTimeScale;
        private readonly Vector2 previousGravity;
        private readonly object inputSystemManager;
        private readonly PropertyInfo editModeUpdates;
        private readonly bool previousEditModeUpdates;
        public Rigidbody2D Body { get; }

        public Fixture()
        {
            previousTimeScale = Time.timeScale;
            previousGravity = Physics2D.gravity;
            Time.timeScale = 1f;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            // Input System 1.7 normally routes EditMode events to editor state, not gameplay actions.
            inputSystemManager = typeof(InputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            editModeUpdates = inputSystemManager.GetType().GetProperty("runPlayerUpdatesInEditMode");
            previousEditModeUpdates = (bool)editModeUpdates.GetValue(inputSystemManager);
            editModeUpdates.SetValue(inputSystemManager, true);
            scene = EditorSceneManager.NewPreviewScene();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = Vector3.zero;
            player = instance.GetComponent<PlayerMove>();
            // EditMode does not invoke Awake/Start: initialize just the actual movement component.
            Invoke("Awake");
            PlayerVisual visual = instance.GetComponentInChildren<PlayerVisual>();
            typeof(PlayerVisual).GetMethod("Awake", PrivateInstance).Invoke(visual, null);
            player.GetResistance = position => Vector2.zero;
            Body = instance.GetComponent<Rigidbody2D>();
            Body.position = Vector2.zero;

            var inputObject = new GameObject("WallSlideTestInput");
            SceneManager.MoveGameObjectToScene(inputObject, scene);
            InputManager input = inputObject.AddComponent<InputManager>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            controls = new PlayerControls();
            controls.asset.devices = new InputDevice[] { keyboard };
            controls.Gameplay.Enable();
            typeof(InputManager).GetField("controls", PrivateInstance).SetValue(input, controls);
            typeof(PlayerMove).GetField("input", PrivateInstance).SetValue(player, input);
        }

        public BoxCollider2D Surface(Vector2 position, Vector2 size)
        {
            var surface = new GameObject("TestSurface");
            SceneManager.MoveGameObjectToScene(surface, scene);
            surface.transform.position = position;
            BoxCollider2D collider = surface.AddComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        public void Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            float expected = Array.IndexOf(keys, Key.D) >= 0 ? 1f : Array.IndexOf(keys, Key.A) >= 0 ? -1f : 0f;
            Require(Mathf.Approximately(controls.Gameplay.MovementInput.ReadValue<Vector2>().x, expected),
                "The real input action must receive the requested horizontal keyboard input.");
        }

        public void Step(int count)
        {
            Physics2D.SyncTransforms();
            PhysicsScene2D physics = scene.GetPhysicsScene2D();
            for (int index = 0; index < count; index++)
            {
                Invoke("FixedUpdate");
                Require(physics.Simulate(0.02f), "The isolated physics scene must simulate.");
            }
        }

        public bool IsGrounded() => (bool)Invoke("IsGrounded");
        public void RequestJump() => Invoke("HandleUpPressed");
        private object Invoke(string method) => typeof(PlayerMove).GetMethod(method, PrivateInstance).Invoke(player, null);

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            controls.Disable();
            UnityEngine.Object.DestroyImmediate(controls.asset);
            InputSystem.RemoveDevice(keyboard);
            editModeUpdates.SetValue(inputSystemManager, previousEditModeUpdates);
            Physics2D.gravity = previousGravity;
            Time.timeScale = previousTimeScale;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
