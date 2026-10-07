using UnityEngine;

/// <summary>Applies accepted movement input to the sprite without changing physics or attachment transforms.
/// 根据已接受的移动输入更新精灵朝向，不改变碰撞体或挂点的 Transform。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class PlayerVisual : MonoBehaviour
{
    [SerializeField, Tooltip("未翻转的原始精灵是否朝右。")]
    private bool spriteFacesRight = true;

    [SerializeField, Tooltip("可选的朝向标记，必须是 Visual 的子对象。")]
    private Transform facingMarker;

    private SpriteRenderer spriteRenderer;
    private Vector3 markerPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (facingMarker != null && facingMarker.IsChildOf(transform) && facingMarker != transform)
        {
            markerPosition = facingMarker.localPosition;
        }
        else
        {
            facingMarker = null;
        }
    }

    public void ApplyMovementInput(float horizontal)
    {
        if (!isActiveAndEnabled || Mathf.Abs(horizontal) <= 0.01f)
        {
            return;
        }

        spriteRenderer.flipX = (horizontal > 0f) != spriteFacesRight;
        if (facingMarker != null)
        {
            facingMarker.localPosition = new Vector3(
                spriteRenderer.flipX ? -markerPosition.x : markerPosition.x, markerPosition.y, markerPosition.z);
        }
    }
}
