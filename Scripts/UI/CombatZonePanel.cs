using Godot;
using System;

/// <summary>
/// 副本面板。战斗日志滚动 + 背景图。
/// MVP 阶段用简单文字 + 定时模拟战斗日志。
/// </summary>
public partial class CombatZonePanel : Control
{
	private RichTextLabel _logLabel;
	private Button _evacBtn;
	private int _lineCount;
	public override void _Ready()
	{
		_logLabel = GetNode<RichTextLabel>("%LogLabel");
		_evacBtn = GetNode<Button>("%EvacButton");
		_evacBtn.Pressed += () =>
		{
			GameManager.Instance.Transition(GameState.Settling);
			//结算完成后返回Idle
			GameManager.Instance.Transition(GameState.Idle);
		};

		EventBus.Instance.StateChanged += OnStateChanged;
	}

	private void OnStateChanged(GameState oldState, GameState newState)
	{
		Visible = newState == GameState.InRaid || newState == GameState.Settling;

		if (newState == GameState.InRaid)
		{
			AppendLog("[color=gray]进入工厂...[/color]");
		}
		else if (newState == GameState.Settling)
		{
			AppendLog("[color=gray]撤离中...[/color]");
		}

	}

	public override void _Process(double delta)
	{
		//MVP演示：每秒追加一条假战斗日志
		if (!GameManager.Instance.IsInRaid) return;

		_lineCount++;
		string[] lines =
		{
			"[color=green]✓ 命中 SCAV 胸部 — 造成 23 伤害[/color]",
			"[color=red]✗ MISS — 子弹偏出[/color]",
			"[color=orange]命中 SCAV 腿部 — 造成 15 伤害[/color]",
			"[color=yellow]★ 暴击！命中 SCAV 头部 — 造成 47 伤害[/color]",
			"[color=green]搜刮: 获得 5.45x39 弹药 ×12[/color]",
		};
		AppendLog(lines[_lineCount % lines.Length]);
	}

	private void AppendLog(string text)
	{
		_logLabel.AppendText($"{text}\n");
	}

	public override void _ExitTree()
	{
		EventBus.Instance.StateChanged -= OnStateChanged;
	}
}
