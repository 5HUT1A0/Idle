using Godot;
using System.Collections.Generic;

/// <summary>
/// 战斗结算引擎 Autoload（优先级 7）。
/// 时间戳驱动的六步管线——不依赖帧率。
/// 在线失焦时以 5fps 运行，窗口粒度 60s 批量结算。
/// </summary>
public partial class CombatManager : Node
{
	public static CombatManager Instance { get; private set; }

	//Raid 相关
	private bool _isInRaid;
	private double _lastTickTime;
	private string _currentMapId;
	private PlayerSnapshot _player;
	private EnemyData _currentEnemy;
	private int _ammoLeft;
	private float _elapsedHours;
	private float _mapTimeLimitHours = 2f; //默认 2 小时

	private const double TickInterval = 3.0; //每秒结算一次

	// ═══════════════════════════════════════════════
	// Godot 生命周期
	// ═══════════════════════════════════════════════
	public override void _Ready()
	{
		Instance = this;
		EventBus.Instance.StateChanged += OnStateChanged;
	}

	private void OnStateChanged(GameState oldState, GameState newState)
	{
		if (newState == GameState.InRaid)
		{
			StartRaid();
		}
		else if (oldState == GameState.InRaid)
		{
			EndRaid();
		}

	}

	// ═══════════════════════════════════════════════
	// Raid 开始/结束
	// ═══════════════════════════════════════════════

	private void StartRaid()
	{
		_isInRaid = true;
		_lastTickTime = Time.GetUnixTimeFromSystem();
		_elapsedHours = 0f;

		//MVP:构建默认玩家快照（后续DeploymentUI构建）
		_player = new PlayerSnapshot
		{
			KnowledgeBonus = 1.0f,
			ProficiencyBonus = 1.0f,
			GunAccuracy = 0.85f,
			GunCategory = GunCategory.AR,
			OptimalRange = 80f,
			AmmoDamage = 60f,
			GunCoeff = 1.0f,
			FireRate = 5f,
			TotalWeight = 7f,
			WeightPenalty = 0f,
			SidePlatePenalty = 0f
		};

		//MVP:默认工厂+180发弹药
		_currentMapId = "map_factory";
		_ammoLeft = 180;

		GD.Print($"[CombatManager] Raid开始|地图 {_currentMapId} 弹药： {_ammoLeft} .");
	}

	private void EndRaid()
	{
		_isInRaid = false;
		GD.Print($"[CombatManager] Raid结束|耗时 {_elapsedHours} 小时,剩余弹药 {_ammoLeft} .");
	}

	// ═══════════════════════════════════════════════
	// 时间驱动Tick
	// ═══════════════════════════════════════════════
	public override void _Process(double delta)
	{
		if (!_isInRaid)
		{
			return;
		}

		double now = Time.GetUnixTimeFromSystem();
		double elapsed = now - _lastTickTime;

		if (elapsed < TickInterval)
		{
			return;
		}
		_lastTickTime = now;

		//一转用realtime小时
		float hours = (float)elapsed / 3600f;
		_elapsedHours += hours;

		if (_currentEnemy == null)
		{
			//抽敌人
			_currentEnemy = EnemyPoolResolver.Roll(_currentMapId);
			if (_currentEnemy == null)
			{
				return;
			}
		}

		//执行单发
		ResolveShotAndApply();
	}

	// ═══════════════════════════════════════════════
	// 6 步管线：单发
	// ═══════════════════════════════════════════════
	private void ResolveShotAndApply()
	{
		_ammoLeft--;
		if (_ammoLeft < 0)
		{
			_ammoLeft = 0;
		}

		//1、接敌距离
		var map = ConfigManager.Instance.GetConfig<MapDistanceConfig>(_currentMapId);
		float dist = map != null ? DamageCalculator.RollEngagementDistance(map) : 50f;

		//2、射程修正
		float rangeCorr = DamageCalculator.CalcRangeCorrection(dist, _player.GunCategory, _player.OptimalRange);

		EventBus.Instance.EmitSignal(EventBus.SignalName.EngagementDistance, dist, rangeCorr);

		if (rangeCorr <= 0f)
		{
			AppendCombatLog($"[color=gray]距离 {dist:F0}m — 超出有效射程[/color]");
			CheckEvacAndBody();
			return;
		}

		//3、命中判定
		var weight = WeightSystem.CalcTierMvp(_player.TotalWeight);
		float hitChance = DamageCalculator.CalcHitChance(_player, weight.HitPenalty, rangeCorr);
		bool isHit = DamageCalculator.RollHit(hitChance);

		if (!isHit)
		{
			AppendCombatLog($"[color=red]✗ MISS — 距离 {dist:F0}m 命中率 {hitChance:P0}[/color]");
			CheckEvacAndBody();
			return;
		}

		//4、部位判定
		BodyPart part = DamageCalculator.RollBodyPart(hitChance);

		//5、护甲判定
		bool isUnarmored = !_player.Armor.IsCovered(part);
		float armorReduction = _player.Armor.GetReduction(part);

		//6、伤害计算
		float damage = DamageCalculator.CalcShotDamage(_player.AmmoDamage, _player.GunCoeff, armorReduction, isUnarmored);

		//应用伤害
		float remainingHp = _currentEnemy.GetHp(part) - damage;
		bool killed = remainingHp <= 0f;

		bool isCrit = isUnarmored && damage >= _player.AmmoDamage * _player.GunCoeff * 1.5f;
		string line = killed ? $"[color=orange]☠ {_currentEnemy.DisplayName} {PartName(part)}归零 — 击杀！[/color]"
			  : isCrit
				  ? $"[color=yellow]★ 暴击 {PartName(part)} — {damage:F0} 伤害（无甲）[/color]"
				  : $"[color=green]✓ 命中 {PartName(part)} — {damage:F0} 伤害[/color]";

		AppendCombatLog(line);

		EventBus.Instance.EmitSignal(EventBus.SignalName.Hit, PartName(part), damage, isCrit);

		if (killed)
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, _currentEnemy.EnemyId, "");
			_currentEnemy = null;
		}

		CheckEvacAndBody();
	}
	// ═══════════════════════════════════════════════
	// 撤离 + 身体状态
	// ═══════════════════════════════════════════════

	private void CheckEvacAndBody()
	{
		// 身体状态推进
		BodyStateSystem.AdvanceTime((float)TickInterval / 3600f);

		// 撤离条件检查
		var reason = EvacSystem.Check(_player, _ammoLeft, _elapsedHours, _mapTimeLimitHours);

		if (reason != EvacSystem.EvacReason.None)
		{
			GD.Print($"[CombatManager] 触发撤离: {EvacSystem.ReasonText(reason)}");
			GameManager.Instance.Transition(GameState.Settling);
		}
	}

	// ═══════════════════════════════════════════════
	// 工具
	// ═══════════════════════════════════════════════

	private static string PartName(BodyPart part) => part switch
	{
		BodyPart.Head => "头部",
		BodyPart.Chest => "胸部",
		BodyPart.Abdomen => "腹部",
		BodyPart.LeftArm => "左臂",
		BodyPart.RightArm => "右臂",
		BodyPart.LeftLeg => "左腿",
		BodyPart.RightLeg => "右腿",
		_ => "??"
	};

	private static void AppendCombatLog(string text)
	{
		// CombatZoneUI 等后续通过 Hit/EnemyKilled 等信号拼装日志
		GD.Print($"[Combat] {text}");
	}

}
