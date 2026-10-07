using UnityEngine;

/// <summary>大厅开始界面：显示时锁定角色，开始后留在大厅选择关卡。</summary>
[DisallowMultipleComponent]
public sealed class MainMenuScreen : MonoBehaviour
{
    [SerializeField] private PlayerMove player;
    [SerializeField] private UnityEngine.UI.Button newGameButton;
    [SerializeField] private UnityEngine.UI.Button settingsButton;
    [SerializeField] private UnityEngine.UI.Button exitButton;
    [SerializeField] private SettingsScreen settingsScreen;

    private void OnEnable()
    {
        if (player != null)
        {
            player.LockInput();
        }
    }

    private void Update()
    {
        bool ready = CanStart;
        newGameButton.interactable = ready;
        settingsButton.interactable = ready;
        exitButton.interactable = ready;
    }

    private bool CanStart => player != null && CoreFacade.Instance != null &&
        !CoreFacade.Instance.SceneSwitch.IsSwitching && !settingsScreen.gameObject.activeSelf;

    public void NewGame()
    {
        if (!isActiveAndEnabled || !CanStart)
        {
            return;
        }
        player.UnlockInput();
        gameObject.SetActive(false);
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
