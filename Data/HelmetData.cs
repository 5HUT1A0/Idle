using Godot;
using System;
/// <summary>
/// 头盔——提供头部减伤。
/// </summary>
[GlobalClass]
public partial class HelmetData : BaseItemData
{
    /// <summary>头部减伤值（0-1，如 0.15 = 减伤 15%）</summary>
    [Export] public float DefenseValue { get; set; } = 0.15f;

    /// <summary>最大耐久</summary>
    [Export] public float MaxDurability { get; set; } = 100f;

    /// <summary>遮罩材质类型（与 PlateData 材料兼容性校验对应）</summary>
    [Export] public string MaterialType { get; set; } = "LightHard";
}
