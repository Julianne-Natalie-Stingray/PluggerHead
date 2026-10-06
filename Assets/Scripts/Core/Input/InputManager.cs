using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Thin input wrapper. Publishes the raw device layer and nothing above it.
/// Subsystem: Core (Input).
/// Where it lives: on the Core GameObject, required by CoreFacade and reached through CoreFacade.Input.
/// Responsibility: own the generated PlayerControls instance, expose movement/pointer and held-state queries,
/// and forward Primary, Up, Secondary and Tertiary button events. The generated action set stays private;
/// gameplay meaning belongs to consumers.
/// Does NOT own: what either axis means, which axis gravity opposes, whether an axis pair is a platformer or a
/// top-down layout, or how the game reacts to any input -- the response belongs in gameplay code. It also does
/// not implement control schemes or rebinding; device bindings come from the generated source asset.
/// Lifetime: the generated action set is created in Awake, subscribed in OnEnable and unsubscribed in OnDisable,
/// and disposed in OnDestroy. The component lives on the persistent Core object.
/// Paradigms: none. It is a plain MonoBehaviour service, reached through CoreFacade rather than a Singleton.
/// Data overview: carries no persisted data. Its only state is the generated action set it owns.
/// Input包装. 只发布"设备层", 不发布其上的任何东西.
/// Subsystem 归属: Core (Input).
/// 存在位置: Core GameObject 上, 由 CoreFacade 要求, 并通过 CoreFacade.Input 访问.
/// 职能: 拥有生成的 PlayerControls 实例, 暴露移动/指针及按住状态查询,
/// 转发 Primary、Up、Secondary、Tertiary 按钮事件; 生成集合保持私有, 玩法语义由消费者决定.
/// 不负责: 哪一轴是什么, 重力与哪一轴对立, 一对轴属于横版还是俯视, 以及游戏如何响应输入 —— 响应属于玩法代码.
/// 不实现控制方案或重绑定; 设备绑定来自生成代码的源资产, 当前包含键盘和鼠标.
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

    /// <summary>第二操作按钮（当前为 J）；由 Player 决定收回、拾取或交互。</summary>
    public event Action SecondaryPressed;

    /// <summary>第三操作按钮（当前为 K）；由 Player 解释为放置 Anchor。</summary>
    public event Action TertiaryPressed;

    /// <summary>Up 按钮按下/松开事件；玩法层决定其含义，当前绑定为空格。</summary>
    public event Action UpPressed;
    public event Action UpReleased;

    public bool IsUpPressed => controls?.Gameplay.Up.IsPressed() ?? false;

    /// <summary>
    /// Pointer position in screen space; used by DragAndDropService2D when a host enables that service.
    /// 屏幕空间的指针位置; 宿主启用拖拽服务时由 DragAndDropService2D 读取.
    /// </summary>
    public Vector2 PointerPosition => controls == null
            ? Vector2.zero
            : controls.Gameplay.PointerPosition.ReadValue<Vector2>();

    /// <summary>
    /// Movement vector consumed by PlayerMove. The current asset binds A/D on X and leaves Y unbound.
    /// 由 PlayerMove 读取的移动向量; 当前资产把 A/D 绑定到 X, Y 未绑定.
    /// </summary>
    public Vector2 MovementInput => controls == null
            ? Vector2.zero
            : controls.Gameplay.MovementInput.ReadValue<Vector2>();

    /// <summary>
    /// Whether the primary button is currently held; false before the generated controls exist.
    /// 主按钮当前是否被按住; 生成的 controls 尚未创建时返回 false.
    /// </summary>
    public bool IsPrimaryPressed =>
        controls?.Gameplay.PrimaryPress.IsPressed() ?? false;

    /// <summary>
    /// Private generated action set. This service does not disable it based on GameState;
    /// gameplay consumers are responsible for pause and input-lock decisions.
    /// 私有生成动作集合. 本服务不按 GameState 禁用输入; 消费方负责暂停和输入锁判断.
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
        controls.Gameplay.TertiaryPress.performed += HandleTertiaryPressed;

        controls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        controls.Gameplay.PrimaryPress.performed -= HandlePrimaryPressed;
        controls.Gameplay.PrimaryPress.canceled -= HandlePrimaryReleased;
        controls.Gameplay.Up.performed -= HandleUpPressed;
        controls.Gameplay.Up.canceled -= HandleUpReleased;
        controls.Gameplay.SecondaryPress.performed -= HandleSecondaryPressed;
        controls.Gameplay.TertiaryPress.performed -= HandleTertiaryPressed;

        controls.Gameplay.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void HandlePrimaryPressed(InputAction.CallbackContext context)
    {
        PrimaryPressed?.Invoke();
    }

    private void HandlePrimaryReleased(InputAction.CallbackContext context)
    {
        PrimaryReleased?.Invoke();
    }

    private void HandleUpPressed(InputAction.CallbackContext context)
    {
        UpPressed?.Invoke();
    }

    private void HandleUpReleased(InputAction.CallbackContext context)
    {
        UpReleased?.Invoke();
    }

    private void HandleSecondaryPressed(InputAction.CallbackContext context)
    {
        SecondaryPressed?.Invoke();
    }

    private void HandleTertiaryPressed(InputAction.CallbackContext context)
    {
        TertiaryPressed?.Invoke();
    }
}
