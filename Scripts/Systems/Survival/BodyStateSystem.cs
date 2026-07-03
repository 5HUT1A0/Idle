using Godot;
using System;

/// <summary>
/// 身体状态系统——饥饿/口渴随时间线性扣减。
/// 仅局内(InRaid)有效，安全屋中不扣。
/// </summary>d
public static class BodyStateSystem
{
	private const float HungerDecayPerMin = 0.13f; // 每分钟扣除的饥饿值
	private const float ThirstDecayPerMin = 0.16f; // 每分钟扣除的口渴值

	/// <summary>
	/// 推进游戏时间，扣除饥饿/口渴值。
	/// 仅局内（InRaid）有效，安全屋中不扣。
	/// </summary>
	public static bool AdvanceTime(float minutes)
	{
		DataManager.Instance.Hunger -= HungerDecayPerMin * minutes;
		DataManager.Instance.Thirst -= ThirstDecayPerMin * minutes;

		bool critical = DataManager.Instance.Hunger <= 0 || DataManager.Instance.Thirst <= 0;
		if (critical)
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.BodyStateCritical, "hunger_or_thirst");
		}
		return critical;
	}
}
