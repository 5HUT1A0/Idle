using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 护甲系统——装备/拆卸校验、内衬板槽兼容性判定、ArmorSummary 汇总。
/// MVP 仅支持轻型硬质挡板（20% 减伤）。
/// 核心规则：挡板不可单独装备，必须通过内衬的板槽挂载。
/// </summary>
public static class ArmorSystem
{
	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	/// <summary>获取玩家所有已组装的护甲</summary>
	public static List<CustomArmor> GetAllArmors() => DataManager.Instance.CustomArmors;

	/// <summary>按ID获取</summary>
	public static CustomArmor GetArmor(int armorId) =>
		DataManager.Instance.CustomArmors.Find(a => a.ArmorId == armorId);

	/// <summary>获取已拥有的内衬（库存中的LinerData）</summary>
	public static List<InventoryEntry> GetOwnedLiners()
	{
		return DataManager.Instance.Inventory
			 .Where(s => s.Location == "stash")
			 .Select(s => MakeEntry(s))
			 .Where(e => e.Config is LinerData)
			 .ToList();
	}

	/// <summary>获取已拥有的挡板（库存中的PlateData）</summary>
	public static List<InventoryEntry> GetOwnedPlates()
	{
		return DataManager.Instance.Inventory
			 .Where(s => s.Location == "stash")
			 .Select(s => MakeEntry(s))
			 .Where(e => e.Config is PlateData)
			 .ToList();
	}

	/// <summary>获取已拥有的头盔（库存中的HelmetData）</summary>
	public static List<InventoryEntry> GetOwnedHelmets()
	{
		return DataManager.Instance.Inventory
			 .Where(s => s.Location == "stash")
			 .Select(s => MakeEntry(s))
			 .Where(e => e.Config is HelmetData)
			 .ToList();
	}

	// ═══════════════════════════════════════════════
	// 组装 / 拆卸
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 创建一件新护甲——指定内衬。
	/// 返回 CustomArmor 半成品，后续通过 EquipPlate 挂挡板。
	/// 返回 null 表示内衬校验失败或库存不足。
	/// </summary>
	public static CustomArmor CreateArmor(string linerId, string armorName = null)
	{
		var liner = ConfigManager.Instance.GetConfig<LinerData>(linerId);
		if (liner == null)
		{
			GD.PushWarning($"[ArmorSystem] 内衬ID不存在: {linerId}");
			return null;
		}

		//校验库存有货
		var slots = InventorySystem.FindSlots(linerId);
		if (slots.Count == 0)
		{
			GD.PushWarning($"[ArmorSystem] 库存中没有内衬 {linerId}");
			return null;
		}

		//从库存扣减内衬
		if (!InventorySystem.RemoveItem(slots[0].SlotId, 1))
		{
			GD.PushWarning($"[ArmorSystem] 扣减内衬库存失败 {linerId}");
			return null;
		}

		var armor = new CustomArmor
		{
			ArmorName = armorName ?? $"{liner.DisplayName}",
			LinerId = linerId,
			Durability = 100f,
		};
		DataManager.Instance.AddCustomArmor(armor);
		return armor;
	}

