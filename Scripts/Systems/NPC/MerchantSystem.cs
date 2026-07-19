using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 商人系统——3 商人（杂货商/秃鹫/金盾），固定商品表+价格，24h 刷新。
/// MVP 仅废土币交易，好感度折扣后续扩展。
/// </summary>
public partial class MerchantSystem : Node
{
	public const string JunkDealer = "junk_dealer";
	public const string Vulture = "vulture";
	public const string GoldenShield = "golden_shield";

	private static Dictionary<string, MerchantDef> _defs;
	private static bool _initialized;

	/// <summary>商品条目</summary>
	public class ProductEntry
	{
		public string ItemId;
		public string DisplayName;
		public int BuyPrice;        // 商人卖价
		public int SellPrice;       // 商人收价
		public int Stock;           // 当前库存
		public int MaxStock;        // 刷新时库存上限
	}

	/// <summary>商人定义</summary>
	public class MerchantDef
	{
		public string MerchantId;
		public string DisplayName;
		public float BuyPriceMultiplier = 1.2f;            // 买入价格系数（折扣）
		public float SellPriceMultiplier = 0.4f;           // 卖出价格系数（折扣）
		public (string itemId, int qty)[] Products; // 商品列表（itemId, maxStock）
		public int RefreshHours = 24;                    // 刷新间隔（小时）
	}

	// ═══════════════════════════════════════════════
	// 初始化
	// ═══════════════════════════════════════════════

	public static void Init()
	{
		if (_initialized) return;

		_defs = new Dictionary<string, MerchantDef>
		{
			[JunkDealer] = new MerchantDef
			{
				MerchantId = JunkDealer,
				DisplayName = "杂货商",
				BuyPriceMultiplier = 1.1f,
				SellPriceMultiplier = 0.35f,
				Products = new (string, int)[]
				  {
					  ("body_ar_t1", 2),
					  ("barrel_ar_standard_t1", 2),
					  ("mag_ar_standard_t1", 5),
					  ("liner_chestrig_t1", 1),
				  }
			},
			[Vulture] = new MerchantDef
			{
				MerchantId = Vulture,
				DisplayName = "秃鹫",
				BuyPriceMultiplier = 1.3f,
				SellPriceMultiplier = 0.45f,
				Products = new (string, int)[]
				  {
					  ("barrel_ar_standard_t1", 3),
					  ("plate_lighthard_t1", 1),
					  ("body_ar_t1", 1),
				  }
			},
			[GoldenShield] = new MerchantDef
			{
				MerchantId = GoldenShield,
				DisplayName = "金盾",
				BuyPriceMultiplier = 1.5f,
				SellPriceMultiplier = 0.55f,
				Products = new (string, int)[]
				  {
					  ("plate_lighthard_t1", 2),
					  ("body_ar_t1", 2),
				  }
			}
		};

		//恢复或初始化商品库存
		foreach (var (id, def) in _defs)
		{
			if (!DataManager.Instance.HasMerchantStock(id))
			{
				RefreshMerchant(id);
			}
		}

		_initialized = true;

		foreach (var (id, def) in _defs)
		{
			var products = GetProducts(id);
			GD.Print($"  {def.DisplayName}: {products.Count}种商品");
		}
	}

	// ═══════════════════════════════════════════════
	// 查询
	// ═══════════════════════════════════════════════

	public static List<string> GetAllMerchantIds()
		=> _defs.Keys.ToList();

	public static MerchantDef GetDef(string merchantId)
		=> _defs.TryGetValue(merchantId, out var def) ? def : null;

	/// <summary>获取商人商品列表(含价格和库存)</summary>
	public static List<ProductEntry> GetProducts(string merchantId)
	{
		var def = GetDef(merchantId);
		if (def == null) return new List<ProductEntry>();

		var stock = DataManager.Instance.GetMerchantStock(merchantId);
		var result = new List<ProductEntry>();

		foreach (var (itemId, maxQty) in def.Products)
		{
			var config = ConfigManager.Instance.GetConfig<BaseItemData>(itemId);
			int baseValue = config?.BaseValue ?? 100;
			int stockQty = stock.TryGetValue(itemId, out var s) ? s : maxQty;

			result.Add(new ProductEntry
			{
				ItemId = itemId,
				DisplayName = config?.DisplayName ?? itemId,
				BuyPrice = Mathf.CeilToInt(baseValue * def.BuyPriceMultiplier),
				SellPrice = Mathf.FloorToInt(baseValue * def.SellPriceMultiplier),
				Stock = stockQty,
				MaxStock = maxQty
			});
		}
		return result;
	}

