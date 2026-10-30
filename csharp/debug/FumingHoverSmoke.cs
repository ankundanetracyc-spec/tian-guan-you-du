using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class FumingHoverSmoke : Node
{
    private int _checks;
    private Vector2 _pointer;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); _checks++; }
    private async Task Settle()
    { for (var i = 0; i < 24; i++) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true); } }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        var mode = PlayerInfoText.Mode;
        try
        {
            GetWindow().Size = new Vector2I(1672, 941); GetWindow().ContentScaleSize = new Vector2I(1672, 941);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            var owner = new Character { entity_data = new HumanData { entity_name = "复命显示验收", faction = (int)WorldEntity.Faction.PLAYER, base_physique = 8, base_intelligence = 8, base_soul = 8 } }; AddChild(owner); owner.Hide();
            owner.faction = (int)WorldEntity.Faction.PLAYER;
            var manager = new BattleManager { isolated_trial_battle = true }; AddChild(manager); manager.AddToGroup("battle_mgr"); manager.SetProcess(false);
            manager.all_units.Add(owner); manager.player_units.Add(owner);
            manager.battle_units[owner] = new BattleUnit();
            typeof(BattleManager).GetField("_mainPlayerUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, owner);
            var fuming = GD.Load<ActiveSkillData>("res://resources/activeskills/玄枢含章经/复命.tres");
            var strike = GD.Load<ActiveSkillData>("res://resources/activeskills/玄枢含章经/惊蛰.tres");
            var shield = GD.Load<ActiveSkillData>("res://resources/activeskills/玄枢含章经/云帔.tres");
            var response = GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres");
            var originalIcon = fuming.icon_path; var originalName = fuming.skill_name;
            Check(FumingPresentation.Name(owner, fuming, manager) == "复命", "尚未施展技能时保留复命身份");
            var queued = new BattleQueuedSkill(fuming, owner); manager.get_battle_unit(owner).skill_queue.Add(queued);
            var bar = new CombatSkillBar(); AddChild(bar); bar.set_battle_unit(owner); bar.set_loadout([fuming]); bar.Show();
            var anchor = new Button { Text = "复命悬停验收", Position = new Vector2(90, 100), Size = new Vector2(140, 80) }; AddChild(anchor);
            _pointer = anchor.GetGlobalRect().GetCenter(); await Settle();
            var card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            foreach (var skill in new[] { strike, shield })
            {
                manager.gongfa_combat.Before(owner, skill); await Settle();
                var info = CombatSkillBar.BuildSkillHoverInfo(owner, fuming, manager);
                Check(info.Title == skill.skill_name + "（复命）", "名称跟随 " + skill.skill_name);
                Check(info.Icon?.ResourcePath == skill.icon_path, "悬停图标跟随 " + skill.skill_name);
                Check(info.Body.Contains("复命消耗：") && info.Body.Contains("\n复制《") && info.Body.Contains("\n合计消耗："), "费用分三行");
                Check(info.Body.Contains("从最近一次施展的非应对技能的合法目标中随机选择"), "目标规则保持");
                Check(FumingPresentation.QueueName(owner, queued, manager) == info.Title && queued.skill == fuming, "队列显示变化但技能身份保持");
                var label = bar.FindChildren("SkillName", "Label", true, false).OfType<Label>().Single();
                Check(label.Text == info.Title, "真实技能槽名称更新");
                var icon = bar.FindChildren("Icon", "Control", true, false).OfType<SkillIconVisual>().Single();
                Check(icon.Texture?.ResourcePath == skill.icon_path, "真实技能槽图标更新");
                var snapshot = manager.get_queue_snapshot().Single();
                Check(snapshot["skill_name"].AsString() == info.Title && snapshot["icon_path"].AsString() == skill.icon_path, "队列快照名称和图标跟随");
                var hud = new BattlePlanHud();
                typeof(BattlePlanHud).GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(hud, manager);
                var queueCard = (Control)typeof(BattlePlanHud).GetMethod("Card", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hud, [queued, new Vector2(208, 312)])!;
                AddChild(queueCard);
                Check(queueCard.FindChildren("*", "Label", true, false).OfType<Label>().Any(item => item.Text == info.Title), "实际行动卡名称跟随");
                Check(queueCard.FindChildren("*", "Control", true, false).OfType<SkillIconVisual>().Single().Texture?.ResourcePath == skill.icon_path, "实际行动卡图标跟随");
                queueCard.QueueFree(); hud.Free();
                HoverInfoManager.SetInfo(anchor, info, true); await Settle();
                var rect = (GetViewport().GetFinalTransform() * card.GetGlobalTransformWithCanvas()) * new Rect2(Vector2.Zero, card.Size);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= image.GetWidth() && rect.End.Y <= image.GetHeight(), "实际像素无出屏");
                using var crop = image.GetRegion(new Rect2I((Vector2I)rect.Position, (Vector2I)rect.Size));
                Check(crop.SavePng("res://docs/美术规范/信息栏整理_20261025/28_复命跟随_" + skill.skill_name + ".png") == Error.Ok, "截图保存");
            }
            manager.gongfa_combat.Before(owner, response);
            Check(FumingPresentation.Resolve(owner, fuming, manager) == shield, "应对技能不覆盖复制对象");
            manager.gongfa_combat.Before(owner, fuming);
            Check(FumingPresentation.Resolve(owner, fuming, manager) == shield, "复命不能复制自身");
            var longSkill = GD.Load<ActiveSkillData>("res://resources/activeskills/舒卷天章/云岚三叠.tres");
            manager.gongfa_combat.Before(owner, longSkill); await Settle();
            var longLabel = bar.FindChildren("SkillName", "Label", true, false).OfType<Label>().Single();
            Check(longLabel.Text == "云岚三叠（复命）" && longLabel.AutowrapMode != TextServer.AutowrapMode.Off && longLabel.Size.Y >= 28, "长技能名允许分行以保留复命后缀");
            var actualSlot = longLabel.GetParent<Control>();
            var charge = actualSlot.GetNode<Label>("ChargeText");
            Check(longLabel.Position.Y >= charge.Position.Y + charge.Size.Y && longLabel.Position.Y + longLabel.Size.Y <= CombatSkillBar.SlotHeight, "长名称不覆盖次数且完整位于槽内");
            Check(fuming.skill_name == originalName && fuming.icon_path == originalIcon && fuming.energy_cost == 0 && fuming.mana_cost == 1 && fuming.cooldown_rounds == 2 && queued.skill == fuming, "原始资源费用冷却及队列身份不变");
            GD.Print($"FUMING_HOVER_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(mode, persist: false); }
    }
}
