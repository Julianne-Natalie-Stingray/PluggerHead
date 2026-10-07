using UnityEngine;

/// <summary>
/// Fluent value-type configuration for a playback request; consumption is local to each struct copy.
/// Subsystem: Core (Audio).
/// Where it lives: nowhere. It is a value created on demand by AudioManager.CreateBuilder and discarded
/// after Play.
/// Responsibility: carry the live-effect parameters of one request -- volume, pitch, an optional random
/// pitch jitter, and the Transform the sound should follow -- and hand that request to AudioManager.
/// Does NOT own: the clip, the mixer group, or whether the clip loops. Those are static characteristics of
/// AudioClipData, which is their single source of truth; a request that needs the same clip to loop in one
/// place and not in another is expressed as two AudioClipData assets, not as a builder switch.
/// It also does not own the instance limits or preemption, which are AudioManager's policy.
/// Lifetime: create a fresh builder for each request. With methods mutate their receiver and return a copy.
/// Play with an existing manager consumes only its receiver, even if the manager refuses the request.
/// Earlier copies remain usable; chained Play can consume a temporary instead of the original variable.
/// Paradigms: Fluent API over a value type.
/// 单次播放请求的链式配置凭证.
/// Subsystem 归属: Core (Audio).
/// 存在位置: 无. 它是按需创建的值, 由 AudioManager.CreateBuilder 产生, Play 之后即丢弃.
/// 职能: 承载一次请求的实时效果参数 —— 音量, 音高, 可选的随机音高抖动, 以及该声音应跟随的 Transform ——
/// 并把该请求交给 AudioManager.
/// 不负责: clip, mixerGroup, 以及该 clip 是否循环. 这些是 AudioClipData 的静态特征, 以它为唯一真相来源;
/// 若同一 clip 需要在某处循环而另一处不循环, 正确做法是建两个 AudioClipData 资产, 而不是给 Builder 加开关.
/// 也不负责实例上限与抢占, 那属于 AudioManager 的策略.
/// 生命周期: 每次请求新建 Builder. With 修改接收者并返回副本; 有有效 manager 时 Play 消费当前副本,
/// 即使请求被管理器拒绝也如此. 先前的副本仍可使用, 链式 Play 可能只消费临时值而非原变量.
/// 使用范式: 值类型上的 Fluent API.
/// </summary>
public struct AudioBuilder
{
    private AudioManager manager;
    private float? volume;
    private float? pitch;
    private Transform followTarget;
    private Vector3? position;
    private bool? surviveFreeze;
    private bool? allowWhileFrozen;
    private float? fadeIn;
    private float? fadeOut;
    private bool isUsed;

    internal AudioBuilder(AudioManager manager)
    {
        this.manager = manager;

        volume = null;
        pitch = null;
        followTarget = null;
        position = null;
        surviveFreeze = null;
        allowWhileFrozen = null;
        fadeIn = null;
        isUsed = false;
        fadeOut = null;
    }

    /// <summary>
    /// Set the live volume of this request. Clamped when it is applied, not here, so that intermediate
    /// values in a chain are never truncated.
    /// 设置本次请求的实时音量. 钳制发生在最终应用时, 不在此处, 因此链式调用中的中间值不会被截断.
    /// </summary>
    public AudioBuilder WithVolume(float volume)
    {
        if (isUsed)
        {
            return this;
        }

        this.volume = volume;
        return this;
    }

    /// <summary>
    /// Set the live pitch of this request. Clamped when it is applied, not here.
    /// 设置本次请求的实时音高. 钳制发生在最终应用时, 不在此处.
    /// </summary>
    public AudioBuilder WithPitch(float pitch)
    {
        if (isUsed)
        {
            return this;
        }

        this.pitch = pitch;
        return this;
    }

    /// <summary>
    /// Add a small random offset to the pitch of this request, for clips that would otherwise sound
    /// mechanical when repeated.
    /// Samples immediately, adding to the current override or 1 (not the clip's default pitch).
    /// 为本次请求的音高加上小幅随机偏移, 用于避免重复播放时听感机械.
    /// 调用时立即抽样, 加到当前覆盖值或 1 上, 并非加到 clip 的默认音高上.
    /// </summary>
    public AudioBuilder WithRandomPitch(float min = -0.05f, float max = 0.05f)
    {
        if (isUsed)
        {
            return this;
        }

        pitch = (pitch ?? 1f) + Random.Range(min, max);
        return this;
    }

    /// <summary>
    /// Place this request at an explicit world position.
    /// Implementation approach: records the point and leaves following untouched. Position and follow are
    /// exclusive in practice -- a follow target drives the position every frame, so passing both lets the
    /// target win -- and a request that passes neither keeps the emitter at its current position.
    /// 把本次请求放在指定的世界坐标.
    /// 实现思路: 记录该点, 不触碰跟随状态. 位置与跟随在实践中互斥 —— 跟随目标每帧驱动位置, 因此两者都传时跟随胜出 ——
    /// 两者都不传时 emitter 使用当前坐标; 正常归还路径会把它复位到 AudioRoot.
    /// </summary>
    public AudioBuilder WithPosition(Vector3 position)
    {
        if (isUsed)
        {
            return this;
        }

        this.position = position;
        return this;
    }

