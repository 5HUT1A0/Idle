using Godot;

/// <summary>身体部位</summary>
public enum BodyPart
{
	Head,
	Chest,
	Abdomen,
	LeftArm,
	RightArm,
	LeftLeg,
	RightLeg
}

/// <summary>重量档位</summary>
public enum WeightTier
{
	Light,
	Medium,
	Heavy
}

/// <summary>敌人类型</summary>
public enum EnemyType
{
	Scav,
	Pmc,
	Boss
}

/// <summary>距离档位</summary>
public enum RangeBand
{
	Contact,    // 贴脸 0-25m
	Close,      // 近距 25-60m
	Medium,     // 中距 60-100m
	Far,        // 中远 100-140m
	Distant     // 远距 140m+
}