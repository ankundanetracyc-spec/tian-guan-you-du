using Godot;

/// <summary>临时人物的局内吐纳预览及结算检查；不载入或保存正式旅程。</summary>
public partial class CultivationTransitionPreviewScene : Control
{
    public override async void _Ready()
    {
        try
        {
            ProcessMode = ProcessModeEnum.Always;
            AddToGroup("gameplay_scene");
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var compact = OS.GetCmdlineUserArgs().Contains("--compact");
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = compact ? new Vector2I(1280, 720) : new Vector2I(1672, 941);
            GetWindow().ContentScaleSize = GetWindow().Size;
            var clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
            clock.restore_calendar(1, 3, 15, 10, 0);
            var character = new Character(); AddChild(character);
            character.load_from_data(new HumanData
            {
                entity_name = "苏醒者", cultivation_realm = "云息三层",
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
                spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
                main_gongfa_path = "res://resources/gongfa/黄庭养形篇.tres",
                learned_gongfa_paths = ["res://resources/gongfa/黄庭养形篇.tres"],
                ambient_lingqi_pressure = 180, current_world_stamina = 0, max_world_stamina = 100
            });
            character.Hide();
            var panel = new ActionTransitionPanel(); AddChild(panel);
            var room = new CultivationUI(); AddChild(room); room.show_entity(character);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = compact ? new Vector2I(1280, 720) : new Vector2I(1672, 941);
            GetWindow().ContentScaleSize = GetWindow().Size;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"TRANSITION_PREVIEW_VIEWPORT {GetViewportRect().Size}, window {GetWindow().Size}");
            var hours = character.get_world_cultivation_preview_for_strands(1)["hours"].AsInt32();
            var beforeHours = clock.get_total_game_hours();
            var beforeStrands = character.spirit_strands;
            var initialPause = GetTree().Paused;
            Require(hours > 0 && character.can_cultivate(), "临时人物可吐纳");
            Require(GameTimeText.Duration(361 * 24) == "1年1日" && GameTimeText.Duration(25) == "1日1小时", "时长单位转换");
            await capture(compact, "静室吐纳");
            Descendants(room).OfType<Button>().Single(button => button.Text == "开始修炼").EmitSignal(Button.SignalName.Pressed);
            Require(panel.IsPlaying && GetTree().Paused, "正式吐纳按钮进入水墨演出并阻止其他行动");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(GetTree().Root.GetNodeOrNull("GameplayPauseMenuLayer") is null && panel.IsPlaying, "演出中的 Esc 不呼出暂停菜单");
            var duplicate = 0;
            panel.play_cultivation("重复调用", 25, "", () => duplicate++);
            await ToSignal(GetTree().CreateTimer(4, processAlways: true), SceneTreeTimer.SignalName.Timeout);
            Require(clock.get_total_game_hours() == beforeHours && character.spirit_strands == beforeStrands, "演出中不提前修改历法或灵力");
            await capture(compact, "修炼途中");
            while (panel.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(clock.get_total_game_hours() - beforeHours == hours && character.spirit_strands == beforeStrands + 1, "时间和灵力只提交一次");
            Require(duplicate == 0 && GetTree().Paused == initialPause, "重复调用被阻止且恢复原暂停状态");
            await panel.run_work_async("行旅演算", async update =>
            {
                update(.5, "归纳人物近况…");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(panel.IsPlaying && GetTree().Paused, "后台演算继续阻断输入");
                update(1, "完成");
            });
            Require(!panel.IsPlaying && GetTree().Paused == initialPause, "后台演算恢复状态");
            GetTree().Paused = true;
            var pausedSettlements = 0;
            panel.play("静室研读", "暂停状态检查", "完成", () => pausedSettlements++);
            while (panel.IsPlaying) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(pausedSettlements == 1 && GetTree().Paused, "原本暂停时完成行动仍保留暂停");
            GetTree().Paused = initialPause;
            GD.Print($"CULTIVATION_TRANSITION_PASS hours={hours}, one strand, deferred calendar, duplicate guard, pause restoration, background settlement, compact={compact}");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Paused = false; GetTree().Quit(1); }
    }

    private async System.Threading.Tasks.Task capture(bool compact, string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = ProjectSettings.GlobalizePath($"res://docs/美术规范/修炼时间流逝_20261015/{name}{(compact ? "_1280" : "")}.png");
        GetViewport().GetTexture().GetImage().SavePng(path);
    }

    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (var child in parent.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
