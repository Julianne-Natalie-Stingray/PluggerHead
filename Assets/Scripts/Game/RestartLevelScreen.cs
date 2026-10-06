using UnityEngine;

/// <summary>Shows a restart prompt when the player dies and reloads the authored level.</summary>
[DisallowMultipleComponent]
public sealed class RestartLevelScreen : MonoBehaviour
{
    [SerializeField] private PlayerMove player;
    [SerializeField] private GameObject panel;
    [SerializeField] private UnityEngine.UI.Button restartButton;
    [SerializeField] private TMPro.TMP_Text deathMessage;
    [SerializeField] private SceneId level = SceneId.GameplayIntegration;

    private void Awake()
    {
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.Died += Show;
            if (player.IsDead)
            {
                Show();
            }
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.Died -= Show;
        }
    }

    private void OnDestroy()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartLevel);
        }
    }

    private void Show()
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(true);
        if (restartButton != null)
        {
            restartButton.interactable = true;
        }
    }

    public void RestartLevel()
    {
        if (panel == null || !panel.activeInHierarchy || CoreFacade.Instance == null)
        {
            return;
        }

        if (CoreFacade.Instance.SceneSwitch.RequestSwitch(level) == null)
        {
            if (deathMessage != null)
            {
                deathMessage.text = "Restart failed. Please try again.";
            }
            return;
        }

        if (restartButton != null)
        {
            restartButton.interactable = false;
        }
    }
}
