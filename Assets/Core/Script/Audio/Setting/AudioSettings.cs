using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[Serializable]
public class AudioSettings
{
#region APIs

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            if (float.IsNaN(value))
            {
                ReportIgnoredNaN(nameof(masterVolume));
                return;
            }

            if (value is >= 0f and <= 1f)
            {
                masterVolume = value;
                return;
            }

            GameLog.Warning()
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(masterVolume)))
                .Action(LogAction.ClampValue)
                .Write();
            masterVolume = Mathf.Clamp01(value);
        }
    }

    public float OstVolume
    {
        get => ostVolume;
        set
        {
            if (float.IsNaN(value))
            {
                ReportIgnoredNaN(nameof(ostVolume));
                return;
            }

            if (value is >= 0f and <= 1f)
            {
                ostVolume = value;
                return;
            }

            GameLog.Warning()
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(ostVolume)))
                .Action(LogAction.ClampValue)
                .Write();
            ostVolume = Mathf.Clamp01(value);
        }
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set
        {
            if (float.IsNaN(value))
            {
                ReportIgnoredNaN(nameof(sfxVolume));
                return;
            }

            if (value is >= 0f and <= 1f)
            {
                sfxVolume = value;
                return;
            }

            GameLog.Warning()
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(sfxVolume)))
                .Action(LogAction.ClampValue)
                .Write();
            sfxVolume = Mathf.Clamp01(value);
        }
    }

#endregion

    [SerializeField] private float masterVolume;
    [SerializeField] private float ostVolume;
    [SerializeField] private float sfxVolume;

    private static void ReportIgnoredNaN(string fieldName)
    {
        GameLog.Warning()
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Invalid(fieldName))
            .Action(LogAction.Ignore)
            .Write();
    }

    public static AudioSettings Default()
    {
        return new()
        {
            MasterVolume = 1f,
            OstVolume = .5f,
            SfxVolume = .5f
        };
    }
}
