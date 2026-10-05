using UnityEngine;

/// <summary>
/// The template's concrete Setting store: one JSON file holding one GameSettings instance.
/// Subsystem: Setting.
/// Where it lives: nowhere. It is created by SettingBootstrap during bootstrap and is not a Component.
/// Responsibility: name the persisted type, supply the two JsonUtility halves that the generic base cannot
/// express, and absorb JsonUtility's failure mode. It also demonstrates the template's convention for
/// settings: apply nothing to any system, and expose values through the data object instead.
/// Does NOT own: file I/O, path derivation, the load/save sequence, or the decision to fall back to defaults;
/// those belong to SettingStore.
/// Lifetime: created once by the bootstrap and expected to outlive every scene.
/// Paradigms: none. It is a concrete subclass of a generic store.
/// 模版的具体 Setting 存储: 一个 JSON 文件, 持有 GameSettings 的一个实例.
/// Subsystem 归属: Setting.
/// 存在位置: 无. 由 SettingBootstrap 在自举期创建, 不是 Component.
/// 职能: 指明持久化类型, 提供泛型基类无法表达的那两半 JsonUtility, 并吸收 JsonUtility 的失败方式.
/// 它同时示范模版对设置项的约定: 不对任何系统做应用, 而是通过数据对象暴露值.
/// 不负责: 文件 I/O, 路径推导, 加载/保存序列, 或回落到默认值的决定; 那些属于 SettingStore.
/// 生命周期: 由自举创建一次, 预期比每个场景都长寿.
/// 使用范式: 无. 它是泛型存储的具体子类.
/// </summary>
public sealed class FileSettingStore : SettingStore<GameSettings>
{
    /// <summary>
    /// Single entry point for turning the data into JSON.
    /// Implementation approach: delegates to JsonUtility, which is the only serializer available without
    /// adding a package, and which is why GameSettings must stay a plain serializable class.
    /// 把数据转成 JSON 的单一入口.
    /// 实现思路: 委托给 JsonUtility —— 在不新增包的前提下唯一可用的序列化器, 这也是 GameSettings 必须保持
    /// 普通可序列化类的原因.
    /// </summary>
    protected override string Serialize(GameSettings target)
        => JsonUtility.ToJson(target, true);

    /// <summary>
    /// Single entry point for applying the file's JSON onto the already defaulted instance.
    /// Implementation approach: uses FromJsonOverwrite rather than FromJson, because FromJson constructs a new
    /// instance and would therefore hand type zero values to every member the file omits, discarding the design
    /// defaults that were just seeded. JsonUtility reports a malformed payload by throwing ArgumentException
    /// instead of returning null, so the exception is caught here and reported as a plain false; letting it
    /// escape would abort bootstrap and leave the game with no settings at all.
    /// Granularity limit: this guarantees design defaults for members absent at the top level. Do not assume the
    /// same for individual fields inside a nested serializable member or an array element that is present but
    /// partially filled; JsonUtility gives no documented guarantee there.
    /// 把文件中的 JSON 应用到一个已填好默认值的实例之上的单一入口.
    /// 实现思路: 使用 FromJsonOverwrite 而非 FromJson, 因为 FromJson 会新建实例,
    /// 从而把类型零值交给文件中省略的每个成员, 丢弃刚刚铺好的设计默认值.
    /// JsonUtility 对畸形载荷是以抛 ArgumentException 的方式报错, 而不是返回 null,
    /// 因此此处捕获异常并回报一个普通的 false; 若让它逃出去, 自举会被中断, 游戏将完全没有设置可用.
    /// 粒度限制: 这保证了**顶层**缺失成员取设计默认值. 不要假定嵌套可序列化成员内部,
    /// 或存在但只填了一部分的数组元素内部也如此; JsonUtility 在那里没有给出任何文档化的保证.
    /// </summary>
    protected override bool TryDeserialize(string json, GameSettings target)
    {
        try
        {
            JsonUtility.FromJsonOverwrite(json, target);
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Single entry point for applying freshly loaded values to their owning systems.
    /// Implementation approach: intentionally does nothing, and that is the template's convention rather than
    /// an omission. Settings are stored centrally and read by whichever system they describe, so a store never
    /// needs a reference to AudioManager or to anything else it would otherwise have to know about.
    /// 把刚加载的值应用到其拥有系统的单一入口.
    /// 实现思路: 刻意不做任何事, 这是模版的约定而非遗漏.
    /// 设置集中存储, 由它所描述的系统自行读取, 因此存储永不需要引用 AudioManager 或任何它本需了解的东西.
    /// </summary>
    protected override void Configure(GameSettings target)
    {
    }
}
