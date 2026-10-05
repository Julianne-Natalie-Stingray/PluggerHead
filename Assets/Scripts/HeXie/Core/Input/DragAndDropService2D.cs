using System;
using UnityEngine;

/// <summary>
/// 将输入转换为 2D 刚体拖拽；宿主负责初始化、订阅生命周期及物理帧更新。
/// 拖拽期间临时使用 Kinematic，结束或禁用时恢复原刚体类型。
/// </summary>
[Serializable]
public class DragAndDropService2D
{
    private InputManager input;
    private Rigidbody2D body;
    private Collider2D dragCollider;
    private Camera dragCamera;
    private InputManager subscribedInput;

    [SerializeField] private bool allowDragging = true;
    public bool AllowDragging => allowDragging;

    [SerializeField] private bool retainDragOffset = true;

    public bool IsDragging { get; private set; }
    private Vector2 dragOffset;
    private Vector2 pointerWorld;
    private RigidbodyType2D cachedRbType;

    /// <summary>
    /// Fallback to main camera if no camera is provided.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="rb"></param>
    /// <param name="cld"></param>
    /// <param name="dragCamera"></param>
    public void Initialize(
        InputManager input,
        Rigidbody2D rb,
        Collider2D cld,
        Camera dragCamera = null)
    {
        Disable();
        this.input = input;
        body = rb;
        dragCollider = cld;
        this.dragCamera = dragCamera != null ? dragCamera : Camera.main;
    }

    public void Enable()
    {
        if (subscribedInput == input)
        {
            return;
        }

        Disable();
        if (input == null)
        {
            return;
        }

        subscribedInput = input;
        subscribedInput.PrimaryPressed += BeginDrag;
        subscribedInput.PrimaryReleased += EndDrag;
    }

    public void Disable()
    {
        if (subscribedInput != null)
        {
            subscribedInput.PrimaryPressed -= BeginDrag;
            subscribedInput.PrimaryReleased -= EndDrag;
        }

        subscribedInput = null;
        EndDrag();
    }

    public void BeginDrag()
    {
        if (!allowDragging || IsDragging || input == null || body == null ||
            dragCollider == null || dragCamera == null)
        {
            return;
        }

        pointerWorld = GetPointerWorldPosition();

        if (!dragCollider.OverlapPoint(pointerWorld))
        {
            return;
        }

        IsDragging = true;

        cachedRbType = body.bodyType;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;


        dragOffset = retainDragOffset
            ? body.position - pointerWorld
            : Vector2.zero;
    }

    public void Drag()
    {
        if (!allowDragging || !IsDragging || input == null || body == null || dragCamera == null)
        {
            EndDrag();
            return;
        }

        pointerWorld = GetPointerWorldPosition();
        body.MovePosition(pointerWorld + dragOffset);
    }

    public void EndDrag()
    {
        if (!IsDragging)
        {
            return;
        }

        IsDragging = false;

        if (body != null)
        {
            body.bodyType = cachedRbType;
        }
    }

    private Vector2 GetPointerWorldPosition()
    {
        Vector2 screen = input.PointerPosition;

        Vector3 world = dragCamera.ScreenToWorldPoint(
            new Vector3(
                screen.x,
                screen.y,
                Mathf.Abs(dragCamera.transform.position.z -
                    body.transform.position.z)));

        return world;
    }
}
