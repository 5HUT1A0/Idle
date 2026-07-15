using System.Collections.Generic;

/// <summary>
/// 已组装的护甲——运行时模型。
/// 由内衬 + 多块挡板 + 头盔组成，持久化到 SQLite custom_armors 表。
/// </summary>
public class CustomArmor
{
	/// <summary>护甲唯一 ID（内存自增，不持久化）</summary>
	public int ArmorId { get; set; }

	/// <summary>内衬物品 ID</summary>
	public string LinerId { get; set; }

	/// <summary>头盔物品 ID（可选）</summary>
	public string HelmetId { get; set; }

	/// <summary>前板物品 ID（可选）</summary>
	public string FrontPlateId { get; set; }

	/// <summary>后板物品 ID（可选）</summary>
	public string RearPlateId { get; set; }

	/// <summary>左侧板物品 ID（可选）</summary>
	public string LeftPlateId { get; set; }

	/// <summary>右侧板物品 ID（可选）</summary>
	public string RightPlateId { get; set; }

	/// <summary>当前耐久（取各部件最低值）</summary>
	public float Durability { get; set; } = 100f;

	/// <summary>投保商人 ID（null=未投保）</summary>
	public string InsuredBy { get; set; }

	/// <summary>自定义名称</summary>
	public string ArmorName { get; set; }

	/// <summary>遍历所有已装配的配件ID（跳过Null）</summary>
	public IEnumerable<string> AllPartIds()
	{
		if (!string.IsNullOrEmpty(LinerId)) yield return LinerId;
		if (!string.IsNullOrEmpty(HelmetId)) yield return HelmetId;
		if (!string.IsNullOrEmpty(FrontPlateId)) yield return FrontPlateId;
		if (!string.IsNullOrEmpty(RearPlateId)) yield return RearPlateId;
		if (!string.IsNullOrEmpty(LeftPlateId)) yield return LeftPlateId;
		if (!string.IsNullOrEmpty(RightPlateId)) yield return RightPlateId;
	}

	/// <summary>检查指定槽位是否已装配挡板</summary>
	public bool HasPlate(string slotName) => slotName switch
	{
		"front" => !string.IsNullOrEmpty(FrontPlateId),
		"rear" => !string.IsNullOrEmpty(RearPlateId),
		"left" => !string.IsNullOrEmpty(LeftPlateId),
		"right" => !string.IsNullOrEmpty(RightPlateId),
		_ => false
	};

	/// <summary>设置指定插槽的挡板 ID</summary>
	public void SetPlate(string slotName, string plateId)
	{
		switch (slotName)
		{
			case "front": FrontPlateId = plateId; break;
			case "rear": RearPlateId = plateId; break;
			case "left": LeftPlateId = plateId; break;
			case "right": RightPlateId = plateId; break;
		}
	}
}
