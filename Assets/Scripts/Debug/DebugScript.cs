using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Paste on a GO. 
/// </summary>
public class DebugScript : MonoBehaviour
{
    private void Start()
    {
        Test();
    }

    private void Test()
    {
        Debug.Log($"###MasterVolume: {SettingBootstrap.Settings.Audio.MasterVolume}\n" +
                  $"OstVolume: {SettingBootstrap.Settings.Audio.OstVolume}\n" +
                  $"SfxVolume: {SettingBootstrap.Settings.Audio.SfxVolume}");

        CoreFacade core = CoreFacade.Instance;
    }

    [Button("Test Write in Settings")]
    private void TestWriteInSettings() // passed
    {
        var random = Random.value;
        
        SettingBootstrap.Settings.Audio.MasterVolume = random;
        SettingBootstrap.Settings.Audio.OstVolume = random;
        SettingBootstrap.Settings.Audio.SfxVolume = random;
        
        Debug.Log($"###MasterVolume: {SettingBootstrap.Settings.Audio.MasterVolume}\n" +
                  $"OstVolume: {SettingBootstrap.Settings.Audio.OstVolume}\n" +
                  $"SfxVolume: {SettingBootstrap.Settings.Audio.SfxVolume}");
    }

    [Button("Test Resetting Settings")]
    private void TestResettingSettings() // passed
    {
        SettingBootstrap.ResetToDefault();
        Debug.Log($"###MasterVolume: {SettingBootstrap.Settings.Audio.MasterVolume}\n" +
                  $"OstVolume: {SettingBootstrap.Settings.Audio.OstVolume}\n" +
                  $"SfxVolume: {SettingBootstrap.Settings.Audio.SfxVolume}");
    }
}
