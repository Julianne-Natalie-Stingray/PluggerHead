using UnityEngine;

/// <summary>
/// The project's concrete Setting store: one JSON file holding one GameSettings instance.
/// Subsystem: Setting.
/// Where it lives: nowhere. It is created by SettingBootstrap during bootstrap and is not a Component.
/// Responsibility: select GameSettings and JsonUtility as the persistence strategy and convert parsing
/// exceptions or invalid audio values to a failure result. It follows the project's convention for
/// settings: apply nothing to any system, and expose values through the data object instead.
/// Does NOT own: file I/O, path derivation, the load/save sequence, or the decision to fall back to defaults;
/// those belong to SettingStore.
/// Lifetime: created once by the bootstrap and expected to outlive every scene.
/// Paradigms: none. It is a concrete subclass of a generic store.
/// 项目的具体 Setting 存储: 一个 JSON 文件, 持有 GameSettings 的一个实例.
/// Subsystem 归属: Setting.
/// 存在位置: 无. 由 SettingBootstrap 在自举期创建, 不是 Component.
/// 职能: 选择 GameSettings 与 JsonUtility 序列化策略, 将解析异常或无效音频数值转为失败结果.
/// 它遵循项目对设置项的约定: 不对任何系统做应用, 而是通过数据对象暴露值.
/// 不负责: 文件 I/O, 路径推导, 加载/保存序列, 或回落到默认值的决定; 那些属于 SettingStore.
/// 生命周期: 由自举创建一次, 预期比每个场景都长寿.
/// 使用范式: 无. 它是泛型存储的具体子类.
/// </summary>
public sealed class FileSettingStore : SettingStore<GameSettings>
{
    /// <summary>
    /// Single entry point for turning the data into JSON.
    /// Implementation approach: delegates to the project's chosen JsonUtility serializer.
    /// GameSettings fields must follow Unity's serialization rules.
    /// 把数据转成 JSON 的单一入口.
    /// 实现思路: 委托给本项目选用的 JsonUtility; GameSettings 字段须遵循 Unity 序列化规则.
    /// </summary>
    protected override string Serialize(GameSettings target)
    {
        return JsonUtility.ToJson(target, true);
    }

    /// <summary>
    /// Single entry point for applying the file's JSON onto the already defaulted instance.
    /// Implementation approach: uses FromJsonOverwrite rather than FromJson, because FromJson constructs a new
    /// instance and would therefore hand type zero values to every member the file omits, discarding the design
    /// defaults that were just seeded. Exceptions from JsonUtility are caught here and reported as false;
    /// allowing one to escape would interrupt the remaining load/bootstrap sequence.
    /// After parsing, require audio data and finite bus volumes in the inclusive range 0..1.
    /// Granularity limit: this guarantees design defaults for members absent at the top level. Do not assume the
    /// same for individual fields inside a nested serializable member or an array element that is present but
    /// partially filled; JsonUtility gives no documented guarantee there.
    /// 把文件中的 JSON 应用到一个已填好默认值的实例之上的单一入口.
    /// 实现思路: 使用 FromJsonOverwrite 而非 FromJson, 因为 FromJson 会新建实例,
    /// 从而把类型零值交给文件中省略的每个成员, 丢弃刚刚铺好的设计默认值.
    /// 此处捕获 JsonUtility 抛出的异常并返回 false; 若让异常逃出, 加载/自举的后续步骤会中断.
    /// 解析后检查 Audio 非空, 且各总线音量为 0..1 范围内的有限数值.
    /// 粒度限制: 这保证了**顶层**缺失成员取设计默认值. 不要假定嵌套可序列化成员内部,
    /// 或存在但只填了一部分的数组元素内部也如此; JsonUtility 在那里没有给出任何文档化的保证.
    /// </summary>
    protected override bool TryDeserialize(string json, GameSettings target)
    {
        try
        {
            JsonUtility.FromJsonOverwrite(json, target);
            AudioSettings audio = target.Audio;
            return audio != null && IsValidVolume(audio.MasterVolume) &&
                IsValidVolume(audio.OstVolume) && IsValidVolume(audio.SfxVolume);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private static bool IsValidVolume(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }

    /// <summary>
    /// Post-load/reset hook; this store does not apply values to their owning systems.
    /// Implementation approach: intentionally does nothing, and that is the project's convention rather than
    /// an omission. Settings are stored centrally and read by whichever system they describe, so a store never
    /// needs a reference to AudioManager or to anything else it would otherwise have to know about.
    /// 加载或重置后的钩子; 此存储不向拥有系统应用数值.
    /// 实现思路: 刻意不做任何事, 这是项目的存储边界.
    /// 设置集中存储, 由它所描述的系统自行读取, 因此存储永不需要引用 AudioManager 或任何它本需了解的东西.
    /// </summary>
    protected override void Configure(GameSettings target)
    {
    }
}