    /// <summary>
    /// Make this sound follow a Transform. The emitter syncs to it from LateUpdate and is never parented to
    /// it, so the sound survives the target being destroyed: a one-shot plays out where the target was last
    /// seen, while a loop stops, because a loop has no natural end and would hold a pooled emitter forever.
    /// 让该声音跟随某个 Transform. emitter 在 LateUpdate 中同步, 且永不做它的子物体,
    /// 因此声音能在目标销毁后存续: 一次性音在目标最后出现的位置播完, 循环音则停止 ——
    /// 因为循环没有自然结尾, 会永久占住一个池化 emitter.
    /// </summary>
    public AudioBuilder WithFollowTarget(Transform target)
    {
        if (isUsed)
        {
            return this;
        }

        followTarget = target;
        return this;
    }

    /// <summary>
    /// Override, for this request only, whether the playback is kept while the game is frozen.
    /// Implementation approach: records a nullable override. Absent means the clip's static initial is used,
    /// which is the same static/dynamic split the rest of this builder follows. Being kept has nothing to do
    /// with whether the clip loops; those are independent, and this method is the dynamic half of the first.
    /// 仅对本次请求覆盖"游戏冻结时是否保留该播放".
    /// 实现思路: 记录一个可空覆盖; 不设置即使用 clip 的静态初值 —— 与本 Builder 其余部分遵循同一套静态/动态划分.
    /// "是否被保留"与"是否循环"无关; 两者独立, 而本方法是前者中动态的那一半.
    /// </summary>
    public AudioBuilder WithSurviveFreeze(bool survive)
    {
        if (isUsed)
        {
            return this;
        }

        surviveFreeze = survive;
        return this;
    }

    /// <summary>
    /// Override, for this request only, whether it is accepted at all while the game is frozen.
    /// Implementation approach: records a nullable override consulted by AudioManager before it reserves an
    /// emitter. This is a different question from WithSurviveFreeze: that one decides whether a sound already
    /// playing is kept, whereas this one decides whether a request arriving during a freeze is served at all.
    /// It exists for requests that must be refused during a freeze, such as sounds driven by gameplay that is
    /// not supposed to be running.
    /// Frozen entry also requires an effective SurviveFreeze value of true.
    /// 仅对本次请求覆盖"游戏冻结期间该请求是否被受理".
    /// 实现思路: 记录一个可空覆盖, 由 AudioManager 在预定 emitter 之前查询.
    /// 这与 WithSurviveFreeze 是不同的问题: 后者决定已在播放的音是否被保留,
    /// 而本方法决定冻结期间到达的请求是否被服务. 它存在的意义是让"冻结期间必须被拒绝"的请求得以表达,
    /// 例如由本不该运行的玩法所驱动的音效.
    /// 冻结期进入还要求有效的 SurviveFreeze 为 true.
    /// </summary>
    public AudioBuilder WithAllowWhileFrozen(bool allow)
    {
        if (isUsed)
        {
            return this;
        }

        allowWhileFrozen = allow;
        return this;
    }

    /// <summary>
    /// Override, for this request only, the ramp this playback uses.
    /// Implementation approach: records two nullable overrides; an absent value falls back to the clip's
    /// static AudioClipData.FadeIn / FadeOut. A duration of 0 is a hard start or a hard cut, which is the
    /// original behaviour, so a caller that wants no ramp can say so explicitly.
    /// 仅对本次请求覆盖这次播放所用的渐变.
    /// 实现思路: 记录两个可空覆盖; 未设置时回落到 clip 的静态 `AudioClipData.FadeIn` / `FadeOut`.
    /// 时长为 0 即硬起或硬切, 也就是原先的行为, 因此"不要渐变"的调用方可以显式表达.
    /// </summary>
    public AudioBuilder WithFade(float fadeIn, float fadeOut)
    {
        if (isUsed)
        {
            return this;
        }

        this.fadeIn = fadeIn;
        this.fadeOut = fadeOut;
        return this;
    }

    /// <summary>
    /// Single entry point for submitting this request.
    /// Implementation approach: forwards every carried parameter to AudioManager, which applies the instance
    /// limits, may preempt an older sound, and returns a handle scoped to the playback it started.
    /// Returns null when the request was refused.
    /// 提交本次请求的单一入口.
    /// 实现思路: 把承载的每个参数转交 AudioManager; 由它应用实例上限, 可能抢占更旧的声音,
    /// 并返回一个作用于它所启动那次播放的句柄. 请求被拒绝时返回 null.
    /// </summary>
    public ISoundHandle Play(AudioId audioId)
    {
        if (isUsed || !manager)
        {
            return null;
        }

        isUsed = true;

        return manager.Play(
            audioId, volume, pitch, position, followTarget, surviveFreeze, allowWhileFrozen, fadeIn, fadeOut);
    }
}
