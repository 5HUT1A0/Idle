using Godot;
using System;

/// <summary>
/// 安全屋面板。包含设施入口按钮和部署按钮。
/// </summary>
public partial class SafeHousePanel : Control
{

	private Button _deployBtn;

	public override void _Ready()
	{
		_deployBtn = GetNode<Button>("%DeployButton");

		_deployBtn.Pressed += () =>
		{
			//部署流程（MVP直接战斗，后续添加Deloying状态+部署面板）
			GameManager.Instance.Transition(GameState.Deploying);
			GameManager.Instance.Transition(GameState.InRaid);
		};

		//监听状态切换Idle
		EventBus.Instance.StateChanged += OnStateChanged;
	}

	private void OnStateChanged(GameState oldState, GameState newState)
	{
		Visible = newState == GameState.Idle;
	}


	public override void _ExitTree()
	{
		EventBus.Instance.StateChanged -= OnStateChanged;
	}

}
