using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated playback checks with synthetic audio; no scene or mixer assets are modified.</summary>
public static class PlayerAnimationAudioChecks
{
    public static void Run()
    {
        var state = new IntegrationSceneState();
        CoreFacade originalCore = CoreFacade.Instance;
        var instance = typeof(CoreFacade).GetProperty("Instance");
        GameObject fixture = null;
        GameObject runner = null;
        AudioClip clip = null;
        AudioClipData data = null;
        AudioManagerConfigs configs = null;
        try
        {
            Require(GameStateManager.Current == GameState.Playing, "Fixture requires Playing.");
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (!TimerRunner.Instance)
            {
                runner = new GameObject("Animation audio timer");
                runner.AddComponent<TimerRunner>();
            }
            fixture = new GameObject("Animation audio fixture");
            if (!Array.Exists(Object.FindObjectsOfType<AudioListener>(), item => item.isActiveAndEnabled))
            {
                fixture.AddComponent<AudioListener>();
            }
            var template = new GameObject("Emitter template");
            template.transform.SetParent(fixture.transform);
            template.SetActive(false);
            AudioEmitter emitter = template.AddComponent<AudioEmitter>();
            clip = AudioClip.Create("Animation test silence", 44100, 1, 44100, false);
            data = ScriptableObject.CreateInstance<AudioClipData>();
            Set(data, "audioId", AudioId.DefaultSfx);
            Set(data, "clip", clip);
            Set(data, "fadeIn", 0f);
            Set(data, "fadeOut", 0f);
            Set(data, "maxInstances", 8);
            configs = ScriptableObject.CreateInstance<AudioManagerConfigs>();
            Set(configs, "audios", new List<AudioClipData> { data });
            Set(configs, "maxSoundInstance", 8);
            Set(configs, "maxPoolSize", 8);
            var coreHost = new GameObject("Inactive test Core");
            coreHost.transform.SetParent(fixture.transform);
            coreHost.SetActive(false);
            CoreFacade core = coreHost.AddComponent<CoreFacade>();
            AudioManager manager = coreHost.GetComponent<AudioManager>();
            Set(manager, "configs", configs);
            Set(manager, "emitterPrefab", emitter);
            Set(manager, "emitterRoot", fixture.transform);
            typeof(AudioManager).GetMethod("InitializeInternal", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, null);
            Set(core, "audios", manager);
            instance.SetValue(null, core);

            var visual = new GameObject("Animated audio receiver");
            visual.transform.SetParent(fixture.transform);
            visual.AddComponent<SpriteRenderer>();
            Animator animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Visual/Player/PlayerAC.controller");
            PlayerAnimationAudio receiver = visual.AddComponent<PlayerAnimationAudio>();
            Set(receiver, "deathAudio", AudioId.DefaultSfx);
            Set(receiver, "moveAudio", AudioId.DefaultSfx);
            Set(receiver, "interactAudio", AudioId.DefaultSfx);
            foreach (string action in new[] { "Move", "Interact", "Death" })
            {
                animator.SetBool("tryMoving", action == "Move");
                animator.Play("Player" + action + "Anim", 0, 0f);
                animator.Update(0f);
                animator.Update(0.02f);
                Require(GetHandle(receiver, action.ToLowerInvariant() + "Handle")?.IsPlaying == true,
                    action + " animation must dispatch its event and start playback.");
            }

            receiver.enabled = false;
            Require(manager.Registry.Count == 0, "Disable must release all owned voices.");
            receiver.enabled = true;
            GameStateManager.Freeze();
            receiver.PlayMoveAudio();
            receiver.PlayInteractAudio();
            Require(manager.Registry.Count == 0, "Frozen movement and interaction must not start audio.");
            receiver.PlayDeathAudio();
            ISoundHandle death = GetHandle(receiver, "deathHandle");
            Require(death?.IsPlaying == true && manager.Registry.Count == 1,
                "Death audio must start while frozen.");
            AudioEmitter playing = (AudioEmitter)death.GetType()
                .GetField("emitter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(death);
            Require(playing.GetComponent<AudioSource>().ignoreListenerPause,
                "Death audio must ignore the paused listener.");
            receiver.enabled = false;
            Require(!death.IsPlaying && manager.Registry.Count == 0, "Disable must invalidate the death handle.");
        }
        finally
        {
            instance.SetValue(null, originalCore);
            if (fixture != null)
            {
                Object.DestroyImmediate(fixture);
            }
            Object.DestroyImmediate(configs);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(clip);
            if (runner != null)
            {
                Object.DestroyImmediate(runner);
            }
            state.Restore();
        }
    }

    private static ISoundHandle GetHandle(object target, string field)
    {
        return (ISoundHandle)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
