using UnityEngine;

/// <summary>面板激活时持有冻结请求；隐藏、父对象停用或销毁时自动释放。</summary>
[DisallowMultipleComponent]
public sealed class FreezeWhileVisible : MonoBehaviour
{
    public static void Attach(GameObject panel)
    {
        if (panel != null && panel.GetComponent<FreezeWhileVisible>() == null)
        {
            panel.AddComponent<FreezeWhileVisible>();
        }
    }

    private void OnEnable()
    {
        GameStateManager.RequestFreeze(this);
    }

    private void OnDisable()
    {
        GameStateManager.ReleaseFreeze(this);
    }
}