	/// <summary>
	/// 给护甲挂载挡板——校验槽位存在、材料兼容、库存有货。
	/// </summary>
	public static bool EquipPlate(int armorId, string plateId, string slotName)
	{
		var armor = GetArmor(armorId);
		if (armor == null)
		{
			GD.PushWarning($"[ArmorSystem] 护甲ID不存在: {armorId}");
			return false;
		}

		var liner = ConfigManager.Instance.GetConfig<LinerData>(armor.LinerId);
		var plate = ConfigManager.Instance.GetConfig<PlateData>(plateId);
		if (liner == null || plate == null)
		{
			GD.PushWarning($"[ArmorSystem] 配置缺失: {armor.LinerId}/{plateId}");
			return false;
		}

		// ① 校验内衬是否有该槽位
		if (!LinerHasSlot(liner, slotName))
		{
			GD.PushWarning($"[ArmorSystem] 内衬 {liner.DisplayName} 没有 {slotName} 槽位");
			return false;
		}

		// ② 校验槽位未被占用
		if (armor.HasPlate(slotName))
		{
			GD.PushWarning($"[ArmorSystem] {slotName} 槽位已被占用，请先卸下");
			return false;
		}

		// ③ 校验材料兼容性
		if (!IsMaterialCompatible(liner.CompatibleMaterials, plate.PlateMaterial))
		{
			GD.PushWarning($"[ArmorSystem] 挡板材料 {plate.PlateMaterial} 与内衬不兼容");
			return false;
		}

		// ④ 校验挡板槽位类型匹配（Front 板装 front 槽，Side 板装 left/right 槽）
		if (!IsSlotTypeMatch(plate.PlateSlotType, slotName))
		{
			GD.PushWarning($"[ArmorSystem] 挡板槽位类型 {plate.PlateSlotType} 与目标槽 {slotName} 不匹配");
			return false;
		}

		// ⑤ 校验库存
		var slots = InventorySystem.FindSlots(plateId);
		if (slots.Count == 0)
		{
			GD.PushWarning($"[ArmorSystem] 库存中没有挡板 {plateId}");
			return false;
		}

		// ⑥ 扣库存 + 装配
		if (!InventorySystem.RemoveItem(slots[0].SlotId, 1))
		{
			GD.PushWarning($"[ArmorSystem] 扣减挡板库存失败 {plateId}");
			return false;
		}
		armor.SetPlate(slotName, plateId);
		GD.Print($"[ArmorSystem] {plate.DisplayName} → {armor.ArmorName} [{slotName}]");
		return true;
	}

	/// <summary>卸下指定槽位的挡板，退回库存</summary>
	public static bool UnequipPlate(int armorId, string slotName)
	{
		var armor = GetArmor(armorId);
		if (armor == null || !armor.HasPlate(slotName))
		{
			GD.PushWarning("[ArmorSystem] 槽位为空，无法卸下");
			return false;
		}

		string plateId = slotName switch
		{
			"front" => armor.FrontPlateId,
			"rear" => armor.RearPlateId,
			"left" => armor.LeftPlateId,
			"right" => armor.RightPlateId,
			_ => null
		};

		if (plateId != null)
		{
			InventorySystem.AddItem(plateId, 1, armor.Durability);
			armor.SetPlate(slotName, null);
			GD.Print($"[ArmorSystem] 已卸下 [{slotName}] 挡板");
		}
		return true;
	}

	/// <summary>装备头盔</summary>
	public static bool EquipHelmet(int armorId, string helmetId)
	{
		var armor = GetArmor(armorId);
		if (armor == null) return false;

		if (!string.IsNullOrEmpty(armor.HelmetId))
		{
			GD.PushWarning("[ArmorSystem] 已装备头盔，请先卸下");
			return false;
		}

		var helmet = ConfigManager.Instance.GetConfig<HelmetData>(helmetId);
		if (helmet == null) return false;

		var slots = InventorySystem.FindSlots(helmetId);
		if (slots.Count == 0) return false;

		if (!InventorySystem.RemoveItem(slots[0].SlotId, 1))
		{
			GD.PushWarning("[ArmorSystem] 扣减头盔库存失败");
			return false;
		}

		armor.HelmetId = helmetId;
		GD.Print($"[ArmorSystem] 头盔 {helmet.DisplayName} 已装备");
		return true;
	}

	/// <summary>卸下头盔</summary>
	public static bool UnequipHelmet(int armorId)
	{
		var armor = GetArmor(armorId);
		if (armor == null || string.IsNullOrEmpty(armor.HelmetId)) return false;

		InventorySystem.AddItem(armor.HelmetId, 1, armor.Durability);
		armor.HelmetId = null;
		return true;
	}

	/// <summary>
	/// 完全拆卸护甲——所有配件退回库存，护甲记录删除。
	/// </summary>
	public static bool Disassemble(int armorId)
	{
		var armor = GetArmor(armorId);
		if (armor == null) return false;

		foreach (var partId in armor.AllPartIds())
		{
			InventorySystem.AddItem(partId, 1, armor.Durability);
		}

		DataManager.Instance.RemoveCustomArmor(armorId);
		GD.Print($"[ArmorSystem] 已拆卸: {armor.ArmorName}");
		return true;
	}

