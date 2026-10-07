using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MenuMusicIntegrationChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static IEnumerator CheckRegisteredMusic()
    {
        var state = new IntegrationSceneState();
        GameObject fixture = null;
        GameObject runner = null;
        AudioManager manager = null;
        bool subscribed = false;
        bool background = Application.runInBackground;
        try
        {
            Require(GameStateManager.Current == GameState.Playing, "The isolated audio check requires Playing.");
            var configs = AssetDatabase.LoadAssetAtPath<AudioManagerConfigs>("Assets/SO/Audio/DefaultAudioManagerConfigs.asset");
            var data = AssetDatabase.LoadAssetAtPath<AudioClipData>("Assets/Audios/SO/GameMainMenu.asset");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/Clips/GameMainMenu.wav");
            var click = AssetDatabase.LoadAssetAtPath<AudioClipData>("Assets/Audios/SO/MouseClick.asset");
            Require(configs && data && clip && configs.TryGetClip(AudioId.GameMainMenu, out var mapped) &&
                mapped == data && data.Clip == clip, "The registered menu ID must resolve the new WAV through its real ClipData.");
            Require(configs.TryGetClip(AudioId.MouseClick, out var mappedClick) && mappedClick == click,
                "Registering music must preserve MouseClick.");
            var ids = new HashSet<AudioId>();
            foreach (AudioClipData entry in Get<List<AudioClipData>>(configs, "audios"))
            {
                Require(entry && entry.AudioId != AudioId.None && ids.Add(entry.AudioId),
                    "Default audio registrations must have unique non-None IDs.");
            }
            Require(data.Loop && !data.DefaultSurviveFreeze && data.MixerGroup &&
                data.MixerGroup.audioMixer == configs.Mixer && data.MixerGroup.name == "OST",
                "Menu music must loop on the OST bus and obey listener pause.");

            AudioListener.pause = false;
            Application.runInBackground = true;
            if (!TimerRunner.Instance)
            {
                runner = new GameObject("MenuMusic TimerRunner");
                runner.AddComponent<TimerRunner>();
            }
            fixture = new GameObject("MenuMusic isolated fixture");
            if (!Array.Exists(Object.FindObjectsOfType<AudioListener>(), listener => listener.isActiveAndEnabled))
            {
                fixture.AddComponent<AudioListener>();
            }
            var host = new GameObject("MenuMusic isolated manager");
            host.transform.SetParent(fixture.transform);
            host.SetActive(false);
            manager = host.AddComponent<AudioManager>();
            Set(manager, "configs", configs);
            Set(manager, "emitterPrefab", AssetDatabase.LoadAssetAtPath<AudioEmitter>("Assets/Prefabs/Core/AudioEmitter.prefab"));
            Set(manager, "emitterRoot", fixture.transform);
            typeof(AudioManager).GetMethod("InitializeInternal", PrivateInstance).Invoke(manager, null);
            typeof(AudioManager).GetMethod("OnEnable", PrivateInstance).Invoke(manager, null);
            subscribed = true;
            ISoundHandle handle = manager.PlayBackgroundMusic(AudioId.GameMainMenu);
            Require(handle != null && handle.IsPlaying, "Real registered music must start through Audio API.");
            AudioSource source = Get<AudioEmitter>(handle, "emitter").GetComponent<AudioSource>();
            Require(source.clip == clip && source.loop == data.Loop && source.outputAudioMixerGroup == data.MixerGroup,
                "The source must use the real clip and configured playback routing.");
            Require(ReferenceEquals(handle, manager.PlayBackgroundMusic(AudioId.GameMainMenu)) &&
                manager.Registry.CountOf(AudioId.GameMainMenu) == 1, "Repeated BGM requests must reuse one voice.");
            yield return null;
            GameStateManager.Freeze();
            yield return null;
            int pausedAt = source.timeSamples;
            yield return new WaitForSecondsRealtime(0.1f);
            Require(AudioListener.pause && !source.ignoreListenerPause && source.timeSamples == pausedAt &&
                ReferenceEquals(handle, manager.PlayBackgroundMusic(AudioId.GameMainMenu)),
                "Pause must preserve the same voice and playback position.");
            GameStateManager.Resume();
            yield return IntegrationSceneWait.Until(() => source.timeSamples != pausedAt,
                "resuming the registered menu music", 5f);
            Require(handle.Stop() && !handle.IsPlaying && manager.Registry.CountOf(AudioId.GameMainMenu) == 0,
                "Stopping BGM must release its registered voice.");
        }
        finally
        {
            if (manager && subscribed)
            {
                typeof(AudioManager).GetMethod("OnDisable", PrivateInstance).Invoke(manager, null);
            }
            if (fixture)
            {
                Object.DestroyImmediate(fixture);
            }
            if (runner)
            {
                Object.DestroyImmediate(runner);
            }
            state.Restore();
            Application.runInBackground = background;
        }
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
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
