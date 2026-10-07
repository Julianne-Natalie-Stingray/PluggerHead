/// <summary>
/// The game's global state labels. It is a label, not a behaviour.
/// Subsystem: GameState.
/// Where it lives: nowhere. It is a value read from GameStateManager.Current.
/// Responsibility: name the states a game can be in, so that any component can ask what the game is currently
/// doing and decide its own behaviour.
/// Does NOT own: what should happen when a state is entered. There is deliberately no per-state capability
/// map and no transition table, because a template cannot know which components care about which state.
/// Additional states are added when something actually enters them, not in advance: a state with no producer
/// and no consumer is a label nobody reads.
/// 游戏的全局状态标签. 它是标签, 不是行为.
/// Subsystem 归属: GameState.
/// 存在位置: 无. 它是从 GameStateManager.Current 读到的值.
/// 职能: 命名游戏可能处于的状态, 使任何组件都能询问游戏当前在做什么并据此决定自身行为.
/// 不负责: 进入某个状态时应当发生什么. 这里刻意没有每状态的能力映射, 也没有转移表,
/// 因为模版无从知道哪些组件在意哪个状态.
/// 新增状态应在确实有东西进入它时才加, 而不是提前加: 一个既无生产者也无消费者的状态是没人读的标签.
/// </summary>
public enum GameState
{
    /// <summary>
    /// Normal play. This is the state a session starts in.
    /// 正常游玩. 会话初始即处于此状态.
    /// </summary>
    Playing,

    /// <summary>
    /// Play is suspended: scaled game time is stopped and the audio listener is paused.
    /// Sources configured to ignore listener pause can keep playing.
    /// 游玩被挂起: 缩放游戏时间停止, 音频监听器暂停; 忽略监听器暂停的声源仍可播放.
    /// </summary>
    Freezed,

    /// <summary>
    /// SceneSwitchManager uses this label while waiting for its asynchronous load to complete.
    /// GameStateManager preserves timeScale on entry and exit; a load entered while frozen stays at zero.
    /// Subscribers may apply their own effects: AudioManager unpauses the listener on Loading.
    /// SceneSwitchManager 用此标签表示正在等待异步加载完成, 不保证与首个可见帧精确同步.
    /// GameStateManager 进入和退出时保留 timeScale; 从冻结进入加载时仍为零.
    /// 订阅者可以施加自己的效果: AudioManager 在 Loading 通知中解除监听器暂停.
    /// </summary>
    Loading
}
