using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks real manager admission and synchronous Finished reentry with isolated pooled voices.</summary>
public static class AudioLimitIntegrationChecks
{
    public static IEnumerator Run(string scenario)
    {
        GameObject fixture = null;
        GameObject runner = null;
        AudioClip clip = null;
        AudioManagerConfigs configs = null;
        var clips = new List<AudioClipData>();
        bool listenerPause = AudioListener.pause;
        bool background = Application.runInBackground;
        try
        {
            Require(GameStateManager.Current == GameState.Playing, "Audio limit checks require Playing.");
            AudioListener.pause = false;
            Application.runInBackground = true;
            if (!TimerRunner.Instance)
            {
                runner = new GameObject("AudioLimit TimerRunner");
                runner.AddComponent<TimerRunner>();
            }
            fixture = new GameObject("AudioLimit isolated fixture");
            if (!Array.Exists(Object.FindObjectsOfType<AudioListener>(), item => item.isActiveAndEnabled))
            {
                fixture.AddComponent<AudioListener>();
            }
            var template = new GameObject("AudioLimit template");
            template.transform.SetParent(fixture.transform);
            template.SetActive(false);
            AudioEmitter prefab = template.AddComponent<AudioEmitter>();
            prefab.GetComponent<AudioSource>().playOnAwake = false;
            clip = AudioClip.Create("AudioLimit silence", 441000, 1, 44100, false);
            foreach (AudioId id in new[] { AudioId.DefaultSfx, AudioId.DefaultOst, AudioId.MouseClick })
            {
                var data = ScriptableObject.CreateInstance<AudioClipData>();
                clips.Add(data);
                Set(data, "clip", clip);
                Set(data, "audioId", id);
                Set(data, "maxInstances", 8);
            }
            configs = ScriptableObject.CreateInstance<AudioManagerConfigs>();
            Set(configs, "audios", clips);
            Set(configs, "maxSoundInstance", 8);
            Set(configs, "maxPoolSize", 8);
            Set(configs, "prewarmAmount", 0);
            var host = new GameObject("AudioLimit manager");
            host.transform.SetParent(fixture.transform);
            host.SetActive(false);
            AudioManager manager = host.AddComponent<AudioManager>();
            Set(manager, "configs", configs);
            Set(manager, "emitterPrefab", prefab);
            Set(manager, "emitterRoot", fixture.transform);
            typeof(AudioManager).GetMethod("InitializeInternal", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, null);

            CheckScenario(scenario, manager, configs, clips[0]);
            yield return null;
        }
        finally
        {
            if (fixture)
            {
                Object.DestroyImmediate(fixture);
            }
            Object.DestroyImmediate(configs);
            foreach (AudioClipData data in clips)
            {
                Object.DestroyImmediate(data);
            }
            Object.DestroyImmediate(clip);
            if (runner)
            {
                Object.DestroyImmediate(runner);
            }
            AudioListener.pause = listenerPause;
            Application.runInBackground = background;
        }
    }