	/// <summary>获取单个商品的买入价</summary>
	public static int GetBuyPrice(string merchantId, string itemId)
	{
		var products = GetProducts(merchantId);
		return products.FirstOrDefault(p => p.ItemId == itemId)?.BuyPrice ?? -1;
	}

	/// <summary>距下次刷新的剩余时间</summary>
	public static float GetTimeUntilRefresh(string merchantId)
	{
		var def = GetDef(merchantId);
		if (def == null) return 0f;

		long lastRefresh = DataManager.Instance.GetMerchantLastRefresh(merchantId);
		long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		float elapsed = (now - lastRefresh) / 3600f;
		return Mathf.Max(def.RefreshHours - elapsed, 0f);
	}

	// ═══════════════════════════════════════════════
	// 交易
	// ═══════════════════════════════════════════════

	/// <summary>玩家购买--扣废土币，加库存</summary>
	public static bool Buy(string merchantId, string itemId, int quantity)
	{
		if (quantity <= 0) return false;

		var products = GetProducts(merchantId);
		var product = products.FirstOrDefault(p => p.ItemId == itemId);
		if (product == null)
		{
			GD.PushWarning($"[MerchantSystem] 商品不存在: {itemId}");
			return false;
		}
		if (product.Stock < quantity)
		{
			GD.PushWarning($"[MerchantSystem] 库存不足: {itemId}, 需要 {quantity}, 当前 {product.Stock}");
			return false;
		}

		int totalCost = product.BuyPrice * quantity;
		if (DataManager.Instance.ScrapCurrency < totalCost)
		{
			GD.PushWarning($"[MerchantSystem] 废土币不足: 需要 {totalCost}, 当前 {DataManager.Instance.ScrapCurrency}");
			return false;
		}

		//扣钱
		DataManager.Instance.ScrapCurrency -= totalCost;

		//扣除商人库存
		var stock = DataManager.Instance.GetMerchantStock(merchantId);
		stock[itemId] = product.Stock - quantity;
		DataManager.Instance.SetMerchantStock(merchantId, stock);

		//添加物品到玩家背包
		InventorySystem.AddItem(itemId, quantity);

		var def = GetDef(merchantId);
		GD.Print($"[MerchantSystem] 购买: {quantity}×{product.DisplayName} |−{totalCost}废土币 | 商人: {def?.DisplayName}");

		return true;
	}

	/// <summary>玩家出售--加废土币，减库存</summary>
	public static bool Sell(string merchantId, int slotId, int quantity)
	{
		if (quantity <= 0) return false;

		var slot = DataManager.Instance.Inventory.Find(s => s.SlotId == slotId);
		if (slot == null || slot.Quantity < quantity)
		{
			GD.PushWarning($"[MerchantSystem] 出售失败: 槽位不存在或数量不足");
			return false;
		}

		var def = GetDef(merchantId);
		var config = ConfigManager.Instance.GetConfig<BaseItemData>(slot.ItemId);
		int baseValue = config?.BaseValue ?? 100;
		int unitPrice = Mathf.FloorToInt(baseValue * (def?.SellPriceMultiplier ?? 0.3f));
		int totalRevenue = unitPrice * quantity;

		//扣物品
		InventorySystem.RemoveItem(slotId, quantity);

		// 加钱
		DataManager.Instance.ScrapCurrency += totalRevenue;

		GD.Print($"[MerchantSystem] 出售: {quantity}×{config?.DisplayName ?? slot.ItemId} | +{totalRevenue}废土币");
		return true;
	}

	// ═══════════════════════════════════════════════
	// 刷新
	// ═══════════════════════════════════════════════

	/// <summary>检查并自动刷新（超过刷新间隔时）</summary>
	public static void CheckAndRefresh(string merchantId)
	{
		if (GetTimeUntilRefresh(merchantId) <= 0f)
		{
			RefreshMerchant(merchantId);
		}
	}

	/// <summary>强制刷新商品</summary>
	public static void RefreshMerchant(string merchantId)
	{
		var def = GetDef(merchantId);
		if (def == null) return;

		var stock = new Dictionary<string, int>();
		foreach (var (itemId, maxQty) in def.Products)
		{
			stock[itemId] = maxQty;
		}
		DataManager.Instance.SetMerchantStock(merchantId, stock);
		DataManager.Instance.SetMerchantLastRefresh(merchantId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

		GD.Print($"[MerchantSystem] {def.DisplayName} 商品已刷新 | {stock.Count}种");
	}

	/// <summary>批量检查所有商人刷新（OfflineManager Phase H 调用）</summary>
	public static void CheckAndRefreshAll()
	{
		foreach (var id in _defs.Keys)
			CheckAndRefresh(id);
	}
}
