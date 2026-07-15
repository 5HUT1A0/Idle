using Godot;
using System;
/// <summary>
/// 内衬（底材）——决定可用的板槽数量和兼容材料类型。
/// 挡板必须通过内衬的板槽挂载，不可单独装备。
/// </summary>
[GlobalClass]
public partial class LinerData : BaseItemData
{
    /// <summary>内衬类型：ChestRig=轻型胸挂 / TacticalVest=战术背心 / FullArmor=全防护重甲</summary>
    [Export] public string LinerType { get; set; } = "ChestRig";

    /// <summary>是否有前插板</summary>
    [Export] public bool HasFrontSlot { get; set; }

    /// <summary>是否有后插板</summary>
    [Export] public bool HasRearSlot { get; set; }

    /// <summary>是否有右插板</summary>
    [Export] public bool HasRightSlot { get; set; }

    /// <summary>是否有左插板</summary>
    [Export] public bool HasLeftSlot { get; set; }

    /// <summary>兼容的插板材料类型，逗号分隔，如“LightHard,Steel,Ceramic”</summary>
    [Export] public string CompatibleMaterials { get; set; } = "LightHard";

    /// <summary>内衬自带的基础减伤值（胸部，无挡板时候生效）</summary>
    [Export] public float BaseChestDefense { get; set; }

    /// <summary>基础闪避值</summary>
    [Export] public float BaseDodgeValue { get; set; }
}
