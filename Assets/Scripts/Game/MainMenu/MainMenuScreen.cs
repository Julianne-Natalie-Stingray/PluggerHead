using UnityEngine;

/// <summary>主菜单：通过场景切换服务进入独立大厅，提供设置和退出入口。</summary>
[DisallowMultipleComponent]
public sealed class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button newGameButton;
    [SerializeField] private UnityEngine.UI.Button settingsButton;
    [SerializeField] private UnityEngine.UI.Button exitButton;
    [SerializeField] private SettingsScreen settingsScreen;
    [SerializeField] private TMPro.TMP_Text startStatus;

    private void OnEnable()
    {
        if (startStatus != null)
        {
            startStatus.text = string.Empty;
        }
    }

    private void Update()
    {
        bool ready = CanStart;
        newGameButton.interactable = ready;
        settingsButton.interactable = ready;
        exitButton.interactable = ready;
    }

    private bool CanStart => CoreFacade.Instance != null && CoreFacade.Instance.SceneSwitch != null &&
        !CoreFacade.Instance.SceneSwitch.IsSwitching && GameStateManager.Current == GameState.Playing &&
        settingsScreen != null && !settingsScreen.gameObject.activeSelf;

    public void NewGame()
    {
        if (!isActiveAndEnabled || !CanStart)
        {
            return;
        }
        if (CoreFacade.Instance.SceneSwitch.RequestSwitch(SceneId.HubScene) == null && startStatus != null)
        {
            startStatus.text = "关卡加载失败，请重试。";
        }
    }

    public void OpenSettings()
    {
        settingsScreen.Open();
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
