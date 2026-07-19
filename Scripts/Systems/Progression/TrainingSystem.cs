using Godot;
using System.Collections.Generic;

/// <summary>
/// 训练系统——3 条局外训练线（体能/靶场/学识）。
/// n^1.5 XP 曲线，上限 Lv30（可配置），挂机时长→属性提升。
/// </summary>
public static class TrainingSystem
{
	/// <summary>训练线枚举</summary>
	public enum TrainingLine
	{
		Stamina,         // 体能→ 扩大重量三档阈值
		ShootingRange,   // 靶场→ 缩短换弹CD
		Knowledge        // 学识→ 提升命中率（学识部分）
	}

	/// <summary>基础 XP 系数（小时）。Lv1→2 需 0.05h = 3min</summary>
	private const float BaseXpHours = 0.05f;

	/// <summary>最大默认等级</summary>
	private const int DefaultMaxLevel = 30;

	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	/// <summary>获取训练等级</summary>
	public static int GetLevel(TrainingLine line)
		=> DataManager.Instance.GetTrainingLevel(line);

	/// <summary>获取最大等级</summary>
	public static int GetMaxLevel()
		=> DefaultMaxLevel; // 后续从 TrainingData Resource 读取

	/// <summary>是否已经满级</summary>
	public static bool IsMaxLevel(TrainingLine line)
		=> GetLevel(line) >= GetMaxLevel();

	/// <summary>下一级所需总时长（小时）</summary>
	public static float GetXpRequired(int level)
	{
		if (level >= GetMaxLevel()) return float.MaxValue;
		// n^1.5 曲线：下一级所需时长 = base × level^1.5
		return BaseXpHours * Mathf.Pow(level, 1.5f);
	}

	/// <summary>当前进度（0-1）</summary>
	public static float GetProgress(TrainingLine line)
	{
		if (IsMaxLevel(line)) return 1f;
		float current = DataManager.Instance.GetTrainingProgress(line);
		float required = GetXpRequired(GetLevel(line));
		return required > 0f ? Mathf.Clamp(current / required, 0f, 1f) : 1f;
	}

	///<summary>是否正在训练中</summary>
	public static bool IsTraining(TrainingLine line)
		=> DataManager.Instance.HasTrainingProgress(line);

	///<summary>获取所有训练线的当前等级摘要</summary>
	public static Dictionary<TrainingLine, int> GetAllLevels()
	{
		var result = new Dictionary<TrainingLine, int>();
		foreach (TrainingLine line in System.Enum.GetValues(typeof(TrainingLine)))
		{
			result[line] = GetLevel(line);
		}
		return result;
	}

	// ═══════════════════════════════════════════════
	// 操作
	// ═══════════════════════════════════════════════

	/// <summary>
	/// 开始训练——无需前置消耗（MVP 阶段暂不扣资源）。
	/// 已有训练进度时继续累加。
	/// </summary>
	public static bool StartTraining(TrainingLine line)
	{
		if (IsMaxLevel(line))
		{
			GD.PushWarning($"[TrainingSystem] {LineName(line)} 已满级");
			return false;
		}

		//已有进度→继续，无进度→初始化为0
		if (!DataManager.Instance.HasTrainingProgress(line))
		{
			DataManager.Instance.SetTrainingProgress(line, 0f);
		}
		GD.Print($"[TrainingSystem] {LineName(line)} 开始训练 Lv{GetLevel(line)}→Lv{GetLevel(line) + 1} | 需时:{GetXpRequired(GetLevel(line)):F2}h");

		return true;
	}

	/// <summary>
	/// 推进训练进度——由 _Process 或 OfflineManager Phase G 调用。
	/// </summary>
	public static void AddProgress(TrainingLine line, float hours)
	{
		if (hours <= 0f) return;
		if (IsMaxLevel(line)) return;
		if (!IsTraining(line)) return;

		int currentLevel = GetLevel(line);
		float currentProgress = DataManager.Instance.GetTrainingProgress(line);
		float required = GetXpRequired(currentLevel);
		float newProgress = currentProgress + hours;

		while (newProgress >= required && !IsMaxLevel(line))
		{
			//升级
			newProgress -= required;
			int newLevel = currentLevel + 1;
			DataManager.Instance.SetTrainingLevel(line, newLevel);

			GD.Print($"[TrainingSystem] ✅ {LineName(line)} 升级 Lv{currentLevel}→Lv{newLevel}");

			currentLevel = newLevel;
			if (IsMaxLevel(line))
			{
				DataManager.Instance.ClearTrainingProgress(line);
				GD.Print($"[TrainingSystem] {LineName(line)} 已满级 Lv{GetMaxLevel()}");
				return;
			}

			required = GetXpRequired(currentLevel);
		}

		//保存溢出进度(或训练结束)
		DataManager.Instance.SetTrainingProgress(line, newProgress);

		if (newProgress <= 0f && !IsMaxLevel(line))
		{
			// 恰好升级完，自动开始下一级训练
		}


	}


	/// <summary>批量推进所有训练线的进度</summary>
	public static void AddProgressAll(float hours)
	{
		foreach (TrainingLine line in System.Enum.GetValues(typeof(TrainingLine)))
		{
			if (IsTraining(line))
				AddProgress(line, hours);
		}
	}


	/// <summary>停止训练（保留当前进度）</summary>
	public static void StopTraining(TrainingLine line)
	{
		// MVP 暂不处理——进度保留，下次 StartTraining 继续
	}

	// ═══════════════════════════════════════════════
	// 效果计算（CombatManager / WeightSystem 调用）
	// ═══════════════════════════════════════════════

	/// <summary>体能效果：轻→中 分界阈值扩展</summary>
	public static float GetStaminaLightMax()
	{
		int level = GetLevel(TrainingLine.Stamina);
		return 8f + level * 0.5f;  // Lv1: 8.5kg, Lv30: 23kg
	}

	/// <summary>体能效果：中→重 分界阈值扩展</summary>
	public static float GetStaminaMediumMax()
	{
		int level = GetLevel(TrainingLine.Stamina);
		return 15f + level * 1.0f;  // Lv1: 16kg, Lv30: 45kg
	}

	/// <summary>靶场效果：换弹时间倍率（1.0=基准，越小越快）</summary>
	public static float GetReloadTimeMultiplier()
	{
		int level = GetLevel(TrainingLine.ShootingRange);
		return 1.0f / (1.0f + level * 0.02f);
		// Lv0: 1.00x, Lv10: 0.83x, Lv20: 0.71x, Lv30: 0.63x
	}

	/// <summary>学识效果：命中率学识加成</summary>
	public static float GetKnowledgeBonus()
	{
		int level = GetLevel(TrainingLine.Knowledge);
		return 1.0f + level * 0.005f;
		// Lv0: 1.00, Lv10: 1.05, Lv20: 1.10, Lv30: 1.15
	}

	// ═══════════════════════════════════════════════
	// 工具
	// ═══════════════════════════════════════════════

	private static string LineName(TrainingLine line) => line switch
	{
		TrainingLine.Stamina => "体能",
		TrainingLine.ShootingRange => "靶场",
		TrainingLine.Knowledge => "学识",
		_ => line.ToString()
	};
}
