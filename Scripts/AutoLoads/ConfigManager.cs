using Godot;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// 配置加载器 Autoload（优先级 3）。
/// 启动时一次性加载所有 .tres Resource 到内存字典，运行时只读查询。
/// 依赖：EventBus（仅发信号，不依赖其状态）。
/// </summary>
public partial class ConfigManager : Node
{
	public ConfigManager Instace { get; private set; }

	//===============================
	//内存字典
	//===============================

	private Dictionary<string, BaseItemData> _itemDb = new();
	private Dictionary<string, GunPartData> _gunPartDb = new();
	private Dictionary<string, AmmoData> _ammoDb = new();
	private Dictionary<string, LevelCurve> _curveDb = new();

	//==============================
	//生命周期
	//==============================
	public override void _Ready()
	{
		Instace = this;
		LoadAllConfigs();
		GD.Print($"[ConfigManager] 加载完成 | 物品:{_itemDb.Count} 配件:{_gunPartDb.Count} 弹药:{_ammoDb.Count} 曲线:{_curveDb.Count}");
	}

	private void LoadAllConfigs()
	{
		// 物品（含弹药也会进此表——AmmoData 是 BaseItemData 子类）
		LoadIndex("res://Data/Config/Items/ConfigIndex.tres", (BaseItemData item) =>
		{
			_itemDb[item.ItemId] = item;
			if (item is AmmoData ammo)
				_ammoDb[ammo.ItemId] = ammo;
		});
		// 枪械配件
		LoadIndex("res://Data/Config/Guns/ConfigIndex.tres", (GunPartData part) =>
		{
			_gunPartDb[part.ItemId] = part;
			_itemDb[part.ItemId] = part; // 同时也进物品表
		});

		// 数值曲线
		LoadIndex("res://Data/Config/Curves/ConfigIndex.tres", (LevelCurve curve) =>
		{
			// LevelCurve.ItemId 作为 key
			_curveDb[curve.ItemId] = curve;
		});
	}

	/// <summary>
	/// 泛型加载索引文件，索引不存在时候时打印警告，不崩溃
	/// </summary>
	private void LoadIndex<T>(string path, System.Action<T> onEntry) where T : Resource
	{
		if (!ResourceLoader.Exists(path))
		{
			GD.PushWarning($"[ConfigManager] 索引文件不存在,跳过：{path}");
			return;
		}

		var index = GD.Load<ConfigIndex>(path);
		if (index?.Entries == null)
		{
			GD.PushWarning($"[ConfigManager] 索引文件 Entries 为空,跳过：{path}");
			return;
		}

		foreach (var entry in index.Entries)
		{
			if (entry is T typed)
			{
				try { onEntry(typed); }
				catch (System.Exception e)
				{
					GD.PushError($"[ConfigManager] 加载条目失败: {path} → {e.Message}");
				}
			}
		}
	}

	//==============================
	//查询接口
	//==============================

	/// <summary> 按ID获取配置（泛型版本）</summary>
	public T GetConfig<T>(string Id) where T : Resource
	{
		if (typeof(T) == typeof(GunPartData) || typeof(T).IsSubclassOf(typeof(GunPartData)))
			return _gunPartDb.TryGetValue(Id, out var part) ? part as T : null;

		if (typeof(T) == typeof(AmmoData) || typeof(T).IsSubclassOf(typeof(AmmoData)))
			return _ammoDb.TryGetValue(Id, out var ammo) ? ammo as T : null;

		//默认走物品总表（BaseItemData及其单独建表子类）
		return _itemDb.TryGetValue(Id, out var item) ? item as T : null;
	}

	/// <summary> 按槽位筛选配件</summary>
	public IEnumerable<GunPartData> GetBySlot(PartSlot slot) =>
		_gunPartDb.Values.Where(p => p.Slot == slot);


	/// <summary>获取指定类型配置</summary>
	public IEnumerable<T> GetAll<T>() where T : Resource
	{
		if (typeof(T) == typeof(GunPartData) || typeof(T).IsSubclassOf(typeof(GunPartData)))
			return _gunPartDb.Values.Cast<T>();

		if (typeof(T) == typeof(AmmoData) || typeof(T).IsSubclassOf(typeof(AmmoData)))
			return _ammoDb.Values.Cast<T>();

		return _itemDb.Values.OfType<T>();
	}

	/// <summary>获取数值曲线。失败返回 null（调用方需判空兜底）</summary>
	public LevelCurve GetCurve(string curveId)
	{
		_curveDb.TryGetValue(curveId, out var curve);
		return curve;
	}
}

