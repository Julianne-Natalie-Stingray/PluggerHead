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
#endregion
    
    [SerializeField] private AudioId audioId;
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] private bool loop = false;
    // Whether a clip is kept through a freeze is independent of whether it loops: a loop may be cut by a
    // freeze, and a one-shot may be expected to survive one. Do not re-couple the two.
    // 是否在冻结中被保留, 与是否循环是两件事: 循环音可以被冻结掐断, 一次性音也可能需要熬过冻结. 不要把两者重新绑在一起.
    [SerializeField]
    [Tooltip("Static initial for whether a playback of this clip survives entering Freezed. AudioBuilder.WithSurviveFreeze overrides it per call. Default false, which cuts the clip.")]
    private bool defaultSurviveFreeze = false;
    [SerializeField] private int maxInstances = 10;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Baseline volume for every playback of this clip. The builder may raise or lower it live.")]
    private float volume = 1f;
    [SerializeField, Range(0.1f, 3f)]
    [Tooltip("Baseline pitch for every playback of this clip. The builder may raise or lower it live.")]
    private float pitch = 1f;

    [SerializeField, Range(0f, 1f), BoxGroup("Spatial")]
    [Tooltip("0 is fully 2D and ignores position. Above 0 the playback needs a position, from the builder or from a follow target.")]
    private float spatialBlend = 0f;
    [SerializeField, Min(0f), BoxGroup("Spatial")]
    [Tooltip("Distance from the listener at which the sound stops attenuating.")]
    private float minDistance = 1f;
    [SerializeField, Min(0f), BoxGroup("Spatial")]
    [Tooltip("Distance from the listener beyond which the sound stops being audible.")]
    private float maxDistance = 20f;
    
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
