/// <summary>
/// Contract for the persisted data of one Setting store.
/// Subsystem: Setting.
/// Who should implement this contract: the data class of each setting store, for example GameSettings. It is
/// implemented once per persisted data shape, and a data class holds nothing but serializable fields.
/// What this contract grants: nothing at runtime. It exists so the generic store can require a data type
/// without itself knowing any of that type's fields, and so a data class is recognisable as persisted state
/// rather than as an arbitrary class that happens to be serializable.
/// Meaningful to JsonUtility: implement the data as a plain class whose fields are primitives, strings,
/// arrays, or nested serializable classes. JsonUtility cannot serialize Dictionary or interface-typed
/// fields, so an implementing type must not expose them.
/// 某个 Setting 存储的持久化数据的契约.
/// Subsystem 归属: Setting.
/// 谁应该实现这个契约: 每个 setting 存储的数据类, 例如 GameSettings.
/// 每种持久化数据形态实现一次, 而数据类除可序列化字段外不持有任何东西.
/// 契约赋予了什么特性: 运行时没有任何特性. 它存在的意义是让泛型存储能要求一个数据类型,
/// 而自身不必知道该类型的任何字段; 同时让一个数据类能被识别为"持久化状态",
/// 而不是一个碰巧可序列化的任意类.
/// 对 JsonUtility 有意义: 把数据实现为普通类, 字段为基元, 字符串, 数组, 或嵌套可序列化类.
/// JsonUtility 无法序列化 Dictionary 或接口类型字段, 因此实现类型不得暴露它们.
/// </summary>
public interface ISettingData
{
    /// <summary>
    /// Restore every field to its default. Single entry point for producing first-run state, so a store never
    /// has to know which fields exist in order to start from a clean baseline.
    /// 把每个字段恢复为默认值. 产生首次运行状态的单一入口,
    /// 因此存储无需知道存在哪些字段就能从一个干净基线开始.
    /// </summary>
    void ResetToDefault();
}
