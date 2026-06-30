using Godot;

/// <summary>
/// 配件属性修正值。挂在 GunPartData 上。
/// 正值 = 增益，负值 = 减益。
/// </summary>
[GlobalClass]
public partial class StatModifiers : Resource
{
	/// <summary>命中率修正（如瞄具+0.15 = +15%命中）</summary>
	[Export] public float HitChanceMod { get; set; }

	/// <summary>伤害系数修正（如枪口+0.10 = +10%伤害）</summary>
	[Export] public float DamageMod { get; set; }

	/// <summary>换弹CD修正（快弹匣 −0.30 = −30%换弹时间）</summary>
	[Export] public float ReloadCdMod { get; set; }

	/// <summary>弹匣容量修正（扩容弹匣 +0.50 = +50%容量）</summary>
	[Export] public float MagazineCapacityMod { get; set; }
}