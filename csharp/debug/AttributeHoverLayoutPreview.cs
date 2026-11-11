using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Isolated attribute layout review. Does not replace the character page.</summary>
public partial class AttributeHoverLayoutPreview : Node
{
    private const string Evidence = "res://docs/美术规范/悬停公式与长段排版统一_20261109/属性/";
    private Vector2 _pointer;
    private async Task Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true);
        }
    }

    private static HoverInfoManager.HoverInfoContent Preview(Character owner, int index)
    {
        // Review actor has realm growth but no manuals, gear, tempering or statuses.
        // All terms come from the actual realm configuration, never a final-value residual.
        if (index >= 3) return DerivedPreview(owner, index);
        var names = new[] { "体魄", "灵慧", "神魂" };
        var keys = new[] { "physique", "intelligence", "soul" };
        var values = new[] { owner.physique, owner.intelligence, owner.soul };
        var bases = new[] { owner.base_physique, owner.base_intelligence, owner.base_soul };
        var effects = new[]
        {
            "提高生命上限。\n影响行动速度。\n影响精准。\n影响身法。\n影响会心。\n影响护心。\n与神魂共同影响身心协调。\n影响以体魄计算的技能效果。",
            "提高生命上限。\n影响行动速度。\n影响精准。\n影响身法。\n影响会心。\n影响护心。\n影响秘籍研读效率。\n影响以灵慧计算的技能效果。",
            "提高生命上限。\n影响行动速度。\n影响精准。\n影响身法。\n影响会心。\n影响护心。\n与体魄共同影响身心协调。\n影响秘籍研读效率。\n影响以神魂计算的技能效果。"
        };
        var sheet = GD.Load<Texture2D>("res://textures/ui/infopanel_attribute_icons_ink_v2.png");
        var icon = new AtlasTexture { Atlas = sheet, Region = new Rect2(index * 512, 0, 512, 512) };
        var (growth, flat) = owner.get_realm_attribute_adjustment(keys[index]);
        var condensation = owner.get_condensation_flat_bonus(keys[index]);
        if (!Mathf.IsEqualApprox(values[index], bases[index] * growth + flat + condensation))
            throw new Exception("样例存在尚未追踪的来源，不能省略计算项");
        var formula = $"基础值[{bases[index]:0.###}]×境界倍率[{growth:0.###}]＋境界加值[{flat:0.###}]";
        if (condensation != 0) formula += $"＋凝核加值[{condensation:0.###}]";
        formula += $"＝{values[index]:0.###}";
        var value = $"当前值：{values[index]:0.###} 计算公式：{formula}";
        var additions = $"加成明细：\n{owner.cultivation_realm}倍率：{growth:0.###}\n{owner.cultivation_realm}固定加值：{flat:+0.###;-0.###;0}";
        return HoverInfoManager.Structured(names[index], icon, "",
            new("", value), new("", "作用：\n" + effects[index]), new("", additions))
            with { CardWidth = 340f, IconSize = 96f, ShowRules = false };
    }

    private static HoverInfoManager.HoverInfoContent DerivedPreview(Character owner, int index)
    {
        string name, formula, effect; int result; float calculated;
        if (index == 3)
        {
            name = "生命上限"; result = owner.max_health;
            calculated = 60 + 9 * owner.physique + 2 * owner.base_physique + 3 * owner.soul + 1.5f * owner.intelligence;
            formula = $"60＋9×体魄[{owner.physique:0.###}]＋2×基础体魄[{owner.base_physique:0.###}]＋3×神魂[{owner.soul:0.###}]＋1.5×灵慧[{owner.intelligence:0.###}]＝{result}";
            effect = "决定可承受的生命损失上限。";
        }
        else
        {
            name = "精准"; result = owner.precision;
            calculated = 7 * owner.intelligence + owner.soul + .5f * owner.physique;
            formula = $"7×灵慧[{owner.intelligence:0.###}]＋神魂[{owner.soul:0.###}]＋0.5×体魄[{owner.physique:0.###}]＝{result}";
            effect = "影响攻击与技艺的命中判定。";
        }
        if (result != Mathf.RoundToInt(calculated)) throw new Exception("派生值存在尚未追踪的来源");
        return HoverInfoManager.Structured(name, null, "", new("", $"当前值：{result} 计算公式：{formula}"),
            new("", "作用：\n" + effect), new("", "加成明细：无")) with { CardWidth = 420f, ShowRules = false };
    }

    public override void _Ready() => CallDeferred(nameof(Run));
    private async void Run()
    {
        var mode = PlayerInfoText.Mode;
        try
        {
            GetWindow().ContentScaleSize = new Vector2I(1672, 941);
            GetWindow().Size = new Vector2I(1672, 941);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Evidence));
            var owner = new Character { entity_data = new HumanData {
                entity_name = "属性排版预览", cultivation_realm = "云息一层",
                base_physique = 8, base_intelligence = 10, base_soul = 8 } };
            AddChild(owner);
            if (!Mathf.IsEqualApprox(owner.physique, 9.6f) || !Mathf.IsEqualApprox(owner.intelligence, 10.5f) || !Mathf.IsEqualApprox(owner.soul, 8.6f))
                throw new Exception("云息一层样例必须与正式境界资源一致");
            var anchor = new Button { Text = "属性排版预览", Position = new Vector2(100, 140), Size = new Vector2(140, 100) };
            AddChild(anchor); _pointer = anchor.GetGlobalRect().GetCenter(); await Settle();
            var card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            var checks = 0;
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            var roundingSource = "当前值：87 计算公式：round(7×10.5＋8.6＋0.5×9.6)＝87";
            if (PlayerInfoText.Display(roundingSource) != "当前值：87\n\n计算来源\n87 = (7×10.5＋8.6＋0.5×9.6)")
                throw new Exception("去掉取整用语后须保留全部算术项、分组与实际结果");
            checks++;
            if (PlayerInfoText.Detailed("当前值：10 计算公式：向上取整(8×1.2)＝10") != "当前值：10\n\n计算来源\n10 = (8×1.2)")
                throw new Exception("原有中文取整用语同样不应显示");
            checks++;
            foreach (var example in new[] { (0, false, "体魄_简略"), (0, true, "体魄_详细"), (1, true, "灵慧_详细"), (2, true, "神魂_详细"), (3, true, "生命上限_详细"), (4, true, "精准_详细") })
            {
                PlayerInfoText.SetMode(example.Item2 ? PlayerInfoText.InformationMode.Detailed : PlayerInfoText.InformationMode.Simple, persist: false);
                HoverInfoManager.SetInfo(anchor, Preview(owner, example.Item1), true);
                await Settle();
                var text = card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>().Single().Text;
                if (!text.Contains("当前值：") || !text.Contains("作用：") || text.Contains("简介："))
                    throw new Exception("字段不完整 " + example.Item3);
                checks++;
                if (text.Contains("计算来源") != example.Item2 || !text.StartsWith("当前值：")) throw new Exception("简略／详细公式不符");
                if (example.Item2 && (!text.Contains(" = ") || text.Contains("，计算："))) throw new Exception("详细必须包含结果在前、等号两侧空格的完整计算来源");
                if (text.Contains("四舍五入") || text.Contains("向上取整") || text.Contains("向下取整")) throw new Exception("悬停不展开取整规则");
                checks++;
                var rect = card.GetGlobalRect(); var bounds = GetViewport().GetVisibleRect();
                if (!card.Visible || card.Modulate.A < .9f || rect.Size.X < 300 || rect.Size.Y < 200 ||
                    rect.Position.X < 0 || rect.Position.Y < 0 || rect.End.X > bounds.End.X + 1 || rect.End.Y > bounds.End.Y + 1)
                    throw new Exception("预览未展开或出屏 " + example.Item3);
                checks++;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var screen = GetViewport().GetTexture().GetImage();
                using var crop = screen.GetRegion(new Rect2I((Vector2I)rect.Position, (Vector2I)rect.Size));
                if (crop.SavePng(Evidence + "属性计算预览_" + example.Item3 + ".png") != Error.Ok)
                    throw new Exception("截图保存失败");
                checks++;
            }
            GD.Print($"ATTRIBUTE_HOVER_PREVIEW_OK {checks}"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(mode, persist: false); }
    }
}
