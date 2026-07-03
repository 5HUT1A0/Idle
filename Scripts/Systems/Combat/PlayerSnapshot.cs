/// <summary>
/// 战斗结算时的玩家状态快照。不可变，每个结算窗口重新创建。
/// </summary>
public struct PlayerSnapshot
{
	// ── 命中四因子
	public float KnowledgeBonus;       // 学识加成
	public float ProficiencyBonus;     // 熟练度加成
	public float GunAccuracy;          // 枪械精度
	public float RangeCorrection;      // 射程修正（乘算）

	// ── 惩罚
	public float WeightPenalty;        // 重量惩罚
	public float SidePlatePenalty;     // 侧板惩罚

	// ── 枪械
	public GunCategory GunCategory;
	public float OptimalRange;         // 改装后最优射程
	public float AmmoDamage;           // 弹药伤害
	public float GunCoeff;             // 枪械系数
	public float FireRate;             // 每秒射速

	// ── 护甲
	public ArmorSummary Armor;

	/// <summary>命中率（不含射程修正的应用前值，调用 CalcHitChance 时再乘）</summary>
	public float BaseAccuracy =>
		KnowledgeBonus * ProficiencyBonus * GunAccuracy;
}