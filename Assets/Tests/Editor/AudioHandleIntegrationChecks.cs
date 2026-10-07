using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises public handles from a real manager without modifying assets or the live mixer.</summary>
public static class AudioHandleIntegrationChecks
{
    public static void CheckDestroyedEmitter(float fadeOut, bool environmentCleanup)
    {
        GameObject host = null;
        GameObject owner = null;
        AudioClip clip = null;
        AudioClipData data = null;
        try
        {
            host = new GameObject("Destroyed audio voice");
            AudioEmitter emitter = host.AddComponent<AudioEmitter>();
            clip = AudioClip.Create("Destroyed voice silence", 44100, 1, 44100, false);
            data = ScriptableObject.CreateInstance<AudioClipData>();
            Set(data, "clip", clip);
            Set(data, "loop", true);
            emitter.Configure(data);
            emitter.FadeOut = fadeOut;
            ISoundHandle handle = new SoundHandle(emitter, AudioId.DefaultSfx);
            emitter.Play();
            int notifications = 0;
            handle.Finished += _ => notifications++;

            Object.DestroyImmediate(host);
            Require(!handle.IsPlaying, "A destroyed voice must not report playback.");
            Require(!handle.TrySetVolume(0.5f) && !handle.TrySetPitch(1.5f),
                "A destroyed voice must reject controls without accessing native components.");
            if (environmentCleanup)
            {
                // Exercise the owner's shutdown callback without starting another live level.
                // 直接验证宿主清理回调，避免启动额外关卡污染当前场景。
                owner = new GameObject("Destroyed voice owner");
                owner.SetActive(false);
                EnvironmentFacade environment = owner.AddComponent<EnvironmentFacade>();
                Set(environment, "musicHandle", handle);
                Set(environment, "musicStarted", true);
                typeof(EnvironmentFacade).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(environment, null);
                Require(Get<ISoundHandle>(environment, "musicHandle") == null && !Get<bool>(environment, "musicStarted"),
                    "Environment cleanup must release its stale music handle.");
            }
            else
            {
                Require(!handle.Stop(), "Stopping an already destroyed voice must return false.");
            }
            Require(notifications == 1 && !handle.IsFinished && !handle.IsPlaying,
                "Cleanup must invalidate once as interrupted, not naturally completed.");
            Require(!handle.Stop() && !handle.TrySetVolume(0.2f) && !handle.TrySetPitch(2f),
                "Repeated cleanup and later controls must remain harmless.");
            Require(notifications == 1, "Repeated Stop must not notify twice.");
        }
        finally
        {
            if (owner)
            {
                Object.DestroyImmediate(owner);
            }
            if (host)
            {
                Object.DestroyImmediate(host);
            }
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(clip);
        }
    }

