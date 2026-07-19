using Godot;
using System;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;


/// <summary>
/// 存档系统 Autoload（优先级 2）。
/// SQLite 读写、事务管理、自动备份、版本迁移。
/// 依赖：EventBus → 订阅 SaveRequested 信号。
/// </summary>
public partial class SaveManager : Node
{
	public static SaveManager Instance { get; private set; }
	private const int CurrentVersion = 1;
	private const int MaxBackupCount = 3;
	private Dictionary<string, string> _loadedState;  // 启动时加载的状态缓存
	private List<InventorySlot> _loadedInventory;

	/// <summary>供 DataManager._Ready() 取走加载的状态。取后清空。</summary>
	public Dictionary<string, string> TakeLoadedState()
	{
		var state = _loadedState;
		_loadedState = null;
		return state;
	}

	public List<InventorySlot> TakeLoadedInventory()
	{
		var inventory = _loadedInventory;
		_loadedInventory = null;
		return inventory;
	}

	private string _savePath;
	private string BackupPath(int n) => _savePath.Replace(".db", $"_backup_{n}.db");

	private bool _flushScheduled;

	//======================================================================
	// Godot 生命周期
	//======================================================================

	public override void _Ready()
	{
		Instance = this;
		_savePath = ProjectSettings.GlobalizePath("user://save.db");

		if (FileAccess.FileExists(_savePath))
		{
			try
			{
				_loadedState = LoadSave();
				GD.Print($"[SaveManager] 存档路径: {_savePath}");
				GD.Print($"[SaveManager] 加载行数: inventory={_loadedInventory?.Count ?? 0}, state={_loadedState?.Count ?? 0}");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[SaveManager]加载存档失败：{ex.Message}");
				EventBus.Instance.EmitSignal(EventBus.SignalName.SaveLoadFailed, ex.Message);
			}
		}
		else
		{
			CreateTable();
			GD.Print("[SaveManager]已新建存档");
		}

		//订阅存盘请求
		EventBus.Instance.SaveRequested += OnSaveRequested;
	}

	private void OnSaveRequested()
	{
		if (_flushScheduled) return;
		_flushScheduled = true;
		CallDeferred(nameof(DoFlush));
	}

	//======================================================================
	// 建表
	//======================================================================

	private void CreateTable()
	{
		using var db = new SqliteConnection($"Data Source={_savePath}");
		db.Open();

		var ddl = @"
		CREATE TABLE IF NOT EXISTS player_state (
			Key TEXT PRIMARY KEY,
			Value TEXT NOT NULL
		);

		CREATE TABLE IF NOT EXISTS inventory (
			slot_id     INTEGER PRIMARY KEY AUTOINCREMENT,
			item_id     TEXT NOT NULL,
			quantity    INTEGER NOT NULL DEFAULT 1,
			durability  REAL,
			custom_json TEXT,
			location    TEXT DEFAULT 'stash'
		);

		CREATE TABLE IF NOT EXISTS custom_guns (
			gun_id      INTEGER PRIMARY KEY AUTOINCREMENT,
			gun_name    TEXT,
			body_id     TEXT NOT NULL,
			barrel_id   TEXT NOT NULL,
			magazine_id TEXT NOT NULL,
			sight_id    TEXT,
			muzzle_id   TEXT,
			durability  REAL DEFAULT 100,
			insured_by  TEXT
		);

		CREATE TABLE IF NOT EXISTS custom_armors (
			armor_id        INTEGER PRIMARY KEY,
			liner_id        TEXT NOT NULL,
			helmet_id       TEXT,
			front_plate_id  TEXT,
			rear_plate_id   TEXT,
			left_plate_id   TEXT,
			right_plate_id  TEXT,
			durability      REAL DEFAULT 100.0,
			insured_by      TEXT,
			armor_name      TEXT
		);

		CREATE TABLE IF NOT EXISTS progress (
			key   TEXT PRIMARY KEY,
			value TEXT
		);

		CREATE TABLE IF NOT EXISTS save_meta (
			id         INTEGER PRIMARY KEY CHECK(id = 1),
			version    INTEGER NOT NULL,
			last_close TEXT NOT NULL,
			hash       TEXT NOT NULL
		);

		INSERT OR REPLACE INTO save_meta (id, version, last_close, hash)
		VALUES (1, @version, @now, '');
		";

		using var cmd = new SqliteCommand(ddl, db);
		cmd.Parameters.AddWithValue("@version", CurrentVersion);
		cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
		cmd.ExecuteNonQuery();

		//写入默认玩家状态
		InitDefaultState(db);
	}

