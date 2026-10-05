using System;
using UnityEngine;

[Serializable]
public class DragAndDropService2D
{
    private InputManager _input;
    private Rigidbody2D _rb;
    private Collider2D _cld;
    private Camera _dragCamera;
    
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
        _input = input;
        _rb = rb;
        _cld = cld;
        _dragCamera = dragCamera ?? Camera.main;
    }

    public void Enable()
    {
        _input.PrimaryPressed += BeginDrag;
        _input.PrimaryReleased += EndDrag;
    }

    public void Disable()
    {
        _input.PrimaryPressed -= BeginDrag;
        _input.PrimaryReleased -= EndDrag;
        
        if (IsDragging)
            EndDrag();
    }
    
    public void BeginDrag()
    {
        if (!allowDragging) return;
        
        pointerWorld = GetPointerWorldPosition();
        
        if (!_cld.OverlapPoint(pointerWorld)) return;
        
        IsDragging = true;

        cachedRbType = _rb.bodyType;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;


        dragOffset = retainDragOffset
            ? _rb.position - pointerWorld
            : Vector2.zero;
    }

    public void Drag()
    {
        if (!allowDragging || !IsDragging) return;
        
        pointerWorld = GetPointerWorldPosition();
        _rb.MovePosition(pointerWorld + dragOffset);
    }

    public void EndDrag()
    {
        if (!IsDragging) return;
        
        IsDragging = false;
        
        _rb.bodyType = cachedRbType;
    }
    
    private Vector2 GetPointerWorldPosition()
    {
        var screen = _input.PointerPosition;

        var world = _dragCamera.ScreenToWorldPoint(
            new Vector3(
                screen.x,
                screen.y,
                Mathf.Abs(_dragCamera.transform.position.z -
                    _rb.transform.position.z)));

        return world;
    }
}
