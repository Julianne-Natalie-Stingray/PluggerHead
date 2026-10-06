/// <summary>
/// Keys for the scenes this project can switch to.
/// Subsystem: Core (SceneSwitch).
/// Where it lives: nowhere. It is a value passed to SceneSwitchManager.RequestSwitch and used as a lookup key
/// into SceneSwitchConfigs.
/// Responsibility: give every switchable scene a name the compiler can check, so a mistyped target is a build
/// error rather than a failed load at runtime.
/// Does NOT own: the scene names themselves or the Build Settings order. SceneSwitchConfigs maps each key to a
/// name, and Unity owns whether that name is registered for loading.
/// Extending it: a scene and its key are added together, in the same change -- create the scene file, add the
/// matching member here, and add the mapping to SceneSwitchConfigs. Doing it in that order is what keeps every
/// delivery compiling; adding the key first would reference a scene that does not exist yet.
/// Paradigms: none. It is an identifier set.
/// 工程可切换场景的键.
/// Subsystem 归属: Core (SceneSwitch).
/// 存在位置: 无. 它是传给 SceneSwitchManager.RequestSwitch 的值, 并作为 SceneSwitchConfigs 的查找键.
/// 职能: 给每个可切换场景一个编译器能检查的名字, 使打错目标成为编译错误, 而不是运行时的加载失败.
/// 不负责: 场景名本身, 也不负责 Build Settings 的顺序. SceneSwitchConfigs 把每个键映射到一个名字,
/// 而该名字是否已注册可加载由 Unity 掌管.
/// 扩展方式: 场景与其键必须**在同一次改动中一起加入** —— 创建场景文件, 在此加一个成员, 并在
/// SceneSwitchConfigs 中加映射. 按这个顺序做, 才能保证每份交付都编译得过; 先加键会引用一个尚不存在的场景.
/// 使用范式: 无. 它是标识符集合.
/// </summary>
public enum SceneId
{
#region Jill

    JillTestScene,
    JillTestSceneSwitch,
    JillTestWireScene,

#endregion

#region HeXie

    HeXieTestScene

#endregion

}
