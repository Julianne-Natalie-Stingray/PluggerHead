using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Manual settings diagnostic: logs current volumes in Start and exposes Inspector buttons.
/// Use after SettingBootstrap initialization in Play Mode. Buttons change memory without applying audio
/// or saving immediately; the quitting hook may later persist those changes.
/// 手工设置诊断: Start 打印音量, Inspector 按钮修改内存.
/// 在 Play Mode 且 SettingBootstrap 初始化后使用; 不即时应用音量或保存, 退出钩子可能随后写盘.
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

    [Button("Test Write in Settings", EButtonEnableMode.Playmode)]
    private void TestWriteInSettings()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        var random = Random.value;
        
        SettingBootstrap.Settings.Audio.MasterVolume = random;
        SettingBootstrap.Settings.Audio.OstVolume = random;
        SettingBootstrap.Settings.Audio.SfxVolume = random;
        
        Debug.Log($"###MasterVolume: {SettingBootstrap.Settings.Audio.MasterVolume}\n" +
                  $"OstVolume: {SettingBootstrap.Settings.Audio.OstVolume}\n" +
                  $"SfxVolume: {SettingBootstrap.Settings.Audio.SfxVolume}");
    }

    [Button("Test Resetting Settings", EButtonEnableMode.Playmode)]
    private void TestResettingSettings()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SettingBootstrap.ResetToDefault();
        Debug.Log($"###MasterVolume: {SettingBootstrap.Settings.Audio.MasterVolume}\n" +
                  $"OstVolume: {SettingBootstrap.Settings.Audio.OstVolume}\n" +
                  $"SfxVolume: {SettingBootstrap.Settings.Audio.SfxVolume}");
    }
}
