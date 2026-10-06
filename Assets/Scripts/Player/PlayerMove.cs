using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D 横向移动：通过 Core 输入服务读取 A/D，在物理帧设置水平速度。
/// 挂在带有 Dynamic Rigidbody2D 的玩家上；场景中需要 Core 预制体。
/// Core 的 Up 按下事件触发落地跳跃（当前绑定为空格），重力负责下落。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class PlayerMove : MonoBehaviour
{
    [SerializeField, Min(0f)]
    [Tooltip("水平移动速度，单位：Unity 单位/秒。")]
    private float moveSpeed = 5f;

    [SerializeField, Min(0f)]
    [Tooltip("起跳时的竖直速度，单位：Unity 单位/秒。")]
    private float jumpSpeed = 8f;

    [SerializeField]
    [Tooltip("可作为地面的碰撞层。玩家需要非 Trigger 的 Collider2D。")]
    private LayerMask groundLayers = ~0;

    private readonly List<ContactPoint2D> contacts = new List<ContactPoint2D>();
    private const float GroundedUpwardSpeedThreshold = 0.1f;
    private const float MinimumGroundNormalY = 0.65f;
    private const float MovementAnimationThreshold = 0.01f;
    private bool jumpRequested;

    private Rigidbody2D body;
    private InputManager input;
    private Animator animator;
    private static readonly int MovingParameter = Animator.StringToHash("tryMoving");
    private static readonly int DashParameter = Animator.StringToHash("Dash");
    private static readonly int InteractParameter = Animator.StringToHash("Interact");
    private bool isDashInputLocked;
    private bool isInteractionInputLocked;
    private bool isManuallyInputLocked;

    /// <summary>手动锁或动画锁任一生效时，停止处理移动和跳跃输入。</summary>
    public bool IsInputLocked => isManuallyInputLocked || isDashInputLocked || isInteractionInputLocked;

    /// <summary>
    /// 启动时绑定关卡 Env 的实际阻力查询，也允许调用方覆盖；传入玩家世界坐标，返回由 AddForce 施加的力。
    /// 返回 Vector2.negativeInfinity 时死亡；普通阻力不影响动画输入。
    /// </summary>
    public Func<Vector2, Vector2> GetResistance { protected get; set; }

    public bool IsDead { get; private set; }

    /// <summary>玩家死亡并停用后通知，仅触发一次。</summary>
    public event Action Died;

    public void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        LockInput();
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
        gameObject.SetActive(false);
        Died?.Invoke();
    }

    /// <summary>设置手动输入锁；不会修改冲刺或物理系统施加的速度。</summary>
    public void LockInput(bool locked = true)
    {
        isManuallyInputLocked = locked;
        if (locked)
        {
            jumpRequested = false;
        }
    }

    public void UnlockInput()
    {
        LockInput(false);
    }

    /// <summary>请求冲刺动画；冲刺位移由动作实现负责，不在动画回调中重复触发。</summary>
    public bool TryStartDashAnimation()
    {
        return TryTriggerAnimation(DashParameter);
    }

    /// <summary>在拾取或选中请求成功后调用。</summary>
    public bool TryStartInteractionAnimation()
    {
        return TryTriggerAnimation(InteractParameter);
    }

    private bool TryTriggerAnimation(int trigger)
    {
        if (!isActiveAndEnabled || IsInputLocked || Time.timeScale <= 0f || !CanUseAnimator())
        {
            return false;
        }

        animator.ResetTrigger(DashParameter);
        animator.ResetTrigger(InteractParameter);
        animator.SetTrigger(trigger);
        return true;
    }

    private void UpdateMovementAnimation(bool moving)
    {
        if (CanUseAnimator())
        {
            animator.SetBool(MovingParameter, moving);
        }
    }

    private bool CanUseAnimator()
    {
        return animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.isInitialized;
    }

    public void OnDashStarted()
    {
        SetAnimationInputLock(ref isDashInputLocked, true);
    }

    public void OnDashEnded()
    {
        SetAnimationInputLock(ref isDashInputLocked, false);
    }

    public void OnInteractionStarted()
    {
        SetAnimationInputLock(ref isInteractionInputLocked, true);
    }

    public void OnInteractionEnded()
    {
        SetAnimationInputLock(ref isInteractionInputLocked, false);
    }

    private void SetAnimationInputLock(ref bool source, bool locked)
    {
        source = locked;
        if (locked)
        {
            jumpRequested = false;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>(true);
        PlayerAnimationCallbacks callbacks = GetComponent<PlayerAnimationCallbacks>();
        if (callbacks == null)
        {
            callbacks = gameObject.AddComponent<PlayerAnimationCallbacks>();
        }
        callbacks.Synchronize();
    }

    private void Start()
    {
        // 复用交互系统的 Env 关联，由环境提供具体移动规则。
        if (GetResistance == null)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory != null && inventory.Environment != null)
            {
                GetResistance = inventory.Environment.GetResistance;
            }
            else
            {
                Debug.LogError("PlayerMove 需要关卡的 EnvironmentFacade 阻力查询，或显式指定 GetResistance。", this);
            }
        }

        // 等所有 Awake 完成后，再访问其他组件的初始化结果。
        CoreFacade core = CoreFacade.Instance;
        if (core == null || core.Input == null)
        {
            Debug.LogError("PlayerMove 需要场景中存在带有 InputManager 的 Core 预制体。", this);
            enabled = false;
            return;
        }

        input = core.Input;
        BindInput();
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void BindInput()
    {
        if (input != null)
        {
            input.UpPressed -= HandleUpPressed;
            input.UpPressed += HandleUpPressed;
        }
    }

    private void HandleUpPressed()
    {
        // 只缓存当前落地状态下的按下，不在空中按下后自动落地起跳。
        jumpRequested = !IsInputLocked && Time.timeScale > 0f && IsGrounded();
    }

    private bool IsGrounded()
    {
        if (body.velocity.y > GroundedUpwardSpeedThreshold)
        {
            return false;
        }

        ReadGroundContacts();
        foreach (ContactPoint2D contact in contacts)
        {
            // 仅接受向上的支撑面，避免墙壁或天花板被判定为地面。
            if (contact.normal.y >= MinimumGroundNormalY)
            {
                return true;
            }
        }

        return false;
    }

    private void ReadGroundContacts()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;
        body.GetContacts(filter, contacts);
    }

    private bool CheckGroundPolarity()
    {
        ReadGroundContacts();
        foreach (ContactPoint2D contact in contacts)
        {
            // 与跳跃接地使用相同的支撑面标准；墙壁与天花板不算踩到地面。
            if (contact.normal.y < MinimumGroundNormalY)
            {
                continue;
            }

            Collider2D surface = contact.collider.attachedRigidbody == body
                ? contact.otherCollider
                : contact.collider;
            GroundPolarity ground = surface.GetComponentInParent<GroundPolarity>();
            if (ground == null || !ground.isActiveAndEnabled)
            {
                continue;
            }

            PlayerInventory inventory = GetComponent<PlayerInventory>();
            EnvironmentFacade environment = inventory != null
                ? inventory.Environment
                : EnvironmentFacade.ForScene(gameObject.scene);
            Wire wire = environment != null ? environment.HeldWire : null;
            if (wire != null && wire.IsHeld && ground.IsOppositeTo(wire.Polarity))
            {
                Die();
                return true;
            }
        }

        return false;
    }

    private void FixedUpdate()
    {
        if (IsDead)
        {
            return;
        }

        // 站立时换线也要重新检查；输入锁不会免除地面危险。
        if (CheckGroundPolarity())
        {
            return;
        }

        Vector2 resistance = GetResistance != null ? GetResistance(body.position) : Vector2.zero;
        // 不使用 Vector2 的近似相等运算比较无穷值。
        if (float.IsNegativeInfinity(resistance.x) && float.IsNegativeInfinity(resistance.y))
        {
            Die();
            return;
        }

        if (IsInputLocked)
        {
            UpdateMovementAnimation(false);
            jumpRequested = false;
            body.AddForce(resistance);
            return;
        }

        float horizontal = input != null && input.isActiveAndEnabled
            ? Mathf.Clamp(input.MovementInput.x, -1f, 1f)
            : 0f;

        // velocity 已是每秒速度，不再乘以 fixedDeltaTime。
        float vertical = body.velocity.y;
        if (jumpRequested && input != null && input.isActiveAndEnabled && IsGrounded())
        {
            vertical = Mathf.Max(0f, jumpSpeed);
        }

        jumpRequested = false;
        Vector2 movement = new Vector2(horizontal * Mathf.Max(0f, moveSpeed), vertical);
        body.velocity = GetResistance != null ? movement : new Vector2(0f, body.velocity.y);
        body.AddForce(resistance);
        UpdateMovementAnimation(Mathf.Abs(horizontal) > MovementAnimationThreshold);
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.UpPressed -= HandleUpPressed;
        }
        jumpRequested = false;

        UpdateMovementAnimation(false);
        if (CanUseAnimator())
        {
            animator.ResetTrigger(DashParameter);
            animator.ResetTrigger(InteractParameter);
        }

        // 禁用移动组件时停止水平移动，但继续保留竖直运动。
        if (body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }
}
