using Godot;

/// <summary>
/// 地图距离分布权重配置。
/// 五档权重控制接敌距离分布，总和不需归一化（代码内自动归一）。
/// 工厂值示例：Contact=35, Close=30, Medium=20, Far=10, Distant=5
/// </summary>
[GlobalClass]
public partial class MapDistanceConfig : Resource
{
	[Export] public string MapId { get; set; }

	/// <summary>贴脸权重 (0-25m)</summary>
	[Export] public float ContactWeight { get; set; } = 35f;

	/// <summary>近距离权重 (25-60m)</summary>
	[Export] public float CloseWeight { get; set; } = 30f;

	/// <summary>中距离权重 (60-100m)</summary>
	[Export] public float MediumWeight { get; set; } = 20f;

	/// <summary>中远距离权重 (100-140m)</summary>
	[Export] public float FarWeight { get; set; } = 10f;

	/// <summary>远距离权重 (140m+)</summary>
	[Export] public float DistantWeight { get; set; } = 5f;
}