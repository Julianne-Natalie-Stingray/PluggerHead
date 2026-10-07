/// <summary>
/// Contract for the persisted data of one Setting store.
/// Subsystem: Setting.
/// Who should implement this contract: the data class of each setting store, for example GameSettings. It is
/// implemented once per persisted data shape, and a data class holds nothing but serializable fields.
/// Runtime contract: the generic store calls ResetToDefault without knowing the data type's fields.
/// Implementations define the baseline used by Load, ResetToDefault and failed deserialization.
/// Meaningful to JsonUtility: implement the data as a plain class whose fields are primitives, strings,
/// arrays, or nested serializable classes. JsonUtility cannot serialize Dictionary or interface-typed
/// fields, so an implementing type must not expose them.
/// 某个 Setting 存储的持久化数据的契约.
/// Subsystem 归属: Setting.
/// 谁应该实现这个契约: 每个 setting 存储的数据类, 例如 GameSettings.
/// 每种持久化数据形态实现一次, 而数据类除可序列化字段外不持有任何东西.
/// 运行时契约: 泛型存储通过 ResetToDefault 建立默认值, 无需知道数据类型的字段.
/// 实现定义 Load、ResetToDefault 和反序列化失败后的基线.
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
