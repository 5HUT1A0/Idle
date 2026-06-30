using Godot;

/// <summary>
/// 弹药 Resource。继承 BaseItemData。
/// </summary>
[GlobalClass]
public partial class AmmoData : BaseItemData
{
    /// <summary>口径，如 "5.45x39"、"7.62x51"</summary>
    [Export] public string Caliber { get; set; }

    /// <summary>基础伤害（单发）</summary>
    [Export] public float BaseDamage { get; set; }

    /// <summary>穿甲值</summary>
    [Export] public float Penetration { get; set; }
}