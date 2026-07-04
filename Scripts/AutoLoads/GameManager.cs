using Godot;
using System;

//游戏顶层状态机--控制Idle/Deploying/InRaid/Setting等状态
public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }
	public bool IsFocused { get; private set; } = true;
	private GameState _currentState = GameState.Booting;

	//InRaid 在线状态下可以策略调整，OfflineRecovery 离线状态下不可以调整
	public bool CanAdjustStrategy => _currentState == GameState.InRaid && IsFocused;
	public override void _Ready()
	{
		Instance = this;
		GD.Print("[GameManager]就绪，当前状态：" + _currentState);
	}

	public override void _Notification(int what)
	{
		if (what == MainLoop.NotificationApplicationFocusOut)
		{
			IsFocused = false;
			EventBus.Instance.EmitSignal(EventBus.SignalName.FocusChanged, false);
		}
		else if (what == MainLoop.NotificationApplicationFocusIn)
		{
			IsFocused = true;
			EventBus.Instance.EmitSignal(EventBus.SignalName.FocusChanged, true);
		}
		if (what == NotificationWMCloseRequest)
		{
			GD.Print("[SaveManager] 检测到窗口关闭，执行退出存档...");
			SaveManager.RequestSave();
		}
	}


	//唯一状态切换接口
	public void Transition(GameState newState)
	{
		if (_currentState == newState)
		{
			GD.Print("[GameManager]状态切换失败，当前状态：" + _currentState + "，目标状态：" + newState);
			return;
		}

		GameState oldState = _currentState;
		_currentState = newState;

		GD.Print($"[GameManager]状态切换 :{oldState}  → {newState} ");

		EventBus.Instance.EmitSignal(
			EventBus.SignalName.StateChanged,
			(int)oldState,
			(int)newState
		);
	}

	public GameState CurrentState => _currentState;
	public bool IsInSafeHouse => _currentState == GameState.Idle;
	public bool IsInRaid => _currentState == GameState.InRaid;
	public bool CanProcessOffline => _currentState == GameState.Booting || _currentState == GameState.Idle;
}

