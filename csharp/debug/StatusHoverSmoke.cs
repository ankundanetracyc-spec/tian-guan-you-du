using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Production status badge callbacks with isolated real definitions and status instances.</summary>
public partial class StatusHoverSmoke : Node
{
    private int _checks;
    private Vector2 _pointer;
    private void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args);
    private void Move(Control anchor)
    {
        _pointer = anchor.GetGlobalTransformWithCanvas() * (anchor.Size / 2);
        GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true);
    }
    private async Task Frames(int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true);
        }
    }
    private async Task Capture(Control card, string name)
    {
        var rect = card.GetGlobalRect(); var viewport = GetViewport().GetVisibleRect();
        Check(card.Visible && card.Modulate.A > .9f && rect.Size.X >= 300 && rect.Size.Y > 150, "状态卡完整展开 " + name);
        Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= viewport.End.X + 1 && rect.End.Y <= viewport.End.Y + 1, "状态卡未出屏 " + name);
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screen = GetViewport().GetTexture().GetImage();
        using var crop = screen.GetRegion(new Rect2I((Vector2I)rect.Position, (Vector2I)rect.Size));
        Check(crop.SavePng("res://docs/美术规范/信息栏整理_20261025/23_状态预览_" + name + ".png") == Error.Ok, "状态截图保存 " + name);
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        var mode = PlayerInfoText.Mode;
        BattleUnitOverlay? overlay = null;
        BattleManager? manager = null;
        try
        {
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            GetWindow().ContentScaleSize = new Vector2I(1280, 720); GetWindow().Size = new Vector2I(1280, 720);
            var unit = new Creature { entity_data = new HumanData { entity_name = "状态预览", max_health = 100, current_health = 80 } };
            AddChild(unit); unit.max_qi_blood = 8; unit.qi_blood = 3; unit.shield = 24;
            unit.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/外伤.tres"), stacks: 2);
            unit.add_status(GD.Load<StatusEffect>("res://resources/battle/statuses/僵直.tres"), stacks: 30, sourceName: "乘势再攻");
            manager = new BattleManager(); overlay = new BattleUnitOverlay(); overlay.Setup(manager);
            var row = new HBoxContainer { Position = new Vector2(140, 200), CustomMinimumSize = new Vector2(240, 48) }; AddChild(row);
            var views = (Dictionary<Creature, Control>)typeof(BattleUnitOverlay).GetField("views", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
            views[unit] = row;
            var card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            var badges = new Dictionary<string, Control>();
            foreach (var id in new[] { "外伤", "僵直", "气血", "护盾" })
            {
                var status = unit.status_snapshot().FirstOrDefault(s => s.effect?.effect_id == id);
                var count = status?.stacks ?? (id == "气血" ? unit.qi_blood : unit.shield);
                var badge = (Control)Call(overlay, "CreateStatusBadge", id, id, count, "", id is "外伤" or "僵直", status)!;
                row.AddChild(badge); badges[id] = badge;
                await Frames(); Move(badge); badge.EmitSignal(Control.SignalName.MouseEntered); await Frames();
                var body = card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single();
                Check(body.Text.Contains("持续时间：") && body.Text.Contains("效果：") && body.Text.Contains("当前层数：") && !body.Text.Contains("来源：") && !body.Text.Contains("简介："), id + "状态统一字段");
                Check(!body.Text.Contains("res://") && !body.Text.Contains("source_id"), id + "不暴露来源标识");
                if (id == "护盾") Check(body.Text.Contains("当前层数：24\n") && !body.Text.Contains("上限") && !body.Text.Contains("24/"), "护盾统一计数但不虚构上限");
                if (id == "僵直") Check(body.Text.Contains("剩余30战斗帧") && body.Text.Contains("当前层数：30/120\n") && !body.Text.Contains("秒"), "僵直统一层数并保留战斗帧");
                if (id == "外伤") Check(body.Text.Contains("当前层数：2/4\n"), "有上限状态无层字单位");
                await Capture(card, id);
            }
            var stun = unit.status_snapshot().Single(s => s.effect?.effect_id == "僵直");
            Move(badges["僵直"]); badges["僵直"].EmitSignal(Control.SignalName.MouseEntered); await Frames();
            for (int i = 0; i < 8; i++)
            {
                unit.advance_realtime_status_timers(1, 60, "僵直");
                Call(overlay, "RefreshHoveredStatusInfo"); await Frames(1);
                Check(card.Visible && card.Modulate.A > .9f, "连续计时刷新保持悬停可见");
            }
            var liveBody = card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single();
            Check(stun.stacks == 22 && liveBody.Text.Contains("剩余22战斗帧"), "真实计时器推进同步悬停");
            foreach (var path in new[] { "玄枢含章经/玄枢含章", "太和感物养元宝册/太和感物养元", "舒卷天章/心法" })
            {
                var skill = GD.Load<ActiveSkillData>("res://resources/activeskills/" + path + ".tres");
                var book = GD.Load<GongfaResource>("res://resources/gongfa/" + skill.source_gongfa_name + ".tres");
                unit.SetMeta("combat_heart_method_upgrades", new Godot.Collections.Dictionary
                {
                    [skill.source_gongfa_name + "|current_description"] = book.get_heart_method_description(1, true)
                });
                typeof(GongfaCombatRuntime).GetMethod("AddPowerStatus", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { unit, skill });
                var heart = unit.status_snapshot().Single(s => s.effect?.effect_id == "心法:" + skill.skill_name);
                var badge = (Control)Call(overlay, "CreateStatusBadge", heart.effect!.effect_id, heart.effect.name, heart.stacks, "", false, heart)!;
                row.AddChild(badge); await Frames(); Move(badge); badge.EmitSignal(Control.SignalName.MouseEntered); await Frames();
                Check(!liveBody.Text.Contains("当前层数：") && !liveBody.Text.Contains("来源：") && !liveBody.Text.Contains("简介：") && liveBody.Text.Contains("效果："), "不可叠层心法不凑层数 " + skill.skill_name);
                Check(!liveBody.Text.Contains("持续时间：") && !liveBody.Text.Contains("本场战斗"), "心法不显示默认持续时间 " + skill.skill_name);
                Check(!liveBody.Text.Contains("类型：") && !liveBody.Text.Contains("关键词：") && liveBody.Text.Split("效果：").Length <= 3, "心法效果不重复类型标题 " + skill.skill_name);
                await Capture(card, skill.skill_name);
            }
            var qi = BattleStatusPresentation.Resource(unit, "炁", 5, 12, "");
            Check(PlayerInfoText.Summary(qi.Body).Contains("达到24炁") && PlayerInfoText.Detailed(qi.Body).Contains("当前法力上限[12]×2＝24"), "炁的真实阈值及两档公式");
            Check(!BattleStatusPresentation.Instance(unit.status_snapshot().Single(s => s.effect?.effect_id == "外伤"), "外伤").Body.Contains("未记录"), "全部状态撤下来源占位");
            GD.Print($"STATUS_HOVER_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { overlay?.Free(); manager?.Free(); PlayerInfoText.SetMode(mode, persist: false); }
    }
}
