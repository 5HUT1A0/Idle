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

		GD.Print("═══ SafeHouseSystem 验证开始 ═══");

		// 1. 初始等级
		GD.Print($"仓库 Lv{SafeHouseSystem.GetLevel(SafeHouseSystem.Warehouse)} (应为1)");
		GD.Print($"工作台 Lv{SafeHouseSystem.GetLevel(SafeHouseSystem.Workbench)} (应为1)");
		GD.Print($"健身 Lv{SafeHouseSystem.GetLevel(SafeHouseSystem.Gym)} (应为1)");
		GD.Print($"靶场 Lv{SafeHouseSystem.GetLevel(SafeHouseSystem.Range)} (应为1)");
		GD.Print($"医务 Lv{SafeHouseSystem.GetLevel(SafeHouseSystem.Infirmary)} (应为0)");

		// 2. 解锁状态
		GD.Print($"工作台已解锁: {SafeHouseSystem.IsUnlocked(SafeHouseSystem.Workbench)} (应为True)");
		GD.Print($"医务室已解锁: {SafeHouseSystem.IsUnlocked(SafeHouseSystem.Infirmary)} (应为False)");
		GD.Print($"医务室解锁提示: \"{SafeHouseSystem.GetUnlockHint(SafeHouseSystem.Infirmary)}\"");

		// 3. 升级工作台到 Lv2 → 解锁医务室
		GD.Print("── 升级工作台到 Lv2 ──");
		DataManager.Instance.SetFacilityLevel(SafeHouseSystem.Workbench, 2);
		GD.Print($"工作台 Lv{SafeHouseSystem.GetLevel(SafeHouseSystem.Workbench)}");
		GD.Print($"医务室已解锁: {SafeHouseSystem.IsUnlocked(SafeHouseSystem.Infirmary)} (应为True)");

		// 4. CanUpgrade
		GD.Print($"工作台可升级: {SafeHouseSystem.CanUpgrade(SafeHouseSystem.Workbench)} (应为True)");
		DataManager.Instance.SetFacilityLevel(SafeHouseSystem.Workbench, 10);
		GD.Print($"工作台满级后可升级: {SafeHouseSystem.CanUpgrade(SafeHouseSystem.Workbench)} (应为False)");

		// 5. 升级进度
		GD.Print("── 升级进度 ──");
		SafeHouseSystem.StartUpgrade(SafeHouseSystem.Gym);
		GD.Print($"开始升级后进度: {SafeHouseSystem.GetUpgradeProgress(SafeHouseSystem.Gym):P0}");
		SafeHouseSystem.AddProgress(SafeHouseSystem.Gym, 0.5f);
		GD.Print($"推进0.5h后进度: {SafeHouseSystem.GetUpgradeProgress(SafeHouseSystem.Gym):P0}");


		// 6. 仓库容量
		GD.Print($"仓库容量 Lv1: {SafeHouseSystem.GetWarehouseCapacity()}格 (应为30)");
		DataManager.Instance.SetFacilityLevel(SafeHouseSystem.Warehouse, 3);
		GD.Print($"仓库容量 Lv3: {SafeHouseSystem.GetWarehouseCapacity()}格 (应为48)");

		// 7. 医务室回复
		GD.Print("── 战后治疗 ──");
		DataManager.Instance.HpHead = 60f;
		DataManager.Instance.HpChest = 50f;
		SafeHouseSystem.PostRaidHeal();
		float healRate = SafeHouseSystem.GetInfirmaryHealRate();
		GD.Print($"回复率: {healRate:P0} | 头部HP: {DataManager.Instance.HpHead:F0} (应为{60f + 100f * healRate:F0})");

		GD.Print("═══ SafeHouseSystem 验证结束 ═══");
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
