using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-authored ground corner. Detects the moving wire tail and owns temporary routing anchors.
/// 场景地面拐角：检测线碰撞体与活动端扫掠，按进入方向挂线、反向退绕时释放。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class Corner : MonoBehaviour
{
    [SerializeField] private Anchor anchorPrefab;

    private const float Tolerance = 0.0001f;
    private CircleCollider2D circle;
    private readonly Dictionary<Wire, Hook> hooks = new();
    private readonly List<Wire> staleWires = new();

    private sealed class Hook
    {
        public Anchor Anchor;
        public float Direction;
    }

    public CircleCollider2D DetectionCollider => circle ? circle : circle = GetComponent<CircleCollider2D>();
    public Vector2 Center => DetectionCollider.transform.TransformPoint(DetectionCollider.offset);
    private float Radius => DetectionCollider.radius * Mathf.Max(
        Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
    internal bool CanDetect => isActiveAndEnabled && DetectionCollider.enabled && anchorPrefab;

    private void Reset()
    {
        DetectionCollider.isTrigger = true;
        DetectionCollider.radius = 0.15f;
    }

    private void OnEnable()
    {
        DetectionCollider.isTrigger = true;
        EnvironmentFacade.ForScene(gameObject.scene)?.RegisterCorner(this);
    }

    private void OnDisable()
    {
        ReleaseAll();
        EnvironmentFacade.ForScene(gameObject.scene)?.UnregisterCorner(this);
    }

    public Anchor GetAnchor(Wire wire)
    {
        return wire && hooks.TryGetValue(wire, out Hook hook) ? hook.Anchor : null;
    }

    internal void CleanReleasedAnchors()
    {
        staleWires.Clear();
        foreach (KeyValuePair<Wire, Hook> pair in hooks)
        {
            if (!CanDetect || !pair.Key || !pair.Value.Anchor || !pair.Value.Anchor.IsEngaged)
            {
                staleWires.Add(pair.Key);
            }
        }
        foreach (Wire wire in staleWires)
        {
            Release(wire);
        }
    }

    internal void ReleaseAll()
    {
        foreach (Hook hook in hooks.Values)
        {
            if (hook.Anchor)
            {
                hook.Anchor.ReleaseFromCorner();
            }
        }
        hooks.Clear();
    }

    internal void Release(Wire wire)
    {
        if (!hooks.TryGetValue(wire, out Hook hook))
        {
            return;
        }
        hooks.Remove(wire);
        if (hook.Anchor)
        {
            hook.Anchor.ReleaseFromCorner();
        }
    }

    internal void Engage(EnvironmentFacade environment, Wire wire, Vector2 pivot, Vector2 movement)
    {
        Anchor anchor = Instantiate(anchorPrefab, transform);
        anchor.name = "Corner Anchor (" + wire.name + ")";
        anchor.transform.position = new Vector3(Center.x, Center.y, transform.position.z);
        anchor.gameObject.SetActive(true);
        anchor.HookAtCorner(this, wire);
        hooks.Add(wire, new Hook
        {
            Anchor = anchor,
            Direction = Mathf.Sign(Cross(Center - pivot, movement))
        });
        environment.RegisterAnchor(anchor);
    }

    internal bool TryGetEntry(Wire wire, Vector2 pivot, Vector2 from, Vector2 to, out float time)
    {
        time = 0f;
        if (!CanDetect || GetAnchor(wire) || !wire.PathCollider || !wire.PathCollider.enabled ||
            (to - from).sqrMagnitude <= Tolerance * Tolerance ||
            (Center - pivot).sqrMagnitude <= Radius * Radius)
        {
            return false;
        }

        // Use the real colliders for current contact; the swept triangle also catches a whole
        // crossing between physics ticks, which an Enter/Exit callback alone cannot observe.
        // 真实 Collider 接触 + 帧间扫掠补漏，避免高速移动跨过小圆却没有 Trigger 回调。
        ColliderDistance2D contact = DetectionCollider.Distance(wire.PathCollider);
        float radius = Mathf.Max(0f, Radius - Tolerance);
        bool touchingNow = SegmentDistanceSquared(pivot, to, Center) <= radius * radius;
        if (touchingNow && (!contact.isValid || !contact.isOverlapped))
        {
            return false;
        }
        if (!touchingNow && !SweepTouches(pivot, from, to, Center, radius))
        {
            return false;
        }

        float before = SegmentDistanceSquared(pivot, from, Center);
        if (before <= radius * radius)
        {
            // Already inside on first sample: only inward motion may engage, never motion away.
            return SegmentDistanceSquared(pivot, to, Center) < before - Tolerance * Tolerance;
        }
        if (!SweepTouches(pivot, from, to, Center, radius))
        {
            return false;
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 24; i++)
        {
            float middle = (low + high) * 0.5f;
            if (SweepTouches(pivot, from, Vector2.Lerp(from, to, middle), Center, radius))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }
        time = high;
        return true;
    }

    internal bool TryGetExit(Wire wire, Vector2 pivot, Vector2 from, Vector2 to, out float time)
    {
        time = 0f;
        if (!hooks.TryGetValue(wire, out Hook hook) || (to - from).sqrMagnitude <= Tolerance * Tolerance)
        {
            return false;
        }

        Vector2 axis = Center - pivot;
        Vector2 movement = to - from;
        bool returning = Cross(axis, movement) * hook.Direction < -Tolerance ||
            Vector2.Dot(to - Center, axis) < 0f && Vector2.Dot(movement, axis) < -Tolerance;
        if (!returning || !IsClearOnEntrySide(pivot, to, hook.Direction))
        {
            return false;
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < 24; i++)
        {
            float middle = (low + high) * 0.5f;
            if (IsClearOnEntrySide(pivot, Vector2.Lerp(from, to, middle), hook.Direction))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }
        time = high;
        return true;
    }

    private bool IsClearOnEntrySide(Vector2 pivot, Vector2 end, float direction)
    {
        Vector2 axis = Center - pivot;
        float radius = Radius + Tolerance;
        return SegmentDistanceSquared(pivot, end, Center) > radius * radius &&
            (Cross(axis, end - pivot) * direction < 0f || Vector2.Dot(end - Center, axis) < 0f);
    }

    private static bool SweepTouches(Vector2 pivot, Vector2 from, Vector2 to, Vector2 center, float radius)
    {
        float area = Cross(from - pivot, to - pivot);
        float a = Cross(from - pivot, center - pivot);
        float b = Cross(to - from, center - from);
        float c = Cross(pivot - to, center - to);
        if (Mathf.Abs(area) > Tolerance * Tolerance &&
            (a >= 0f && b >= 0f && c >= 0f || a <= 0f && b <= 0f && c <= 0f))
        {
            return true;
        }
        float squared = radius * radius;
        return SegmentDistanceSquared(pivot, from, center) < squared ||
            SegmentDistanceSquared(pivot, to, center) < squared ||
            SegmentDistanceSquared(from, to, center) < squared;
    }

    private static float SegmentDistanceSquared(Vector2 start, Vector2 end, Vector2 point)
    {
        Vector2 segment = end - start;
        float t = segment.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude) : 0f;
        return (point - start - t * segment).sqrMagnitude;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}
