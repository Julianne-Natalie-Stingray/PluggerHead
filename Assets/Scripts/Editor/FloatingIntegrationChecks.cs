using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises the real component's frame updates in an isolated scene.</summary>
public static class FloatingIntegrationChecks
{
    public static IEnumerator CheckMotion(bool transformedParent)
    {
        float previousTimeScale = Time.timeScale;
        UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
        Scene scene = SceneManager.CreateScene("FloatingCheck-" + Guid.NewGuid().ToString("N"));
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
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = previousTimeScale;
            UnityEngine.Random.state = previousRandomState;
        }
        while (scene.IsValid() && scene.isLoaded)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForMotionFrames(Transform subject, Vector3 expectedPosition)
    {
        float startingTime = Time.time;
        float deadline = Time.realtimeSinceStartup + 5f;
        // Wait for enough scaled time to make rotation observable above quaternion rounding,
        // independently of Editor frame rate or when the resumed timescale takes effect.
        while (Time.time - startingTime < 0.05f)
        {
            Require(Time.realtimeSinceStartup < deadline, "Timed out waiting for scaled motion frames.");
            yield return null;
            Require((subject.position - expectedPosition).sqrMagnitude < 0.000001f,
                "Real Update must preserve position on every observed rotation frame.");
        }
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
