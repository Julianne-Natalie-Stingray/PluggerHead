using System;
using UnityEngine;

/// <summary>
/// The project's setting data, currently containing audio bus volumes.
/// Subsystem: Setting.
/// Where it lives: a managed object held by SettingStore and serialized to one JSON file.
/// Responsibility: be the place where a project's settings are added, and define what "default" means for
/// them. It carries no logic beyond that.
/// Does NOT own: knowing which systems consume it, when it is persisted, or how a value reaches the system it
/// describes. Those are decisions of each owning system, which is why this class has no references to any of
/// them.
/// Lifetime: every SettingStore.Load creates a new instance, seeds defaults, then overlays file values.
/// Direct construction does not seed Audio; call ResetToDefault when constructing outside the store.
/// Paradigms: none. It is a serializable data bag.
/// Data overview: one member, Audio, holding the audio bus volumes. Each member added here must also be
/// assigned in ResetToDefault so reset behavior explicitly matches the intended design defaults.
/// JsonUtility shape: fields here must be primitives, strings, arrays, or nested [Serializable] classes. A
/// Dictionary field would silently persist as nothing, which is why the store offers no key-value surface.
/// 项目的设置数据, 当前包含音频总线音量.
/// Subsystem 归属: Setting.
/// 存在位置: 由 SettingStore 持有的托管对象, 被序列化为一个 JSON 文件.
/// 职能: 作为工程添加设置项的地方, 并定义这些设置项的"默认值"是什么. 除此之外不承载逻辑.
/// 不负责: 知道哪些系统消费它, 何时落盘, 或某个值如何到达它所描述的系统.
/// 这些是各拥有系统的决定, 因此本类不引用其中任何一个.
/// 生命周期: 每次 SettingStore.Load 新建实例、设置默认值, 再覆盖文件内容.
/// 直接构造不会初始化 Audio; 在存储之外构造时需调用 ResetToDefault.
/// 使用范式: 无. 它是可序列化的数据包.
/// 数据概览: 一个成员 Audio, 承载音频总线的音量. 此处每新增一个成员, 都必须在 ResetToDefault 中赋值,
/// 使重置行为明确符合设计默认值, 不依赖成员此前的值.
/// JsonUtility 形态: 此处字段必须是基元, 字符串, 数组, 或嵌套 [Serializable] 类.
/// Dictionary 字段会被静默地持久化为空, 这正是存储不提供键值接口的原因.
/// </summary>
[Serializable]
public class GameSettings : ISettingData
{
    [SerializeField] private AudioSettings audio;

    public AudioSettings Audio => audio;

    /// <summary>
    /// Restore every field to its design default.
    /// Implementation approach: assigns a freshly built default per member rather than leaving a member alone,
    /// so a member can never retain the value it happened to hold. AudioSettings.Default is the single place
    /// that decides what a default audio configuration is, which keeps that decision out of this class.
    /// 把每个字段恢复为其设计默认值.
    /// 实现思路: 为每个成员赋一个新建的默认值, 而不是放着不管, 因此成员绝不会保留它当时碰巧持有的值.
    /// AudioSettings.Default 是决定"默认音频配置是什么"的唯一位置, 从而把这个决定挡在本类之外.
    /// </summary>
    public void ResetToDefault()
    {
        audio = AudioSettings.Default();
    }
}
