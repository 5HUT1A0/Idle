using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>
/// 敌人对象池抽选----根据地图敌人的Tier区间+权重随机选取敌人
/// </summary>
public static class EnemyPoolResolver
{
	// <summary>从地图配置的敌人区间随机选取一个敌人</summary>
	public static EnemyData Roll(string mapId)
	{
		var allEnemies = ConfigManager.Instance.GetAll<EnemyData>();
		var candidates = new List<EnemyData>();

		foreach (var e in allEnemies)
		{
			if (e.EnemyId.StartsWith(mapId) || e.EnemyId.StartsWith("scav_"))
			{
				candidates.Add(e);
			}
		}

		if (candidates.Count == 0)
		{
			GD.PrintErr($"[EnemyPoolResolver] 没有找到地图 {mapId} 的敌人配置");
			return null;
		}
		return candidates[GD.RandRange(0, candidates.Count)];
	}

	/// <summary>按权重抽取----高tier敌人低权重</summary>
	public static EnemyData RollWeighted(List<EnemyData> pool)
	{
		if (pool == null || pool.Count == 0)
		{
			GD.PrintErr($"[EnemyPoolResolver] 敌人池为空");
			return null;
		}

		float total = 0;
		foreach (var e in pool)
		{
			total += TierWeight(e.Tier);
		}

		float roll = GD.Randf() * total;
		float acc = 0f;
		foreach (var e in pool)
		{
			acc += TierWeight(e.Tier);
			if (roll <= acc)
			{
				return e;
			}
		}
		return pool[^1];
	}

	private static float TierWeight(int tier) => tier switch
	{
		1 => 5f,
		2 => 4f,
		3 => 3f,
		4 => 2f,
		5 => 1f,
		_ => 1f
	};
}