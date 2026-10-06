using UnityEngine;

/// <summary>
/// The Setting subsystem's entry point and owner of the live settings.
/// Subsystem: Setting.
/// What service it provides: it creates the settings store in BeforeSceneLoad, loads the persisted values
/// once, and exposes the resulting data for the whole session.
/// Unity calls initialization. Consumers read Settings after initialization and may Save or ResetToDefault.
/// Changing values does not save immediately; Application.quitting also attempts to save current values.
/// Why it is a static class rather than a Component: settings must be readable before the first scene object
/// awakes, without adding a bootstrap GameObject. The cost is that this object is implicit and cannot be
/// found in the Hierarchy.
/// Who calls the bootstrap: Unity itself, through RuntimeInitializeOnLoadMethod before the scene loads. No
/// user code calls Initialize.
/// Lifetime: the store is created once per play session and never destroyed. The Application.quitting hook is
/// subscribed once for the same reason.
/// Data overview: holds no data of its own beyond the store reference; the data lives in SettingStore.Data.
/// Setting 子系统的入口与当前设置的持有者.
/// Subsystem 归属: Setting.
/// 提供什么服务: 在 BeforeSceneLoad 建立存储, 加载持久化的值, 并在整个会话中暴露所得到的数据.
/// Unity 调用初始化. 消费方在初始化完成后读 Settings, 可调用 Save 或 ResetToDefault.
/// 修改值不会立即保存; Application.quitting 也会尝试保存当前值.
/// 为什么是静态类而不是 Component: 设置必须在第一个场景对象 Awake 之前即可读,
/// 使用运行时初始化回调避免额外装配自举 GameObject.
/// 代价是这个对象是隐式的, 无法在 Hierarchy 中找到它.
/// 谁调用本自举: Unity 自身, 通过 RuntimeInitializeOnLoadMethod 在场景加载之前. 没有任何用户代码调用 Initialize.
/// 生命周期: 存储每次运行创建一次, 从不销毁. Application.quitting 的订阅出于同一原因也只建立一次.
/// 数据概览: 除存储引用外不持有自己的数据; 数据位于 SettingStore.Data.
/// </summary>
public static class SettingBootstrap
{
    /// <summary>
    /// The live settings after Initialize has completed. No lazy initialization or null guard is provided;
    /// EditMode and earlier runtime initialization callbacks must not assume readiness.
    /// Initialize 完成后的当前设置. 没有延迟初始化或空值保护;
    /// EditMode 或更早的运行时初始化回调不能假定它已就绪.
    /// </summary>
    public static GameSettings Settings => store.Data;

    private static FileSettingStore store;

    /// <summary>
    /// Single entry point for creating and loading the settings store.
    /// Implementation approach: runs before the first scene loads, which is what makes Settings safe to read
    /// from scene Awake. Load handles missing files, read failures and parser exceptions with defaults;
    /// successfully parsed values are not validated for business correctness.
    /// 创建并加载设置存储的单一入口.
    /// 实现思路: 在第一个场景加载之前运行, 这正是 Settings 能被任何 Awake 安全读取的原因.
    /// Load 对缺失文件、读取失败和解析异常使用默认值; 成功解析的数值没有业务校验.
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
    /// Manual verification is available through SettingsScreen.SaveSettings in the main menu and gameplay
    /// scenes, explicit Save calls, and the quitting hook. This static class has no Inspector button.
    /// 人工验证可通过主菜单和玩法场景的 SettingsScreen.SaveSettings、显式 Save 或退出钩子进行.
    /// 本静态类自身没有 Inspector 按钮.
    /// </summary>
}
