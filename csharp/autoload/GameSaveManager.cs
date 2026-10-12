using Godot;
using Godot.Collections;
using System;
using System.Linq;

/// <summary>Owns versioned progress slots and the handoff between the title screen and MainScene.</summary>
public partial class GameSaveManager : Node
{
	private const string SaveDirectory = "user://game_saves";
	private PersistentGameSave? _pendingLoad;

	public string active_slot_id { get; private set; } = "";
	public string active_slot_name { get; private set; } = "";
	public bool has_active_slot => !string.IsNullOrWhiteSpace(active_slot_id);

	public override void _Ready() => SetProcess(false);

	public void begin_new_game(string playerName)
	{
		var safeName = string.IsNullOrWhiteSpace(playerName) ? "苏醒者" : playerName.Trim();
		active_slot_id = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
		active_slot_name = safeName;
		_pendingLoad = null;
		GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.clear_reward_ledger();
		GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager")?.restore_calendar(1, 1, 1, 0, 0f);
		GetNodeOrNull<NpcWorldManager>("/root/NpcWorldManager")?.clear_for_new_game();
	}

	public bool load_slot(string slotId)
	{
		if (!is_safe_slot_id(slotId)) return false;
		var path = $"{SaveDirectory}/{slotId}.tres";
		var save = ResourceLoader.Load<PersistentGameSave>(path);
		if (save is null || save.format_version < 1 || save.format_version > PersistentGameSave.CurrentFormatVersion || save.player_data is null)
		{
			GD.PushWarning($"无法载入存档：{slotId}");
			return false;
		}

		var pendingLoad = save.Duplicate(true) as PersistentGameSave ?? save;
		if (!pendingLoad.migrate_to_current_format())
		{
			GD.PushWarning($"无法迁移存档：{slotId}");
			return false;
		}
		try
		{
			var dungeon = pendingLoad.scene_progress.GetValueOrDefault("east_woods_dungeon_state",new Dictionary()).AsGodotDictionary();
			EastWoodsDungeonRules.validate_layout(dungeon.GetValueOrDefault("layout",new Dictionary()).AsGodotDictionary());
		}
		catch (Exception error)
		{
			GD.PushWarning($"无法载入副本地图，原存档未修改：{error.Message}");
			return false;
		}
		_pendingLoad = pendingLoad;
		active_slot_id = slotId;
		active_slot_name = _pendingLoad.save_name;
		return true;
	}

	public PersistentGameSave? consume_pending_load()
	{
		var result = _pendingLoad;
		_pendingLoad = null;
		return result;
	}

	public Array<Dictionary> get_save_slots()
	{
		Array<Dictionary> result = [];
		var directory = DirAccess.Open(SaveDirectory);
		if (directory is null) return result;

		directory.ListDirBegin();
		var fileName = directory.GetNext();
		while (!string.IsNullOrEmpty(fileName))
		{
			if (!directory.CurrentIsDir() && fileName.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
			{
				var slotId = fileName[..^5];
				if (is_safe_slot_id(slotId) && ResourceLoader.Load<PersistentGameSave>($"{SaveDirectory}/{fileName}") is { } save)
				{
					var time = $"{save.year}年{save.month}月{save.day}日 {save.hour:00}时";
					var realm = save.player_data?.cultivation_realm ?? "境界未明";
					result.Add(new Dictionary
					{
						["id"] = slotId,
						["name"] = save.save_name,
						["realm"] = realm,
						["time"] = time,
						["saved_at"] = save.saved_at_unix,
					});
				}
			}
			fileName = directory.GetNext();
		}
		directory.ListDirEnd();

		var sorted = result.OrderByDescending(item => item["saved_at"].AsInt64()).ToArray();
		return new Array<Dictionary>(sorted);
	}

	public bool save_snapshot(PersistentGameSave snapshot)
	{
		if (!has_active_slot || snapshot is null) return false;
		if (snapshot.player_data is null)
		{
			GD.PushWarning("存档失败：角色数据为空。");
			return false;
		}

		var absoluteDirectory = ProjectSettings.GlobalizePath(SaveDirectory);
		var directoryError = DirAccess.MakeDirRecursiveAbsolute(absoluteDirectory);
		if (directoryError != Error.Ok && directoryError != Error.AlreadyExists)
		{
			GD.PushError($"无法创建存档目录：{directoryError}");
			return false;
		}

		snapshot.battle_reward_ledger=GetNodeOrNull<BattleTransitionManager>("/root/BattleTransitionManager")?.export_reward_ledger()??[];
		snapshot.format_version = PersistentGameSave.CurrentFormatVersion;
		snapshot.slot_id = active_slot_id;
		snapshot.save_name = active_slot_name;
		snapshot.saved_at_unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var error = ResourceSaver.Save(snapshot, $"{SaveDirectory}/{active_slot_id}.tres");
		if (error != Error.Ok)
		{
			GD.PushError($"存档写入失败：{error}");
			return false;
		}
		return true;
	}

	private static bool is_safe_slot_id(string slotId)
		=> !string.IsNullOrWhiteSpace(slotId) && slotId.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
