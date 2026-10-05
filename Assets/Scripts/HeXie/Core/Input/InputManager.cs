using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Thin input wrapper. Publishes the raw device layer and nothing above it.
/// Subsystem: Core (Input).
/// Where it lives: on the Core GameObject, required by CoreFacade and reached through CoreFacade.Input.
/// Responsibility: own the generated PlayerControls instance, forward its primary button as events, expose the
/// primary axis and the pointer position, and hand the whole generated action set to whoever needs more than
/// that. It carries no notion of gameplay meaning.
/// Does NOT own: what either axis means, which axis gravity opposes, whether an axis pair is a platformer or a
/// top-down layout, or how the game reacts to any input -- the response belongs in gameplay code. It also does
/// not own control schemes, rebinding, or non-keyboard devices.
/// Lifetime: the generated action set is created in Awake, subscribed in OnEnable and unsubscribed in OnDisable,
/// and disposed in OnDestroy. The component lives on the persistent Core object.
/// Paradigms: none. It is a plain MonoBehaviour service, reached through CoreFacade rather than a Singleton.
/// Data overview: carries no persisted data. Its only state is the generated action set it owns.
/// Input包装. 只发布"设备层", 不发布其上的任何东西.
/// Subsystem 归属: Core (Input).
/// 存在位置: Core GameObject 上, 由 CoreFacade 要求, 并通过 CoreFacade.Input 访问.
/// 职能: 拥有生成的 PlayerControls 实例, 把主按钮转发为事件, 暴露主轴与指针位置,
/// 它不承载任何玩法语义.
/// 不负责: 哪一轴是什么, 重力与哪一轴对立, 一对轴属于横版还是俯视, 以及游戏如何响应输入 —— 响应属于玩法代码.
/// 也不负责控制方案(control schemes), 重绑定, 以及非键盘设备.
/// 生命周期: 生成的输入集合在 Awake 中创建, 在 OnEnable 订阅, 在 OnDisable 退订, 在 OnDestroy 释放.
/// 组件存活于常驻的 Core 物体上.
/// 使用范式: 无. 它是普通 MonoBehaviour 服务, 通过 CoreFacade 而非单例访问.
/// 数据概览: 不承载持久化数据. 唯一状态是它拥有的生成的输入集合.
/// </summary>
[DisallowMultipleComponent]
public sealed class InputManager : MonoBehaviour
{
    public event Action PrimaryPressed;
    public event Action PrimaryReleased;

    /// <summary>第二操作按钮（当前为 J）；由 Player 决定拾取或交互。</summary>
    public event Action SecondaryPressed;

    /// <summary>Up 按钮按下/松开事件；玩法层决定其含义，当前绑定为空格。</summary>
    public event Action UpPressed;
    public event Action UpReleased;

    public bool IsUpPressed => controls?.Gameplay.Up.IsPressed() ?? false;

    /// <summary>
    /// Pointer position in screen space. Extension point: the template ships no consumer for it.
    /// 屏幕空间的指针位置. 扩展点: 模版不为它附带消费者.
    /// </summary>
    public Vector2 PointerPosition => controls == null
            ? Vector2.zero
            : controls.Gameplay.PointerPosition.ReadValue<Vector2>();

    /// <summary>
    /// The primary two-axis input. Extension point: the template ships no consumer for it.
    /// Its Y axis may be unbound, because the asset belongs to the project rather than to this wrapper.
    /// 主二维轴输入. 扩展点: 模版不为它附带消费者.
    /// 它的 Y 轴可能是空绑定, 因为 asset 属于工程而非本 wrapper.
    /// </summary>
    public Vector2 MovementInput => controls == null
            ? Vector2.zero
            : controls.Gameplay.MovementInput.ReadValue<Vector2>();

    /// <summary>
    /// Whether the primary button is currently held. Extension point: the template ships no consumer for it.
    /// 主按钮当前是否被按住. 扩展点: 模版不为它附带消费者.
    /// </summary>
    public bool IsPrimaryPressed =>
        controls?.Gameplay.PrimaryPress.IsPressed() ?? false;

    /// <summary>
    /// TODO: The template deliberately ships no consumer for MovementInput, PointerPosition, or IsPrimaryPressed;
    /// they are vocabulary, not sentences, and which game reads them is gameplay. Do not add a demo consumer in
    /// the template. Not implemented because the only consumers the project had were early code that is no
    /// longer instantiated.
    /// TODO: 模版刻意不为 MovementInput, PointerPosition, IsPrimaryPressed 附带消费者;
    /// 它们是词汇而非句子, 由哪个游戏读取它们属于玩法. 不要在模版内添加演示用消费者.
    /// 未实现原因: 工程内曾经的使用者都是早期代码, 如今已不再被实例化.
    /// </summary>
    private PlayerControls controls;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Gameplay.PrimaryPress.performed += HandlePrimaryPressed;
        controls.Gameplay.PrimaryPress.canceled += HandlePrimaryReleased;
        controls.Gameplay.Up.performed += HandleUpPressed;
        controls.Gameplay.Up.canceled += HandleUpReleased;
        controls.Gameplay.SecondaryPress.performed += HandleSecondaryPressed;

        controls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        controls.Gameplay.PrimaryPress.performed -= HandlePrimaryPressed;
        controls.Gameplay.PrimaryPress.canceled -= HandlePrimaryReleased;
        controls.Gameplay.Up.performed -= HandleUpPressed;
        controls.Gameplay.Up.canceled -= HandleUpReleased;
        controls.Gameplay.SecondaryPress.performed -= HandleSecondaryPressed;

        controls.Gameplay.Disable();
    }

    private void OnDestroy()
        => controls?.Dispose();

    private void HandlePrimaryPressed(InputAction.CallbackContext context)
        => PrimaryPressed?.Invoke();

    private void HandlePrimaryReleased(InputAction.CallbackContext context)
        => PrimaryReleased?.Invoke();

    private void HandleUpPressed(InputAction.CallbackContext context)
        => UpPressed?.Invoke();

    private void HandleUpReleased(InputAction.CallbackContext context)
        => UpReleased?.Invoke();

    private void HandleSecondaryPressed(InputAction.CallbackContext context)
        => SecondaryPressed?.Invoke();
}
