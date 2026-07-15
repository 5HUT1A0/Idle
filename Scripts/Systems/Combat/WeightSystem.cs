using Godot;
using System;


/// <summary>
/// 重量系统——根据总负重判定三档。
/// MVP 使用固定阈值（≤8kg轻 / ≤15kg中 / >15kg重）。
/// </summary>
public static class WeightSystem
{

	/// <summary>MVP 固定阈值版本</summary>
	public static WeightResult CalcTierMvp(float totalWeight)
	{
		const float LightMax = 8f;
		const float MediumMax = 15f;

		if (totalWeight <= LightMax)
		{
			return new WeightResult(WeightTier.Light, 0.7f, 1.5f, 0f);
		}
		else if (totalWeight <= MediumMax)
		{
			return new WeightResult(WeightTier.Medium, 0.4f, 1.0f, 0f);
		}
		else
		{
			return new WeightResult(WeightTier.Heavy, 0.10f, 0.6f, 0.15f);
		}
	}

	/// <summary>重量判定结果</summary>
	public readonly struct WeightResult
	{
		public readonly WeightTier Tier;
		public readonly float AvoidChance; // 避免命中率修正系数
		public readonly float SearchSpeed; // 搜刮速度倍率
		public readonly float HitPenalty; // 命中惩罚（负重过高导致的疲劳，影响射击精度）

		public WeightResult(WeightTier tier, float avoidChance, float searchSpeed, float hitPenalty)
		{
			Tier = tier;
			AvoidChance = avoidChance;
			SearchSpeed = searchSpeed;
			HitPenalty = hitPenalty;
		}
	}
}
