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
/// Missing files use defaults with an info log; read failures, whitespace, and a false deserialize result
/// report warnings. A successfully parsed partial object does not itself produce a warning.
/// The subclass is responsible for parse and business validation; this base does not validate fields.
/// 某一组持久化设置的泛型存储.
/// Subsystem 归属: Setting.
/// 存在位置: 自身无处. 具体子类由自举创建, 或由拥有自身设置的系统创建; 本基类型永不贴在 GameObject 上.
/// 职能: 加载, 持有, 保存并重置一个 TData 实例, 并拥有 TData 持久化时所用的文件名.
/// 不负责: 知道 TData 的任何字段, 决定何时保存, 或把加载到的值应用到任何系统.
/// 应用是"谁拥有受影响的系统, 谁来应用", 这正是 Configure 为抽象的原因.
/// 生命周期: 创建一次, 预期比每个场景都长寿. 由自举持有; 本类型自身不创建也不销毁自己.
/// JSON 形态: TData 必须是普通可序列化类. JsonUtility 无法序列化 Dictionary 或接口类型字段,
/// 因此本类型刻意不提供键值接口; 若某个存储的数据必须动态, 应在自己的数据类里持久化一个可序列化的键值对列表.
/// 文件缺失时使用默认值并记录 info; 读取失败、空白或反序列化返回 false 时记录 warning.
/// 成功解析但缺少字段本身不会触发 warning. 解析与业务校验属于子类, 基类不校验字段.
/// </summary>
public abstract class SettingStore<TData>
    where TData : class, ISettingData, new()
{
    /// <summary>
    /// The live data, null before Load. Load replaces the instance; ResetToDefault mutates it in place.
    /// 当前数据, Load 前为 null. Load 替换实例, ResetToDefault 在当前实例上重置.
    /// </summary>
    public TData Data => data;

    public string FilePath => filePath;

    protected TData data;
    protected string filePath;

    private const string FileExtension = ".json";

    /// <summary>
    /// Single entry point for producing the live data.
    /// Implementation approach: starts from design defaults and lets the subclass apply the file on top of
    /// them. This explicitly seeds the intended baseline before the subclass deserializes the file;
    /// preservation of omitted members depends on that deserialization strategy.
    /// 产生当前数据的单一入口.
    /// 实现思路: 先从设计默认值开始, 再由子类把文件覆盖在其上.
    /// 在子类反序列化前明确建立设计基线; 缺失成员是否保留基线取决于具体反序列化策略.
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
    /// Implementation approach: catches read exceptions and reports blank content. A false deserialize result
    /// resets any partially overwritten fields before warning. Missing members are not validated here.
    /// Subclasses must report parse failure through the return value; exceptions from TryDeserialize,
    /// ResetToDefault or Configure are not caught by this loading pipeline.
    /// 加载中"文件那一半"的单一入口, 且作用于一个已经填好默认值的实例之上.
    /// 实现思路: 捕获读取异常、检查空白内容; 反序列化返回 false 后先重置部分覆盖的字段, 再报告 warning.
    /// 此处不检查缺失成员. 子类须通过返回值报告解析失败;
    /// 加载流程不捕获 TryDeserialize、ResetToDefault 或 Configure 抛出的异常.
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
        {
            return;
        }

        // A failed overwrite may already have changed some fields.
        // 覆盖失败前可能已写入部分字段，恢复完整默认值后再报告失败。
        target.ResetToDefault();
        ReportLoadFailure($"could not parse or validate {filePath}");
    }

    /// <summary>
    /// Single entry point for writing the live data to disk.
    /// Implementation approach: serializes and writes immediately, and reports success through its return
    /// value so a caller can decide what to do. Serialization and write exceptions are caught and logged.
    /// Writes a unique sibling temporary file before replacing the destination; failures preserve the old file.
    /// 把当前数据写入磁盘的单一入口.
    /// 实现思路: 立即序列化并写入, 并通过返回值报告成功与否, 使调用方自行决定后续;
    /// 序列化与写入异常会被捕获并记录. 先写同目录唯一临时文件, 再替换目标; 失败保留旧文件.
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

        string temporaryPath = filePath + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            System.IO.File.WriteAllText(temporaryPath, json);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Replace(temporaryPath, filePath, null);
            }
            else
            {
                System.IO.File.Move(temporaryPath, filePath);
            }
        }
        catch (System.Exception exception)
        {
            return ReportSaveFailure($"could not write {filePath}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(temporaryPath))
                {
                    System.IO.File.Delete(temporaryPath);
                }
            }
            catch (System.Exception exception)
            {
                GameLog.Warning()
                    .Subsystem("Setting")
                    .Name(LogName.Class)
                    .Issue(LogIssue.Specify($"Could not remove temporary settings file {temporaryPath}: {exception.Message}"))
                    .Write();
            }
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
    /// Implementation approach: an abstract hook isolates the serialization strategy from file I/O.
    /// Save catches exceptions from this hook and reports failure.
    /// 持久化中与类型相关的那一半的单一入口.
    /// 实现思路: 抽象钩子把序列化策略与文件 I/O 分开. Save 捕获此钩子异常并报告失败.
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
    {
        return System.IO.Path.Combine(
            Application.persistentDataPath,
            typeof(TData).Name + FileExtension);
    }

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
