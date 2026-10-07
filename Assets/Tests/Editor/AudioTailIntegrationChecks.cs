using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated natural-tail envelope checks and actual pitched playback through the pool.</summary>
public static class AudioTailIntegrationChecks
{
    public static IEnumerator Run(string scenario)
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
            Require(GameStateManager.Current == GameState.Playing, "Audio tail tests require Playing.");
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Application.runInBackground = true;
            if (!TimerRunner.Instance)
            {
                runner = new GameObject("AudioTail TimerRunner");
                runner.AddComponent<TimerRunner>();
            }
            fixture = new GameObject("AudioTail isolated fixture");
            if (!Array.Exists(Object.FindObjectsOfType<AudioListener>(), item => item.isActiveAndEnabled))
            {
                fixture.AddComponent<AudioListener>();
            }
            var template = new GameObject("AudioTail template");
            template.transform.SetParent(fixture.transform);
            template.SetActive(false);
            AudioEmitter prefab = template.AddComponent<AudioEmitter>();
            prefab.GetComponent<AudioSource>().playOnAwake = false;
            clip = AudioClip.Create("AudioTail silence", 88200, 1, 44100, false);
            data = ScriptableObject.CreateInstance<AudioClipData>();
            Set(data, "clip", clip);
            Set(data, "volume", 1f);
            Set(data, "maxInstances", 1);
            configs = ScriptableObject.CreateInstance<AudioManagerConfigs>();
            Set(configs, "audios", new List<AudioClipData> { data });
            Set(configs, "maxSoundInstance", 1);
            Set(configs, "maxPoolSize", 1);
            Set(configs, "prewarmAmount", 0);
            var host = new GameObject("AudioTail manager");
            host.transform.SetParent(fixture.transform);
            host.SetActive(false);
            AudioManager manager = host.AddComponent<AudioManager>();
            Set(manager, "configs", configs);
            Set(manager, "emitterPrefab", prefab);
            Set(manager, "emitterRoot", fixture.transform);
            Call(manager, "InitializeInternal");

            if (scenario == "Values")
            {
                prefab.Configure(data);
                CheckEnvelope(prefab);
                yield break;
            }

            if (scenario == "FrozenStop")
            {
                Set(data, "loop", true);
                ISoundHandle loop = manager.CreateBuilder().WithFade(0f, 0.1f).Play(AudioId.DefaultSfx);
                Require(loop != null, "Loop must be accepted before freeze.");
                Time.timeScale = 0f;
                AudioListener.pause = true;
                Require(loop.Stop(), "A frozen loop must accept graceful stop.");
                float stopDeadline = Time.realtimeSinceStartup + 2f;
                while (manager.Registry.Count > 0 && Time.realtimeSinceStartup < stopDeadline)
                {
                    yield return null;
                }
                Require(manager.Registry.Count == 0, "Manual stop must finish and return its pool slot while frozen.");
                yield break;
            }

            float initialPitch = scenario == "Slow" ? 0.5f : 2f;
            ISoundHandle handle = manager.CreateBuilder().WithPitch(initialPitch).WithFade(0f, 0.4f)
                .Play(AudioId.DefaultSfx);
            Require(handle != null, "Pitched playback must be accepted.");
            AudioEmitter emitter = Get<AudioEmitter>(handle, "emitter");
            AudioSource source = emitter.GetComponent<AudioSource>();
            int finished = 0;
            handle.Finished += _ => finished++;
            bool sawTail = false;
            bool changedPitch = false;
            float lastVolume = 1f;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (finished == 0 && Time.realtimeSinceStartup < deadline)
            {
                if (Get<bool>(emitter, "naturalTailStarted"))
                {
                    sawTail = true;
                    Require(source.volume <= lastVolume + 0.001f, "A natural tail must never rise.");
                    lastVolume = source.volume;
                    if (scenario == "Change" && !changedPitch)
                    {
                        Require(handle.TrySetPitch(0.5f), "Live handle must accept a slower pitch during its tail.");
                        changedPitch = true;
                    }
                }
                yield return null;
            }
            Require(finished == 1 && handle.IsFinished, "Real playback must naturally finish exactly once.");
            Require(sawTail && (scenario != "Change" || changedPitch), "Real playback must exercise its natural tail.");
            Require(manager.Registry.Count == 0, "Natural completion must release the pool slot.");
            ISoundHandle reused = manager.CreateBuilder().WithFade(0f, 0f).Play(AudioId.DefaultSfx);
            Require(reused != null && Get<AudioEmitter>(reused, "emitter") == emitter, "The same emitter must be reusable.");
            Require(Mathf.Approximately(source.volume, 1f), "Reuse must clear the old natural-tail envelope.");
            Require(reused.Stop() && manager.Registry.Count == 0 && finished == 1,
                "Stopping reuse must release its slot without notifying the old playback twice.");
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

