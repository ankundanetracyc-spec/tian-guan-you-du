using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>Review-only skill hover layout using production formatters, without replacing skill UI.</summary>
public partial class SkillHoverLayoutPreview : Node
{
    private int _checks;
    private Vector2 _pointer;
    private void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    private async Task Settle()
    {
        for (int i = 0; i < 16; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true);
        }
    }
    private static HoverInfoManager.HoverInfoContent Preview(Character owner, ActiveSkillData skill)
    {
        var original = CombatSkillBar.BuildSkillHoverInfo(owner, skill, null);
        var effect = original.Sections!.First(section => section.Title == "效果").Body;
        var tags = skill.get_presentation_tags().Concat(skill.keywords).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
        effect = string.Join("\n", effect.Split('\n').Where(line => !tags.Contains(line.Trim())));
        var target = CardPresentation.CombatTargetText(skill, effect);
        var cost = $"消耗：体力{skill.energy_cost}、法力{skill.mana_cost}";
        if (CardRules.IsResponse(skill))
        {
            var normal = CardRules.NormalCast(skill);
            cost = $"消耗：\n触发应对时，消耗体力{skill.energy_cost}、法力{skill.mana_cost}\n未触发应对时，消耗体力{normal?.energy_cost ?? 0}、法力{normal?.mana_cost ?? 0}";
            if (skill.target_type == 0 && normal is null) target = "自身";
            effect = effect.Replace("\n未触发应对时，", "\n\n未触发应对时，");
        }
        if (skill.skill_name == "架招") effect = Regex.Replace(effect, @"(?m)^状态层数：[^\r\n]*(?:\r?\n)?", "").Trim();
        if (skill.skill_name == "云岚三叠")
        {
            var manager = new BattleManager();
            var runtime = new GongfaCombatRuntime(manager);
            var extra = runtime.CreateCloudFollowUp(owner, skill);
            var sentence = (string)typeof(CardPresentation).GetMethod("StandardDamageSentence", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { extra, "", owner })!;
            effect += "\n" + sentence.Replace("对指定1名敌方", "每段追加对原目标");
            manager.Free();
        }
        var cooldown = CardPresentation.CombatCooldownText(skill, owner, null);
        if (string.IsNullOrWhiteSpace(cooldown)) cooldown = "冷却：无";
        return HoverInfoManager.Structured(skill.skill_name, original.Icon, "",
            new("", "关键词：" + (tags.Length > 0 ? string.Join("、", tags) : "无") + "\n" + cost + "\n目标：" + target), new("", "效果：\n" + effect),
            new("", "使用次数：" + CardPresentation.CombatUseLimitText(skill, owner, null) + "\n" + cooldown))
            with { CardWidth = 420f, IconSize = 96f, ShowRules = false };
    }
    private async Task Capture(Control card, string name)
    {
        var rect = card.GetGlobalRect(); var bounds = GetViewport().GetVisibleRect();
        Check(card.Visible && card.Modulate.A > .9f && rect.Size.X >= 300 && rect.Size.Y > 150, "技能卡完整展开 " + name);
        Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= bounds.End.X + 1 && rect.End.Y <= bounds.End.Y + 1, "技能卡不出屏 " + name);
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screen = GetViewport().GetTexture().GetImage();
        using var crop = screen.GetRegion(new Rect2I((Vector2I)rect.Position, (Vector2I)rect.Size));
        Check(crop.SavePng("res://docs/美术规范/信息栏整理_20261025/24_技能排版预览_" + name + ".png") == Error.Ok, "截图保存 " + name);
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        var mode = PlayerInfoText.Mode;
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1672, 941); GetWindow().Size = new Vector2I(1672, 941);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            var owner = new Character { entity_data = new HumanData { entity_name = "技能预览", base_physique = 10, physique = 10, base_intelligence = 8, intelligence = 8, base_soul = 8, soul = 8 } }; AddChild(owner);
            var anchor = new Button { Text = "技能排版预览", Position = new Vector2(100, 140), Size = new Vector2(140, 100) }; AddChild(anchor);
            _pointer = anchor.GetGlobalRect().GetCenter(); await Settle();
            var card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            foreach (var path in new[] { "痛击", "架招", "舒卷天章/云岚三叠" })
            {
                var skill = GD.Load<ActiveSkillData>("res://resources/activeskills/" + path + ".tres");
                var info = Preview(owner, skill);
                HoverInfoManager.SetInfo(anchor, info, true); await Settle();
                var body = card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single();
                Check(body.Text.Contains("关键词：") && body.Text.Contains("消耗：") && body.Text.Contains("目标：") && body.Text.Contains("使用次数：") && body.Text.Contains("冷却："), path + "预览固定字段");
                Check(!body.Text.Contains("计算公式：") && !body.Text.Contains("只计当前已生效"), path + "简略无底层结算说明");
                Check(!Regex.IsMatch(body.Text, @"(?:触发应对|未触发应对)[：:]"), path + "条件从句不用嵌套冒号");
                if (skill.skill_name == "痛击") Check(body.Text.Contains("关键词：无") && !skill.keywords.Contains("重击"), "痛击没有重击关键词");
                if (skill.skill_name == "架招") Check(body.Text.Contains("触发应对时，") && body.Text.Contains("未触发应对时，"), "应对两种分支使用时表述");
                await Capture(card, skill.skill_name);
            }
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            var pain = GD.Load<ActiveSkillData>("res://resources/activeskills/痛击.tres");
            foreach (var path in new[] { "痛击", "内景损益篇/化法/痛击", "蓄势重击" })
                Check(!GD.Load<ActiveSkillData>("res://resources/activeskills/" + path + ".tres").keywords.Contains("重击"), "所有遗留重击标签已清理 " + path);
            owner.punch_frame = 0;
            var baselineDamage = BattleActionResolver.preview_damage(owner, pain);
            owner.punch_frame = 1;
            Check(Mathf.IsEqualApprox(baselineDamage, BattleActionResolver.preview_damage(owner, pain)), "旧拳架不再给痛击额外增伤");
            var combatManager = new BattleManager(); AddChild(combatManager); combatManager.set_spectator_battle(true);
            var target = new Creature { entity_data = new HumanData { max_health = 500, current_health = 500 } }; AddChild(target);
            var executor = new SkillExecutor(); AddChild(executor);
            object?[] hitArguments = { owner, pain, target, false, 0, false, 0, true, false, false, null };
            typeof(SkillExecutor).GetMethod("ApplyToTarget", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(executor, hitArguments);
            Check((bool)hitArguments[5]! && target.current_health < 500 && owner.punch_frame == 1, "实际命中结算不再消耗拳架");
            executor.QueueFree(); target.QueueFree(); combatManager.QueueFree(); await Settle();
            owner.punch_frame = 0;
            Check(pain.energy_cost == 2 && pain.mana_cost == 0 && pain.target_type == 2 && pain.base_damage == 11, "痛击费用目标及基础伤害保持");
            HoverInfoManager.SetInfo(anchor, Preview(owner, pain), true); await Settle();
            Check(card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single().Text.Contains("（"), "详细保留括号公式");
            await Capture(card, "痛击_详细");
            GD.Print($"SKILL_HOVER_LAYOUT_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(mode, persist: false); }
    }
}
