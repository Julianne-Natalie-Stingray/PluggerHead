using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Repeatable Play Mode smoke checks against the real audio manager and pooled emitters.
/// Uses temporary objects and generated silent clips; never saves scenes or changes project assets.
/// 在 Play Mode 中验证真实音频管理器与池化声部, 仅创建临时对象及静音 clip.
/// </summary>
public static class AudioIntegrationChecks
{
    public static string LastResult { get; private set; } = "Not run.";

    private static bool isRunning;
    private static GameObject fixture;
    private static AudioClip clip;
    private static AudioClipData data;
    private static AudioManagerConfigs configs;
    private static AudioManager manager;
    private static int checks;

    public static string Run()
    {
        if (isRunning)
        {
            return LastResult;
        }

        if (!Application.isPlaying || !TimerRunner.Instance)
        {
            return "Requires Play Mode in a scene with TimerRunner.";
        }

        if (GameStateManager.Current != GameState.Playing || AudioListener.pause)
        {
            return "Requires an unpaused Playing state.";
        }

        isRunning = true;
        checks = 0;
        LastResult = "RUNNING: audio integration checks.";
        TimerRunner.Instance.StartCoroutine(Execute());
        return LastResult;
    }

    private static IEnumerator Execute()
    {
        IEnumerator routine = CheckPlayback();
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = routine.MoveNext();
                }
                catch (Exception exception)
                {
                    LastResult = $"FAIL after {checks} audio checks: {exception}";
                    yield break;
                }

                if (!hasNext)
                {
                    LastResult = $"PASS: {checks} audio integration checks.";
                    yield break;
                }

                yield return routine.Current;
            }
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
            manager = null;
            isRunning = false;
        }
    }

    private static IEnumerator CheckPlayback()
    {
        CreateFixture();
        ISoundHandle first = manager.CreateBuilder().Play(AudioId.DefaultSfx);
        Check(first != null, "Default request accepted.");
        AudioEmitter emitter = GetField<AudioEmitter>(first, "emitter");
        Check(Mathf.Approximately(emitter.Volume, 0.35f), "Clip volume default survives Play.");
        Check(Mathf.Approximately(emitter.Pitch, 1.25f), "Clip pitch default survives Play.");
        Timer oldTimer = GetField<Timer>(emitter, "completionTimer");
        Check(oldTimer != null && oldTimer.IsRunning, "One-shot owns a running completion timer.");
        int stoppedEvents = 0;
        first.Finished += _ => stoppedEvents++;
        Check(first.Stop(), "First stop accepted.");
        Check(!first.Stop() && stoppedEvents == 1 && !first.IsFinished, "Manual stop is idempotent and not natural.");
        Check(manager.Registry.Count == 0 && !oldTimer.IsRunning, "Stop releases registry and cancels old timer.");

        ISoundHandle overridden = manager.CreateBuilder().WithVolume(0.7f).WithPitch(0.8f).Play(AudioId.DefaultSfx);
        Check(overridden != null, "Request after release accepted at global cap one.");
        AudioEmitter reused = GetField<AudioEmitter>(overridden, "emitter");
        Check(reused == emitter, "Emitter is reused.");
        Check(Mathf.Approximately(reused.Volume, 0.7f) && Mathf.Approximately(reused.Pitch, 0.8f), "Builder overrides survive Play.");
        Check(!first.TrySetVolume(0f), "Old handle cannot mutate the reused emitter.");
        int naturalEvents = 0;
        overridden.Finished += _ => naturalEvents++;
        float deadline = Time.realtimeSinceStartup + 3f;
        while (naturalEvents == 0 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Check(naturalEvents == 1 && overridden.IsFinished, "Actual one-shot playback reports natural completion exactly once.");
        Check(manager.Registry.Count == 0 && !oldTimer.IsRunning, "Natural end releases slot and old timer remains canceled.");

        SetField(data, "loop", true);
        ISoundHandle loop = manager.CreateBuilder().WithFade(0f, 0.05f).Play(AudioId.DefaultSfx);
        Check(loop != null, "Loop request accepted.");
        AudioEmitter loopingEmitter = GetField<AudioEmitter>(loop, "emitter");
        Check(GetField<Timer>(loopingEmitter, "completionTimer") == null, "Reused loop has no leftover completion timer.");
        yield return new WaitForSecondsRealtime(0.2f);
        Check(loop.IsPlaying && manager.Registry.Count == 1, "Loop stays alive beyond the prior one-shot duration.");
        Check(loop.Stop() && !loop.IsPlaying && !loop.IsFinished, "Graceful stop invalidates its handle immediately.");
        Check(manager.Registry.Count == 1, "Fade retains the slot until its tail ends.");
        yield return new WaitForSecondsRealtime(0.15f);
        Check(manager.Registry.Count == 0, "Fade completion releases the slot.");
    }

    private static void CreateFixture()
    {
        fixture = new GameObject("AudioIntegrationChecks (temporary)");
        GameObject template = new GameObject("Emitter template");
        template.transform.SetParent(fixture.transform);
        template.SetActive(false);
        AudioEmitter prefab = template.AddComponent<AudioEmitter>();
        prefab.GetComponent<AudioSource>().playOnAwake = false;

        clip = AudioClip.Create("Audio check silence", 4410, 1, 44100, false);
        data = ScriptableObject.CreateInstance<AudioClipData>();
        SetField(data, "clip", clip);
        SetField(data, "volume", 0.35f);
        SetField(data, "pitch", 1.25f);
        SetField(data, "maxInstances", 1);

        configs = ScriptableObject.CreateInstance<AudioManagerConfigs>();
        SetField(configs, "audios", new List<AudioClipData> { data });
        SetField(configs, "maxSoundInstance", 1);
        SetField(configs, "maxPoolSize", 1);
        SetField(configs, "prewarmAmount", 0);

        // Keep the manager host inactive and initialize only its pool: no Start means no changes
        // to the live game's mixer. Emitters live under the active fixture and use the real TimerRunner.
        // 管理器宿主保持禁用, 仅初始化池; 避免 Start 修改当前游戏的 Mixer.
        GameObject host = new GameObject("Manager");
        host.transform.SetParent(fixture.transform);
        host.SetActive(false);
        manager = host.AddComponent<AudioManager>();
        SetField(manager, "configs", configs);
        SetField(manager, "emitterPrefab", prefab);
        SetField(manager, "emitterRoot", fixture.transform);
        typeof(AudioManager).GetMethod("InitializeInternal", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }

    private static T GetField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException(description);
        }

        checks++;
    }
}
