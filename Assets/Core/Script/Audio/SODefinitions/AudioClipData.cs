using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(
    fileName = MenuTool.Game.BasicSettings.AudioClipData.FileName,
    menuName = MenuTool.Game.BasicSettings.AudioClipData.Path)]
public class AudioClipData : ScriptableObject
{
#region APIs
    public AudioId AudioId => audioId;
    public AudioClip Clip => clip;
    public AudioMixerGroup MixerGroup => mixerGroup;
    public bool Loop => loop;
    public int MaxInstances => maxInstances;
    public float Volume => volume;
    public float Pitch => pitch;
    public float SpatialBlend => spatialBlend;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public bool DefaultSurviveFreeze => defaultSurviveFreeze;
    public float FadeIn => fadeIn;
    public float FadeOut => fadeOut;
#endregion

    [SerializeField] private AudioId audioId;
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioClip[] variants = new AudioClip[0];
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] private bool loop = false;
    // Listener-pause behavior is independent of looping: either kind of sound may opt out of the pause.
    // This flag configures ignoreListenerPause; it does not itself stop or release an emitter.
    // 是否忽略监听器暂停与是否循环相互独立; 此开关不直接停止或归还声部.
    [SerializeField]
    [Tooltip("Default AudioSource.ignoreListenerPause value. False obeys listener pause; true ignores it. AudioBuilder.WithSurviveFreeze overrides this per request.")]
    private bool defaultSurviveFreeze = false;
    [SerializeField] private int maxInstances = 10;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Baseline playback volume. AudioBuilder overrides it per request; ISoundHandle.TrySetVolume changes an active playback.")]
    private float volume = 1f;
    [SerializeField, Range(0.1f, 3f)]
    [Tooltip("Baseline playback pitch. AudioBuilder overrides it per request; ISoundHandle.TrySetPitch changes an active playback.")]
    private float pitch = 1f;

    // Fades are opt-in: 0 reproduces the original hard start and hard cut exactly, so existing assets keep
    // their current behaviour until someone asks for a ramp on that clip.
    // 淡入淡出是选配: 0 精确复现原先的硬起与硬切, 因此既有资产在有人为某 clip 要求渐变之前, 行为完全不变.
    [SerializeField, Min(0f), BoxGroup("Fade")]
    [Tooltip("Seconds spent ramping this clip up from silence on every playback. 0 keeps the hard start.")]
    private float fadeIn = 0f;
    [SerializeField, Min(0f), BoxGroup("Fade")]
    [Tooltip("Seconds spent ramping this clip down. Used by a graceful stop, and as the tail of a natural playback for a non-looping clip. 0 keeps the hard cut.")]
    private float fadeOut = 0f;

    [SerializeField, Range(0f, 1f), BoxGroup("Spatial")]
    [Tooltip("AudioSource spatial blend. Supply a builder position or follow target for spatial playback; without either, the emitter uses its current position.")]
    private float spatialBlend = 0f;
    [SerializeField, Min(0f), BoxGroup("Spatial")]
    [Tooltip("Value copied to AudioSource.minDistance; interpreted by the source's distance rolloff settings.")]
    private float minDistance = 1f;
    [SerializeField, Min(0f), BoxGroup("Spatial")]
    [Tooltip("Value copied to AudioSource.maxDistance. This field does not implement a hard mute beyond that distance.")]
    private float maxDistance = 20f;

    /// <summary>Select one clip per playback; empty variant slots are ignored.</summary>
    public AudioClip SelectClip()
    {
        int count = 1;
        if (variants != null)
        {
            foreach (AudioClip variant in variants)
            {
                if (variant != null)
                {
                    count++;
                }
            }
        }
        if (count == 1)
        {
            return clip;
        }
        int choice = Random.Range(0, count);
        if (choice == 0)
        {
            return clip;
        }
        foreach (AudioClip variant in variants)
        {
            if (variant != null && --choice == 0)
            {
                return variant;
            }
        }
        return clip;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
        ValidateRefs();
    }

    private void ClampValues()
    {
        if (maxInstances < 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(maxInstances)))
                .Action(LogAction.ClampValue)
                .Write();
            maxInstances = 0;
        }
    }

    private void ValidateRefs()
    {
        if (!clip)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(clip)))
                .Action(LogAction.UseFallbackValue("null"))
                .Write();
        }

        if (!mixerGroup)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(mixerGroup)))
                .Action(LogAction.UseFallbackValue("null"))
                .Write();
        }
    }
#endif
}
