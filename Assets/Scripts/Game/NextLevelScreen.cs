using UnityEngine;

/// <summary>
/// Shows the completion prompt after the scene environment declares victory.
/// 监听同场景环境的通关事件，显示祝贺文字并请求进入配置的下一关。
/// </summary>
[DisallowMultipleComponent]
public sealed class NextLevelScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMPro.TMP_Text congratulationsText;
    [SerializeField] private UnityEngine.UI.Button nextLevelButton;
    [SerializeField] private SceneId nextLevel = SceneId.GameplayIntegration;

    private EnvironmentFacade environment;
    private string congratulationsMessage;
    private bool started;
    private bool switching;

    private void Awake()
    {
        congratulationsMessage = congratulationsText != null ? congratulationsText.text : string.Empty;
        if (panel != null)
        {
            panel.SetActive(false);
        }
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(LoadNextLevel);
        }
    }

    private void Start()
    {
        started = true;
        BindEnvironment();
    }

    private void OnEnable()
    {
        if (started)
        {
            BindEnvironment();
        }
    }

    private void BindEnvironment()
    {
        environment = EnvironmentFacade.ForScene(gameObject.scene);
        if (environment != null)
        {
            environment.LevelCleared += Show;
            if (environment.IsCircuitClosed)
            {
                Show();
            }
        }
    }

    private void OnDisable()
    {
        if (environment != null)
        {
            environment.LevelCleared -= Show;
        }
        environment = null;
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveListener(LoadNextLevel);
        }
    }

    private void Update()
    {
        // The environment's debug restart can reopen the circuit without reloading the scene.
        // 环境调试重开可在不切场景的情况下清除通关状态。
        if (panel != null && panel.activeSelf && (environment == null || !environment.IsCircuitClosed))
        {
            panel.SetActive(false);
        }
    }

    private void Show()
    {
        if (panel == null || switching)
        {
            return;
        }
        if (congratulationsText != null)
        {
            congratulationsText.text = congratulationsMessage;
        }
        if (nextLevelButton != null)
        {
            nextLevelButton.interactable = true;
        }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    public void LoadNextLevel()
    {
        if (!isActiveAndEnabled || switching || panel == null || !panel.activeInHierarchy ||
            environment == null || !environment.IsCircuitClosed)
        {
            return;
        }

        CoreFacade core = CoreFacade.Instance;
        if (core == null || core.SceneSwitch == null || core.SceneSwitch.RequestSwitch(nextLevel) == null)
        {
            if (congratulationsText != null)
            {
                congratulationsText.text = "下一关加载失败，请重试。";
            }
            return;
        }

        switching = true;
        if (nextLevelButton != null)
        {
            nextLevelButton.interactable = false;
        }
    }
}
