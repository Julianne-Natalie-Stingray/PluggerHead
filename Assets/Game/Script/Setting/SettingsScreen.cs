using UnityEngine;

/// <summary>
/// 暂停设置界面。读取 Core 定义的音频设置，显式保存，成功后立即应用实际音量。
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
    [SerializeField] private UnityEngine.UI.Button restartButton;
    [SerializeField] private RestartLevelScreen restartScreen;

    private void OnEnable()
    {
        GameStateManager.RequestFreeze(this);
    }

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
        exitButton.interactable = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenuScene" &&
            CoreFacade.Instance != null;
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(restartScreen != null &&
                restartScreen.gameObject.scene == gameObject.scene && gameObject.scene.name != "MainMenuScene");
            restartButton.interactable = true;
        }
        gameObject.SetActive(true);
    }

    /// <summary>Restarts the current level through the same controller used by the death prompt.</summary>
    public void RestartLevel()
    {
        if (!isActiveAndEnabled || GameStateManager.Current == GameState.Loading)
        {
            return;
        }

        if (restartScreen == null || restartScreen.gameObject.scene != gameObject.scene ||
            gameObject.scene.name == "MainMenuScene" || !restartScreen.TryRestartLevel())
        {
            if (saveStatus != null)
            {
                saveStatus.text = "重新开始失败，请重试。";
            }
            return;
        }

        if (restartButton != null)
        {
            restartButton.interactable = false;
        }
        // Release only this panel's pause; other visible prompts retain their own pause until unload.
        gameObject.SetActive(false);
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

    public void ReturnToMainMenu()
    {
        if (CoreFacade.Instance == null || CoreFacade.Instance.SceneSwitch.IsSwitching)
        {
            return;
        }

        // Release this panel's pause before the switch captures the previous game state.
        gameObject.SetActive(false);
        if (CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.MainMenuScene) == null)
        {
            gameObject.SetActive(true);
            saveStatus.text = "返回主菜单失败，请重试。";
        }
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
            if (CoreFacade.Instance != null && CoreFacade.Instance.Audio != null)
            {
                CoreFacade.Instance.Audio.ApplyAudioSettings();
            }

            saveStatus.text = "设置已保存";
            return;
        }

        audio.MasterVolume = previousMaster;
        audio.OstVolume = previousOst;
        audio.SfxVolume = previousSfx;
        saveStatus.text = "保存失败，请重试。";
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
        GameStateManager.ReleaseFreeze(this);
    }
}
