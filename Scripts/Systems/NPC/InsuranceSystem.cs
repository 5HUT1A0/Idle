using Godot;
using System.Collections.Generic;

/// <summary>
/// 投保系统——持续投保制：一次付费覆盖装备直至死亡丢失。
/// 存活撤离→保险继续有效；死亡→按商人保留率结算。
/// </summary>

public static class InsuranceSystem
{
	/// <summary>商人保留率</summary>
	private static readonly Dictionary<string, float> RetentionRate = new()
	{
		[MerchantSystem.Vulture] = 0.40f,
		[MerchantSystem.GoldenShield] = 0.70f,
	};

	/// <summary>保费倍率(占装备BaseValue的比例)</summary>
	private static readonly Dictionary<string, float> PremiumRate = new()
	{
		[MerchantSystem.Vulture] = 0.08f,
		[MerchantSystem.GoldenShield] = 0.15f,
	};

	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	/// <summary>获取某商人的保留率</summary>
	public static float GetRetentionRate(string merchantId)
	=> RetentionRate.TryGetValue(merchantId, out var r) ? r : 0f;

	/// <summary>获取某商人的保费倍率</summary>
	public static float GetPremiumRate(string merchantId)
	=> PremiumRate.TryGetValue(merchantId, out var r) ? r : 0f;

	///<summary>计算投保费用</summary>
	public static int CalcPremium(string itemId, string merchantId)
	{
		var config = ConfigManager.Instance.GetConfig<BaseItemData>(itemId);
		int baseValue = config?.BaseValue ?? 100;
		float rate = GetPremiumRate(merchantId);
		return Mathf.CeilToInt(baseValue * rate);
	}

	/// <summary>检查商人是否提供投保服务</summary>
	public static bool OffersInsurance(string merchantId)
	=> RetentionRate.ContainsKey(merchantId);

	// ═══════════════════════════════════════════════
	// 投保
	// ═══════════════════════════════════════════════

	/// <summary>为一把枪投保</summary>
	public static bool InsureGun(int gunId, string merchantId)
	{
		if (!OffersInsurance(merchantId))
		{
			GD.PushWarning("[InsuranceSystem] {merchantId} 不提供投保服务");
			return false;
		}

		var gun = CustomGunSystem.GetGun(gunId);
		if (gun == null)
		{
			GD.PushWarning($"[InsuranceSystem] 枪械 {gunId} 不存在");
			return false;
		}

		if (!string.IsNullOrEmpty(gun.InsuredBy))
		{
			GD.PushWarning($"[InsuranceSystem] 该枪已由 {gun.InsuredBy} 承保");
			return false;
		}

		//计算保费（取枪身BaseValue）
		int premium = CalcPremium(gun.BodyId, merchantId);
		if (DataManager.Instance.ScrapCurrency < premium)
		{
			GD.PushWarning($"[InsuranceSystem] 废土币不足: 保费{premium}, 持有{DataManager.Instance.ScrapCurrency}");
			return false;
		}

		DataManager.Instance.ScrapCurrency -= premium;
		gun.InsuredBy = merchantId;

		string merchantName = MerchantSystem.GetDef(merchantId)?.DisplayName ?? merchantId;
		GD.Print($"[InsuranceSystem] {gun.GunName} 已投保 | 承保:{merchantName} | 保费:{premium} |保留率:{GetRetentionRate(merchantId):P0}");
		return true;
	}

	/// <summary>为一件护甲投保</summary>
	public static bool InsureArmor(int armorId, string merchantId)
	{
		if (!OffersInsurance(merchantId))
		{
			GD.PushWarning($"[InsuranceSystem] {merchantId} 不提供投保服务");
			return false;
		}

		var armor = ArmorSystem.GetArmor(armorId);
		if (armor == null)
		{
			GD.PushWarning($"[InsuranceSystem] 护甲 {armorId} 不存在");
			return false;
		}

		if (!string.IsNullOrEmpty(armor.InsuredBy))
		{
			GD.PushWarning($"[InsuranceSystem] 该护甲已由 {armor.InsuredBy} 承保");
			return false;
		}

		//计算保费（取护甲BaseValue）
		int premium = CalcPremium(armor.LinerId, merchantId);
		if (DataManager.Instance.ScrapCurrency < premium)
		{
			GD.PushWarning($"[InsuranceSystem] 废土币不足: 保费{premium}, 持有{DataManager.Instance.ScrapCurrency}");
			return false;
		}

		DataManager.Instance.ScrapCurrency -= premium;
		armor.InsuredBy = merchantId;

		string merchantName = MerchantSystem.GetDef(merchantId)?.DisplayName ?? merchantId;
		GD.Print($"[InsuranceSystem] {armor.ArmorName} 已投保 | 承保:{merchantName} | 保费:{premium} |保留率:{GetRetentionRate(merchantId):P0}");
		return true;
	}

