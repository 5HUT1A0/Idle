/// <summary>单发射击结果（不可变）</summary>
public readonly struct ShotResult
{
	public readonly bool IsHit;
	public readonly BodyPart Part;
	public readonly float Damage;
	public readonly bool IsUnarmored;
	public readonly float Distance;
	public readonly float RangeCorrection;

	public ShotResult(bool hit, BodyPart part, float damage,
		bool unarmored, float dist, float rangeCorr)
	{
		IsHit = hit;
		Part = part;
		Damage = damage;
		IsUnarmored = unarmored;
		Distance = dist;
		RangeCorrection = rangeCorr;
	}

	public static ShotResult Miss() => new(false, BodyPart.Head, 0, false, 0, 1f);
	public static ShotResult OutOfRange() => new(false, BodyPart.Head, 0, false, 0, 0);
}