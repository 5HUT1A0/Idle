using Godot;
using System;

/// <summary>
/// 耐久系统——三档衰减 + 故障判定 + 撤离时统一更新。
/// MVP 阈值：70%/30%，精度惩罚和故障率按档位递增。
/// </summary>
public static class DurabilitySystem
{
	/// <summary>耐久>=70%：无惩罚</summary>
	private const float ThresholdGood = 70f;

	/// <summary>耐久>=30%：轻微惩罚</summary>
	private const float ThresholdBad = 30f;

	/// <summary>70%档：精度惩罚</summary>
	private const float PenaltyMidAccuracy = 0.05f;

	/// <summary>70%档：故障率</summary>
	private const float PenaltyMidMalfunction = 0.05f;

	/// <summary>30%档：精度惩罚</summary>
	private const float PenaltyLowAccuracy = 0.20f;

	/// <summary>30%档：故障率</summary>
	private const float PenaltyLowMalfunction = 0.20f;

	/// <summary>MVP基础耐久消耗速率：每小时消耗耐久点数</summary>
	private const float BaseWearRate = 1.0f;

	// ═══════════════════════════════════════════════
	// 效果查询（战斗中每发调用）
	// ═══════════════════════════════════════════════

	/// <summary>耐久三档效果</summary>
	public readonly struct DurabilityEffects
	{
		/// <summary>命中率惩罚（正值=减益，如 0.05 = −5% 命中率）</summary>
		public float AccuracyPenalty { get; init; }
		/// <summary>故障概率（0-1）</summary>
		public float MalfunctionChance { get; init; }
		/// <summary>是否已报废</summary>
		public bool IsBroken { get; init; }
		/// <summary>处于哪一个档位</summary>
		public string TierLabel { get; init; }
	}

	/// <summary>
	/// 根据耐久百分比计算三档效果。
	/// </summary>
	public static DurabilityEffects CalcEffects(float durability)
	{
		if (durability <= 0f)
		{
			return new DurabilityEffects
			{
				AccuracyPenalty = 1f,       //命中率归零
				MalfunctionChance = 1f,     //必故障
				IsBroken = true,
				TierLabel = "报废"
			};
		}

		if (durability <= ThresholdBad)
		{
			return new DurabilityEffects
			{
				AccuracyPenalty = PenaltyLowAccuracy,
				MalfunctionChance = PenaltyLowMalfunction,
				IsBroken = false,
				TierLabel = "严重磨损"
			};
		}
		if (durability <= ThresholdGood)
			return new DurabilityEffects
			{
				AccuracyPenalty = PenaltyMidAccuracy,
				MalfunctionChance = PenaltyMidMalfunction,
				IsBroken = false,
				TierLabel = "轻度磨损"
			};

		return new DurabilityEffects { TierLabel = "良好" };
	}

	/// <summary>故障判定——返回 true 表示本轮卡壳，攻击跳过</summary>
	public static bool RollMalfunction(float malfunctionChance)
	=> malfunctionChance > 0f && GD.Randf() < malfunctionChance;

	// ═══════════════════════════════════════════════
	// 耐久消耗（撤离时调用）
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 计算本次 Raid 的耐久消耗量。
	/// 基础消耗 × 配件耐久消耗倍率 × 副本时长。
	/// </summary>
	public static float CalcWear(float hoursInRaid, float durabilityCost = 1.0f)
	{
		if (hoursInRaid <= 0f) return 0f;
		return hoursInRaid * BaseWearRate * durabilityCost;
	}

	/// <summary>对一把枪施加耐久消耗</summary>
	public static void ApplyWear(CustomGun gun, float hoursInRaid)
	{
		if (gun == null || hoursInRaid <= 0f) return;

		//取各配件中最大的DurabilityCost（最娇贵的配件决定消耗速度）
		float maxCost = 1.0f;
		foreach (var partId in gun.AllPartIds())
		{
			var part = ConfigManager.Instance.GetConfig<GunPartData>(partId);
			if (part != null && part.DurabilityCost > maxCost)
			{
				maxCost = part.DurabilityCost;
			}
		}

		float wear = CalcWear(hoursInRaid, maxCost);
		gun.Durability = Math.Max(0f, gun.Durability - wear);

		var effects = CalcEffects(gun.Durability);
		GD.Print($"[DurabilitySystem] 枪械耐久: {gun.Durability:F0}% ({effects.TierLabel}) | 消耗: {wear:F1}");
	}

	/// <summary>对一件护甲施加耐久消耗</summary>
	public static void ApplyWear(CustomArmor armor, float hoursInRaid)
	{
		if (armor == null || hoursInRaid <= 0f) return;

		// 护甲使用基础消耗速率
		float wear = CalcWear(hoursInRaid, 1.0f);
		armor.Durability = Mathf.Max(0f, armor.Durability - wear);

		var effects = CalcEffects(armor.Durability);
		GD.Print($"[DurabilitySystem] 护甲耐久: {armor.Durability:F0}% ({effects.TierLabel}) | 消耗: {wear:F1}");
	}

	/// <summary>
	/// 撤离时统一处理：枪械 + 护甲 + 库存物品耐久消耗。
	/// </summary>
	public static void OnRaidEnd(float hoursInRaid)
	{
		if (hoursInRaid <= 0f) return;

		GD.Print($"[DurabilitySystem] 撤离结算——副本时长: {hoursInRaid:F2}h");

		// 所有已组装枪械
		foreach (var gun in DataManager.Instance.CustomGuns)
		{
			ApplyWear(gun, hoursInRaid);
		}

		// 所有已组装护甲
		foreach (var armor in DataManager.Instance.CustomArmors)
		{
			ApplyWear(armor, hoursInRaid);
		}
	}

	/// <summary>检查物品是否报废</summary>
	public static bool IsBroken(float durability) => durability <= 0f;
}
