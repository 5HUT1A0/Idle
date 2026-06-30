using Godot;
using System;


public enum GameState
{
	//MainMenu, //主界面(MVP阶段先不设置)
	Booting, //游戏启动中
	Idle,//安全屋中，等待玩家操作
	Deploying, //部署中（配装/选副本/设策略）
	InRaid, //副本挂机中
	Settling, //副本结算中
	OfflineRecovery, //上线结算中
}