    public static IEnumerator Run(bool gracefulStop)
    {
        GameObject fixture = null;
        GameObject runner = null;
        AudioClip clip = null;
        AudioClipData data = null;
        AudioManagerConfigs configs = null;
        bool listenerPause = AudioListener.pause;
        float timeScale = Time.timeScale;
        bool background = Application.runInBackground;
        try
        {
            Require(GameStateManager.Current == GameState.Playing, "Audio handle tests require Playing.");
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Application.runInBackground = true;
            if (!TimerRunner.Instance)
            {
                runner = new GameObject("AudioHandle TimerRunner");
                runner.AddComponent<TimerRunner>();
            }
            fixture = new GameObject("AudioHandle isolated fixture");
            if (!Array.Exists(Object.FindObjectsOfType<AudioListener>(), item => item.isActiveAndEnabled))
            {
                fixture.AddComponent<AudioListener>();
            }
            var template = new GameObject("AudioHandle template");
            template.transform.SetParent(fixture.transform);
            template.SetActive(false);
            AudioEmitter prefab = template.AddComponent<AudioEmitter>();
            prefab.GetComponent<AudioSource>().playOnAwake = false;
            clip = AudioClip.Create("AudioHandle silence", 44100, 1, 44100, false);
            data = ScriptableObject.CreateInstance<AudioClipData>();
            Set(data, "clip", clip);
            Set(data, "loop", gracefulStop);
            Set(data, "maxInstances", 1);
            configs = ScriptableObject.CreateInstance<AudioManagerConfigs>();
            Set(configs, "audios", new List<AudioClipData> { data });
            Set(configs, "maxSoundInstance", 1);
            Set(configs, "maxPoolSize", 1);
            Set(configs, "prewarmAmount", 0);
            var host = new GameObject("AudioHandle manager");
            host.transform.SetParent(fixture.transform);
            host.SetActive(false);
            AudioManager manager = host.AddComponent<AudioManager>();
            Set(manager, "configs", configs);
            Set(manager, "emitterPrefab", prefab);
            Set(manager, "emitterRoot", fixture.transform);
            typeof(AudioManager).GetMethod("InitializeInternal", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, null);

            ISoundHandle handle = manager.CreateBuilder().WithFade(0f, gracefulStop ? 0.1f : 0f).Play(AudioId.DefaultSfx);
            Require(handle != null, "Manager must accept the isolated playback.");
            AudioEmitter emitter = Get<AudioEmitter>(handle, "emitter");
            Require(handle.TrySetVolume(0.4f) && handle.TrySetPitch(1.2f), "Live handles must accept valid values.");
            Require(!handle.TrySetVolume(float.NaN) && !handle.TrySetPitch(float.NaN), "NaN must be rejected.");
            Require(Mathf.Approximately(emitter.Volume, 0.4f) && Mathf.Approximately(emitter.Pitch, 1.2f),
                "NaN rejection must preserve both previous values.");
            Require(handle.TrySetVolume(float.PositiveInfinity) && handle.TrySetPitch(float.PositiveInfinity) &&
                emitter.Volume == 1f && emitter.Pitch == 3f, "Positive infinity must keep the existing upper clamp.");
            Require(handle.TrySetVolume(float.NegativeInfinity) && handle.TrySetPitch(float.NegativeInfinity) &&
                emitter.Volume == 0f && Mathf.Approximately(emitter.Pitch, 0.1f), "Negative infinity must keep the existing lower clamp.");
            Require(handle.TrySetVolume(0.4f) && handle.TrySetPitch(1f), "Valid updates must still work after rejected values.");

            // At capacity one, validation must run before the manager could preempt this valid playback.
            Func<ISoundHandle>[] invalidRequests =
            {
                () => manager.CreateBuilder().WithVolume(float.NaN).Play(AudioId.DefaultSfx),
                () => manager.CreateBuilder().WithPitch(float.NaN).Play(AudioId.DefaultSfx),
                () => manager.CreateBuilder().WithFade(float.NaN, 0f).Play(AudioId.DefaultSfx),
                () => manager.CreateBuilder().WithFade(0f, float.PositiveInfinity).Play(AudioId.DefaultSfx)
            };
            foreach (Func<ISoundHandle> request in invalidRequests)
            {
                RequireRejectedRequest(request(), handle, manager);
            }
            float originalVolume = data.Volume;
            float originalPitch = data.Pitch;
            Set(data, "volume", float.NaN);
            RequireRejectedRequest(manager.CreateBuilder().WithVolume(0.5f).Play(AudioId.DefaultSfx), handle, manager);
            Set(data, "volume", originalVolume);
            Set(data, "pitch", float.NaN);
            RequireRejectedRequest(manager.CreateBuilder().WithPitch(1f).Play(AudioId.DefaultSfx), handle, manager);
            Set(data, "pitch", originalPitch);

            int throwingNotifications = 0;
            int subsequentNotifications = 0;
            bool observedInvalid = false;
            handle.Finished += _ =>
            {
                throwingNotifications++;
                throw new InvalidOperationException("Expected AudioHandle subscriber failure.");
            };
            handle.Finished += completed =>
            {
                subsequentNotifications++;
                observedInvalid = !completed.IsPlaying && !completed.Stop() && !completed.TrySetVolume(0f) && !completed.TrySetPitch(2f);
            };
            if (gracefulStop)
            {
                Require(handle.Stop(), "Graceful Stop must return true without propagating subscriber exceptions.");
                Require(manager.Registry.Count == 1, "A graceful stop must retain its slot during the fade.");
                Require(subsequentNotifications == 1, "Graceful stop must notify immediately despite the first subscriber exception.");
            }
            float deadline = Time.realtimeSinceStartup + 4f;
            while (manager.Registry.Count > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Require(manager.Registry.Count == 0, "Playback completion must return its pool slot.");
            Require(throwingNotifications == 1 && subsequentNotifications == 1 && observedInvalid,
                "Both subscribers must run once after the handle is invalidated.");
            Require(handle.IsFinished == !gracefulStop, "Completion classification must remain natural or explicitly stopped.");
            Require(!handle.Stop() && !handle.TrySetVolume(0.2f) && !handle.TrySetPitch(0.7f), "Ended handles must reject later controls.");
            yield return null;
            Require(throwingNotifications == 1 && subsequentNotifications == 1, "Completion must not notify again on later frames.");
        }
        finally
        {
            if (fixture)
            {
                Object.DestroyImmediate(fixture);
            }
            Object.DestroyImmediate(configs);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(clip);
            if (runner)
            {
                Object.DestroyImmediate(runner);
            }
            AudioListener.pause = listenerPause;
            Time.timeScale = timeScale;
            Application.runInBackground = background;
        }
    }

    private static void RequireRejectedRequest(ISoundHandle rejected, ISoundHandle active, AudioManager manager)
    {
        Require(rejected == null && active.IsPlaying && manager.Registry.Count == 1,
            "Invalid request or clip defaults must be rejected before preempting the existing playback.");
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
