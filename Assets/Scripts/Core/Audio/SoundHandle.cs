using System;

/// <summary>
/// The single implementation of ISoundHandle. It pins one playback to one pooled AudioEmitter.
/// Subsystem: Core (Audio).
/// Where it lives: nowhere. It is a plain object created per playback by AudioManager.Play, never
/// attached to a GameObject and never serialized.
/// Responsibility: answer whether a specific playback is still running, change its live volume and pitch,
/// stop it early, and report its end through Finished.
/// Does NOT own: pooling, the instance limits, the registry, or any decision about which sound to preempt.
/// Lifetime: invalidated by Stop or an emitter completion callback; external destruction is not notified.
/// In the manager's completion chain, pool release runs before this handle's callback. ResetEmitter retains
/// the completion reason so this handle can latch it. After invalidation, control operations are refused.
/// Graceful Stop invalidates immediately while the emitter may still be fading.
/// Paradigms: none. It is a scope token, not a service and not a singleton.
/// ISoundHandle 的唯一实现. 它把一次播放钉在一个池化 AudioEmitter 上.
/// Subsystem 归属: Core (Audio).
/// 存在位置: 无. 它是普通对象, 由 AudioManager.Play 每次播放创建一个, 不贴在 GameObject 上, 也不被序列化.
/// 职能: 回答某一次播放是否仍在进行, 修改其实时音量与音高, 提前停止它, 并通过 Finished 报告结束.
/// 不负责: 池化, 实例上限, 注册表, 以及任何"该抢占哪个声音"的决策.
/// 生命周期: Stop 或 emitter 完成回调使其失效; 外部销毁不会通知. 管理器的完成链先归还池,
/// 再通知句柄; ResetEmitter 保留结束原因供句柄锁存. 失效后拒绝控制操作, 但 emitter 可能仍在淡出.
/// 使用范式: 无. 它是作用域凭证, 不是服务, 也不是单例.
/// </summary>
public sealed class SoundHandle : ISoundHandle
{
    public AudioId AudioId => audioId;
    public bool IsPlaying => isValid && emitter && emitter.IsPlaying;
    public bool IsFinished => isFinished;

    public event Action<ISoundHandle> Finished;

    private readonly AudioEmitter emitter;
    private readonly AudioId audioId;

    private bool isValid;
    private bool isFinished;

    /// <summary>
    /// Single entry point for attaching a handle to a playback that is about to start.
    /// Implementation approach: subscribes to the emitter's completion callback, which is the one signal
    /// used by normal completion and stop paths. External destruction does not raise it.
    /// 把句柄绑定到即将开始的一次播放的单一入口.
    /// 实现思路: 订阅正常完成和停止路径使用的 emitter 回调; 外部销毁不触发它.
    /// </summary>
    public SoundHandle(AudioEmitter emitter, AudioId audioId)
    {
        this.emitter = emitter;
        this.audioId = audioId;

        isValid = true;
        isFinished = false;

        this.emitter.onAudioFinished += OnPlaybackEnded;
    }

    /// <summary>
    /// Single entry point for ending this playback early.
    /// Implementation approach: asks the emitter for a graceful stop, so a playback with positive FadeOut
    /// ramps down before it ends, and invalidates this handle immediately either way. Invalidation is not
    /// delayed to the end of the ramp: the caller has asked for the sound to be over, so IsPlaying must not
    /// keep reporting true for the length of the fade.
    /// 提前结束本次播放的单一入口.
    /// 实现思路: 向 emitter 请求优雅停止, 有效 FadeOut 为正时先渐变再结束; 两种情况都立即让句柄失效.
    /// 失效不推迟到渐变结束: 调用方已经要求该声音结束, 因此 IsPlaying 不应在整个淡出期间继续报真.
    /// </summary>
    public bool Stop()
    {
        if (!isValid)
        {
            return false;
        }

        // Unity can destroy Core's pooled voices before a scene owner releases its handle.
        // Core 的声部可能先于场景宿主销毁；清理旧句柄时不能再访问其原生组件。
        if (!emitter)
        {
            Invalidate(false);
            return false;
        }

        emitter.RequestStop();
        Invalidate(false);
        return true;
    }

    public bool TrySetVolume(float volume)
    {
        if (!isValid || !emitter || float.IsNaN(volume))
        {
            return false;
        }

        emitter.Volume = volume;
        return true;
    }

    public bool TrySetPitch(float pitch)
    {
        if (!isValid || !emitter || float.IsNaN(pitch))
        {
            return false;
        }

        emitter.Pitch = pitch;
        return true;
    }

    /// <summary>
    /// Single entry point for invalidating this handle.
    /// Implementation approach: latches invalid state and reason, unsubscribes, then raises Finished once.
    /// Finished subscribers are individually exception-isolated so each receives the notification.
    /// 使本句柄失效的单一入口.
    /// 实现思路: 先锁定失效状态与原因, 再退订并触发一次 Finished; 逐一隔离订阅者异常, 保证后续通知.
    /// </summary>
    private void Invalidate(bool finishedNaturally)
    {
        if (!isValid)
        {
            return;
        }

        isValid = false;
        isFinished = finishedNaturally;

        emitter.onAudioFinished -= OnPlaybackEnded;

        Delegate[] callbacks = Finished?.GetInvocationList();
        if (callbacks == null)
        {
            return;
        }

        foreach (Delegate callback in callbacks)
        {
            try
            {
                ((Action<ISoundHandle>)callback)(this);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }

    private void OnPlaybackEnded(AudioEmitter source)
        => Invalidate(source.IsFinished);
}
