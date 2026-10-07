using UnityEngine;

/// <summary>
/// Receives animation events on the Animator object and requests audio through Core.
/// 挂在 Animator 同物体上，通过动画事件播放音效；不改变移动或交互状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimationAudio : MonoBehaviour
{
    [SerializeField] private AudioId deathAudio = AudioId.None;
    [SerializeField] private AudioId moveAudio = AudioId.None;
    [SerializeField] private AudioId interactAudio = AudioId.None;

    private ISoundHandle deathHandle;
    private ISoundHandle moveHandle;
    private ISoundHandle interactHandle;

    /// <summary>死亡动画事件：允许死亡面板冻结游戏后继续发声。</summary>
    public void PlayDeathAudio()
    {
        Play(deathAudio, true, ref deathHandle);
    }

    /// <summary>移动动画事件：可在每个落脚帧调用。</summary>
    public void PlayMoveAudio()
    {
        Play(moveAudio, false, ref moveHandle);
    }

    /// <summary>交互动画事件：在动作需要发声的帧调用。</summary>
    public void PlayInteractAudio()
    {
        Play(interactAudio, false, ref interactHandle);
    }

    private void Play(AudioId id, bool surviveFreeze, ref ISoundHandle handle)
    {
        if (!isActiveAndEnabled || id == AudioId.None)
        {
            return;
        }

        PlayerMove player = GetComponentInParent<PlayerMove>();
        if (!surviveFreeze && (Time.timeScale <= 0f || (player != null && player.IsDead)))
        {
            return;
        }

        CoreFacade core = CoreFacade.Instance;
        if (core == null || core.Audio == null)
        {
            return;
        }

        handle?.Stop();
        handle = core.Audio.CreateBuilder()
            .WithPosition(transform.position)
            .WithFollowTarget(transform)
            .WithSurviveFreeze(surviveFreeze)
            .WithAllowWhileFrozen(surviveFreeze)
            .Play(id);
    }

    private void OnDisable()
    {
        deathHandle?.Stop();
        moveHandle?.Stop();
        interactHandle?.Stop();
        deathHandle = null;
        moveHandle = null;
        interactHandle = null;
    }
}
