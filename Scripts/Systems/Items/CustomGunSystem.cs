using Godot;
using System.Collections.Generic;
using System.Linq;

public static class CustomGunSystem
{
	/// <summary>五类别基础最优射程</summary>
	private static readonly Dictionary<GunCategory, float> BaseOptimalRange = new()
	{
		[GunCategory.AR] = 80f,
		[GunCategory.Shotgun] = 15f,
		[GunCategory.DMR] = 115f,
		[GunCategory.SMG] = 40f,
		[GunCategory.Sniper] = 150f
	};

	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	/// <summary>获取玩家所有已组装的枪</summary>
	public static List<CustomGun> GetAllGuns() => DataManager.Instance.CustomGuns;

	/// <summary>获取玩家已组装的枪，按 GunId 排序</summary>
	public static CustomGun GetGun(int gunId) => DataManager.Instance.CustomGuns.Find(g => g.GunId == gunId);

	// ═══════════════════════════════════════════════
	// 组装
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 尝试组装一把枪。Body + Barrel + Magazine 缺一不可。
	/// 校验通过后从库存扣减配件，生成新 CustomGun。
	/// 返回 null 表示校验失败。
	/// </summary>
	public static CustomGun TryAssemble(string bodyId, string barrelId, string magazineId,
		  string sightId = null, string muzzleId = null, string gunName = null)
	{
		//校验必备三件
		var body = ConfigManager.Instance.GetConfig<GunPartData>(bodyId);
		var barrel = ConfigManager.Instance.GetConfig<GunPartData>(barrelId);
		var magazine = ConfigManager.Instance.GetConfig<GunPartData>(magazineId);

		if (body == null || body.Slot != PartSlot.Body)
		{ GD.PushWarning("[CustomGunSystem] 枪身校验失败"); return null; }
		if (barrel == null || barrel.Slot != PartSlot.Barrel)
		{ GD.PushWarning("[CustomGunSystem] 枪管校验失败"); return null; }
		if (magazine == null || magazine.Slot != PartSlot.Magazine)
		{ GD.PushWarning("[CustomGunSystem] 弹匣校验失败"); return null; }

		//校验可选两件
		GunPartData sight = null, muzzle = null;
		if (sightId != null)
		{
			sight = ConfigManager.Instance.GetConfig<GunPartData>(sightId);
			if (sight == null || sight.Slot != PartSlot.Sight)
			{ GD.PushWarning("[CustomGunSystem] 瞄具校验失败"); return null; }
		}
		if (muzzleId != null)
		{
			muzzle = ConfigManager.Instance.GetConfig<GunPartData>(muzzleId);
			if (muzzle == null || muzzle.Slot != PartSlot.Muzzle)
			{ GD.PushWarning("[CustomGunSystem] 枪口校验失败"); return null; }
		}

		//校验库存有货
		if (!ConsumePart(bodyId)) return null;
		if (!ConsumePart(barrelId)) return null;
		if (!ConsumePart(magazineId)) return null;
		if (sightId != null && !ConsumePart(sightId)) return null;
		if (muzzleId != null && !ConsumePart(muzzleId)) return null;

		var gun = new CustomGun
		{
			GunName = gunName ?? $"{body.DisplayName}",
			BodyId = bodyId,
			BarrelId = barrelId,
			MagazineId = magazineId,
			SightId = sightId,
			MuzzleId = muzzleId,
			Durability = 100f,
		};
		DataManager.Instance.AddCustomGun(gun);
		return gun;
	}

	/// <summary>尝试消耗库存中的配件(第一把可用的同ID槽位)</summary>
	private static bool ConsumePart(string itemId)
	{
		var slots = InventorySystem.FindSlots(itemId);
		if (slots.Count == 0)
		{
			GD.PushWarning($"[CustomGunSystem] 库存中没有配件 {itemId}");
			return false;
		}
		return InventorySystem.RemoveItem(slots[0].SlotId, 1);
	}

	// ═══════════════════════════════════════════════
	// 拆卸
	// ═══════════════════════════════════════════════

	/// <summary>拆枪——所有配件退回仓库，枪械记录删除</summary>
	public static bool Disassemble(int gunId)
	{
		var gun = GetGun(gunId);
		if (gun == null) return false;

		foreach (var partId in gun.AllPartIds())
		{
			InventorySystem.AddItem(partId, 1, gun.Durability);
		}

		DataManager.Instance.RemoveCustomGun(gunId);
		GD.Print($"[CustomGunSystem] 已拆卸: {gun.GunName}");
		return true;
	}

	// ═══════════════════════════════════════════════
	// 射程计算
	// ═══════════════════════════════════════════════

	/// <summary>类别基础最优射程</summary>
	public static float GetBaseOptimalRange(GunCategory category) => BaseOptimalRange.TryGetValue(category, out var v) ? v : 80f;

	/// <summary>
	/// 改装后最优射程 = 类别基础最优射程 × (1 + 枪管 RangeOffset)
	/// 长管+30% / 标准 0 / 短管−25% / 截短−40%
	/// </summary>
	public static float CalcModifiedOptimalRange(CustomGun gun, GunPartData body, GunPartData barrel)
	{
		float baseRange = GetBaseOptimalRange(body.Category);
		return baseRange * (1f + barrel.RangeOffset);
	}

	// ═══════════════════════════════════════════════
	// CombatManager 用：构建 PlayerSnapshot
	// ═══════════════════════════════════════════════

	/// <summary>从组装好的枪 + 弹药构建玩家快照</summary>
	public static PlayerSnapshot BuildSnapshot(CustomGun gun, AmmoData ammo, ArmorSummary armor)
	{
		var body = ConfigManager.Instance.GetConfig<GunPartData>(gun.BodyId);
		var barrel = ConfigManager.Instance.GetConfig<GunPartData>(gun.BarrelId);
		var magazine = ConfigManager.Instance.GetConfig<GunPartData>(gun.MagazineId);

		//汇总五件重量
		float totalWeight = 0f;
		foreach (var partId in gun.AllPartIds())
		{
			var part = ConfigManager.Instance.GetConfig<GunPartData>(partId);
			if (part != null) totalWeight += part.Weight;
		}

		//汇总精度
		float accuracy = 0.85f; //基础精度
		foreach (var partId in gun.AllPartIds())
		{
			var part = ConfigManager.Instance.GetConfig<GunPartData>(partId);
			if (part != null) accuracy += part.AccuracyModifier;
		}

		accuracy = Mathf.Clamp(accuracy, 0.5f, 1.0f);
		return new PlayerSnapshot
		{
			KnowledgeBonus = 1.0f,
			ProficiencyBonus = 1.0f,
			GunAccuracy = accuracy,
			GunCategory = body.Category,
			OptimalRange = CalcModifiedOptimalRange(gun, body, barrel),
			AmmoDamage = ammo.BaseDamage,
			GunCoeff = 1.0f,
			FireRate = 5f,
			TotalWeight = totalWeight,
			WeightPenalty = 0f,
			SidePlatePenalty = 0f,
			Armor = armor
		};
	}
}
