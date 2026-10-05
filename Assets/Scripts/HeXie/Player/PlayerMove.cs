using UnityEngine;

/// <summary>
/// 2D 横向移动示例：通过 Core 输入服务读取 A/D，在物理帧设置水平速度。
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

    private readonly System.Collections.Generic.List<ContactPoint2D> contacts =
        new System.Collections.Generic.List<ContactPoint2D>();
    private bool jumpRequested;

    private Rigidbody2D body;
    private InputManager input;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // 等所有 Awake 完成后，再访问其他组件的初始化结果。
        CoreFacade core = CoreFacade.Instance;
        if (core == null || core.Input == null)
        {
            Debug.LogError("PlayerMove 需要场景中存在带有 InputManager 的 Core 预制体。", this);
            enabled = false;
            return;
        }

        input = core.Input;
        input.UpPressed += HandleUpPressed;
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.UpPressed += HandleUpPressed;
        }
    }

    private void HandleUpPressed()
    {
        // 只缓存当前落地状态下的按下，不在空中按下后自动落地起跳。
        jumpRequested = Time.timeScale > 0f && IsGrounded();
    }

    private bool IsGrounded()
    {
        if (body.velocity.y > 0.1f)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayers);
        filter.useTriggers = false;
        body.GetContacts(filter, contacts);
        foreach (ContactPoint2D contact in contacts)
        {
            // 仅接受向上的支撑面，避免墙壁或天花板被判定为地面。
            if (contact.normal.y >= 0.65f)
            {
                return true;
            }
        }

        return false;
    }

    private void FixedUpdate()
    {
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
        body.velocity = new Vector2(horizontal * Mathf.Max(0f, moveSpeed), vertical);
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.UpPressed -= HandleUpPressed;
        }
        jumpRequested = false;

        // 禁用移动组件时停止水平移动，但继续保留竖直运动。
        if (body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }
}
