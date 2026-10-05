using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 显示背包内 Env 道具的 SpriteRenderer.sprite，不改变原道具。
/// 场景中的槽位由 uGUI GridLayoutGroup 排列，物品增加时复用并扩展槽位。
/// 最终道具图标的数据来源由 Jill 决定。
/// </summary>
[DisallowMultipleComponent]
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private RectTransform slotsRoot;
    [SerializeField] private RectTransform slotTemplate;
    [SerializeField, Min(1)] private int minimumSlots = 8;

    private readonly List<RectTransform> slots = new List<RectTransform>();
    private readonly List<UnityEngine.UI.Image> icons = new List<UnityEngine.UI.Image>();

    private void Awake()
    {
        CacheSlots();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void CacheSlots()
    {
        slots.Clear();
        icons.Clear();
        if (slotsRoot == null)
        {
            return;
        }

        foreach (Transform child in slotsRoot)
        {
            Transform icon = child.Find("Icon");
            if (icon == null)
            {
                continue;
            }

            slots.Add((RectTransform)child);
            icons.Add(icon.GetComponent<UnityEngine.UI.Image>());
        }
    }

    /// <summary>刷新引用与 Sprite；无物品或无 Sprite 时仅显示灰色槽位。</summary>
    public void Refresh()
    {
        if (slotsRoot == null || slotTemplate == null)
        {
            return;
        }

        if (slots.Count == 0)
        {
            CacheSlots();
        }

        IReadOnlyList<GameObject> items = inventory != null ? inventory.Items : null;
        int count = items != null ? items.Count : 0;
        int visibleCount = Mathf.Max(minimumSlots, count);
        while (slots.Count < visibleCount)
        {
            RectTransform slot = Instantiate(slotTemplate, slotsRoot);
            slot.name = "Slot" + (slots.Count + 1).ToString("00");
            slots.Add(slot);
            icons.Add(slot.Find("Icon").GetComponent<UnityEngine.UI.Image>());
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool visible = i < visibleCount;
            if (slots[i].gameObject.activeSelf != visible)
            {
                slots[i].gameObject.SetActive(visible);
            }

            Sprite sprite = null;
            if (visible && i < count && items[i] != null)
            {
                SpriteRenderer renderer = items[i].GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    sprite = renderer.sprite;
                }
            }

            UnityEngine.UI.Image icon = icons[i];
            if (icon == null)
            {
                continue;
            }

            if (icon.sprite != sprite)
            {
                icon.sprite = sprite;
            }
            icon.enabled = sprite != null;
        }
    }
}
