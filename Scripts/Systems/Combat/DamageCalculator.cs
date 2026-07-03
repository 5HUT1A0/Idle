using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 伤害公式——纯数学工具类，无状态、无节点依赖。
/// 所有参数从方法传入，数值阈值从 ConfigManager 曲线读取（失败降级为内置默认）。
/// </summary>
public static class DamageCalculator
{
	private const float MinHitChance = 0.05f; // 最低命中率
	private const float MaxHitChance = 0.95f; // 最高命中率
	private const float CritMultiplier = 1.5f; // 暴击伤害倍率

	//==================================
	//①接敌距离
	//==================================

	/// <summary>
	/// 从地图距离分布权重中随机抽一个接敌距离。
	/// 返回距离的米数。
	/// </summary>
	public static float RollEngagementDistance(MapDistanceConfig map)
	{
		float[] distMidPoints = { 12.5f, 42.5f, 80f, 120f, 170f }; // 各距离档位的中点
		float[] weights =
		{
			map.ContactWeight,
			map.CloseWeight,
			map.MediumWeight,
			map.FarWeight,
			map.DistantWeight,
		 };

		float total = 0f;
		foreach (var w in weights)
		{
			total += w;
		}

		if (total <= 0f)
		{
			total = 50f; // 兜底，中距离
		}

		float roll = GD.Randf() * total;
		float acc = 0f;
		for (int i = 0; i < weights.Length; i++)
		{
			acc += weights[i];
			if (roll <= acc)
			{
				return distMidPoints[i] + (GD.Randf() - 0.5f) * 20f; // 在档位中点±10随机偏移
			}
		}
		return distMidPoints[^1];
	}

	// ═══════════════════════════════════════════════
	// ② 射程修正
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 射程修正系数。实际距离 − 改装后最优射程 → 按类别曲线采样。
	/// 曲线不存在时降级为线性衰减。
	/// </summary>

	public static float CalcRangeCorrection(float actualDist, GunCategory category, float modifiedOptimalRange)
	{
		float offset = actualDist - modifiedOptimalRange;
		string curveId = category switch
		{
			GunCategory.Shotgun => "curve_range_shotgun",
			GunCategory.SMG => "curve_range_smg",
			GunCategory.AR => "curve_range_ar",
			GunCategory.DMR => "curve_range_dmr",
			GunCategory.Sniper => "curve_range_sniper",
			_ => null
		};

		var curve = ConfigManager.Instance.GetCurve(curveId);
		if (curve != null)
		{
			return curve.Sample(offset);
		}

		//降级：线性衰减（距离越远越低）
		return FallbackRangeCorrection(offset, category);
	}

	/// <summary>
	/// 射程修正的降级计算方法。用于曲线不存在时。
	/// </summary>
	private static float FallbackRangeCorrection(float offset, GunCategory category)
	{
		float maxRange = category switch
		{
			GunCategory.Shotgun => 30f,
			GunCategory.SMG => 90f,
			GunCategory.AR => 200f,
			GunCategory.DMR => 220f,
			GunCategory.Sniper => 300f,
			_ => 150f
		};

		if (offset <= 0f)
		{
			// 在最优射程内，修正系数为1
			return 1f;
		}
		float factory = 1f - (offset / maxRange);
		return Mathf.Max(factory, 0f);//超出最大射程范围返回0(无伤害)
	}


	// ═══════════════════════════════════════════════
	// ③ 命中判定
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 命中率 = 学识 × 熟练度 × 精度 × 射程修正 − 侧板惩罚 − 重档惩罚
	/// 钳制到 [5%, 95%]。
	/// </summary>
	public static BodyPart RollBodyPart(float hitChance)
	{
		float roll = GD.Randf();

		if (hitChance > 0.65f)

			return RollFromTable(roll, 0.15f, 0.30f, 0.22f, 0.10f, 0.10f, 0.10f, 0.10f, 0.03f);
		else if (hitChance >= 0.35f)
			return RollFromTable(roll, 0.08f, 0.25f, 0.20f, 0.14f, 0.14f, 0.14f, 0.14f, 0.05f);
		else
			return RollFromTable(roll, 0.03f, 0.13f, 0.14f, 0.17f, 0.17f, 0.17f, 0.17f, 0.19f);
	}

	private static BodyPart RollFromTable(float roll,
		float head, float chest, float abdomen,
		 float armL, float armR, float legL, float legR, float miss)
	{
		float t = head + chest + abdomen + armL + armR + legL + legR + miss;
		roll *= t;

		float a = 0;
		a += head; if (roll <= a) return BodyPart.Head;
		a += chest; if (roll <= a) return BodyPart.Chest;
		a += abdomen; if (roll <= a) return BodyPart.Abdomen;
		a += armL; if (roll <= a) return BodyPart.LeftArm;
		a += armR; if (roll <= a) return BodyPart.RightArm;
		a += legL; if (roll <= a) return BodyPart.LeftLeg;
		a += legR; if (roll <= a) return BodyPart.RightLeg;
		// MISS 区间（roll > 命中区间）
		return BodyPart.Chest; // 保底不会走到这里
	}

	// ═══════════════════════════════════════════════
	// ⑤ 伤害计算
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 单发伤害 = 弹药伤害 × 枪械系数 − 护甲减伤
	/// 未着甲区域触发暴击 ×1.5
	/// 最低伤害 1
	/// </summary>

	public static float CalcShotDamage(float ammoDamage, float gunCoeff,
		float armorReduction, bool isUnarmored)
	{
		float raw = ammoDamage * gunCoeff - armorReduction;
		if (raw < 1f) raw = 1f;
		if (isUnarmored) raw *= CritMultiplier;
		return raw;
	}
}
