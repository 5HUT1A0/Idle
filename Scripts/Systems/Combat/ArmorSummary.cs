/// <summary>
/// 护甲摘要。用于战斗结算中查询各部位的减伤和覆盖状态。
/// MVP 阶段使用默认值，后续由 ArmorSystem 填充。
/// </summary>
public struct ArmorSummary
{
	/// <summary>各部位减伤值（0-1，如 0.2 = 减伤 20%）</summary>
	public float HeadReduction, ChestReduction, AbdomenReduction;
	public float LeftArmReduction, RightArmReduction;
	public float LeftLegReduction, RightLegReduction;

	/// <summary>各部位是否有护甲覆盖</summary>
	public bool HeadCovered, ChestCovered, AbdomenCovered;
	public bool LeftArmCovered, RightArmCovered;
	public bool LeftLegCovered, RightLegCovered;

	public bool IsCovered(BodyPart part) => part switch
	{
		BodyPart.Head => HeadCovered,
		BodyPart.Chest => ChestCovered,
		BodyPart.Abdomen => AbdomenCovered,
		BodyPart.LeftArm => LeftArmCovered,
		BodyPart.RightArm => RightArmCovered,
		BodyPart.LeftLeg => LeftLegCovered,
		BodyPart.RightLeg => RightLegCovered,
		_ => false
	};

	public float GetReduction(BodyPart part) => part switch
	{
		BodyPart.Head => HeadReduction,
		BodyPart.Chest => ChestReduction,
		BodyPart.Abdomen => AbdomenReduction,
		BodyPart.LeftArm => LeftArmReduction,
		BodyPart.RightArm => RightArmReduction,
		BodyPart.LeftLeg => LeftLegReduction,
		BodyPart.RightLeg => RightLegReduction,
		_ => 0f
	};
}