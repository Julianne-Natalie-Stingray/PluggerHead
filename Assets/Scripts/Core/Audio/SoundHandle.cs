using System;

/// <summary>
/// The single implementation of ISoundHandle. It pins one playback to one pooled AudioEmitter.
/// Subsystem: Core (Audio).
/// Where it lives: nowhere. It is a plain object created per playback by AudioBuilder.Play, never
/// attached to a GameObject and never serialized.
/// Responsibility: answer whether a specific playback is still running, change its live volume and pitch,
/// stop it early, and report its end through Finished.
/// Does NOT own: pooling, the instance limits, the registry, or any decision about which sound to preempt.
/// Lifetime: created when AudioBuilder.Play reserves an emitter; invalidated exactly once, when that
/// playback ends for any reason. After invalidation it holds its emitter reference only for the history
/// of the call and refuses every further operation.
/// The invalidation rule is what keeps a reused pooled emitter from being reported as this playback: the
/// emitter raises onAudioFinished before it is released back to the pool, so a handle always goes invalid
/// before a different clip can occupy the same emitter.
/// Paradigms: none. It is a scope token, not a service and not a singleton.
/// ISoundHandle 的唯一实现. 它把一次播放钉在一个池化 AudioEmitter 上.
/// Subsystem 归属: Core (Audio).
/// 存在位置: 无. 它是普通对象, 由 AudioBuilder.Play 每次播放创建一个, 不贴在任何 GameObject 上, 也不被序列化.
/// 职能: 回答某一次播放是否仍在进行, 修改其实时音量与音高, 提前停止它, 并通过 Finished 报告结束.
/// 不负责: 池化, 实例上限, 注册表, 以及任何"该抢占哪个声音"的决策.
/// 生命周期: AudioBuilder.Play 预定到 emitter 时创建; 该次播放因任何原因结束时失效一次.
/// 失效后它仍持有 emitter 引用, 但拒绝一切后续操作.
/// 失效规则正是防止"被复用的池化 emitter 被误报为本次播放"的关键:
/// emitter 在归还池之前触发 onAudioFinished, 因此句柄总是在另一个 clip 占用同一 emitter 之前失效.
/// 使用范式: 无. 它是作用域凭证, 不是服务, 也不是单例.
/// </summary>
public sealed class SoundHandle : ISoundHandle
{
    public AudioId AudioId => audioId;
    public bool IsPlaying => isValid && emitter.IsPlaying;
    public bool IsFinished => isFinished;

    public event Action<ISoundHandle> Finished;

    private readonly AudioEmitter emitter;
    private readonly AudioId audioId;

    private bool isValid;
    private bool isFinished;

    /// <summary>
    /// Single entry point for attaching a handle to a playback that is about to start.
    /// Implementation approach: subscribes to the emitter's completion callback, which is the one signal
    /// every end path funnels through, so the handle cannot miss an interruption.
    /// 把句柄绑定到即将开始的一次播放的单一入口.
    /// 实现思路: 订阅 emitter 的完成回调 —— 所有结束路径都汇入它, 因此句柄不会漏掉任何打断.
    /// </summary>
    public SoundHandle(AudioEmitter emitter, AudioId audioId)
    {
        this.emitter = emitter;
        this.audioId = audioId;

        isValid = true;
        isFinished = false;

        this.emitter.onAudioFinished += OnPlaybackEnded;
    }

    public bool Stop()
    {
        if (!isValid)
            return false;

        emitter.Stop();
        Invalidate(false);
        return true;
    }

    public bool TrySetVolume(float volume)
    {
        if (!isValid)
            return false;

        emitter.Volume = volume;
        return true;
    }

    public bool TrySetPitch(float pitch)
    {
        if (!isValid)
            return false;

        emitter.Pitch = pitch;
        return true;
    }

    /// <summary>
    /// Single entry point for invalidating this handle.
    /// Implementation approach: unsubscribes first, so a second completion callback cannot re-enter,
    /// then latches the invalid state and raises Finished exactly once.
    /// 使本句柄失效的单一入口.
    /// 实现思路: 先退订以阻断第二次完成回调, 再锁定失效状态, 并只触发一次 Finished.
    /// </summary>
    private void Invalidate(bool finishedNaturally)
    {
        if (!isValid)
            return;

        isValid = false;
        isFinished = finishedNaturally;

        emitter.onAudioFinished -= OnPlaybackEnded;

        Finished?.Invoke(this);
    }

    private void OnPlaybackEnded(AudioEmitter source)
        => Invalidate(source.IsFinished);
}