    private static void CheckEnvelope(AudioEmitter emitter)
    {
        AudioSource source = emitter.GetComponent<AudioSource>();
        Reset(emitter, 0.5f, 2f);
        Tail(emitter, 8f);
        Near(source.volume, 1f, "Slow pitch must not begin fading four real seconds early.");
        Tail(emitter, 9.5f);
        Near(source.volume, 0.5f, "Slow pitch half-tail must retain half volume.");
        Tail(emitter, 9.5f);
        Near(source.volume, 0.5f, "A paused playback head must not advance the tail.");

        Reset(emitter, 2f, 2f);
        Tail(emitter, 7f);
        Near(source.volume, 0.75f, "Fast pitch must enter the correct real-time tail window.");
        Tail(emitter, 9f);
        Near(source.volume, 0.25f, "Fast pitch must approach zero before actual completion.");
        emitter.Pitch = 0.5f;
        Tail(emitter, 9f);
        Near(source.volume, 0.25f, "Slowing during a tail must not raise volume.");
        Tail(emitter, 9.9f);
        Near(source.volume, 0.1f, "The slower tail must eventually continue decreasing.");
        Tail(emitter, 0f);
        Near(source.volume, 0.1f, "A reset completion cursor must not raise volume.");
        emitter.Pitch = 2f;
        Tail(emitter, 9.9f);
        Near(source.volume, 0.025f, "Speeding during a tail must follow the new remaining duration.");

        Reset(emitter, 1f, 2f);
        emitter.FadeIn = 20f;
        Call(emitter, "BeginFadeOnPlay");
        Set(emitter, "fadeScale", 0.4f);
        Tail(emitter, 9f);
        Near(source.volume, 0.2f, "A tail must start even while fade-in is incomplete.");
        Require(Get<object>(emitter, "fadePhase").ToString() == "None", "Tail entry must end the overlapping fade-in.");
        Tail(emitter, 9.5f);
        Near(source.volume, 0.1f, "Overlapping fade-in must not raise the tail ceiling.");

        Set(emitter, "isPlaying", true);
        emitter.RequestStop();
        Near(Get<float>(emitter, "fadeFrom"), 0.1f, "Manual stop during a tail must begin at the effective volume.");
        Set(emitter, "fadeElapsed", emitter.FadeOut);
        Call(emitter, "AdvanceFade");
        Require(!Get<bool>(emitter, "isPlaying"), "Manual stop must still complete after a natural tail has begun.");

        Reset(emitter, 1f, 4f);
        Tail(emitter, 8f);
        Near(source.volume, 0.5f, "A valid tail must begin before a later pitch change.");
        emitter.Pitch = 3f;
        Tail(emitter, 8f);
        Near(source.volume, 1f / 6f, "An active tail must survive pitch making FadeOut exceed the full clip duration.");

        Reset(emitter, 2f, 5f);
        Tail(emitter, 9f);
        Near(source.volume, 1f, "A fade covering the full pitched clip must remain ignored.");
        Reset(emitter, 1f, 0f);
        Tail(emitter, 9f);
        Near(source.volume, 1f, "Zero duration must leave volume unchanged.");
        Reset(emitter, 1f, 2f);
        Call(emitter, "AdvanceNaturalTail", 10f, 9f, true);
        Near(source.volume, 1f, "Loop playback must not have a natural tail.");
    }

    private static void Reset(AudioEmitter emitter, float pitch, float fadeOut)
    {
        emitter.FadeIn = 0f;
        emitter.FadeOut = fadeOut;
        emitter.Pitch = pitch;
        Call(emitter, "BeginFadeOnPlay");
    }

    private static void Tail(AudioEmitter emitter, float position)
    {
        Call(emitter, "AdvanceNaturalTail", 10f, position, false);
    }

    private static object Call(object target, string method, params object[] arguments)
    {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    private static T Get<T>(object target, string field)
    {
        return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Near(float actual, float expected, string message)
    {
        Require(Mathf.Abs(actual - expected) < 0.001f, message + $" Expected {expected}, got {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
