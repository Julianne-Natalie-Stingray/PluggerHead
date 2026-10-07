using UnityEngine;

/// <summary>挂在实际关卡的环境上，仅在通关后更新本次运行的解锁进度。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnvironmentFacade))]
public sealed class LevelProgressTracker : MonoBehaviour
{
    [SerializeField, Range(1, LevelProgressStore.LevelCount)] private int levelNumber = 1;
    private EnvironmentFacade environment;

    private void OnEnable()
    {
        environment = GetComponent<EnvironmentFacade>();
        environment.LevelCleared += RecordCompletion;
        if (environment.IsCircuitClosed)
        {
            RecordCompletion();
        }
    }

    private void OnDisable()
    {
        if (environment != null)
        {
            environment.LevelCleared -= RecordCompletion;
        }
    }

    private void RecordCompletion()
    {
        GameProgress.Store.CompleteLevel(levelNumber);
    }
}
