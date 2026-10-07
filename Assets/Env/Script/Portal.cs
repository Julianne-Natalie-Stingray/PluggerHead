using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>大厅选关入口。未解锁时只播放玩家反馈动画，不切换场景。</summary>
[DisallowMultipleComponent]
public sealed class Portal : MonoBehaviour, IEnvironmentInteractable
{
    [SerializeField, Range(1, LevelProgressStore.LevelCount)] private int levelNumber = 1;
    [SerializeField] private bool hasDestination;
    [SerializeField] private SceneId destination = SceneId.Level0;
    [SerializeField] private SceneSwitchConfigs configs;
    [SerializeField] private TMP_Text label;
    private string failure;

    public int LevelNumber => levelNumber;
    public bool HasDestination => hasDestination;
    public bool CanShowLockedFeedback => isActiveAndEnabled && !GameProgress.Store.IsUnlocked(levelNumber) &&
        GameStateManager.Current == GameState.Playing && Time.timeScale > 0f;
    public bool CanInteract => isActiveAndEnabled && hasDestination && GameProgress.Store.IsUnlocked(levelNumber) &&
        GameStateManager.Current == GameState.Playing && CoreFacade.Instance != null &&
        CoreFacade.Instance.SceneSwitch.isActiveAndEnabled && !CoreFacade.Instance.SceneSwitch.IsSwitching &&
        configs != null && configs.IsGameplayLevel(destination) &&
        configs.TryGetSceneName(destination, out string sceneName) && SceneUtility.GetBuildIndexByScenePath(sceneName) >= 0;

    public event Action<IEnvironmentInteractable> OnInteracted;

    private void Update()
    {
        if (label == null)
        {
            return;
        }
        string title = levelNumber == 1 ? "第一关" : levelNumber == 2 ? "第二关" : "第三关";
        string state = !hasDestination ? "尚未开放" : !GameProgress.Store.IsUnlocked(levelNumber) ? "请先通过上一关" : "按 J 进入";
        label.text = title + "\n" + (failure ?? state);
    }

    public void Interact(InteractionDetails details)
    {
        PlayerMove actor = details?.Actor != null ? details.Actor.GetComponent<PlayerMove>() : null;
        if (actor == null || actor.gameObject.scene != gameObject.scene || actor.IsInputLocked || actor.IsDead)
        {
            return;
        }
        if (CanShowLockedFeedback)
        {
            actor.TryStartPortalLockedAnimation();
            return;
        }
        if (!CanInteract)
        {
            return;
        }
        if (CoreFacade.Instance.SceneSwitch.RequestSwitch(destination) == null)
        {
            failure = "加载失败，请重试";
            return;
        }
        failure = null;
        OnInteracted?.Invoke(this);
    }
}
