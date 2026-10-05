using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;

[CreateAssetMenu(
    fileName = MenuTool.Game.BasicSettings.AudioManagerConfigs.FileName,
    menuName = MenuTool.Game.BasicSettings.AudioManagerConfigs.Path)]
public class AudioManagerConfigs : ScriptableObject
{
    [SerializeField] private List<AudioClipData> audios = new();

#region Bus Volume

    // A mixer is an asset, not a component, so it cannot be resolved with GetComponent<>(); this is the
    // "genuine need" §5.2.1 allows for a serialized reference. The parameter names live here as data rather
    // than as constants inside logic, so renaming one in the AudioMixer window needs no code change.
    // mixer 是资产而不是组件, 无法用 GetComponent<>() 解析, 这正是 §5.2.1 为序列化引用保留的"确有需要".
    // 参数名作为数据放在这里, 而不是写成逻辑里的常量, 因此在 AudioMixer 窗口改名无需改代码.
    [SerializeField, BoxGroup("Bus Volume")]
    [Tooltip("AudioMixer whose exposed volume parameters this bus drives.")]
    private AudioMixer mixer;
    [SerializeField, BoxGroup("Bus Volume")]
    [Tooltip("Exposed parameter name carrying the master bus volume.")]
    private string masterVolumeParameter = "MasterVolume";
    [SerializeField, BoxGroup("Bus Volume")]
    [Tooltip("Exposed parameter name carrying the OST bus volume.")]
    private string ostVolumeParameter = "OstVolume";
    [SerializeField, BoxGroup("Bus Volume")]
    [Tooltip("Exposed parameter name carrying the SFX bus volume.")]
    private string sfxVolumeParameter = "SfxVolume";

#endregion

#region Emitter Pooling

    [SerializeField, BoxGroup("Emitter Pooling")] 
    private bool collectionCheck = true;
    [SerializeField, BoxGroup("Emitter Pooling")] 
    private int defaultCapacity = 10;
    [SerializeField, BoxGroup("Emitter Pooling")] 
    private int maxPoolSize = 30;
    [SerializeField, BoxGroup("Emitter Pooling")]
    private int prewarmAmount = 10;
    [SerializeField, BoxGroup("Emitter Pooling")] 
    private int maxSoundInstance = 30;

#endregion

#region APIS

    public AudioMixer Mixer => mixer;
    public string MasterVolumeParameter => masterVolumeParameter;
    public string OstVolumeParameter => ostVolumeParameter;
    public string SfxVolumeParameter => sfxVolumeParameter;
    public bool CollectionCheck => collectionCheck;
    public int DefaultCapacity => defaultCapacity;
    public int MaxPoolSize => maxPoolSize;
    public int PrewarmAmount => prewarmAmount;
    public int MaxSoundInstance => maxSoundInstance;

#endregion

    public bool TryGetClip(AudioId requested, 
        out AudioClipData clip)
    {
        clip = audios.FirstOrDefault(audio => audio.AudioId == requested);
        if (!clip)
            GameLog.Error(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.CannotFind(nameof(requested)))
                .Action(LogAction.UseFallbackValue("null"))
                .Write();
        return clip;
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
    }

    private void ClampValues()
    {
        if (!mixer)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.NotAssigned(nameof(mixer)))
                .Action(LogAction.UseFallbackValue("no bus volume control"))
                .Write();
        }

        if (defaultCapacity < 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(defaultCapacity)))
                .Action(LogAction.ClampValue)
                .Write();
            defaultCapacity = 0;
        }

        if (maxPoolSize < 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(maxPoolSize)))
                .Action(LogAction.ClampValue)
                .Write();
            maxPoolSize = 0;
        }
        
        if (maxSoundInstance < 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(maxSoundInstance)))
                .Action(LogAction.ClampValue)
                .Write();
            maxSoundInstance = 0;
        }

        if (prewarmAmount < 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(prewarmAmount)))
                .Action(LogAction.ClampValue)
                .Write();
            prewarmAmount = 0;
        }
    }

    [Button("Remove Duplicates")]
    private void RemoveDuplicates()
    {
        if (audios == null || audios.Count == 0)
            return;

        var seenIds = new HashSet<AudioId>();
        int originalCount = audios.Count;

        audios.RemoveAll(audio =>
            !audio || !seenIds.Add(audio.AudioId));

        int removedCount = originalCount - audios.Count;

        if (removedCount > 0)
        {
            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify(
                    $"{nameof(audios)} contained duplicate AudioIds. "))
                .Action(LogAction.Specify(
                    $"Removed {removedCount} duplicate(s), keeping the earliest occurrence. "))
                .Write();
        }
    }
#endif
}
