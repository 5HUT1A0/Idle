using Godot;
using System;

/// <summary>
/// 撤离判定——检查五个条件，任一满足即触发撤离。
/// 条件顺序决定优先级，可配置扩展。
/// </summary>
public static class EvacSystem
{

	///<summary> 撤离原因。</summary>
	public enum EvacReason
	{
		None,
		OutOfAmmo,        // 弹药耗空
		HpTooLow,         // HP 过低（部位归零）
		TimeUp,           // 时间到（地图时限）
		Manual,           // 玩家手动撤离
		BodyCritical      // 饥饿/口渴归零恶化
	}

	/// <summary>
	/// 检查是否应撤离。
	/// </summary>
	public static EvacReason Check(PlayerSnapshot player, int ammoLeft, float elapsedHours, float mapTimeLimitHours)
	{
		if (ammoLeft <= 0)
			return EvacReason.OutOfAmmo;

		if (player.Armor.IsCovered(BodyPart.Head) && DataManager.Instance.HpHead <= 0) //破甲且血量归零
			return EvacReason.HpTooLow;
		if (player.Armor.IsCovered(BodyPart.Chest) && DataManager.Instance.HpChest <= 0)
			return EvacReason.HpTooLow;

		if (DataManager.Instance.Hunger <= 0f || DataManager.Instance.Thirst <= 0f)
			return EvacReason.BodyCritical;

		if (elapsedHours >= mapTimeLimitHours)
			return EvacReason.TimeUp;

		return EvacReason.None;
	}

	public static string ReasonText(EvacReason reason) => reason switch
	{
		EvacReason.OutOfAmmo => "弹药耗尽，强制撤离",
		EvacReason.HpTooLow => "部位重伤，强制撤离",
		EvacReason.TimeUp => "副本时限到，自动撤离",
		EvacReason.Manual => "手动撤离",
		EvacReason.BodyCritical => "饥渴恶化，强制撤离",
		_ => ""
	};
}
