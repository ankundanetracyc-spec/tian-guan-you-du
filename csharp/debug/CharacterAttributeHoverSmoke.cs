using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class CharacterAttributeHoverSmoke : Node
{
    private int _checks;
    private Vector2 _pointer;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; }
    private async Task Settle()
    {
        for (var i = 0; i < 20; i++) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true); }
    }
    private static string Text(Creature owner, string row, string name) => PlayerInfoText.Display(CharacterAttributePresentation.Build(owner, row, name).Body);
    private async Task Capture(Control card, string name)
    {
        var rect = card.GetGlobalRect(); var bounds = GetViewport().GetVisibleRect();
        Check(card.Visible && card.Modulate.A > .9f && rect.Size.X >= 300 && rect.Size.Y > 150, "完整展开 " + name);
        Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= bounds.End.X + 1 && rect.End.Y <= bounds.End.Y + 1, "悬停不出屏 " + name);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        var pixels = (GetViewport().GetFinalTransform() * card.GetGlobalTransformWithCanvas()) * new Rect2(Vector2.Zero, card.Size);
        Check(pixels.Position.X >= 0 && pixels.Position.Y >= 0 && pixels.End.X <= image.GetWidth() + 1 && pixels.End.Y <= image.GetHeight() + 1, "实际像素范围不裁切 " + name);
        using var crop = image.GetRegion(new Rect2I((Vector2I)pixels.Position, (Vector2I)pixels.Size));
        Check(crop.SavePng("res://docs/美术规范/信息栏整理_20261025/27_人物属性实装_" + name + ".png") == Error.Ok, "截图 " + name);
    }
    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        var mode = PlayerInfoText.Mode;
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1672, 941); GetWindow().Size = new Vector2I(1672, 941);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            var owner = new Character { entity_data = new HumanData { entity_name = "属性实装验收", cultivation_realm = "云息一层", base_physique = 8, base_intelligence = 10, base_soul = 8 } }; AddChild(owner); owner.Hide();
            var data = (HumanData)owner.entity_data!;
            Check(Mathf.IsEqualApprox(owner.physique, 9.6f) && owner.max_health == 204 && owner.precision == 87, "正式境界及派生数值保持");
            foreach (var entry in new[] { ("PhysiqueRow", "体魄"), ("IntelligenceRow", "灵慧"), ("SoulRow", "神魂"), ("FortuneRow", "福缘"), ("ComprehensionRow", "悟性"), ("CharismaRow", "魅力"), ("SocialRow", "交际"), ("ManagementRow", "治事"), ("HealthRow", "生命"), ("StaminaRow", "体力"), ("ManaRow", "法力"), ("MindRow", "速度"), ("SpiritPerceptionRow", "灵识"), ("PhysicalDefenseRow", "物理防御"), ("PrecisionRow", "精准"), ("DodgeRow", "身法"), ("CriticalRow", "会心"), ("CritResistRow", "护心"), ("CritDamageRow", "会心伤害"), ("StaminaRegenRow", "物理减伤"), ("ManaRegenRow", "遁速"), ("LearningRow", "学习效率"), ("CultivationRow", "修炼效率"), ("StrandsRow", "灵力") })
            {
                var text = Text(owner, entry.Item1, entry.Item2);
                Check(text.Contains("当前") && text.Contains("＝") && text.Contains("作用：") && text.Contains("加成明细："), "字段与算式 " + entry.Item1);
                Check(!text.Contains("取整") && !text.Contains("四舍五入") && !text.Contains("简介："), "无底层处理与简介 " + entry.Item1);
            }
            Check(Text(owner, "PrecisionRow", "精准").Contains("灵慧[10.5]") && Text(owner, "PrecisionRow", "精准").Contains("＝87"), "精准真实代入");
            foreach (var key in new[] { "sword", "blade", "spear", "body", "magic", "forging", "talisman", "formation", "alchemy" })
                Check(Text(owner, key + "_proficiency", "造诣").Contains("基础造诣["), "九类造诣 " + key);
            var sword = "res://resources/entities/equipment/承露剑.tres";
            Check(owner.add_item_to_inventory_silently(sword) && owner.equip_inventory_item(sword), "真实装备承露剑");
            Check(owner.physical_defense == 2 && Text(owner, "PhysicalDefenseRow", "物理防御").Contains("承露剑[2]"), "实际装备来源记录");
            data.body_refinement_progress[BodyRefinementRules.Bone] = BodyRefinementRules.GetThreshold(BodyRefinementRules.Bone, 5);
            data.body_refinement_progress[BodyRefinementRules.Skin] = BodyRefinementRules.GetThreshold(BodyRefinementRules.Skin, 5);
            owner.refresh_gongfa_effects();
            Check(Text(owner, "PhysicalDefenseRow", "物理防御").Contains("炼体·皮") && Text(owner, "PhysicalDefenseRow", "物理防御").Contains("承露剑"), "实际炼体与装备共同来源");
            Check(Text(owner, "HealthRow", "生命").Contains("炼体·骨[2]"), "骨部位生命奖励只计入生命");
            var before = owner.physical_defense; var healthBefore = owner.max_health;
            owner.refresh_gongfa_effects();
            Check(owner.physical_defense == before && owner.max_health == healthBefore, "显示记录与重复刷新不累加奖励");
            Check(owner.unequip_inventory_item(sword), "真实卸下");
            Check(!Text(owner, "PhysicalDefenseRow", "物理防御").Contains("承露剑"), "来源随卸下消失");
            var artifact = ForgedArtifactInventory.Create(new ProcessForgingTrial.Artifact {
                InstanceId = "attribute-source-test", Name = "属性来源验收剑", Category = "剑", Tier = "法器", BondContributions = ["抱朴 +2"],
                Effects = [new() { CarrierId = "base", AtomId = "physical_defense", Value = 10 }] });
            owner.add_artifact_to_inventory(artifact);
            Check(owner.equip_artifact(artifact.instance_id), "真实成器装备");
            Check(owner.physical_defense == 12 && Text(owner, "PhysicalDefenseRow", "物理防御").Contains("×器蕴倍率[1.1]"), "物防器蕴保留真实乘法顺序");
            Check(Text(owner, "PhysicalDefenseRow", "物理防御").Contains("属性来源验收剑[10]"), "实际成器来源");
            Check(owner.unequip_artifact(artifact.instance_id) && owner.physical_defense == 1, "卸下成器恢复实际炼体物防");
            const string bookPath = "res://resources/gongfa/太和感物养元宝册.tres";
            var book = GD.Load<GongfaResource>(bookPath);
            var learned = new HumanData { entity_name = "永久奖励核对", cultivation_realm = "云息一层", base_physique = 8,
                learned_gongfa_paths = [bookPath], main_gongfa_path = bookPath,
                spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER, spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0] };
            learned.learned_gongfa_chapters[bookPath] = new Godot.Collections.Array<string>(book.get_sorted_chapters().Select(chapter => chapter.get_id()));
            var reader = new Character { entity_data = learned }; AddChild(reader); reader.Hide();
            var permanentBase = reader.base_physique; var permanentFinal = reader.physique;
            Check(permanentBase > 8 && Text(reader, "PhysiqueRow", "体魄").Contains($"基础值[{Character.AttributeNumber(permanentBase)}]"), "真实永久研读奖励已计入基础值");
            reader.refresh_gongfa_effects();
            Check(reader.base_physique == permanentBase && reader.physique == permanentFinal, "永久奖励不会再计为额外加成");
            Check(!Text(reader, "PhysiqueRow", "体魄").Contains("＋太和"), "不会重复计算已入基础值的秘籍奖励");
            Check(reader.get_cultivation_rate() > 0 && Text(reader, "CultivationRow", "修炼效率").Contains("0.005×境界系数"), "修炼效率读取现行基础速率与环境");
            var anchor = new Button { Position = new Vector2(90, 100), Size = new Vector2(100, 70), Text = "属性验收" }; AddChild(anchor);
            _pointer = anchor.GetGlobalRect().GetCenter(); await Settle();
            var card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            foreach (var entry in new[] { ("PhysiqueRow", "体魄"), ("PrecisionRow", "精准"), ("PhysicalDefenseRow", "物理防御"), ("HealthRow", "生命") })
            {
                HoverInfoManager.SetInfo(anchor, CharacterAttributePresentation.Build(owner, entry.Item1, entry.Item2), true); await Settle(); await Capture(card, entry.Item2);
            }
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            Check(!Text(owner, "PhysiqueRow", "体魄").Contains("＝"), "简略不显示公式");
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            var panel = GD.Load<PackedScene>("res://tscn/panel/InfoPanel.tscn").Instantiate<InfoPanel>(); AddChild(panel); panel.show_entity(owner);
            typeof(InfoPanel).GetMethod("render_new_page", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [1]); await Settle();
            var pages = (Dictionary<int, Control>)typeof(InfoPanel).GetField("_characterPages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
            var anchors = pages[1].FindChildren("*", "Control", true, false).OfType<Control>().Where(c => c.HasMeta("attribute_hover_row")).ToArray();
            foreach (var row in new[] { "PhysiqueRow", "PrecisionRow", "HealthRow", "LearningRow", "CultivationRow", "sword_proficiency", "RootWeight:0" })
                Check(anchors.Count(c => c.GetMeta("attribute_hover_row").AsString() == row) >= 2, "真实页面名称与数值共用 " + row);
            var actual = anchors.First(c => c.GetMeta("attribute_hover_row").AsString() == "PhysiqueRow" && c.IsVisibleInTree());
            _pointer = actual.GetGlobalRect().GetCenter(); actual.EmitSignal(Control.SignalName.MouseEntered); await Settle();
            HoverInfoManager.SetInfo(actual, CharacterAttributePresentation.Build(owner, "PhysiqueRow", "体魄"), true); await Settle(); await Capture(card, "真实页面体魄");
            GetWindow().Size = new Vector2I(1280, 720); await Settle();
            _pointer = actual.GetGlobalRect().GetCenter(); HoverInfoManager.SetInfo(actual, CharacterAttributePresentation.Build(owner, "PhysiqueRow", "体魄"), true); await Settle(); await Capture(card, "小窗口体魄");
            GD.Print($"CHARACTER_ATTRIBUTE_HOVER_OK {_checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(mode, persist: false); }
    }
}
