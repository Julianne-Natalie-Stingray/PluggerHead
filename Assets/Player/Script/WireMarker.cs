using UnityEngine;

/// <summary>独立向世界上方浮动，在一秒游戏时间后销毁；暂停时同步暂停。</summary>
[DisallowMultipleComponent]
public sealed class WireMarker : MonoBehaviour
{
    private const float Lifetime = 1f;
    private const float FloatSpeed = 1f;

    private void Start()
    {
        Destroy(gameObject, Lifetime);
    }

    private void Update()
    {
        transform.position += Vector3.up * (FloatSpeed * Time.deltaTime);
    }
}
