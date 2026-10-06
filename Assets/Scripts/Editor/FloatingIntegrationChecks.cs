using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises the real component's frame updates in an isolated scene.</summary>
public static class FloatingIntegrationChecks
{
    private static Scene ownedScene;
    private static AsyncOperation pendingUnload;
    public static IEnumerator CheckMotion(bool transformedParent)
    {
        return IntegrationSceneWait.Finally(CheckMotionBody(transformedParent), () => { });
    }

    private static IEnumerator CheckMotionBody(bool transformedParent)
    {
        Require(pendingUnload == null && (!ownedScene.IsValid() || !ownedScene.isLoaded),
            "A previous floating fixture still owns its scene; finish cleanup before another run.");
        float previousTimeScale = Time.timeScale;
        UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
        Scene scene = SceneManager.CreateScene("FloatingCheck-" + Guid.NewGuid().ToString("N"));
        ownedScene = scene;
        GameObject root = null;
        try
        {
            Time.timeScale = 1f;
            root = new GameObject("FloatingTestRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            Transform subject = root.transform;
            if (transformedParent)
            {
                root.transform.SetPositionAndRotation(new Vector3(10f, -7f, 3f), Quaternion.Euler(15f, 25f, 70f));
                root.transform.localScale = new Vector3(2f, 0.7f, 1.3f);
                subject = new GameObject("FloatingSubject").transform;
                subject.SetParent(root.transform, false);
            }
            subject.localPosition = new Vector3(1.2f, -0.5f, 0.3f);
            FloatingLogic component = subject.gameObject.AddComponent<FloatingLogic>();
            // Remove configured displacement to isolate rotation while still exercising real Update.
            SetField(component, "_xDriftAmplitude", 0f);
            SetField(component, "_yDriftAmplitude", 0f);
            SetField(component, "_OscillationAmplitude", 0f);
            SetField(component, "_AngularVelocity", 90f);
            Vector3 position = subject.position;
            Quaternion rotation = subject.rotation;
            yield return WaitForMotionFrames(subject, position);
            Require((subject.position - position).sqrMagnitude < 0.000001f,
                "Rotation must not displace the subject under root or transformed-parent coordinates.");
            Require(Quaternion.Angle(subject.rotation, rotation) > 0.001f,
                "The real component must rotate during advancing frames.");

            Time.timeScale = 0f;
            yield return null;
            rotation = subject.rotation;
            position = subject.position;
            yield return null;
            yield return null;
            Require(Time.deltaTime == 0f && Quaternion.Angle(subject.rotation, rotation) < 0.001f,
                "Scaled deltaTime zero must stop rotation.");
            Require((subject.position - position).sqrMagnitude < 0.000001f,
                "Pause must preserve position.");

            Time.timeScale = 1f;
            yield return WaitForMotionFrames(subject, position);
            Require(Quaternion.Angle(subject.rotation, rotation) > 0.001f,
                "Rotation must resume with scaled time.");
            Require((subject.position - position).sqrMagnitude < 0.000001f,
                "Resuming rotation must still preserve position.");
        }
        finally
        {
            IntegrationSceneWait.RestoreAll(
                () => { if (root != null) { UnityEngine.Object.DestroyImmediate(root); } },
                () => Time.timeScale = previousTimeScale,
                () => UnityEngine.Random.state = previousRandomState);
        }
    }

    public static IEnumerator Cleanup()
    {
        return IntegrationSceneWait.Finally(CleanupScene(), () =>
        {
            if ((pendingUnload == null || pendingUnload.isDone) && (!ownedScene.IsValid() || !ownedScene.isLoaded))
            {
                pendingUnload = null;
                ownedScene = default;
            }
        });
    }

    private static IEnumerator CleanupScene()
    {
        if (pendingUnload == null && ownedScene.IsValid() && ownedScene.isLoaded)
        {
            pendingUnload = SceneManager.UnloadSceneAsync(ownedScene);
        }
        if (pendingUnload != null || (ownedScene.IsValid() && ownedScene.isLoaded))
        {
            yield return IntegrationSceneWait.Operation(pendingUnload, "unloading the floating test scene");
            Require(!ownedScene.IsValid() || !ownedScene.isLoaded, "Floating unload completed but its scene remains loaded.");
        }
    }

    private static IEnumerator WaitForMotionFrames(Transform subject, Vector3 expectedPosition)
    {
        float startingTime = Time.time;
        return IntegrationSceneWait.Until(() =>
        {
            Require((subject.position - expectedPosition).sqrMagnitude < 0.000001f,
                "Real Update must preserve position on every observed rotation frame.");
            return Time.time - startingTime >= 0.05f;
        }, "waiting for scaled floating motion", 5f);
    }

    private static void SetField(FloatingLogic target, string name, float value)
    {
        typeof(FloatingLogic).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
