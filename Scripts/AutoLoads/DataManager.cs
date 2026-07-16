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

	//=================================================
	//生命周期
	//=================================================
	public override void _Ready()
	{
		Instance = this;


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

		GD.Print("═══ DurabilitySystem 验证开始 ═══");

		// 1. 三档效果边界
		GD.Print("── 耐久度 100% ──");
		var e100 = DurabilitySystem.CalcEffects(100f);
		GD.Print($"  精度惩罚: {e100.AccuracyPenalty} (应为0)");
		GD.Print($"  故障率: {e100.MalfunctionChance} (应为0)");
		GD.Print($"  报废: {e100.IsBroken} (应为False)");
		GD.Print($"  档位: {e100.TierLabel} (应为良好)");

		GD.Print("── 耐久度 70% ──");
		var e70 = DurabilitySystem.CalcEffects(70f);
		GD.Print($"  精度惩罚: {e70.AccuracyPenalty} (应为0)");

		GD.Print("── 耐久度 69% ──");
		var e69 = DurabilitySystem.CalcEffects(69f);
		GD.Print($"  精度惩罚: {e69.AccuracyPenalty} (应为0.05)");
		GD.Print($"  故障率: {e69.MalfunctionChance} (应为0.05)");
		GD.Print($"  档位: {e69.TierLabel} (应为轻度磨损)");

		GD.Print("── 耐久度 30% ──");
		var e30 = DurabilitySystem.CalcEffects(30f);
		GD.Print($"  精度惩罚: {e30.AccuracyPenalty} (应为0)");

		GD.Print("── 耐久度 29% ──");
		var e29 = DurabilitySystem.CalcEffects(29f);
		GD.Print($"  精度惩罚: {e29.AccuracyPenalty} (应为0.20)");
		GD.Print($"  故障率: {e29.MalfunctionChance} (应为0.20)");
		GD.Print($"  档位: {e29.TierLabel} (应为严重磨损)");

		GD.Print("── 耐久度 0% ──");
		var e0 = DurabilitySystem.CalcEffects(0f);
		GD.Print($"  报废: {e0.IsBroken} (应为True)");
		GD.Print($"  档位: {e0.TierLabel} (应为报废)");

		// 2. 磨损计算
		GD.Print("── 磨损计算 ──");
		float wear1 = DurabilitySystem.CalcWear(1.5f, 1.0f);
		GD.Print($"  1.5h 标准消耗: {wear1:F1} (应为1.5)");

		float wear2 = DurabilitySystem.CalcWear(0.5f, 2.0f);
		GD.Print($"  0.5h 双倍消耗: {wear2:F1} (应为1.0)");

		// 3. 故障率批量统计（1000发）
		GD.Print("── 故障率抽样（1000发，故障率10%）──");
		int malfunctions = 0;
		for (int i = 0; i < 1000; i++)
		{
			if (DurabilitySystem.RollMalfunction(0.10f))
				malfunctions++;
		}
		GD.Print($"  故障次数: {malfunctions}/1000 (期望 ~100)");

		GD.Print("═══ DurabilitySystem 验证结束 ═══");
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

		GD.Print($"[DataManager] 存档数据已加载 | 废土币:{_scrapCurrency}");
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
