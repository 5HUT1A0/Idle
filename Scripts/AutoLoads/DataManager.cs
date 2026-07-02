using Godot;
using System.Collections.Generic;

public partial class DataManager : Node
{
	public static DataManager Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
	}

	public void LoadState(Dictionary<string, string> state)
	{ }

	public Dictionary<string, string> CollectDirtyState()
	{
		return new Dictionary<string, string>();
	}
}
