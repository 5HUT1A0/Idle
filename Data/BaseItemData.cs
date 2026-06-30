using Godot;
using System;


/// <summary>
/// 基础物品数据，所有物品的基类Resource。
/// 设计时在Godot编辑器中创建.tres文件配置
/// </summary>
[GlobalClass]
public partial class BaseItemData : Resource
{
	/// <summary>唯一 ID，如 "ammo_545x39_T3"</summary>
	[Export] public string ItemId { get; set; }

	/// <summary> 物品显示名称 </summary>
	[Export] public string DisplayName { get; set; }

	/// <summary> 物品描述 </summary>
	[Export] public string Description { get; set; }

	/// <summary> 物品最大堆叠 </summary>
	[Export] public int MaxStack { get; set; } = 99;

	/// <summary> 物品档次 </summary>
	[Export] public int QualityTier { get; set; } = 1;

	/// <summary> 物品基础价值 </summary>
	[Export] public int BaseValue { get; set; }

	/// <summary> 物品图标 </summary>
	[Export] public Texture2D Icon { get; set; }
}
