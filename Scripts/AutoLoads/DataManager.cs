using Godot;
using System;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// 运行时数据中心 Autoload（优先级 4）。
/// 持有所有运行时可变数据。数据变更 → _isDirty + EventBus 信号广播。
/// 依赖：EventBus。
/// </summary>
public partial class DataManager : Node
{
	public static DataManager Instance { get; private set; }
	private bool _isDirty;
	private Dictionary<string, string> _dirtyState = new();


	//=================================================
	//玩家属性
	//=================================================
	private int _scrapCurrency;
	public int ScrapCurrency
	{
		get => _scrapCurrency;
		set
		{
			if (_scrapCurrency == value)
			{
				return;
			}
			int delta = value - _scrapCurrency;
			_scrapCurrency = value;
			MarkDirty("scrap_currency", value.ToString());
			EventBus.Instance.EmitSignal(EventBus.SignalName.CurrencyChanged, "scrap", _scrapCurrency, delta);
		}
	}

	private float _hunger = 100f;
	public float Hunger
	{
		get => _hunger;
		set
		{
			_hunger = Mathf.Clamp(value, 0f, 100f);
			MarkDirty("hunger", _hunger.ToString("F1"));
		}
	}

	private float _thirst = 100f;
	public float Thirst
	{
		get => _thirst;
		set
		{
			_thirst = Mathf.Clamp(value, 0f, 100f);
			MarkDirty("thirst", _thirst.ToString("F1"));
		}
	}

	// 部位 HP（后续 BodyStateSystem 会用到）
	public float HpHead { get; set; } = 100f;
	public float HpChest { get; set; } = 100f;
	public float HpAbdomen { get; set; } = 100f;
	public float HpLeftArm { get; set; } = 100f;
	public float HpRightArm { get; set; } = 100f;
	public float HpLeftLeg { get; set; } = 100f;
	public float HpRightLeg { get; set; } = 100f;

	//=================================================
	//仓库物品
	//=================================================
	private List<InventorySlot> _inventory = new();
	public List<InventorySlot> Inventory => _inventory;
	private int _nextSlotId = 1; // 内存中唯一标识槽位的自增ID，存档时不写入

	private List<CustomGun> _customGuns = new();
	public List<CustomGun> CustomGuns => _customGuns;
	private int _nextGunId = 1; // 内存中唯一标识枪械的自增ID，存档时不写入

	private List<CustomArmor> _customArmors = new();
	public List<CustomArmor> CustomArmors => _customArmors;
	private int _nextArmorId = 1;

	public void AddInventorySlot(InventorySlot slot)
	{
		slot.SlotId = _nextSlotId++;
		_inventory.Add(slot);
		MarkDirty("inventory_version", DateTime.UtcNow.Ticks.ToString());
	}

	public void RemoveInventorySlot(int slotId)
	{
		_inventory.RemoveAll(s => s.SlotId == slotId);
		MarkDirty("inventory_version", DateTime.UtcNow.Ticks.ToString());
	}

	/// <summary>SaveManager 加载库存后调用</summary>
	public void SetInventory(List<InventorySlot> slots)
	{
		_inventory = slots;
		_nextSlotId = slots.Count > 0 ? slots.Max(s => s.SlotId) + 1 : 1;
	}

	public void AddCustomGun(CustomGun gun)
	{
		gun.GunId = _nextGunId++;
		_customGuns.Add(gun);
	}

	public void RemoveCustomGun(int gunId)
	{
		_customGuns.RemoveAll(g => g.GunId == gunId);
	}

	public void SetCustomGuns(List<CustomGun> guns)
	{
		_customGuns = guns;
		_nextGunId = guns.Count > 0 ? guns.Max(g => g.GunId) + 1 : 1;
	}

	public void AddCustomArmor(CustomArmor armor)
	{
		armor.ArmorId = _nextArmorId++;
		_customArmors.Add(armor);
		MarkDirty("custom_armor_version", DateTime.UtcNow.Ticks.ToString());
	}

