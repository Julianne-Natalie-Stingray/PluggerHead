using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Attached to the persistent Core object; records successfully loaded gameplay scenes.
/// 主菜单、诊断场景及切换失败都不会覆盖进度。</summary>
[DisallowMultipleComponent]
public sealed class LevelProgressTracker : MonoBehaviour
{
    [SerializeField] private SceneSwitchConfigs configs;

    private void Start()
    {
        // Start runs after Core's duplicate guard has removed extra instances.
        SceneManager.sceneLoaded += OnSceneLoaded;
        RecordLevel(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RecordLevel(scene);
    }

    private void RecordLevel(Scene scene)
    {
        if (configs != null && configs.TryGetGameplayLevel(scene.name, out SceneId level))
        {
            GameProgress.Store.SaveLevel(level);
        }
    }
}