	private void InitDefaultState(SqliteConnection db)
	{
		var defaults = new Dictionary<string, string>
		{
			["scrap_currency"] = "5000",
			["hunger"] = "100",
			["thirst"] = "100",
			["hp_head"] = "100",
			["hp_chest"] = "100",
			["hp_abdomen"] = "100",
			["hp_left_arm"] = "100",
			["hp_right_arm"] = "100",
			["hp_left_leg"] = "100",
			["hp_right_leg"] = "100",
			["stamina_level"] = "1",
			["knowledge_level"] = "1",
			["reload_cd_level"] = "1",
			["repair_proficiency_level"] = "1",
			["max_weight"] = "25",
			["prestige_count"] = "0",
			["survivor_mark"] = "0",
		};

		foreach (var (key, value) in defaults)
		{
			using var cmd = new SqliteCommand("INSERT OR REPLACE INTO player_state (key, value) VALUES (@k, @v)", db);
			cmd.Parameters.AddWithValue("@k", key);
			cmd.Parameters.AddWithValue("@v", value);
			cmd.ExecuteNonQuery();
		}
	}

	//======================================================================
	// 加载
	//======================================================================

	private Dictionary<string, string> LoadSave()
	{
		using var db = new SqliteConnection($"Data Source={_savePath}");
		db.Open();

		// 检查版本
		using var metaCmd = new SqliteCommand("SELECT version FROM save_meta WHERE id = 1", db);
		var versionObj = metaCmd.ExecuteScalar();
		int saveVersion = versionObj != null ? Convert.ToInt32(versionObj) : 0;

		if (saveVersion < CurrentVersion)
		{
			RunMigrations(db, saveVersion);
		}

		//加载 player_state →推给DataManager
		var state = new Dictionary<string, string>();
		using var stateCmd = new SqliteCommand("SELECT key, value FROM player_state", db);
		using var reader = stateCmd.ExecuteReader();
		while (reader.Read())
		{
			state[reader.GetString(0)] = reader.GetString(1);
		}
		//库存加载
		var inventortSlots = new List<InventorySlot>();
		using (var invCmd = new SqliteCommand("SELECT slot_id, item_id, quantity, durability, location FROM inventory", db))

		using (var invReader = invCmd.ExecuteReader())
		{
			while (invReader.Read())
			{
				inventortSlots.Add(new InventorySlot
				{
					SlotId = invReader.GetInt32(0),
					ItemId = invReader.GetString(1),
					Quantity = invReader.GetInt32(2),
					Durability = invReader.IsDBNull(3) ? 100f : invReader.GetFloat(3),
					Location = invReader.IsDBNull(4) ? "stash" : invReader.GetString(4)
				});
			}
		}
		_loadedInventory = inventortSlots; //DataManager 会在 _Ready() 时取走
										   //DataManager 应为 AutoLoad优先级4，此时已_Ready()，可以直接调用
		return state;
	}

	private void RunMigrations(SqliteConnection db, int fromVersion)
	{
		for (int v = fromVersion; v < CurrentVersion; v++)
		{
			GD.Print($"[SaveManager] 执行迁移 v{v} → v{v + 1}");
			// 后续版本在这里添加迁移脚本
		}

		using var updateMeta = new SqliteCommand("UPDATE save_meta SET version = @v WHERE id = 1", db);
		updateMeta.Parameters.AddWithValue("@v", CurrentVersion);
		updateMeta.ExecuteNonQuery();
	}

