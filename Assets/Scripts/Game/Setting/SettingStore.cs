using UnityEngine;

/// <summary>
/// Generic store for one body of persisted settings.
/// Subsystem: Setting.
/// Where it lives: nowhere by itself. A concrete subclass is created by the bootstrap or by a subsystem that
/// owns its own settings; this base type is never attached to a GameObject.
/// Responsibility: load, hold, save, and reset one TData instance, and own the file name that TData is
/// persisted under.
/// Does NOT own: knowing any field of TData, deciding when to save, or applying the loaded values to any
/// system. Applying is the job of whoever owns the affected system, which is why Configure is abstract.
/// Lifetime: created once and expected to outlive every scene. The bootstrap holds it; nothing in this type
/// creates or destroys itself.
/// JSON shape: TData must be a plain serializable class. JsonUtility cannot serialize Dictionary or
/// interface-typed fields, so this type deliberately offers no key-value surface; a store whose data must be
/// dynamic should persist a serializable list of pairs in its own data class instead.
/// Corruption is expected, not exceptional: a missing, unreadable, empty, syntactically broken, or
/// member-less file all resolve to design defaults with a log entry, because a damaged settings file must
/// never stop a game from starting.
/// 某一组持久化设置的泛型存储.
/// Subsystem 归属: Setting.
/// 存在位置: 自身无处. 具体子类由自举创建, 或由拥有自身设置的系统创建; 本基类型永不贴在 GameObject 上.
/// 职能: 加载, 持有, 保存并重置一个 TData 实例, 并拥有 TData 持久化时所用的文件名.
/// 不负责: 知道 TData 的任何字段, 决定何时保存, 或把加载到的值应用到任何系统.
/// 应用是"谁拥有受影响的系统, 谁来应用", 这正是 Configure 为抽象的原因.
/// 生命周期: 创建一次, 预期比每个场景都长寿. 由自举持有; 本类型自身不创建也不销毁自己.
/// JSON 形态: TData 必须是普通可序列化类. JsonUtility 无法序列化 Dictionary 或接口类型字段,
/// 因此本类型刻意不提供键值接口; 若某个存储的数据必须动态, 应在自己的数据类里持久化一个可序列化的键值对列表.
/// 损坏是预期情况而非异常情况: 文件缺失, 不可读, 空白, 语法损坏, 或缺少成员, 一律落到设计默认值并记录一条日志,
/// 因为损坏的设置文件绝不应阻止游戏启动.
/// </summary>
public abstract class SettingStore<TData>
    where TData : class, ISettingData, new()
{
    /// <summary>
    /// The live data. Readable at any time, because loading happens during bootstrap before any scene object
    /// exists, so no reader can observe a half-loaded store.
    /// 当前数据. 任意时刻可读, 因为加载发生在自举期, 早于任何场景对象存在, 因此读者不会看到半加载的存储.
    /// </summary>
    public TData Data => data;

    public string FilePath => filePath;

    protected TData data;
    protected string filePath;

    private const string FileExtension = ".json";

    /// <summary>
    /// Single entry point for producing the live data.
    /// Implementation approach: starts from design defaults and lets the subclass apply the file on top of
    /// them. Seeding defaults first is what makes a file that lacks some members fall back to the designed
    /// value for those members rather than to the type's zero value; a value of zero is almost never a
    /// sensible audio volume or frame rate.
    /// 产生当前数据的单一入口.
    /// 实现思路: 先从设计默认值开始, 再由子类把文件覆盖在其上.
    /// 之所以先铺默认值, 是为了让"文件里缺少某些成员"回退到**被设计出的值**, 而不是该类型的零值;
    /// 而零值几乎从不可能是合理的音量或帧率.
    /// </summary>
    public void Load()
    {
        data = new TData();
        data.ResetToDefault();
        filePath = BuildFilePath();

        if (!System.IO.File.Exists(filePath))
        {
            GameLog.Info()
                .Subsystem("Setting")
                .Name(LogName.Class)
                .Issue(LogIssue.Specify($"No saved settings at {filePath}; using defaults. "))
                .Write();

            Configure(data);
            return;
        }

        LoadInto(data);

        Configure(data);
    }

    /// <summary>
    /// Single entry point for the file half of loading, applied on top of an already defaulted instance.
    /// Implementation approach: every failure path -- unreadable file, unparseable content, or content missing
    /// required members -- falls through to the caller's defaults with a log entry, and the method itself never
    /// throws. A subclass must therefore report failure through its return value rather than by propagating an
    /// exception, because JsonUtility signals a parse error by throwing, and letting that escape would abort
    /// bootstrap and leave the game with no settings at all.
    /// 加载中"文件那一半"的单一入口, 且作用于一个已经填好默认值的实例之上.
    /// 实现思路: 每条失败路径 —— 文件不可读, 内容无法解析, 或内容缺少必需成员 —— 都带着一条日志回落到调用方的默认值,
    /// 且本方法自身从不抛异常. 因此子类必须通过返回值报告失败, 而不是让异常传播:
    /// JsonUtility 对解析错误是以抛异常的方式报错的, 若让它逃出去, 自举会被中断, 游戏将完全没有设置可用.
    /// </summary>
    protected void LoadInto(TData target)
    {
        string json;

        try
        {
            json = System.IO.File.ReadAllText(filePath);
        }
        catch (System.Exception exception)
        {
            ReportLoadFailure($"could not read {filePath}: {exception.Message}");

            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            ReportLoadFailure($"{filePath} is empty");

            return;
        }

        if (TryDeserialize(json, target))
            return;

        ReportLoadFailure($"could not parse {filePath}");
    }

    /// <summary>
    /// Single entry point for writing the live data to disk.
    /// Implementation approach: serializes and writes immediately, and reports success through its return
    /// value so a caller can decide what to do; it never throws, because a failed save must not break the
    /// caller's flow.
    /// 把当前数据写入磁盘的单一入口.
    /// 实现思路: 立即序列化并写入, 并通过返回值报告成功与否, 使调用方自行决定后续;
    /// 它从不抛异常, 因为保存失败不应打断调用方的流程.
    /// </summary>
    public bool Save()
    {
        string json;

        try
        {
            json = Serialize(data);
        }
        catch (System.Exception exception)
        {
            return ReportSaveFailure($"serialization failed: {exception.Message}");
        }

        try
        {
            System.IO.File.WriteAllText(filePath, json);
        }
        catch (System.Exception exception)
        {
            return ReportSaveFailure($"could not write {filePath}: {exception.Message}");
        }

        GameLog.Info()
            .Subsystem("Setting")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Settings saved to {filePath}. "))
            .Write();

        return true;
    }

    /// <summary>
    /// Single entry point for discarding the live values in favour of defaults, without touching the file.
    /// Implementation approach: resets in memory only, so a caller must Save to make it persistent; that keeps
    /// "reset" and "persist" as separate decisions, consistent with saving being explicit.
    /// 丢弃当前值改用默认值的单一入口, 且不触碰文件.
    /// 实现思路: 只在内存中重置, 因此调用方必须 Save 才能使其持久化;
    /// 这保持了"重置"与"落盘"是两个独立决定, 与"保存是显式的"一致.
    /// </summary>
    public void ResetToDefault()
    {
        data.ResetToDefault();
        Configure(data);
    }

    /// <summary>
    /// Single entry point for the type-specific half of persistence.
    /// Implementation approach: abstract rather than varied, because JsonUtility cannot serialize a generic
    /// type parameter, so each concrete store must name its own type. It must not throw.
    /// 持久化中与类型相关的那一半的单一入口.
    /// 实现思路: 为抽象而非多态, 因为 JsonUtility 无法序列化泛型类型参数, 每个具体存储必须写出自己的类型.
    /// 它不得抛异常.
    /// </summary>
    protected abstract string Serialize(TData target);

    /// <summary>
    /// Single entry point for applying JSON onto an already defaulted instance.
    /// Implementation approach: returns false instead of throwing, so the caller can fall back to defaults.
    /// It must overwrite rather than construct, or every member absent from the file would take the type's zero
    /// value instead of the designed one.
    /// 把 JSON 应用到一个已填默认值的实例之上的单一入口.
    /// 实现思路: 返回 false 而非抛异常, 使调用方可以回落到默认值.
    /// 它必须是"覆盖"而不是"新建", 否则文件中缺失的每个成员都会取该类型的零值, 而不是被设计出的值.
    /// </summary>
    protected abstract bool TryDeserialize(string json, TData target);

    /// <summary>
    /// Single entry point for applying a freshly obtained data instance to whatever owns it.
    /// Implementation approach: abstract and called on every load and reset, so a store cannot be left holding
    /// values that were never handed to the system they describe. An empty implementation is legitimate when
    /// the owning system pulls values on its own.
    /// 把刚取得的数据实例应用到其拥有者的单一入口.
    /// 实现思路: 为抽象, 并在每次加载与重置时调用, 因此存储不会持有"从未交给它所描述的系统"的值.
    /// 当拥有者自行拉取值时, 空实现是合理的.
    /// </summary>
    protected abstract void Configure(TData target);

    /// <summary>
    /// Single entry point for deriving the persisted file path.
    /// Implementation approach: one file per data type under the platform's persistent data path, named after
    /// the type so the name is stable without any configuration.
    /// 推导持久化文件路径的单一入口.
    /// 实现思路: 每个数据类型一个文件, 位于平台的持久化数据目录下, 以类型命名, 因此无需任何配置即可稳定.
    /// </summary>
    private string BuildFilePath()
        => System.IO.Path.Combine(
            Application.persistentDataPath,
            typeof(TData).Name + FileExtension);

    private void ReportLoadFailure(string reason)
    {
        GameLog.Warning()
            .Subsystem("Setting")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"{reason}; design defaults were used instead. "))
            .Action(LogAction.UseFallbackValue("design defaults"))
            .Write();
    }

    private bool ReportSaveFailure(string reason)
    {
        GameLog.Error()
            .Subsystem("Setting")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"Settings were not saved: {reason} "))
            .Action(LogAction.Ignore)
            .Write();

        return false;
    }
}
