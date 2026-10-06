using System.Collections.Generic;

/// <summary>
/// Tracks registered emitters in registration order; it does not verify AudioSource playback state.
/// Subsystem: Core (Audio).
/// Where it lives: nowhere. It is a plain object owned by AudioManager and never attached to a GameObject.
/// Responsibility: registration, removal, per-AudioId counting, and answering which registration is the
/// oldest. That is the whole of its job.
/// Does NOT own: any policy. It never decides whether to preempt, what to preempt, or whether a request
/// may proceed; AudioManager owns every such decision and only asks this type for facts.
/// Lifetime: created by AudioManager during InitializeInternal and lives exactly as long as the manager.
/// Ordering: registrations carry a monotonically increasing sequence number. A sequence is used instead of
/// a timestamp because Time.time repeats within one frame, which would make two sounds started in the same
/// frame indistinguishable, while a sequence is always strictly ordered.
/// Removal is deferred: Unregister marks the first matching slot empty. Register compacts first;
/// callers can also invoke Compact explicitly. Duplicate registrations are not rejected.
/// Paradigms: none. It is a bookkeeping helper, not a service, and is not a singleton.
/// 按注册顺序跟踪 emitter, 不检查 AudioSource 是否真正播放.
/// Subsystem 归属: Core (Audio).
/// 存在位置: 无. 它是普通对象, 归 AudioManager 所有, 不贴在任何 GameObject 上.
/// 职能: 注册, 移除, 按 AudioId 计数, 以及回答"哪个注册项最旧". 这就是它的全部工作.
/// 不负责: 任何策略. 它永不确定是否抢占, 抢占谁, 或某个请求是否放行;
/// 这些决策全部归 AudioManager, 它只被询问事实.
/// 生命周期: 由 AudioManager 在 InitializeInternal 中创建, 与管理器同寿命.
/// 顺序语义: 注册项携带单调递增的序号. 用序号而不是时间戳, 是因为 Time.time 在同一帧内会重复,
/// 使同帧启动的两个声音无法区分, 而序号永远严格有序.
/// 移除是延迟的: Unregister 只把首个匹配槽位置空, Register 前压实, 也可显式调用 Compact.
/// 不拒绝同一 emitter 的重复注册.
/// 使用范式: 无. 它是记账助手, 不是服务, 也不是单例.
/// </summary>
public sealed class AudioRegistry
{
    public int Count
    {
        get
        {
            int count = 0;
            foreach (AudioEmitter emitter in emitters)
            {
                if (emitter)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private readonly List<AudioEmitter> emitters = new();
    private readonly List<long> sequences = new();

    private long nextSequence;

    /// <summary>
    /// Record an emitter; AudioManager calls this before Configure and Play.
    /// Implementation approach: compacts deferred removals first, then appends the emitter together with
    /// the next sequence number, which is what establishes start order.
    /// 注册 emitter; AudioManager 在 Configure 与 Play 之前调用.
    /// 实现思路: 先压实延迟移除, 再把 emitter 与下一个序号一起追加 —— 序号即为开始顺序的依据.
    /// </summary>
    public void Register(AudioEmitter emitter)
    {
        if (!emitter)
        {
            return;
        }

        Compact();

        emitters.Add(emitter);
        sequences.Add(nextSequence++);
    }

    /// <summary>
    /// Mark the first matching registration empty without shifting later slots or stopping playback.
    /// 将首个匹配注册项置空, 不移动后续槽位, 也不停止音源.
    /// </summary>
    public void Unregister(AudioEmitter emitter)
    {
        if (!emitter)
        {
            return;
        }

        int index = emitters.IndexOf(emitter);
        if (index < 0)
        {
            return;
        }

        emitters[index] = null;
        sequences[index] = 0;
    }

    public int CountOf(AudioId audioId)
    {
        int count = 0;

        for (int i = 0; i < emitters.Count; i++)
        {
            if (emitters[i] && emitters[i].AudioId == audioId)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Single entry point for asking which playback started first.
    /// Implementation approach: scans for the smallest sequence number, optionally restricted to one
    /// AudioId, and optionally skipping looping sounds. The scan is linear, but it runs only when a limit
    /// has already been reached, so it is not on the ordinary playback path.
    /// includeLooping is a constraint supplied by the caller, not a policy of this type: AudioManager owns
    /// the rule that a loop is never preempted, and passes false when it needs a preemptable victim.
    /// 询问哪一次播放开始得最早的单一入口.
    /// 实现思路: 找出最小序号, 可选地限定在某个 AudioId 上, 并可跳过循环音.
    /// 该扫描是线性的, 但只在已经触顶时执行, 因此不在常规播放路径上.
    /// includeLooping 是由调用方给出的约束, 不是本类型的策略: "循环音永不被抢占"这条规则归 AudioManager,
    /// 它需要可抢占的受害者时传 false.
    /// </summary>
    public bool TryGetOldest(AudioId audioId, bool includeLooping, out AudioEmitter emitter)
    {
        emitter = null;
        long oldest = long.MaxValue;

        for (int i = 0; i < emitters.Count; i++)
        {
            AudioEmitter candidate = emitters[i];

            if (!candidate || candidate.AudioId != audioId)
            {
                continue;
            }

            if (!includeLooping && candidate.IsLooping)
            {
                continue;
            }

            if (sequences[i] >= oldest)
            {
                continue;
            }

            oldest = sequences[i];
            emitter = candidate;
        }

        return emitter;
    }

    public bool TryGetOldest(bool includeLooping, out AudioEmitter emitter)
    {
        emitter = null;
        long oldest = long.MaxValue;

        for (int i = 0; i < emitters.Count; i++)
        {
            AudioEmitter candidate = emitters[i];

            if (!candidate)
            {
                continue;
            }

            if (!includeLooping && candidate.IsLooping)
            {
                continue;
            }

            if (sequences[i] >= oldest)
            {
                continue;
            }

            oldest = sequences[i];
            emitter = candidate;
        }

        return emitter;
    }

    /// <summary>
    /// Single entry point for dropping the deferred removals.
    /// Implementation approach: rebuilds both parallel lists in place, keeping the relative order of the
    /// surviving registrations so start order is preserved.
    /// 丢弃延迟移除项的单一入口.
    /// 实现思路: 原地重建两个并列列表, 保持存活注册项的相对顺序, 因此开始顺序不被破坏.
    /// </summary>
    public void Compact()
    {
        int write = 0;

        for (int read = 0; read < emitters.Count; read++)
        {
            if (!emitters[read])
            {
                continue;
            }

            emitters[write] = emitters[read];
            sequences[write] = sequences[read];
            write++;
        }

        if (write >= emitters.Count)
        {
            return;
        }

        emitters.RemoveRange(write, emitters.Count - write);
        sequences.RemoveRange(write, sequences.Count - write);
    }
}