	//======================================================================
	// 写入
	//======================================================================

	private void DoFlush()
	{
		try
		{
			using var db = new SqliteConnection($"Data Source={_savePath}");
			db.Open();
			using var tx = db.BeginTransaction();

			//写入 player_state(只写入dirty字段)
			var dirtyState = DataManager.Instance.CollectDirtyState();
			foreach (var (key, value) in dirtyState)
			{
				using var cmd = new SqliteCommand(
	  				"INSERT OR REPLACE INTO player_state (key, value) VALUES (@k, @v)", db, tx);
				cmd.Parameters.AddWithValue("@k", key);
				cmd.Parameters.AddWithValue("@v", value);
				cmd.ExecuteNonQuery();
			}



			//库存全量覆写（清旧+插新）
			GD.Print($"[SaveManager] DoFlush 开始 | 内存库存: {DataManager.Instance.Inventory.Count}件 | dirty: {DataManager.Instance.IsDirty}");
			using (var delCmd = new SqliteCommand("DELETE FROM inventory", db, tx))
			{
				delCmd.ExecuteNonQuery();
			}
			foreach (var slot in DataManager.Instance.Inventory)
			{
				using var insCmd = new SqliteCommand(
					@"INSERT INTO inventory (slot_id, item_id, quantity, durability, location)
                     VALUES (@id, @item, @qty, @dur, @loc)", db, tx);
				insCmd.Parameters.AddWithValue("@id", slot.SlotId);
				insCmd.Parameters.AddWithValue("@item", slot.ItemId);
				insCmd.Parameters.AddWithValue("@qty", slot.Quantity);
				insCmd.Parameters.AddWithValue("@dur", slot.Durability);
				insCmd.Parameters.AddWithValue("@loc", slot.Location);
				insCmd.ExecuteNonQuery();
			}

			//自定义枪械全量覆写
			using (var delGunsCmd = new SqliteCommand("DELETE FROM custom_guns", db, tx))
			{
				delGunsCmd.ExecuteNonQuery();
			}
			foreach (var gun in DataManager.Instance.CustomGuns)
			{
				GD.Print($"[SaveManager] 写入枪械: {gun.GunName} | ID: {gun.GunId}| 配件: {gun.BodyId}, {gun.BarrelId}, {gun.MagazineId}, {gun.SightId}, {gun.MuzzleId} | 耐久: {gun.Durability}");
				using var insCmd = new SqliteCommand(
					@"INSERT INTO custom_guns (gun_id, gun_name, body_id, barrel_id, magazine_id, sight_id, muzzle_id, durability, insured_by)
					 VALUES (@id, @name, @body, @barrel, @magazine, @sight, @muzzle, @durability, @insured)", db, tx);
				insCmd.Parameters.AddWithValue("@id", gun.GunId);
				insCmd.Parameters.AddWithValue("@name", gun.GunName);
				insCmd.Parameters.AddWithValue("@body", gun.BodyId);
				insCmd.Parameters.AddWithValue("@barrel", gun.BarrelId);
				insCmd.Parameters.AddWithValue("@magazine", gun.MagazineId);
				insCmd.Parameters.AddWithValue("@sight", gun.SightId);
				insCmd.Parameters.AddWithValue("@muzzle", gun.MuzzleId);
				insCmd.Parameters.AddWithValue("@durability", gun.Durability);
				insCmd.Parameters.AddWithValue("@insured", gun.InsuredBy);
				insCmd.ExecuteNonQuery();
			}

			//自定义护甲全量覆写
			using (var delArmorsCmd = new SqliteCommand("DELETE FROM custom_armors", db, tx))
			{
				delArmorsCmd.ExecuteNonQuery();
			}
			foreach (var armor in DataManager.Instance.CustomArmors)
			{
				using var ins = new SqliteCommand(
					@"INSERT INTO custom_armors (armor_id, liner_id, helmet_id,
						front_plate_id, rear_plate_id, left_plate_id, right_plate_id,
						durability, insured_by, armor_name)
						VALUES (@id, @liner, @helmet, @front, @rear, @left, @right,
						@dur, @insured, @name)", db, tx);
				ins.Parameters.AddWithValue("@id", armor.ArmorId);
				ins.Parameters.AddWithValue("@liner", armor.LinerId);
				ins.Parameters.AddWithValue("@helmet", (object)armor.HelmetId ?? DBNull.Value);
				ins.Parameters.AddWithValue("@front", (object)armor.FrontPlateId ?? DBNull.Value);
				ins.Parameters.AddWithValue("@rear", (object)armor.RearPlateId ?? DBNull.Value);
				ins.Parameters.AddWithValue("@left", (object)armor.LeftPlateId ?? DBNull.Value);
				ins.Parameters.AddWithValue("@right", (object)armor.RightPlateId ?? DBNull.Value);
				ins.Parameters.AddWithValue("@dur", armor.Durability);
				ins.Parameters.AddWithValue("@insured", (object)armor.InsuredBy ?? DBNull.Value);
				ins.Parameters.AddWithValue("@name", armor.ArmorName ?? "");
				ins.ExecuteNonQuery();
			}


			GD.Print($"[SaveManager] 库存写入完成，即将 commit");
			tx.Commit();
			_flushScheduled = false;

			//更新元数据（事务外，失败不影响核心流程）
			try
			{
				var hash = ComputeHash(db);
				using var metaCmd = new SqliteCommand(
					 "UPDATE save_meta SET last_close = @now, hash = @hash WHERE id = 1", db);
				metaCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
				metaCmd.Parameters.AddWithValue("@hash", hash);
				metaCmd.ExecuteNonQuery();
			}
			catch (Exception metaEx)
			{
				GD.PushError($"[SaveManager] 元数据更新失败（存档数据已保存）: {metaEx.Message}");
			}

			RotateBackups();
			EventBus.Instance.EmitSignal(EventBus.SignalName.SaveCompleted);
			GD.Print("[SaveManager] 存档已完成");
		}
		catch (Exception ex)
		{
			GD.PushError($"[SaveManager] 存档失败: {ex.Message}");
			_flushScheduled = false;
		}
	}


	//======================================================================
	// 哈希+备份
	//======================================================================

	private string ComputeHash(SqliteConnection db)
	{
		//简易哈希：对 player_state 全表内容做 SHA256
		using var cmd = new SqliteCommand("SELECT key, value FROM player_state ORDER BY key", db);
		using var reader = cmd.ExecuteReader();
		var sb = new StringBuilder();

		while (reader.Read())
		{
			sb.Append(reader.GetString(0)).Append("=").Append(reader.GetString(1)).Append("\n");
		}
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
		return Convert.ToHexString(bytes);
	}



	private void RotateBackups()
	{
		// save_backup_1 → save_backup_2 → save_backup_3 → 删除
		try
		{
			if (FileAccess.FileExists(BackupPath(MaxBackupCount)))
			{
				DirAccess.RemoveAbsolute(BackupPath(MaxBackupCount));
			}

			for (int n = MaxBackupCount - 1; n >= 1; n--)
			{
				var older = BackupPath(n);
				var newer = BackupPath(n + 1);
				if (FileAccess.FileExists(older))
				{
					if (FileAccess.FileExists(newer))
					{
						DirAccess.RemoveAbsolute(newer);
					}
					DirAccess.RenameAbsolute(older, newer);
				}
			}

			//复制当前存档 → 创新的backup_1
			DirAccess.CopyAbsolute(_savePath, BackupPath(1));
		}
		catch (Exception ex)
		{
			GD.PushError($"[SaveManager] 备份失败: {ex.Message}");
		}
	}

	//======================================================================
	// 外部接口
	//======================================================================

	/// <summary>请求存盘（由外部系统调用）</summary>
	public static void RequestSave()
	{
		EventBus.Instance.EmitSignal(EventBus.SignalName.SaveRequested);
	}
}