	public void RemoveCustomArmor(int armorId)
	{
		_customArmors.RemoveAll(a => a.ArmorId == armorId);
		MarkDirty("custom_armor_version", DateTime.UtcNow.Ticks.ToString());
	}

	public void SetCustomArmors(List<CustomArmor> armors)
	{
		_customArmors = armors;
		_nextArmorId = armors.Count > 0 ? armors.Max(a => a.ArmorId) + 1 : 1;
	}

	// ═══════════════════════════════════════════════
	// 设施等级（SafeHouseSystem 使用）
	// ═══════════════════════════════════════════════

	private Dictionary<string, int> _facilityLevels = new();
	private Dictionary<string, float> _facilityUpgradeProgress = new();

	public int GetFacilityLevel(string facilityId)
	=> _facilityLevels.TryGetValue(facilityId, out var v) ? v : 0;

	public void SetFacilityLevel(string facilityId, int level)
	{
		_facilityLevels[facilityId] = level;
		MarkDirty($"facility_{facilityId}_level", level.ToString());
	}

	public bool HasFacilityLevel(string facilityId)
	=> _facilityLevels.ContainsKey(facilityId);

	public float GetFacilityUpgradeProgress(string facilityId)
	=> _facilityUpgradeProgress.TryGetValue(facilityId, out var v) ? v : 0f;

	public void SetFacilityUpgradeProgress(string facilityId, float progress)
	{
		_facilityUpgradeProgress[facilityId] = progress;
		MarkDirty($"facility_{facilityId}_progress", progress.ToString("F2"));
	}

	public bool HasFacilityUpgradeProgress(string facilityId)
	=> _facilityUpgradeProgress.ContainsKey(facilityId);

	public void ClearFacilityUpgradeProgress(string facilityId)
	{
		_facilityUpgradeProgress.Remove(facilityId);
		MarkDirty($"facility_{facilityId}_progress", "");
	}

	// ═══════════════════════════════════════════════
	// 训练系统（TrainingSystem 使用）
	// ═══════════════════════════════════════════════

	private Dictionary<string, int> _trainingLevels = new();
	private Dictionary<string, float> _trainingProgress = new();

	private static string TrainKey(TrainingSystem.TrainingLine line)
		=> $"train_{line}_level";

	private static string ProgKey(TrainingSystem.TrainingLine line)
		=> $"train_{line}_progress";

	public int GetTrainingLevel(TrainingSystem.TrainingLine line)
		=> _trainingLevels.TryGetValue(TrainKey(line), out var v) ? v : 1;

	public void SetTrainingLevel(TrainingSystem.TrainingLine line, int level)
	{
		string key = TrainKey(line);
		_trainingLevels[key] = level;
		MarkDirty(key, level.ToString());
	}

	public float GetTrainingProgress(TrainingSystem.TrainingLine line)
		=> _trainingProgress.TryGetValue(ProgKey(line), out var v) ? v : 0f;

	public void SetTrainingProgress(TrainingSystem.TrainingLine line, float progress)
	{
		string key = ProgKey(line);
		_trainingProgress[key] = progress;
		MarkDirty(key, progress.ToString("F3"));
	}

	public bool HasTrainingProgress(TrainingSystem.TrainingLine line)
		=> _trainingProgress.TryGetValue(ProgKey(line), out var v) && v >= 0f;

	public void ClearTrainingProgress(TrainingSystem.TrainingLine line)
	{
		string key = ProgKey(line);
		_trainingProgress.Remove(key);
		MarkDirty(key, "");
	}


