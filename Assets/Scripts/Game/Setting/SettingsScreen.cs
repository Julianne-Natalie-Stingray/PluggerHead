using UnityEngine;

/// <summary>
/// 暂停设置界面。读取 Core 定义的音频设置，显式保存，不应用实际音量。
/// MenuButton 调用 Open，Continue 调用 ContinueGame。
/// </summary>
[DisallowMultipleComponent]
public sealed class SettingsScreen : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Slider masterVolume;
    [SerializeField] private UnityEngine.UI.Slider ostVolume;
    [SerializeField] private UnityEngine.UI.Slider sfxVolume;
    [SerializeField] private UnityEngine.UI.Button exitButton;
    [SerializeField] private TMPro.TMP_Text saveStatus;

    private bool ownsPause;

    public void Open()
    {
        if (gameObject.activeSelf || GameStateManager.Current == GameState.Loading)
        {
            return;
        }

        if (!HasRequiredReferences())
        {
            return;
        }

        LoadVolumeValues();
        saveStatus.text = string.Empty;
        // MainMenu 场景尚未实现，暂不提供退出操作。
        exitButton.interactable = false;
        ownsPause = GameStateManager.Current == GameState.Playing;
        if (ownsPause)
        {
            GameStateManager.Freeze();
        }

        gameObject.SetActive(true);
    }

    private void LoadVolumeValues()
    {
        AudioSettings audio = SettingBootstrap.Settings.Audio;
        masterVolume.SetValueWithoutNotify(audio.MasterVolume);
        ostVolume.SetValueWithoutNotify(audio.OstVolume);
        sfxVolume.SetValueWithoutNotify(audio.SfxVolume);
    }

    public void ContinueGame()
    {
        gameObject.SetActive(false);
    }

    public void SaveSettings()
    {
        if (!gameObject.activeInHierarchy || !HasRequiredReferences())
        {
            return;
        }

        AudioSettings audio = SettingBootstrap.Settings.Audio;
        float previousMaster = audio.MasterVolume;
        float previousOst = audio.OstVolume;
        float previousSfx = audio.SfxVolume;
        audio.MasterVolume = masterVolume.value;
        audio.OstVolume = ostVolume.value;
        audio.SfxVolume = sfxVolume.value;
        if (SettingBootstrap.Save())
        {
            saveStatus.text = "Settings saved";
            return;
        }

        audio.MasterVolume = previousMaster;
        audio.OstVolume = previousOst;
        audio.SfxVolume = previousSfx;
        saveStatus.text = "Save failed. Please try again.";
    }

    public void OnVolumeChanged(float value)
    {
        if (saveStatus != null)
        {
            saveStatus.text = string.Empty;
        }
    }

    private bool HasRequiredReferences()
    {
        if (masterVolume != null && ostVolume != null && sfxVolume != null &&
            exitButton != null && saveStatus != null)
        {
            return true;
        }

        Debug.LogError("SettingsScreen 需要音量滑块、退出按钮和保存状态文本引用。", this);
        return false;
    }

    private void OnDisable()
    {
        if (ownsPause && GameStateManager.Current == GameState.Freezed)
        {
            GameStateManager.Resume();
        }

        ownsPause = false;
    }
}
