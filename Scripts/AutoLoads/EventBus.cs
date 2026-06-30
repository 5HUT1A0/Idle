using Godot;
using System;

//全局信号总线，各个模块解耦唯一通道
public partial class EventBus : Node
{
	public static EventBus Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
	}

	//===========Game 状态相关=================
	#region Game
	[Signal]
	public delegate void StateChangedEventHandler(GameState oldState, GameState newState);
	[Signal]
	public delegate void FocusChangedEventHandler(bool focused);
	#endregion

	//===========Combat 战斗相关=================
	#region Combat
	[Signal]
	public delegate void EngagementDistanceEventHandler(float distance, float rangeCorrection);
	[Signal]
	public delegate void HitEventHandler(string targetPart, float damage, bool isCritical);
	[Signal]
	public delegate void BodyPartDisabledEventHandler(string partId);
	[Signal]
	public delegate void BodyStateCriticalEventHandler(string statType);
	[Signal]
	public delegate void EnemyKilledEventHandler(string enemyId, string lootSummaryJson);
	[Signal]
	public delegate void CombatAvoidedEventHandler();
	[Signal]
	public delegate void CombatFledEventHandler();
	#endregion

	//===========Economy 经济相关=================
	#region Economy
	[Signal]
	public delegate void CurrencyChangedEventHandler(string currencyId, int newValue, int delta);
	#endregion

	//===========Save 存档相关=================
	#region Save
	[Signal]
	public delegate void SaveRequestedEventHandler();
	[Signal]
	public delegate void SaveCompletedEventHandler();
	[Signal]
	public delegate void SaveLoadFailedEventHandler(string reason);
	#endregion

	//===========UI 界面相关=================
	#region UI
	[Signal]
	public delegate void PanelOpenedEventHandler(string panelId);
	[Signal]
	public delegate void PanelClosedEventHandler(string panelId);
	#endregion
}
