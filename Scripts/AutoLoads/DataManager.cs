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
			InventorySystem.InitDefultItems();  //新档，送
		}

		// ═══ 验证 CustomGunSystem ═══
		GD.Print("═══ CustomGunSystem 验证开始 ═══");

		// 1. 查库存
		var bodySlots = InventorySystem.FindSlots("body_ar_t1");
		GD.Print($"枪身库存: {bodySlots.Count}件");

		var barrelSlots = InventorySystem.FindSlots("barrel_ar_standard_t1");
		GD.Print($"枪管库存: {barrelSlots.Count}件");

		var magSlots = InventorySystem.FindSlots("mag_ar_standard_t1");
		GD.Print($"弹匣库存: {magSlots.Count}件");

		// 2. 组装
		if (bodySlots.Count > 0 && barrelSlots.Count > 0 && magSlots.Count > 0)
		{
			var gun = CustomGunSystem.TryAssemble(
				"body_ar_t1", "barrel_ar_standard_t1", "mag_ar_standard_t1",
				gunName: "验证用AR");

			if (gun != null)
			{
				GD.Print($"✅ 组装成功: {gun.GunName} (ID={gun.GunId})");

				// 3. 查枪库
				var allGuns = CustomGunSystem.GetAllGun();
				GD.Print($"枪库数量: {allGuns.Count}");

				// 4. 验库存扣减
				GD.Print($"组装后枪身库存: {InventorySystem.FindSlots("body_ar_t1").Count}件 (应为0)");

				// 5. 拆卸
				CustomGunSystem.Disassemble(gun.GunId);
				GD.Print($"拆卸后枪身库存: {InventorySystem.FindSlots("body_ar_t1").Count}件 (应为1)");
				GD.Print($"拆卸后枪库: {CustomGunSystem.GetAllGun().Count}把 (应为0)");
			}
			else
			{
				GD.Print("❌ 组装失败");
			}
		}
		else
		{
			GD.Print("❌ 库存不足，无法验证组装");
		}

		GD.Print("═══ CustomGunSystem 验证结束 ═══");

		GD.Print($"[DataManager] 就绪 | 废土币:{_scrapCurrency} 饥饿:{_hunger} 口渴:{_thirst}");
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