	//=================================================
	//生命周期
	//=================================================
	public override void _Ready()
	{
		Instance = this;

		SafeHouseSystem.Init();

		// 从 SaveManager 取回存档数据（如果存在）
		var savedState = SaveManager.Instance.TakeLoadedState();
		if (savedState != null)
		{
			LoadState(savedState);
		}

		var savedInv = SaveManager.Instance.TakeLoadedInventory();
		if (savedInv != null && savedInv.Count > 0)
		{
			SetInventory(savedInv);
		}
		else
		{
			InventorySystem.InitDefaultItems();  //新档，送
		}

		GD.Print("═══ TrainingSystem 验证开始 ═══");

		// 1. 初始等级
		GD.Print($"体能 Lv{TrainingSystem.GetLevel(TrainingSystem.TrainingLine.Stamina)} (应为1)");
		GD.Print($"靶场 Lv{TrainingSystem.GetLevel(TrainingSystem.TrainingLine.ShootingRange)} (应为1)");
		GD.Print($"学识 Lv{TrainingSystem.GetLevel(TrainingSystem.TrainingLine.Knowledge)} (应为1)");

		// 2. XP 曲线
		GD.Print("── XP 需求 ──");
		GD.Print($"Lv1→2: {TrainingSystem.GetXpRequired(1):F2}h (应为0.05)");
		GD.Print($"Lv10→11: {TrainingSystem.GetXpRequired(10):F2}h (应为1.58)");
		GD.Print($"Lv29→30: {TrainingSystem.GetXpRequired(29):F2}h (应为7.81)");

		// 3. 开始训练 + 推进进度
		GD.Print("── 体能训练 ──");
		TrainingSystem.StartTraining(TrainingSystem.TrainingLine.Stamina);
		GD.Print($"训练中: {TrainingSystem.IsTraining(TrainingSystem.TrainingLine.Stamina)} (应为True)");
		GD.Print($"进度: {TrainingSystem.GetProgress(TrainingSystem.TrainingLine.Stamina):P0} (应为0%)");

		// 推进 0.02h (1.2min) → 应该还在 Lv1
		TrainingSystem.AddProgress(TrainingSystem.TrainingLine.Stamina, 0.02f);
		GD.Print($"推进0.02h后进度: {TrainingSystem.GetProgress(TrainingSystem.TrainingLine.Stamina):P0} (应为40%)");

		// 推进到升级 (还需要 0.03h)
		TrainingSystem.AddProgress(TrainingSystem.TrainingLine.Stamina, 0.05f);
		GD.Print($"继续0.05h后 Lv: {TrainingSystem.GetLevel(TrainingSystem.TrainingLine.Stamina)} (应为2)");
		GD.Print($"新进度: {TrainingSystem.GetProgress(TrainingSystem.TrainingLine.Stamina):P0}");

		// 4. 批量升级测试
		GD.Print("── 批量升级 Lv2→5 ──");
		DataManager.Instance.SetTrainingLevel(TrainingSystem.TrainingLine.Knowledge, 2);
		TrainingSystem.StartTraining(TrainingSystem.TrainingLine.Knowledge);
		// Lv2: 0.14h, Lv3: 0.26h, Lv4: 0.40h, Lv5: 0.56h  合计≈1.36h
		TrainingSystem.AddProgress(TrainingSystem.TrainingLine.Knowledge, 2.0f);
		GD.Print($"学识推进2h后 Lv: {TrainingSystem.GetLevel(TrainingSystem.TrainingLine.Knowledge)} (应为5+)");

		// 5. 效果计算
		GD.Print("── 效果 ──");
		GD.Print($"体能 轻/中 阈值: {TrainingSystem.GetStaminaLightMax():F1}kg (Lv1→8.5)");
		GD.Print($"体能 中/重 阈值: {TrainingSystem.GetStaminaMediumMax():F1}kg (Lv1→16)");
		GD.Print($"靶场 换弹倍率: {TrainingSystem.GetReloadTimeMultiplier():F3} (Lv1→0.980)");
		GD.Print($"学识 命中加成: {TrainingSystem.GetKnowledgeBonus():F3} (Lv1→1.005)");

		// 6. 满级测试
		GD.Print("── 满级 ──");
		GD.Print($"Lv30 是满级: {TrainingSystem.IsMaxLevel(TrainingSystem.TrainingLine.Stamina)} (Lv1→False)");

		GD.Print("═══ TrainingSystem 验证结束 ═══");
	}

