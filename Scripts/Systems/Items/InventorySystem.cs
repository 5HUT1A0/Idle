using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 仓库管理——物品进出、仓库↔身上转移、查询。
/// 数据走 DataManager 内存缓存，持久化由 SaveManager 处理。
/// </summary>
public static class InventorySystem
{
	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	/// <summary>获取仓库所有物品（合并 Config 属性 + Save 数量）</summary>
	public static List<InventoryEntry> GetAllItems()
	{
		return DataManager.Instance.Inventory.Select(slot => MakeEntry(slot)).ToList();
	}

	/// <summary>按位置查询</summary>
	public static List<InventoryEntry> GetByLocation(string location) //stash=仓库/loadout=身上
	{
		return DataManager.Instance.Inventory
			.Where(s => s.Location == location)
			.Select(MakeEntry)
			.ToList();
	}

	/// <summary>按物品 ID 查询</summary>
	public static List<InventorySlot> FindSlots(string itemId)
	{
		return DataManager.Instance.Inventory
		   .Where(s => s.ItemId == itemId)
		   .ToList();
	}

	/// <summary>获取某槽位的已拥有配件（CustomGunSystem使用）</summary>
	public static List<InventoryEntry> GetOwnedParts(PartSlot slot)
	{
		return DataManager.Instance.Inventory
			.Where(s => s.Location == "stash")
			.Select(s => MakeEntry(s))
			.Where(e => e.Config is GunPartData gp && gp.Slot == slot)
			.ToList();
	}

	// ═══════════════════════════════════════════════
	// 增删
	// ═══════════════════════════════════════════════

	/// <summary>添加物品。优先堆叠到已有同 ID 槽位，超量新建。</summary>
	public static bool AddItem(string itemId, int quantity, float durability = 100f)
	{
		if (quantity <= 0) return false;

		var config = ConfigManager.Instance.GetConfig<BaseItemData>(itemId);
		if (config == null)
		{
			GD.PushWarning($"[InventorySystem] 物品ID不存在: {itemId}");
			return false;
		}

		int remaining = quantity;

		// 先尝试堆叠到已有槽位
		if (config.MaxStack > 1)
		{
			var stackableSlots = DataManager.Instance.Inventory
				.Where(s => s.ItemId == itemId && s.Location == "stash" && s.Quantity < config.MaxStack);

			foreach (var slot in stackableSlots)
			{
				int canAdd = config.MaxStack - slot.Quantity;
				int toAdd = Mathf.Min(canAdd, remaining);
				slot.Quantity += toAdd;
				remaining -= toAdd;
				if (remaining <= 0) break;
			}
		}

		// 如果还有剩余数量，创建新槽位
		while (remaining > 0)
		{
			int stackSize = Mathf.Min(remaining, config.MaxStack);
			var newSlot = new InventorySlot
			{
				ItemId = itemId,
				Quantity = stackSize,
				Durability = (config is GunPartData || config is AmmoData) ? durability : 0f,
				Location = "stash"
			};
			DataManager.Instance.AddInventorySlot(newSlot);
			remaining -= stackSize;
		}

		GD.Print($"[InventorySystem] 添加物品: +{quantity}{itemId} ");
		return true;
	}

	/// <summary>从指定槽位移除指定数量，数量归零则删除槽位</summary>
	public static bool RemoveItem(int slotId, int quantity)
	{
		var slot = DataManager.Instance.Inventory.Find(s => s.SlotId == slotId);
		if (slot == null || slot.Quantity < quantity)
		{
			GD.PushWarning($"[InventorySystem] 移除物品失败: 槽位不存在或数量不足 SlotId={slotId}, Quantity={quantity}");
			return false;
		}

		slot.Quantity -= quantity;
		if (slot.Quantity <= 0)
		{
			DataManager.Instance.RemoveInventorySlot(slotId);
		}
		return true;
	}

	// ═══════════════════════════════════════════════
	// 转移
	// ═══════════════════════════════════════════════

	/// <summary>仓库 → 身上</summary>
	public static bool MoveToLoadout(int slotId)
	{
		var slot = DataManager.Instance.Inventory.Find(s => s.SlotId == slotId);
		if (slot == null || slot.Location != "stash")
		{
			GD.PushWarning($"[InventorySystem] 转移物品失败: 槽位不存在或不在仓库 SlotId={slotId}");
			return false;
		}
		slot.Location = "loadout";
		return true;
	}

	/// <summary>身上 → 仓库</summary>
	public static bool MoveToStash(int slotId)
	{
		var slot = DataManager.Instance.Inventory.Find(s => s.SlotId == slotId);
		if (slot == null || slot.Location != "loadout")
		{
			GD.PushWarning($"[InventorySystem] 转移物品失败: 槽位不存在或不在身上 SlotId={slotId}");
			return false;
		}
		slot.Location = "stash";
		return true;
	}

	public static void InitDefultItems()
	{
		AddItem("body_ar_t1", 1);
		AddItem("barrel_ar_standard_t1", 1);
		AddItem("mag_ar_standard_t1", 3);
	}

	// ═══════════════════════════════════════════════
	// 工具
	// ═══════════════════════════════════════════════

	private static InventoryEntry MakeEntry(InventorySlot slot)
	{
		var config = ConfigManager.Instance.GetConfig<BaseItemData>(slot.ItemId);
		return new InventoryEntry(slot, config);
	}

}


/// <summary>合并显示条目：配置属性 + 运行时数据</summary>
public class InventoryEntry
{
	public InventorySlot Slot { get; }
	public BaseItemData Config { get; }

	public string DisplayName => Config?.DisplayName ?? Slot.ItemId;
	public string ItemId => Slot.ItemId;
	public int Quantity => Slot.Quantity;
	public float Durability => Slot.Durability;
	public int MaxStack => Config?.MaxStack ?? 99;
	public Texture2D Icon => Config?.Icon;

	public InventoryEntry(InventorySlot slot, BaseItemData config)
	{
		Slot = slot;
		Config = config;
	}
}