	// ═══════════════════════════════════════════════
	// ArmorSummary 汇总（CombatManager 调用）
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 从已组装护甲构建 ArmorSummary。
	/// 内衬覆盖胸腹（轻型胸挂仅前胸），挡板覆盖对应部位。
	/// </summary>
	public static ArmorSummary GetSummary(CustomArmor armor)
	{
		if (armor == null)
		{
			return new ArmorSummary();//全裸，无覆盖
		}

		var liner = ConfigManager.Instance.GetConfig<LinerData>(armor.LinerId);
		var helmet = !string.IsNullOrEmpty(armor.HelmetId)
			? ConfigManager.Instance.GetConfig<HelmetData>(armor.HelmetId) : null;
		var frontPlate = !string.IsNullOrEmpty(armor.FrontPlateId)
			? ConfigManager.Instance.GetConfig<PlateData>(armor.FrontPlateId) : null;
		var rearPlate = !string.IsNullOrEmpty(armor.RearPlateId)
			? ConfigManager.Instance.GetConfig<PlateData>(armor.RearPlateId) : null;
		var leftPlate = !string.IsNullOrEmpty(armor.LeftPlateId)
			? ConfigManager.Instance.GetConfig<PlateData>(armor.LeftPlateId) : null;
		var rightPlate = !string.IsNullOrEmpty(armor.RightPlateId)
			? ConfigManager.Instance.GetConfig<PlateData>(armor.RightPlateId) : null;

		var summary = new ArmorSummary();

		// ── 头部：由头盔决定
		if (helmet != null)
		{
			summary.HeadCovered = true;
			summary.HeadReduction = helmet.DefenseValue;
		}

		// ── 胸部：内衬基础 + 前板（取较大值覆盖）
		float chestDefense = liner?.BaseChestDefense ?? 0f;
		summary.ChestCovered = liner != null;
		if (frontPlate != null)
		{
			summary.ChestCovered = true;
			chestDefense = Mathf.Max(chestDefense, frontPlate.DefenseValue);
		}
		summary.ChestReduction = chestDefense;

		// ── 腹部：内衬覆盖（轻型胸挂不覆盖腹部）
		summary.AbdomenCovered = liner != null && liner.LinerType != "ChestRig";
		summary.AbdomenReduction = summary.AbdomenCovered ? (liner?.BaseChestDefense ?? 0f) : 0f;

		// ── 手臂：侧板覆盖（左板→左臂，右板→右臂）
		if (leftPlate != null)
		{
			summary.LeftArmCovered = true;
			summary.LeftArmReduction = leftPlate.DefenseValue;
		}
		if (rightPlate != null)
		{
			summary.RightArmCovered = true;
			summary.RightArmReduction = rightPlate.DefenseValue;
		}
		return summary;
	}

	/// <summary>计算护甲总重量</summary>
	public static float CalcTotalWeight(CustomArmor armor)
	{
		float total = 0f;
		foreach (var partId in armor.AllPartIds())
		{
			if (ConfigManager.Instance.GetConfig<BaseItemData>(partId) is GunPartData gp)

			{
				total += gp.Weight;
				// LinerData / PlateData / HelmetData 的重量后续从 BaseItemData 扩展
			}
		}
		return total;
	}

	/// <summary>
	/// MVP 默认护甲摘要——当玩家未装备任何护甲时使用。
	/// 返回全裸状态（无覆盖、无减伤）。
	/// </summary>
	public static ArmorSummary DefaultSummary() => new();

	// ═══════════════════════════════════════════════
	// 内部校验
	// ═══════════════════════════════════════════════

	private static bool LinerHasSlot(LinerData liner, string slotName) => slotName switch
	{
		"front" => liner.HasFrontSlot,
		"rear" => liner.HasRearSlot,
		"left" => liner.HasLeftSlot,
		"right" => liner.HasRightSlot,
		_ => false
	};

	private static bool IsMaterialCompatible(string compatibleMaterials, string plateMaterial)
	{
		if (string.IsNullOrEmpty(compatibleMaterials)) return false;
		var allowed = compatibleMaterials.Split(',', StringSplitOptions.TrimEntries);
		return allowed.Contains(plateMaterial);
	}
	private static bool IsSlotTypeMatch(string plateSlotType, string slotName)
	{
		return (plateSlotType, slotName) switch
		{
			("Front", "front") => true,
			("Rear", "rear") => true,
			("Side", "left") => true,
			("Side", "right") => true,
			_ => false
		};
	}

	private static InventoryEntry MakeEntry(InventorySlot slot)
	{
		var config = ConfigManager.Instance.GetConfig<BaseItemData>(slot.ItemId);
		return new InventoryEntry(slot, config);
	}
}