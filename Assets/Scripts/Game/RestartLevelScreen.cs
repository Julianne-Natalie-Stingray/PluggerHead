using UnityEngine;

/// <summary>Shows a restart prompt when the player dies and reloads the authored level.</summary>
[DisallowMultipleComponent]
public sealed class RestartLevelScreen : MonoBehaviour
{
    [SerializeField] private PlayerMove player;
    [SerializeField] private GameObject panel;
    [SerializeField] private UnityEngine.UI.Button restartButton;
    [SerializeField] private TMPro.TMP_Text deathMessage;

    private PlayerMove subscribedPlayer;
    private float nextPlayerSearchTime;

    private void Awake()
    {
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (panel != null)
        {
            panel.SetActive(false);
            FreezeWhileVisible.Attach(panel);
        }
    }

    private void OnEnable()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (subscribedPlayer == null && Time.unscaledTime >= nextPlayerSearchTime)
        {
            FindPlayer();
        }
    }

    /// <summary>Retains an assigned player, or finds one in this UI's scene, including inactive players.</summary>
    [ContextMenu("Find Player")]
    public void FindPlayer()
    {
        nextPlayerSearchTime = Time.unscaledTime + 0.5f;
        if (player == null && gameObject.scene.IsValid())
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                player = root.GetComponentInChildren<PlayerMove>(true);
                if (player != null)
                {
                    break;
                }
            }
        }

        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        if (subscribedPlayer != player)
        {
            UnsubscribePlayer();
            subscribedPlayer = player;
            if (subscribedPlayer != null)
            {
                subscribedPlayer.Died += Show;
            }
        }

        if (subscribedPlayer != null && subscribedPlayer.IsDead)
        {
            Show();
        }
    }

    private void OnDisable()
    {
        UnsubscribePlayer();
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void UnsubscribePlayer()
    {
        if (subscribedPlayer != null)
        {
            subscribedPlayer.Died -= Show;
        }
        subscribedPlayer = null;
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
        if (!isActiveAndEnabled || panel == null || !panel.activeInHierarchy ||
            GameStateManager.Current == GameState.Loading)
        {
            return;
        }

        if (!TryRestartLevel())
        {
            if (deathMessage != null)
            {
                deathMessage.text = "重新开始失败，请重试。";
            }
            return;
        }

        if (restartButton != null)
        {
            restartButton.interactable = false;
        }
    }

    /// <summary>Shared by the death prompt and the in-level menu; reloads this UI's own scene.</summary>
    public bool TryRestartLevel()
    {
        if (!isActiveAndEnabled || CoreFacade.Instance == null || CoreFacade.Instance.SceneSwitch == null)
        {
            return false;
        }

        try
        {
            return CoreFacade.Instance.SceneSwitch.RequestReload(gameObject.scene) != null;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }
}
