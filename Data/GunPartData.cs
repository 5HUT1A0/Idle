using Godot;

/// <summary>
/// 枪械配件 Resource。五槽位。
/// 部分字段仅特定 Slot 生效——详见字段注释，策划按约定填写。
/// </summary>
[GlobalClass]
public partial class GunPartData : BaseItemData
{
    [Export] public PartSlot Slot { get; set; }

    /// <summary>枪械类别【Body 专属】</summary>
    [Export] public GunCategory Category { get; set; }

    /// <summary>枪管类型【Barrel 专属】</summary>
    [Export] public BarrelType BarrelType { get; set; } = BarrelType.Standard;

    /// <summary>最优射程偏移倍率【Barrel 专属】</summary>
    [Export] public float RangeOffset { get; set; }

    /// <summary>基础容弹量【Magazine 专属】</summary>
    [Export] public int BaseCapacity { get; set; }

    /// <summary>重量修正倍率</summary>
    [Export] public float WeightModifier { get; set; } = 1.0f;

    /// <summary>精度修正值</summary>
    [Export] public float AccuracyModifier { get; set; }

    /// <summary>属性修正</summary>
    [Export] public StatModifiers StatMods { get; set; }

    /// <summary>重量（kg）</summary>
    [Export] public float Weight { get; set; }

    /// <summary>耐久消耗速率</summary>
    [Export] public float DurabilityCost { get; set; } = 1.0f;

    /// <summary>是否需要图纸解锁</summary>
    [Export] public bool BlueprintRequired { get; set; }
}