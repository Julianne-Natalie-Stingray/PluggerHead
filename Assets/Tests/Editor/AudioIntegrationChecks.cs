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

        TimerRunner.Instance.StartCoroutine(Execute(false));
        return LastResult;
    }

    /// <summary>
    /// Runs the real playback checks for Unity Test Runner and propagates failures to its result.
    /// A temporary TimerRunner is supplied when the test scene has none; every owned object and
    /// changed global is restored on success, failure, timeout, or coroutine disposal.
    /// 为 Test Runner 提供独立的播放检查, 自动补齐 TimerRunner 并清理测试状态, 失败直接抛出.
    /// </summary>
    public static IEnumerator RunForTests()
    {
        return Execute(true);
    }

    private static IEnumerator Execute(bool propagateFailure)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException("Audio integration checks require Play Mode.");
        }

        if (isRunning)
        {
            throw new InvalidOperationException("Audio integration checks are already running.");
        }

        if (GameStateManager.Current != GameState.Playing)
        {
            throw new InvalidOperationException("Audio integration checks require GameState.Playing.");
        }

        isRunning = true;
        checks = 0;
        LastResult = "RUNNING: audio integration checks.";
        float previousTimeScale = Time.timeScale;
        bool previousListenerPause = AudioListener.pause;
        bool previousRunInBackground = Application.runInBackground;
        GameObject temporaryRunner = null;
        IEnumerator routine = null;
        try
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Application.runInBackground = true;
            if (!TimerRunner.Instance)
            {
                temporaryRunner = new GameObject("AudioIntegrationChecks TimerRunner (temporary)");
                temporaryRunner.AddComponent<TimerRunner>();
            }

            routine = CheckPlayback();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (true)
            {
                bool hasNext;
                try
                {
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        throw new TimeoutException("Audio integration checks exceeded 10 seconds.");
                    }

                    hasNext = routine.MoveNext();
                }
                catch (Exception exception)
                {
                    LastResult = $"FAIL after {checks} audio checks: {exception}";
                    if (propagateFailure)
                    {
                        throw;
                    }

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
            try
            {
                (routine as IDisposable)?.Dispose();
                if (fixture)
                {
                    // Destroy emitters before the runner so OnDestroy can cancel their timers.
                    // 先销毁声部, 保留 Runner 供 OnDestroy 停止尚未结束的 Timer.
                    Object.DestroyImmediate(fixture);
                }

                Object.DestroyImmediate(configs);
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(clip);
            }
            finally
            {
                if (temporaryRunner)
                {
                    Object.DestroyImmediate(temporaryRunner);
                }

                fixture = null;
                configs = null;
                data = null;
                clip = null;
                manager = null;
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousListenerPause;
                Application.runInBackground = previousRunInBackground;
                isRunning = false;
            }
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
        if (!Array.Exists(Object.FindObjectsOfType<AudioListener>(), listener => listener.isActiveAndEnabled))
        {
            fixture.AddComponent<AudioListener>();
        }

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
