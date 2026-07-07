using System.Collections.Generic;
/// <summary>
/// 一把完整的组装枪械——运行时对象，持久化到 SQLite custom_guns 表。
/// </summary>
public class CustomGun
{
	public int GunId { get; set; }   //SQLite 主键
	public string GunName { get; set; }   //枪械名称
	public string BodyId { get; set; }   //枪身 ID
	public string BarrelId { get; set; }   //枪管 ID
	public string MagazineId { get; set; }   //弹匣 ID
	public string SightId { get; set; }   //瞄具 ID
	public string MuzzleId { get; set; }   //枪口 ID
	public float Durability { get; set; }   //耐久度
	public string InsuredBy { get; set; }   //保险人

	/// <summary> 枪械类别---从枪身读取 </summary>
	public GunCategory Category(GunPartData body) => body?.Category ?? GunCategory.AR;

	/// <summary> 所有五槽位的ItemID集合 </summary>
	public IEnumerable<string> AllPartIds()
	{
		yield return BodyId;
		yield return BarrelId;
		yield return MagazineId;
		if (SightId != null) yield return SightId;
		if (MuzzleId != null) yield return MuzzleId;
	}

}
