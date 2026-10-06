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

        bool interact = false;
        if (CanReadAnimator())
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (layer > 0 && animator.GetLayerWeight(layer) <= 0f)
                {
                    continue;
                }

                interact |= animator.GetCurrentAnimatorStateInfo(layer).IsTag("PlayerInteract");
                if (animator.IsInTransition(layer))
                {
                    interact |= animator.GetNextAnimatorStateInfo(layer).IsTag("PlayerInteract");
                }
            }
        }

        DispatchAnimationCallbacks(interact);
    }

    private bool CanReadAnimator()
    {
        return isActiveAndEnabled && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.isInitialized;
    }

    private void DispatchAnimationCallbacks(bool interact)
    {
        if (player != null)
        {
            if (interact && !wasInteractionActive)
            {
                player.OnInteractionStarted();
            }
            if (!interact && wasInteractionActive)
            {
                player.OnInteractionEnded();
            }
        }

        wasInteractionActive = interact;
    }

    private void OnDisable()
    {
        DispatchAnimationCallbacks(false);
    }
}
