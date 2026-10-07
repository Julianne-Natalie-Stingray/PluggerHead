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
    /// Reads the source's playing state while this handle is valid; false after invalidation.
    /// False alone does not prove the completion callback has already run.
    /// 句柄有效时读取音源播放状态, 失效后为 false; false 本身不证明完成回调已经执行.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// True when the emitter classified completion as natural, false when it was interrupted.
    /// Natural completion is detected from a stopped source, not from proof that the clip reached its end.
    /// Interruptions are an explicit Stop, a preemption by the instance limits, or the loop policy
    /// applied when the followed target was destroyed.
    /// emitter 将结束归类为自然完成时为真; 被打断时为假. 检测依据是音源停止, 不证明已播放到 clip 末尾.
    /// 打断包括显式 Stop, 被实例上限抢占, 以及被跟随目标销毁时应用的循环策略.
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// Raised once on invalidation. A graceful Stop invalidates before the audible fade has ended.
    /// Subscriber exceptions are logged independently and do not prevent later subscribers from running.
    /// External emitter destruction does not currently guarantee this notification.
    /// 句柄失效时触发一次; 优雅停止会在实际淡出结束前通知. 外部销毁 emitter 不保证触发此事件.
    /// 各订阅者异常分别记录, 不阻止后续订阅者执行.
    /// </summary>
    event System.Action<ISoundHandle> Finished;

    /// <summary>
    /// Stop this playback early. Idempotent: stopping an already ended handle does nothing and returns false.
    /// If the emitter was destroyed, invalidates the stale handle as interrupted and returns false.
    /// When this playback's effective FadeOut is positive, the sound ramps down and this handle is invalidated
    /// immediately either way: IsPlaying reports false as soon as this call returns.
    /// 提前停止本次播放. 幂等: 对已结束的句柄再次调用不产生副作用并返回 false.
    /// emitter 已销毁时将旧句柄按中断失效并返回 false，不再访问已销毁组件。
    /// 若本次播放的有效 FadeOut 为正, 声音会先降音再结束; 两种情况都立即使句柄失效, 返回后 IsPlaying 为 false.
    /// </summary>
    bool Stop();

    /// <summary>
    /// Set the live volume. An invalidated handle or NaN returns false without changing volume.
    /// Other out-of-range values, including infinities, are clamped.
    /// 设置实时音量. 句柄失效或传入 NaN 时返回 false 且保留原值; 其他越界值(包括无穷)做钳制.
    /// </summary>
    bool TrySetVolume(float volume);

    /// <summary>
    /// Set the live pitch. An invalidated handle or NaN returns false without changing pitch.
    /// Other out-of-range values, including infinities, are clamped.
    /// 设置实时音高. 句柄失效或传入 NaN 时返回 false 且保留原值; 其他越界值(包括无穷)做钳制.
    /// </summary>
    bool TrySetPitch(float pitch);
}
