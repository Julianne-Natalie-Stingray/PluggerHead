using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Main menu actions. Continuing always loads a fresh scene from its authored defaults.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private SceneSwitchConfigs configs;
    [SerializeField] private SceneId firstLevel = SceneId.GameplayIntegration;
    [SerializeField] private UnityEngine.UI.Button newGameButton;
    [SerializeField] private UnityEngine.UI.Button continueGameButton;
    [SerializeField] private UnityEngine.UI.Button settingsButton;
    [SerializeField] private UnityEngine.UI.Button exitButton;
    [SerializeField] private SettingsScreen settingsScreen;
    [SerializeField] private TMPro.TMP_Text status;

    private void Start()
    {
        RefreshButtons();
    }

    private void Update()
    {
        RefreshButtons();
    }

    private bool CanLoad(SceneId level)
    {
        return configs != null && configs.IsGameplayLevel(level) &&
            configs.TryGetSceneName(level, out string name) &&
            SceneUtility.GetBuildIndexByScenePath(name) >= 0;
    }

    private void RefreshButtons()
    {
        bool ready = CoreFacade.Instance != null && !CoreFacade.Instance.SceneSwitch.IsSwitching &&
            !settingsScreen.gameObject.activeSelf;
        newGameButton.interactable = ready && CanLoad(firstLevel);
        continueGameButton.interactable = ready && GameProgress.Store.TryGetLevel(out SceneId level) && CanLoad(level);
        settingsButton.interactable = ready;
        exitButton.interactable = ready;
    }

    public void NewGame()
    {
        LoadLevel(firstLevel);
    }

    public void ContinueGame()
    {
        if (GameProgress.Store.TryGetLevel(out SceneId level))
        {
            LoadLevel(level);
        }
    }

    private void LoadLevel(SceneId level)
    {
        if (!CanLoad(level) || CoreFacade.Instance == null || settingsScreen.gameObject.activeSelf)
        {
            return;
        }

        // The tracker commits progress after the scene has actually loaded.
        if (CoreFacade.Instance.SceneSwitch.RequestSwitch(level) == null)
        {
            status.text = "关卡加载失败，请重试。";
        }
        RefreshButtons();
    }

    public void OpenSettings()
    {
        settingsScreen.Open();
        RefreshButtons();
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
