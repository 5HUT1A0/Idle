/// <summary>
/// 仓库中一个物品槽位。运行时在 DataManager 中维护，存档时写入 SQLite。
/// </summary>

public class InventorySlot
{
	public int SlotId { get; set; }        //SQLite 自增主键，内存中唯一标识
	public string ItemId { get; set; }      //对应BaseItemData.ItemId
	public int Quantity { get; set; } = 1;     //物品数量
	public float Durability { get; set; } = 100f; //耐久度，0~100之间,不可装备的物品为100
	public string Location { get; set; } = "stash"; //物品所在位置，stash=仓库/loadout=身上
}
