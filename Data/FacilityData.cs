using Godot;
using System;

/// <summary>
/// 安全屋设施配置——定义单个设施的等级上限、前置条件、升级消耗。
/// 策划在 Godot 编辑器中修改 .tres 即可调整，不改代码。
/// </summary>
[GlobalClass]
public partial class FacilityData : Resource
{
    /// <summary>设施唯一 ID：warehouse/workbench/gym/range/infirmary</summary>
    [Export] public string FacilityId { get; set; }

    /// <summary>显示名称</summary>
    [Export] public string DisplayName { get; set; }

    /// <summary>初始等级</summary>
    [Export] public int DefaultLevel { get; set; }

    /// <summary>设施等级上限</summary>
    [Export] public int MaxLevel { get; set; } = 10;

    /// <summary>前置设施ID（null=无前置）</summary>
    [Export] public string PrerequisiteFacility { get; set; }

    /// <summary>前置设施所需等级</summary>
    [Export] public int PrerequisiteLevel { get; set; }

    /// <summary>每级升级时长（小时），升级时长曲线 ID</summary>
    [Export] public string UpgradeTimeCurveId { get; set; } = "curve_safehouse_upgrade";

    /// <summary>升级描述——升级后解锁的效果说明</summary>
    [Export] public string UpgradeDescription { get; set; }
}
