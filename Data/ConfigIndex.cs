using Godot;

/// <summary>
/// 配置索引文件，每个配置文件夹放一个ConfigIndex.tres
/// ConfigManager只Load索引文件，遍历Entries即可
/// 新增物品时候在索引中追加条目，ConfigManager代码不动
/// </summary>
[GlobalClass]
public partial class ConfigIndex : Resource
{
	[Export] public Resource[] Entries { get; set; }
}