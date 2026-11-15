using Godot;
using System;
using System.IO;
using System.Linq;

/// <summary>Loads the requested formal slot before entering the real MainScene.</summary>
public partial class EastWoodsFormalSaveRestartBootstrap : Node
{
    private const string SaveName = "东林地跨进程验收-资源点版";
    private const string TestMainScene = "res://tools/EastWoodsFormalSaveRestartMain.tscn";

    private static string UserArgument(string prefix)
        => OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..] ?? "";

    private static string NormalizePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static void VerifyIsolatedUserDataDirectory()
    {
        var expectedAppData = UserArgument("--expected-appdata=");
        if (string.IsNullOrWhiteSpace(expectedAppData) ||
            !string.Equals(NormalizePath(OS.GetEnvironment("APPDATA")), NormalizePath(expectedAppData), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("APPDATA 未指向本次隔离临时目录；拒绝读取或写入任何存档。");

        var customName = ProjectSettings.GetSetting("application/config/custom_user_dir_name").AsString();
        if (string.IsNullOrWhiteSpace(customName))
            throw new InvalidOperationException("项目未配置独立 user:// 目录名；拒绝读取或写入任何存档。");
        var expectedUserData = NormalizePath(Path.Combine(expectedAppData, customName));
        var actualUserData = NormalizePath(OS.GetUserDataDir());
        if (!string.Equals(actualUserData, expectedUserData, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"user:// 未解析到隔离目录：期望 {expectedUserData}，实际 {actualUserData}；拒绝访问存档。");

        var savesPath = NormalizePath(ProjectSettings.GlobalizePath("user://game_saves"));
        if (!savesPath.StartsWith(actualUserData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"正式存档目录越出隔离 user://：{savesPath}；拒绝访问存档。");
    }

    public override void _Ready() => CallDeferred(nameof(start_test_scene));

    private void start_test_scene()
    {
        try
        {
            VerifyIsolatedUserDataDirectory();
            var phase = UserArgument("--phase=");
            if (phase is not ("write" or "read"))
                throw new InvalidOperationException("请提供 --phase=write 或 --phase=read。");

            if (phase == "read")
            {
                var saveManager = GetNode<GameSaveManager>("/root/GameSaveManager");
                var matchingSlots = saveManager.get_save_slots()
                    .Where(slot => slot.GetValueOrDefault("name", "").AsString() == SaveName)
                    .ToArray();
                if (matchingSlots.Length != 1)
                    throw new InvalidOperationException($"隔离目录中应有且仅有一个资源点验收槽，实际为 {matchingSlots.Length} 个。");
                var slotId = matchingSlots[0].GetValueOrDefault("id", "").AsString();
                if (string.IsNullOrWhiteSpace(slotId) || !saveManager.load_slot(slotId))
                    throw new InvalidOperationException("正式存档槽读档入口拒绝资源点验收槽。");
            }

            var error = GetTree().ChangeSceneToFile(TestMainScene);
            if (error != Error.Ok)
                throw new InvalidOperationException($"无法切换到正式主场景验收副本：{error}。");
        }
        catch (Exception error)
        {
            GD.PushError("EAST_WOODS_FORMAL_SAVE_BOOTSTRAP_FAILED: " + error);
            GetTree().Quit(1);
        }
    }
}
