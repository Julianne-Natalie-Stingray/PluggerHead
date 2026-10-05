using UnityEngine;

/// <summary>
/// 动画适配层：检测当前/过渡目标状态，仅在开始或结束时通知 PlayerMove。
/// 不处理输入或刚体运动。聚合多个动画层，避免一次退出提前解除另一层的锁。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PlayerAnimationCallbacks : MonoBehaviour
{
    private PlayerMove player;
    private Animator animator;
    private bool wasDashActive;
    private bool wasInteractionActive;

    private void Awake()
    {
        player = GetComponent<PlayerMove>();
        animator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        Synchronize();
    }

    private void FixedUpdate()
    {
        Synchronize();
    }

    private void LateUpdate()
    {
        Synchronize();
    }

    /// <summary>在物理输入处理前及动画更新后同步，覆盖过渡和播放被中断的情况。</summary>
    public void Synchronize()
    {
        if (player == null)
        {
            player = GetComponent<PlayerMove>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        bool dash = false;
        bool interact = false;
        if (isActiveAndEnabled && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.isInitialized)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (layer > 0 && animator.GetLayerWeight(layer) <= 0f)
                {
                    continue;
                }

                AccumulateActiveActions(animator.GetCurrentAnimatorStateInfo(layer), ref dash, ref interact);
                if (animator.IsInTransition(layer))
                {
                    AccumulateActiveActions(animator.GetNextAnimatorStateInfo(layer), ref dash, ref interact);
                }
            }
        }

        DispatchAnimationCallbacks(dash, interact);
    }

    private void AccumulateActiveActions(AnimatorStateInfo state, ref bool dash, ref bool interact)
    {
        dash |= state.IsTag("PlayerDash");
        interact |= state.IsTag("PlayerInteract");
    }

    private void DispatchAnimationCallbacks(bool dash, bool interact)
    {
        if (player != null)
        {
            // 先开始再结束，保证两种动作相互过渡时不会出现短暂解锁。
            if (dash && !wasDashActive)
            {
                player.OnDashStarted();
            }
            if (interact && !wasInteractionActive)
            {
                player.OnInteractionStarted();
            }
            if (!dash && wasDashActive)
            {
                player.OnDashEnded();
            }
            if (!interact && wasInteractionActive)
            {
                player.OnInteractionEnded();
            }
        }

        wasDashActive = dash;
        wasInteractionActive = interact;
    }

    private void OnDisable()
    {
        DispatchAnimationCallbacks(false, false);
    }
}
