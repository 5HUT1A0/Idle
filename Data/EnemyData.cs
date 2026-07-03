using Godot;

/// <summary>
/// 敌人配置 Resource。
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
    [Export] public string EnemyId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public EnemyType Type { get; set; }
    [Export] public int Tier { get; set; } = 1;

    // 部位 HP
    [Export] public float HeadHp { get; set; } = 15f;
    [Export] public float ChestHp { get; set; } = 30f;
    [Export] public float AbdomenHp { get; set; } = 25f;
    [Export] public float LeftArmHp { get; set; } = 10f;
    [Export] public float RightArmHp { get; set; } = 10f;
    [Export] public float LeftLegHp { get; set; } = 10f;
    [Export] public float RightLegHp { get; set; } = 10f;

    /// <summary>获取指定部位 HP</summary>
    public float GetHp(BodyPart part) => part switch
    {
        BodyPart.Head => HeadHp,
        BodyPart.Chest => ChestHp,
        BodyPart.Abdomen => AbdomenHp,
        BodyPart.LeftArm => LeftArmHp,
        BodyPart.RightArm => RightArmHp,
        BodyPart.LeftLeg => LeftLegHp,
        BodyPart.RightLeg => RightLegHp,
        _ => 0f
    };
}