    private static void CheckScenario(string scenario, AudioManager manager, AudioManagerConfigs configs, AudioClipData requestedData)
    {
        const AudioId requested = AudioId.DefaultSfx;
        const AudioId other = AudioId.DefaultOst;
        const AudioId third = AudioId.MouseClick;
        if (scenario == "PerIdReentry" || scenario == "GlobalReentry")
        {
            if (scenario == "PerIdReentry")
            {
                Set(requestedData, "maxInstances", 1);
            }
            else
            {
                Set(configs, "maxSoundInstance", 1);
            }
            ISoundHandle victim = Play(manager, requested);
            ISoundHandle nested = null;
            int notifications = 0;
            victim.Finished += _ =>
            {
                notifications++;
                nested = Play(manager, scenario == "PerIdReentry" ? requested : other);
            };
            ISoundHandle outer = manager.CreateBuilder().Play(requested);
            Require(outer == null && nested != null && nested.IsPlaying && notifications == 1,
                "A reentrant playback must keep its slot; the outer request must refuse instead of chasing it.");
            Require(manager.Registry.Count == 1 && !victim.Stop(), "Reentry must leave exactly one voice and invalidate the victim.");
            Require(manager.Registry.CountOf(scenario == "PerIdReentry" ? requested : other) == 1,
                "The surviving voice must be the callback's playback.");
            return;
        }
        if (scenario == "CrossCapReentry" || scenario == "CallbackReducesCap")
        {
            Set(configs, "maxSoundInstance", 2);
            Set(requestedData, "maxInstances", 1);
            ISoundHandle oldest = Play(manager, other);
            ISoundHandle younger = Play(manager, third);
            ISoundHandle nested = null;
            int notifications = 0;
            oldest.Finished += _ =>
            {
                notifications++;
                if (scenario == "CrossCapReentry")
                {
                    Require(younger.Stop(), "The callback must free global room before filling the requested ID's slot.");
                    nested = Play(manager, requested);
                }
                else
                {
                    Set(requestedData, "maxInstances", 0);
                }
            };
            Require(manager.CreateBuilder().Play(requested) == null && notifications == 1,
                "Global preemption must be followed by a check of the requested ID's current cap.");
            Require(manager.Registry.Count == 1, "The outer request must not consume the remaining global slot.");
            if (scenario == "CrossCapReentry")
            {
                Require(nested != null && nested.IsPlaying && manager.Registry.CountOf(requested) == 1,
                    "The callback's requested-ID playback must remain intact.");
            }
            else
            {
                Require(younger.IsPlaying && manager.Registry.CountOf(requested) == 0,
                    "A callback's newly reduced cap must be observed without further preemption.");
            }
            return;
        }
        if (scenario == "PerIdReduction" || scenario == "GlobalReduction")
        {
            ISoundHandle oldest = Play(manager, requested);
            ISoundHandle second = Play(manager, scenario == "PerIdReduction" ? requested : other);
            ISoundHandle thirdHandle = Play(manager, scenario == "PerIdReduction" ? requested : third);
            int notifications = 0;
            oldest.Finished += _ => notifications++;
            if (scenario == "PerIdReduction")
            {
                Set(requestedData, "maxInstances", 1);
            }
            else
            {
                Set(configs, "maxSoundInstance", 1);
            }
            Require(manager.CreateBuilder().Play(requested) == null, "One preemption insufficient after a cap reduction must refuse admission.");
            Require(notifications == 1 && manager.Registry.Count == 2 && second.IsPlaying && thirdHandle.IsPlaying,
                "A cap reduction must stop only one candidate, retain remaining voices, and not create another.");
            return;
        }
        if (scenario == "OrdinaryPerId" || scenario == "OrdinaryGlobal")
        {
            if (scenario == "OrdinaryPerId")
            {
                Set(requestedData, "maxInstances", 2);
            }
            else
            {
                Set(configs, "maxSoundInstance", 2);
            }
            ISoundHandle oldest = Play(manager, requested);
            ISoundHandle younger = Play(manager, scenario == "OrdinaryPerId" ? requested : other);
            ISoundHandle replacement = Play(manager, scenario == "OrdinaryPerId" ? requested : third);
            Require(!oldest.Stop() && younger.IsPlaying && replacement.IsPlaying && manager.Registry.Count == 2,
                "Ordinary admission must preempt only the oldest eligible voice and preserve the younger voice.");
            return;
        }
        if (scenario == "LoopProtection")
        {
            Set(requestedData, "maxInstances", 1);
            Set(requestedData, "loop", true);
            ISoundHandle loop = Play(manager, requested);
            Require(manager.CreateBuilder().Play(requested) == null && loop.IsPlaying,
                "Per-ID admission must refuse instead of preempting a loop.");
            Set(configs, "maxSoundInstance", 1);
            Require(manager.CreateBuilder().Play(other) == null && loop.IsPlaying,
                "Global admission must refuse when only looping victims exist.");
            Set(configs, "maxSoundInstance", 2);
            ISoundHandle oneShot = Play(manager, other);
            ISoundHandle replacement = Play(manager, third);
            Require(loop.IsPlaying && !oneShot.Stop() && replacement.IsPlaying && manager.Registry.Count == 2,
                "Global selection must skip an older loop and stop the eligible one-shot.");
            return;
        }
        if (scenario == "NonPositiveCaps")
        {
            ISoundHandle existing = Play(manager, requested);
            foreach (int value in new[] { 0, -1 })
            {
                Set(requestedData, "maxInstances", value);
                Require(manager.CreateBuilder().Play(requested) == null && existing.IsPlaying && manager.Registry.Count == 1,
                    "A non-positive per-ID cap must reject without preempting the existing voice.");
                Set(requestedData, "maxInstances", 8);
                Set(configs, "maxSoundInstance", value);
                Require(manager.CreateBuilder().Play(other) == null && existing.IsPlaying && manager.Registry.Count == 1,
                    "A non-positive global cap must reject without preempting the existing voice.");
                Set(configs, "maxSoundInstance", 8);
            }
            return;
        }
        throw new ArgumentException("Unknown audio limit scenario: " + scenario);
    }

    private static ISoundHandle Play(AudioManager manager, AudioId id)
    {
        ISoundHandle handle = manager.CreateBuilder().Play(id);
        Require(handle != null, "Fixture setup or callback playback must be accepted: " + id);
        return handle;
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
