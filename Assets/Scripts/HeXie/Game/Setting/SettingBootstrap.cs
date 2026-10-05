using UnityEngine;

/// <summary>
/// The Setting subsystem's entry point and owner of the live settings.
/// Subsystem: Setting.
/// What service it provides: it creates the settings store before any scene exists, loads the persisted values
/// once, and exposes the resulting data for the whole session.
/// Who needs to call it: nothing has to. Reading is done through Settings at any time; writing is done by
/// whichever system owns the setting. Only Save is expected to be called by consumers.
/// Why it is a static class rather than a Component: settings must be readable before the first scene object
/// awakes, and a Component cannot guarantee that without an object the user has to place by hand, which would
/// break the template's "no manual wiring" convention. The cost is that this object is implicit and cannot be
/// found in the Hierarchy.
/// Who calls the bootstrap: Unity itself, through RuntimeInitializeOnLoadMethod before the scene loads. No
/// user code calls Initialize.
/// Lifetime: the store is created once per play session and never destroyed. The Application.quitting hook is
/// subscribed once for the same reason.
/// Data overview: holds no data of its own beyond the store reference; the data lives in SettingStore.Data.
/// Setting 子系统的入口与当前设置的持有者.
/// Subsystem 归属: Setting.
/// 提供什么服务: 在任何场景存在之前建立设置存储, 一次性加载持久化的值, 并在整个会话中暴露所得到的数据.
/// 谁需要调用: 无需任何人调用. 读取随时通过 Settings 进行; 写入由拥有该设置项的系统进行.
/// 预期由消费方调用的只有 Save.
/// 为什么是静态类而不是 Component: 设置必须在第一个场景对象 Awake 之前即可读,
/// 而 Component 要做到这一点就得依赖一个必须由你手动放置的物体, 那会破坏模版"无需手动装配"的约定.
/// 代价是这个对象是隐式的, 无法在 Hierarchy 中找到它.
/// 谁调用本自举: Unity 自身, 通过 RuntimeInitializeOnLoadMethod 在场景加载之前. 没有任何用户代码调用 Initialize.
/// 生命周期: 存储每次运行创建一次, 从不销毁. Application.quitting 的订阅出于同一原因也只建立一次.
/// 数据概览: 除存储引用外不持有自己的数据; 数据位于 SettingStore.Data.
/// </summary>
public static class SettingBootstrap
{
    /// <summary>
    /// The live settings. Valid from before the first scene loads until the process ends, so any code may read
    /// it at any time without checking for readiness.
    /// 当前设置. 从第一个场景加载之前到进程结束均有效, 因此任何代码随时可读, 无需检查是否就绪.
    /// </summary>
    public static GameSettings Settings => store.Data;

    private static FileSettingStore store;

    /// <summary>
    /// Single entry point for creating and loading the settings store.
    /// Implementation approach: runs before the first scene loads, which is what makes Settings safe to read
    /// from any Awake. It never throws: a missing, unreadable, or corrupt file falls back to defaults inside
    /// Load, so a broken settings file cannot prevent the game from starting.
    /// 创建并加载设置存储的单一入口.
    /// 实现思路: 在第一个场景加载之前运行, 这正是 Settings 能被任何 Awake 安全读取的原因.
    /// 它从不抛异常: 文件缺失, 不可读, 或损坏都会在 Load 内部回退到默认值,
    /// 因此损坏的设置文件无法阻止游戏启动.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        store = new FileSettingStore();
        store.Load();

        Application.quitting -= HandleQuitting;
        Application.quitting += HandleQuitting;
    }

    /// <summary>
    /// Single entry point for persisting the current settings.
    /// Implementation approach: forwards to the store and reports its result unchanged, so a caller that cares
    /// about a failed save can react to it. Saving is explicit by design: changing a value does not persist
    /// it, which keeps "changed" and "committed" as separate decisions.
    /// 持久化当前设置的单一入口.
    /// 实现思路: 转发给存储并原样返回其结果, 因此在意保存失败的调用方可以对此作出反应.
    /// 保存按设计是显式的: 改动值不会使其持久化, 这使"已改动"与"已提交"保持为两个独立决定.
    /// </summary>
    public static bool Save()
    {
        return store.Save();
    }

    /// <summary>
    /// Single entry point for discarding the live values in favour of defaults.
    /// Implementation approach: resets in memory only, and leaves persisting to a following Save, so a caller
    /// cannot accidentally overwrite a saved file merely by asking for defaults.
    /// 丢弃当前值改用默认值的单一入口.
    /// 实现思路: 只在内存中重置, 将其持久化留给随后的 Save,
    /// 因此调用方不会仅仅因为索取默认值就意外覆盖已保存的文件.
    /// </summary>
    public static void ResetToDefault()
    {
        store.ResetToDefault();
    }

    private static void HandleQuitting()
    {
        Save();
    }

    /// <summary>
    /// TODO: Provide a way to trigger a save from the Inspector for manual verification. Not implemented
    /// because this bootstrap is a static class, and NaughtyAttributes' Button attribute only appears on a
    /// Component's inspector, so there is no surface to hang it on. Verification therefore goes through
    /// Application.quitting or an explicit Save call from gameplay code.
    /// TODO: 提供从 Inspector 触发保存以进行人工验证的手段. 未实现原因: 本自举是静态类,
    /// 而 NaughtyAttributes 的 Button 属性只出现在 Component 的 Inspector 上, 没有可挂载的面.
    /// 因此验证途径是 Application.quitting, 或由玩法代码显式调用 Save.
    /// </summary>
}
