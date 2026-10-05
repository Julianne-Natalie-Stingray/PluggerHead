/// <summary>
/// Contract for one playback of one audio clip.
/// Subsystem: Core (Audio).
/// Who should implement this contract: only SoundHandle. It is implemented once, inside this Subsystem,
/// and is never implemented by gameplay code.
/// What this contract grants: scoped control over a single playback, namely asking whether it is still
/// playing, changing its live volume and pitch, and stopping it early.
/// 单次音频播放的契约.
/// Subsystem 归属: Core (Audio).
/// 谁应该实现这个契约: 只有 SoundHandle. 本 Subsystem 内实现一次, 玩法代码永不实现它.
/// 契约赋予了什么特性: 对单次播放的作用域控制, 即可询问是否仍在播放,
/// 修改实时音量与音高, 以及提前停止.
/// </summary>
public interface ISoundHandle
{
    /// <summary>
    /// The clip identifier this playback was started for. Readable even after the handle is invalidated.
    /// 本次播放所针对的 clip 标识. 即使句柄已失效也可读.
    /// </summary>
    AudioId AudioId { get; }

    /// <summary>
    /// True while this playback is still running. Returns false once the handle is invalidated,
    /// and never reports the state of a later playback that reused the same pooled emitter.
    /// 本次播放仍在进行时为真. 句柄失效后返回 false, 且永不会误报复用同一池化 emitter 的后一次播放.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// True when the playback ended by reaching the end of the clip, false when it was interrupted.
    /// Interruptions are an explicit Stop, a preemption by the instance limits, or the loop policy
    /// applied when the followed target was destroyed.
    /// 播放自然到达 clip 末尾结束时为真; 被打断时为假.
    /// 打断包括显式 Stop, 被实例上限抢占, 以及被跟随目标销毁时应用的循环策略.
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Raised once, when this playback ends for any reason. The handle is already invalid when it fires.
    /// 本次播放因任何原因结束时触发一次. 触发时句柄已失效.
    /// </summary>
    event System.Action<ISoundHandle> Finished;

    /// <summary>
    /// Stop this playback early. Idempotent: stopping an already ended handle does nothing and returns false.
    /// 提前停止本次播放. 幂等: 对已结束的句柄再次调用不产生副作用并返回 false.
    /// </summary>
    bool Stop();

    /// <summary>
    /// Set the live volume of this playback. Fails on an invalidated handle and clamps out-of-range values.
    /// 设置本次播放的实时音量. 句柄失效时失败, 并对越界值做钳制.
    /// </summary>
    bool TrySetVolume(float volume);

    /// <summary>
    /// Set the live pitch of this playback. Fails on an invalidated handle and clamps out-of-range values.
    /// 设置本次播放的实时音高. 句柄失效时失败, 并对越界值做钳制.
    /// </summary>
    bool TrySetPitch(float pitch);
}