	// ═══════════════════════════════════════════════
	// 死亡结算
	// ═══════════════════════════════════════════════

	/// <summary>死亡结算结果</summary>
	public struct SettlementResult
	{

		public string ItemName;
		public string ItemId;
		public bool Retained;
		public string InsuredBy;
	}

	/// <summary>
	/// 死亡结算——对所有已投保装备逐件判定保留。
	/// 保留 → 装备留在仓库；丢失 → 移除。
	/// 返回结算结果列表。
	/// </summary>
	public static List<SettlementResult> SettleDeath()
	{
		var results = new List<SettlementResult>();

		// 结算枪械
		foreach (var gun in DataManager.Instance.CustomGuns)
		{
			if (string.IsNullOrEmpty(gun.InsuredBy)) continue;

			float rate = GetRetentionRate(gun.InsuredBy);
			bool retained = GD.Randf() < rate;

			results.Add(new SettlementResult
			{
				ItemName = gun.GunName,
				ItemId = gun.BodyId,
				Retained = retained,
				InsuredBy = gun.InsuredBy
			});

			if (retained)
			{
				// 保留 → 保险失效（一次死亡消耗），但装备退回仓库\
				gun.InsuredBy = null;
				GD.Print($"[InsuranceSystem] ✅ 保留: {gun.GunName} (承保:{gun.InsuredBy})");
			}
			else
			{
				// 丢失 → 移除
				GD.Print($"[InsuranceSystem] ❌ 丢失: {gun.GunName}");
			}

			EventBus.Instance.EmitSignal(EventBus.SignalName.InsuranceSettled, gun.GunId.ToString(), retained);
		}

		// 结算护甲
		foreach (var armor in DataManager.Instance.CustomArmors)
		{
			if (string.IsNullOrEmpty(armor.InsuredBy)) continue;

			float rate = GetRetentionRate(armor.InsuredBy);
			bool retained = GD.Randf() < rate;

			results.Add(new SettlementResult
			{
				ItemName = armor.ArmorName,
				ItemId = armor.LinerId,
				Retained = retained,
				InsuredBy = armor.InsuredBy
			});

			if (retained)
			{
				// 保留 → 保险失效（一次死亡消耗），但装备退回仓库
				armor.InsuredBy = null;
				GD.Print($"[InsuranceSystem] ✅ 保留: {armor.ArmorName} (承保:{armor.InsuredBy})");
			}
			else
			{
				// 丢失 → 移除
				GD.Print($"[InsuranceSystem] ❌ 丢失: {armor.ArmorName}");
			}

			EventBus.Instance.EmitSignal(EventBus.SignalName.InsuranceSettled, armor.ArmorId.ToString(), retained);
		}

		//移除丢失的装备（护甲待ArmorSystem.Disassemble 处理，枪械直接删）
		// MVP：丢失 = 装备直接删除（不退回配件，模拟"死在副本里"）
		var lostGuns = DataManager.Instance.CustomGuns.FindAll(g => !string.IsNullOrEmpty(g.InsuredBy) && results.Find(r => r.ItemId == g.BodyId && r.Retained).ItemName != null);

		//简化处理：结算后清除所有投保标记（存活→保留标记，死亡→已结算）
		foreach (var gun in DataManager.Instance.CustomGuns)
		{
			if (!string.IsNullOrEmpty(gun.InsuredBy))
			{
				var result = results.Find(r => r.ItemId == gun.BodyId);
				if (result.ItemName != null && !result.Retained)
				{
					gun.InsuredBy = null;//标记已结算
				}

			}

		}

		return results;
	}

	/// <summary>查询装备是否已投保</summary>
	public static bool IsInsuredGun(int gunId)
	{
		var gun = CustomGunSystem.GetGun(gunId);
		return gun != null && !string.IsNullOrEmpty(gun.InsuredBy);
	}

	public static bool IsInsuredArmor(int armorId)
	{
		var armor = ArmorSystem.GetArmor(armorId);
		return armor != null && !string.IsNullOrEmpty(armor.InsuredBy);
	}
}
