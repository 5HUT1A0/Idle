using Godot;
using System;


///<summary>
/// 枪械部位枚举
/// </summary>
public enum PartSlot
{
	Body,       //枪身
	Barrel,     //枪管
	Magazine,   //弹匣
	Sight,      //瞄准镜
	Muzzle,     //枪口
}

///<summary>
/// 枪管类型枚举，仅用于枪械部位枚举中的Barrel
/// </summary>
public enum BarrelType
{
	Standard,   //标准管
	Short,      //短管
	SawedOff,   //截短管
	Long,       //长管
}

///<summary>
/// 枪械类型，由枪身 BaseItemData.Category 决定
/// </summary>
public enum GunCategory
{
	AR,         //突击步枪
	SMG,        //冲锋枪
	Shotgun,    //霰弹枪
	Sniper,     //狙击步枪
	DMR,        //射手步枪
}