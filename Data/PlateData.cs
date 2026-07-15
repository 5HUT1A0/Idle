using Godot;
using System;
/// <summary>
/// 挡板——挂在内衬槽位上，提供主要减伤。
/// 不可单独装备，必须通过内衬的板槽挂载。
/// </summary>
[GlobalClass]
public partial class PlateData : BaseItemData
{
    /// <summary>挡板材料类型：Soft=软质 / LightHard=轻型硬质 / Steel=纯钢 / Ceramic=陶瓷</summary>
    [Export] public string PlateMaterial { get; set; } = "LightHard";

    /// <summary>适用的槽位类型：Front=前板 / Rear=后板 / Side=侧板</summary>
    [Export] public string PlateSlotType { get; set; } = "Front";

    /// <summary>减伤值（0-1，如 0.2 = 减伤 20%）</summary>
    [Export] public float DefenseValue { get; set; } = 0.2f;

    /// <summary>闪避值惩罚（正值=降低闪避）</summary>
    [Export] public float DodgePenalty { get; set; }

    /// <summary>最大耐久</summary>
    [Export] public float MaxDurability { get; set; } = 100f;

    /// <summary>维修消耗倍率</summary>
    [Export] public float RepairCostMultiplier { get; set; } = 1.0f;
}
