using Godot;
using Godot.Collections;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Owns versioned progress slots and the handoff between the title screen and MainScene.</summary>
public partial class GameSaveManager : Node
{
	private const string SaveDirectory = "user://game_saves";
	private PersistentGameSave? _pendingLoad;
	private Task<Error>? _backgroundSave;
	private readonly DetachedResourceSnapshot.CaptureCache _captureCache = new();
	public bool IsBackgroundSaveBusy => _backgroundSave is not null;

	public string active_slot_id { get; private set; } = "";
	public string active_slot_name { get; private set; } = "";
	public bool has_active_slot => !string.IsNullOrWhiteSpace(active_slot_id);

	public override void _Ready()
	{
		SetProcess(false);
		DetachedResourceSnapshot.WarmUp();
	}
	public override void _ExitTree() => finish_background_save();

	public void begin_new_game(string playerName)
	{
		finish_background_save();
		_captureCache.Clear();
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
		finish_background_save();
		_captureCache.Clear();
		var path = $"{SaveDirectory}/{slotId}.tres";
		var save = ResourceLoader.Load<PersistentGameSave>(path, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
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
			if (!DynamicDungeonExpedition.ValidateState(pendingLoad.dynamic_dungeon_state))
				throw new InvalidOperationException("动态副本实例表无效。");
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
				if (is_safe_slot_id(slotId) && ResourceLoader.Load<PersistentGameSave>($"{SaveDirectory}/{fileName}", cacheMode: ResourceLoader.CacheMode.IgnoreDeep) is { } save)
				{
					var time = $"{save.year}年{save.month}月{save.day}日";
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
		// Drain the older writer before a manual/exit save can replace its result.
		finish_background_save();
		if (!prepare_snapshot(snapshot)) return false;
		return report_save_result(write_snapshot(snapshot, ProjectSettings.GlobalizePath($"{SaveDirectory}/{active_slot_id}.tres")));
	}

	public bool begin_background_save(PersistentGameSave snapshot)
	{
		if (IsBackgroundSaveBusy || !prepare_snapshot(snapshot)) return false;
		try
		{
			// Capture values on the scene thread. The worker receives no live scene data.
			var detached = DetachedResourceSnapshot.Capture(snapshot, _captureCache);
			var destination = ProjectSettings.GlobalizePath($"{SaveDirectory}/{active_slot_id}.tres");
			_backgroundSave = Task.Run(() => write_snapshot((PersistentGameSave)detached.Materialize(), destination));
			return true;
		}
		catch (Exception exception)
		{
			GD.PushError($"无法捕获自动存档：{exception}");
			return false;
		}
	}

	public bool? poll_background_save()
	{
		if (_backgroundSave?.IsCompleted != true) return null;
		return finish_background_save();
	}

	public bool finish_background_save()
	{
		var pending = _backgroundSave;
		if (pending is null) return true;
		try { return report_save_result(pending.GetAwaiter().GetResult()); }
		catch (Exception exception) { GD.PushError($"自动存档失败：{exception}"); return false; }
		finally { _backgroundSave = null; }
	}

	private static bool report_save_result(Error error)
	{
		if (error == Error.Ok) return true;
		GD.PushError($"存档写入失败：{error}");
		return false;
	}

	private static Error write_snapshot(PersistentGameSave snapshot, string destination)
	{
		// The old slot remains valid if serialization or disk writing fails.
		var temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination)!, $".pending_{Guid.NewGuid():N}.tres");
		try
		{
			var error = ResourceSaver.Save(snapshot, temporary);
			if (error != Error.Ok) return error;
			System.IO.File.Move(temporary, destination, true);
			return Error.Ok;
		}
		catch (System.IO.IOException) { return Error.FileCantWrite; }
		catch (UnauthorizedAccessException) { return Error.FileNoPermission; }
		finally
		{
			try { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
			catch (System.IO.IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}

	private bool prepare_snapshot(PersistentGameSave snapshot)
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
		return true;
	}

	private static bool is_safe_slot_id(string slotId)
		=> !string.IsNullOrWhiteSpace(slotId) && slotId.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}