	// ═══════════════════════════════════════════════
	// 存档接口（由 SaveManager 调用）
	// ═══════════════════════════════════════════════

	/// <summary>
	/// SaveManager 加载存档后调用，填充运行时数据。
	/// </summary>

	public void LoadState(Dictionary<string, string> state)
	{
		_scrapCurrency = GetInt(state, "scrap_currency", 500);
		_hunger = GetFloat(state, "hunger", 100f);
		_thirst = GetFloat(state, "thirst", 100f);
		HpHead = GetFloat(state, "hp_head", 100f);
		HpChest = GetFloat(state, "hp_chest", 100f);
		HpAbdomen = GetFloat(state, "hp_abdomen", 100f);
		HpLeftArm = GetFloat(state, "hp_left_arm", 100f);
		HpRightArm = GetFloat(state, "hp_right_arm", 100f);
		HpLeftLeg = GetFloat(state, "hp_left_leg", 100f);
		HpRightLeg = GetFloat(state, "hp_right_leg", 100f);

		// 恢复设施等级
		_facilityLevels = new Dictionary<string, int>();
		foreach (var kv in state)
		{
			if (kv.Key.StartsWith("facility_") && kv.Key.EndsWith("_level"))
			{
				string facilityId = kv.Key.Replace("facility_", "").Replace("_level", "");
				if (int.TryParse(kv.Value, out var lv))
					_facilityLevels[facilityId] = lv;
			}
		}

		_facilityUpgradeProgress = new Dictionary<string, float>();
		foreach (var kv in state)
		{
			if (kv.Key.StartsWith("facility_") && kv.Key.EndsWith("_progress"))
			{
				string facilityId = kv.Key.Replace("facility_", "").Replace("_progress", "");
				if (float.TryParse(kv.Value, out var prog) && prog > 0f)
					_facilityUpgradeProgress[facilityId] = prog;
			}
		}

		// 恢复训练等级和进度
		_trainingLevels = new Dictionary<string, int>();
		_trainingProgress = new Dictionary<string, float>();
		foreach (var line in System.Enum.GetNames(typeof(TrainingSystem.TrainingLine)))
		{
			string levelKey = $"train_{line}_level";
			string progKey = $"train_{line}_progress";
			if (state.TryGetValue(levelKey, out var lv) && int.TryParse(lv, out var lvInt))
				_trainingLevels[levelKey] = lvInt;
			if (state.TryGetValue(progKey, out var prog) && float.TryParse(prog, out var progFloat))
				_trainingProgress[progKey] = progFloat;
		}

		GD.Print($"[DataManager] 存档数据已加载 | 废土币:{_scrapCurrency} | 升级进度: {string.Join(", ", _facilityUpgradeProgress.Select(kv => $"{kv.Key}:{kv.Value:F2}"))}");
		//库存由SaveManager加载 → SetInventory()

	}



	/// <summary>
	/// SaveManager.DoFlush() 调用，收集所有脏数据批量写入。
	/// </summary>
	public Dictionary<string, string> CollectDirtyState()
	{
		var snapshot = new Dictionary<string, string>(_dirtyState);
		_dirtyState.Clear();
		_isDirty = false;
		return snapshot;
	}

	public bool IsDirty() => _isDirty;

	//================================================
	//私有方法
	//================================================
	private void MarkDirty(string key, string value)
	{
		_isDirty = true;
		_dirtyState[key] = value;

	}

	private static int GetInt(Dictionary<string, string> dict, string key, int fallback)
	{
		return dict.TryGetValue(key, out var v) && int.TryParse(v, out var result) ? result : fallback;
	}

	private static float GetFloat(Dictionary<string, string> dict, string key, float fallback)
	{
		return dict.TryGetValue(key, out var v) && float.TryParse(v, out var result) ? result : fallback;
	}


}
