using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class SafeHouseSystem
{
	private static Dictionary<string, FacilityData> _defs;
	private static bool _initialized;

	/// <summary>设施ID常量</summary>
	public const string Warehouse = "warehouse";
	public const string Workbench = "workbench";
	public const string Gym = "gym";
	public const string Range = "range";
	public const string Infirmary = "infirmary";

	// ═══════════════════════════════════════════════
	// 初始化
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 加载全部 FacilityData，恢复设施等级。
	/// 由 DataManager._Ready() 调用。
	/// </summary>
	public static void Init()
	{
		//从ConfigManager加载设施定义
		_defs = new Dictionary<string, FacilityData>();
		var all = ConfigManager.Instance.GetAll<FacilityData>();
		if (all != null)
		{
			foreach (var def in all)
			{
				if (!string.IsNullOrEmpty(def.FacilityId))
					_defs[def.FacilityId] = def;
			}
		}

		//没有配置时使用内置默认(MVP 5 设施)
		if (_defs.Count == 0)
		{
			GD.Print("[SafeHouseSystem]  未找到设施配置，使用内置默认。");
			InitDefaults();
		}

		_initialized = true;

		// 设施等级：新档用默认值，旧档保持已有等级（但 0 级设施如默认>0 则修正）
		bool isNewSave = !DataManager.Instance.HasFacilityLevel(Warehouse);
		foreach (var (id, def) in _defs)
		{
			if (isNewSave || DataManager.Instance.GetFacilityLevel(id) == 0 && def.DefaultLevel > 0)
				DataManager.Instance.SetFacilityLevel(id, def.DefaultLevel);
		}
	}

	/// <summary>MVP 内置默认 5 设施（ConfigManager 没有设施 .tres 时使用）</summary>
	private static void InitDefaults()
	{
		_defs = new Dictionary<string, FacilityData>
		{
			[Warehouse] = new FacilityData
			{
				FacilityId = Warehouse,
				DisplayName = "仓库",
				DefaultLevel = 1,
				MaxLevel = 10,
				UpgradeDescription = "每级+{0}格容量",
			},
			[Workbench] = new FacilityData
			{
				FacilityId = Workbench,
				DisplayName = "工作台",
				DefaultLevel = 1,
				MaxLevel = 10,
				UpgradeDescription = "每级提高维修耐久上限",
			},
			[Gym] = new FacilityData
			{
				FacilityId = Gym,
				DisplayName = "健身房",
				DefaultLevel = 1,
				MaxLevel = 10,
				UpgradeDescription = "每级扩大重量三档阈值",
			},
			[Range] = new FacilityData
			{
				FacilityId = Range,
				DisplayName = "靶场",
				DefaultLevel = 1,
				MaxLevel = 10,
				UpgradeDescription = "每级缩短换弹CD训练时间",
			},
			[Infirmary] = new FacilityData
			{
				FacilityId = Infirmary,
				DisplayName = "医务室",
				DefaultLevel = 0,
				MaxLevel = 10,
				UpgradeDescription = "每级提高战后回复比例",
				PrerequisiteFacility = Workbench,
				PrerequisiteLevel = 2,
			},
		};
	}

	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	/// <summary>获取设施当前等级</summary>
	public static int GetLevel(string facilityId)
	=> DataManager.Instance.GetFacilityLevel(facilityId);

	/// <summary>获取设施最大等级</summary>
	public static int GetMaxLevel(string facilityId)
	=> _defs.TryGetValue(facilityId, out var def) ? def.MaxLevel : 10;

	/// <summary>获取设施定义</summary>
	public static FacilityData GetDef(string facilityId)
	=> _defs.TryGetValue(facilityId, out var def) ? def : null;

	/// <summary>获取所有设施</summary>
	public static IEnumerable<string> GetAllFacilityIds() => _defs.Keys;

	/// <summary>
	/// 检查设施是否已解锁——全程走配置，不写死 if-else。
	/// </summary>
	public static bool IsUnlocked(string facilityId)
	{
		if (!_defs.TryGetValue(facilityId, out var def))
			return false;

		if (def.DefaultLevel > 0)
			return true;

		if (string.IsNullOrEmpty(def.PrerequisiteFacility)) return false;
		return GetLevel(def.PrerequisiteFacility) >= def.PrerequisiteLevel;
	}

	/// <summary>获取未解锁原因（给UI提示）</summary>
	public static string GetUnlockHint(string facilityId)
	{
		if (!_defs.TryGetValue(facilityId, out var def))
			return "设施不存在";
		if (def.DefaultLevel > 0)
			return null; //默认解锁的设施不提示
		if (string.IsNullOrEmpty(def.PrerequisiteFacility))
			return "需通过任务解锁";
		var prereqDef = _defs.TryGetValue(def.PrerequisiteFacility, out var prereq) ? prereq : null;
		string prereqName = prereqDef?.DisplayName ?? def.PrerequisiteFacility;
		return $"需要 {prereqName} Lv{def.PrerequisiteLevel}";
	}

	/// <summary>检查设施是否可升级（已解锁 + 未满级 + 没有正在进行的升级）</summary>
	public static bool CanUpgrade(string facilityId)
	{
		if (!IsUnlocked(facilityId)) return false;
		if (GetLevel(facilityId) >= GetMaxLevel(facilityId)) return false;
		return !DataManager.Instance.HasFacilityUpgradeProgress(facilityId);
	}

	/// <summary>获取升级进度（0-1）</summary>
	public static float GetUpgradeProgress(string facilityId)
	{
		var progress = DataManager.Instance.GetFacilityUpgradeProgress(facilityId);
		float total = GetUpgradeTime(facilityId);
		return total > 0 ? Mathf.Clamp(progress / total, 0f, 1f) : 0;
	}

	// ═══════════════════════════════════════════════
	// 升级
	// ═══════════════════════════════════════════════

	/// <summary>获取升级所需时长（小时），从 Curve 或默认公式计算</summary>
	public static float GetUpgradeTime(string facilityId)
	{
		int nextLevel = GetLevel(facilityId) + 1;
		var def = GetDef(facilityId);

		if (def != null && !string.IsNullOrEmpty(def.UpgradeTimeCurveId))
		{
			var curve = ConfigManager.Instance.GetCurve(def.UpgradeTimeCurveId);
			if (curve != null)
				return curve.Sample(nextLevel);
		}

		// 默认公式：每级递增 0.5h，如 Lv1→2 需 1h，Lv9→10 需 5h
		return 0.5f + nextLevel * 0.5f;
	}

	/// <summary>
	/// 开始升级——扣除材料，启动挂机计时器。
	/// 返回 false 表示条件不满足。
	/// </summary>
	public static bool StartUpgrade(string facilityId)
	{
		if (!CanUpgrade(facilityId))
		{
			GD.PushWarning($"[SafeHouseSystem] {facilityId} 无法升级");
			return false;
		}

		// MVP：暂不扣除材料（材料消耗后续由 CraftRecipeData 驱动）
		DataManager.Instance.SetFacilityUpgradeProgress(facilityId, 0f);

		float time = GetUpgradeTime(facilityId);
		var def = GetDef(facilityId);
		GD.Print($"[SafeHouseSystem] {def?.DisplayName ?? facilityId} 开始升级Lv{GetLevel(facilityId)}→Lv{GetLevel(facilityId) + 1} | 需时: {time}h");
		return true;
	}

	/// <summary>
	/// 推进升级进度——由 _Process 或 OfflineManager Phase F 调用。
	/// </summary>
	public static void AddProgress(string facilityId, float hours)
	{
		if (hours <= 0f)
		{
			GD.PushWarning($"[SafeHouseSystem] {facilityId} 推进的时长必须大于0");
			return;
		}
		if (!DataManager.Instance.HasFacilityUpgradeProgress(facilityId))
		{
			GD.Print($"[SafeHouseSystem] {facilityId} 没有正在进行的升级，无法推进进度");
			return;
		}

		float current = DataManager.Instance.GetFacilityUpgradeProgress(facilityId);
		float required = GetUpgradeTime(facilityId);
		float newProgress = current + hours;

		if (newProgress >= required)
		{
			//升级完成
			int oldLevel = GetLevel(facilityId);
			DataManager.Instance.SetFacilityLevel(facilityId, oldLevel + 1);
			DataManager.Instance.ClearFacilityUpgradeProgress(facilityId);

			var def = GetDef(facilityId);
			GD.Print($"[SafeHouseSystem] ✅ {def?.DisplayName ?? facilityId} 升级完成 Lv{oldLevel}→Lv{oldLevel + 1}");
		}

	}

	/// <summary>批量推进所有设施的升级进度（OfflineManager 使用）</summary>
	public static void AddProgressAll(float hours)
	{
		foreach (var id in _defs.Keys)
		{
			AddProgress(id, hours);
		}
	}

	// ═══════════════════════════════════════════════
	// 设施功能
	// ═══════════════════════════════════════════════

	/// <summary>仓库容量（格数）= 基础 + 等级 × 递减增量</summary>
	public static int GetWarehouseCapacity()
	{
		int level = GetLevel(Warehouse);
		// 每级 +10/+8/+5/+5/+5...（递减但不少于 3）
		int total = 30;
		int increment = 10;
		for (int i = 1; i < level; i++)
		{
			total += increment;
			increment = Mathf.Max(3, increment - 2);
		}

		return total;
	}

	/// <summary>医务室战后回复比例（Lv1: 8% → Lv10: 32%）</summary>
	public static float GetInfirmaryHealRate()
	{
		if (!IsUnlocked(Infirmary)) return 0f;
		int level = GetLevel(Infirmary);
		return 0.05f + level * 0.03f; // Lv1: 8%, Lv10: 35%
	}

	/// <summary>撤离后自动治疗--回复各部位</summary>
	public static void PostRaidHeal()
	{
		float rate = GetInfirmaryHealRate();
		if (rate <= 0f) return;

		DataManager.Instance.HpHead = Mathf.Min(100f, DataManager.Instance.HpHead + 100f * rate);
		DataManager.Instance.HpChest = Mathf.Min(100f, DataManager.Instance.HpChest + 100f * rate);
		DataManager.Instance.HpAbdomen = Mathf.Min(100f, DataManager.Instance.HpAbdomen + 100f * rate);
		DataManager.Instance.HpLeftArm = Mathf.Min(100f, DataManager.Instance.HpLeftArm + 100f * rate);
		DataManager.Instance.HpRightArm = Mathf.Min(100f, DataManager.Instance.HpRightArm + 100f * rate);
		DataManager.Instance.HpLeftLeg = Mathf.Min(100f, DataManager.Instance.HpLeftLeg + 100f * rate);
		DataManager.Instance.HpRightLeg = Mathf.Min(100f, DataManager.Instance.HpRightLeg + 100f * rate);

		GD.Print($"[SafeHouseSystem] 战后治疗完成 | 回复率: {rate:P0}");
	}
}

