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

    private sealed class SlotView
    {
        public readonly RectTransform Root;
        public readonly UnityEngine.UI.Image Icon;

        public SlotView(RectTransform root, UnityEngine.UI.Image icon)
        {
            Root = root;
            Icon = icon;
        }
    }

    private readonly List<SlotView> slots = new List<SlotView>();
    private const float SpriteCheckInterval = 0.1f;
    private PlayerInventory subscribedInventory;
    private bool refreshRequested = true;
    private float nextSpriteCheckTime;
    private int displayedItemCount;
    private int displayedMinimumSlots;

    private void Awake()
    {
        CacheSlots();
    }

    private void LateUpdate()
    {
        BindInventory();
        if (refreshRequested || displayedMinimumSlots != minimumSlots ||
            (inventory == null && displayedItemCount > 0))
        {
            Refresh();
        }
        else if (Time.unscaledTime >= nextSpriteCheckTime)
        {
            CheckItemSprites();
        }
    }

    private void OnEnable()
    {
        BindInventory();
        RequestRefresh();
    }

    private void OnDisable()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.Changed -= RequestRefresh;
        }
        subscribedInventory = null;
    }

    private void BindInventory()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (subscribedInventory == inventory)
        {
            return;
        }

        if (subscribedInventory != null)
        {
            subscribedInventory.Changed -= RequestRefresh;
        }
        subscribedInventory = inventory;
        if (subscribedInventory != null)
        {
            subscribedInventory.Changed += RequestRefresh;
        }
        RequestRefresh();
    }

    private void RequestRefresh()
    {
        refreshRequested = true;
    }

    private void CheckItemSprites()
    {
        nextSpriteCheckTime = Time.unscaledTime + SpriteCheckInterval;
        // SpriteRenderer 不提供 Sprite 变更事件，低频检查也会清理背包已销毁引用。
        IReadOnlyList<GameObject> items = inventory != null ? inventory.Items : null;
        int count = items != null ? items.Count : 0;
        if (refreshRequested || count != displayedItemCount)
        {
            Refresh();
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Sprite sprite = GetItemSprite(items[i]);
            UnityEngine.UI.Image icon = slots[i].Icon;
            if (icon != null && (icon.sprite != sprite || icon.enabled != (sprite != null)))
            {
                UpdateIcon(icon, sprite);
            }
        }
    }

    private void CacheSlots()
    {
        slots.Clear();
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

            slots.Add(new SlotView((RectTransform)child, icon.GetComponent<UnityEngine.UI.Image>()));
        }
    }

    /// <summary>刷新引用与 Sprite；无物品或无 Sprite 时仅显示灰色槽位。</summary>
    public void Refresh()
    {
        BindInventory();
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
        ExtendSlots(visibleCount);

        for (int i = 0; i < slots.Count; i++)
        {
            bool visible = i < visibleCount;
            SetSlotVisible(slots[i].Root, visible);

            Sprite sprite = visible && i < count ? GetItemSprite(items[i]) : null;

            UnityEngine.UI.Image icon = slots[i].Icon;
            UpdateIcon(icon, sprite);
        }

        displayedItemCount = count;
        displayedMinimumSlots = minimumSlots;
        nextSpriteCheckTime = Time.unscaledTime + SpriteCheckInterval;
        refreshRequested = false;
    }

    private void SetSlotVisible(RectTransform slot, bool visible)
    {
        if (slot.gameObject.activeSelf != visible)
        {
            slot.gameObject.SetActive(visible);
        }
    }

    private Sprite GetItemSprite(GameObject item)
    {
        if (item == null)
        {
            return null;
        }

        SpriteRenderer renderer = item.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    private void ExtendSlots(int visibleCount)
    {
        while (slots.Count < visibleCount)
        {
            RectTransform slot = Instantiate(slotTemplate, slotsRoot);
            slot.name = "Slot" + (slots.Count + 1).ToString("00");
            slots.Add(new SlotView(slot, slot.Find("Icon").GetComponent<UnityEngine.UI.Image>()));
        }
    }

    private void UpdateIcon(UnityEngine.UI.Image icon, Sprite sprite)
    {
        if (icon == null)
        {
            return;
        }

        if (icon.sprite != sprite)
        {
            icon.sprite = sprite;
        }
        icon.enabled = sprite != null;
    }
}
