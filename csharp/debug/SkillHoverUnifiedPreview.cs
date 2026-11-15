using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>Exercise the actual board hover handler and shared reader with isolated actors.</summary>
public partial class SkillHoverUnifiedPreview : Control
{
    private const string Output = "res://docs/美术规范/悬停公式与长段排版统一_20261109/技能/";
    private Character _owner = null!;
    private BattleBoardView _board = null!;
    private CombatBoardCatalog _catalog = null!;
    private Control _card = null!;
    private Button _source = null!;
    private Vector2 _pointer;
    private string _resolution = "";
    private int _checks;
    private readonly List<string> _captures = [];
    private readonly Dictionary<string, string> _bodyBySkill = new();
    private readonly List<object> _rewriteOutputs = [];
    private int _formatCasesAudited;
    private int _rewritesAudited;

    public override void _Ready() => CallDeferred(nameof(Run));
    private void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; }
    private void Move(Vector2 point)
    {
        _pointer = point;
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
    }
    private async Task Settle(int frames = 15)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Move(_pointer);
        }
    }
    private string VisibleBody() => string.Join("\n\n", _card.FindChildren("*", "RichTextLabel", true, false)
        .OfType<RichTextLabel>().Where(label => label.IsVisibleInTree()).Select(label => label.GetParsedText()));

    private async Task WaitForHover(HoverInfoManager.HoverInfoContent info)
    {
        var expected = Regex.Replace(PlayerInfoText.Display(info.Body), @"\s+", "");
        for (var frame = 0; frame < 90; frame++)
        {
            if (_card.IsVisibleInTree() && _card.Modulate.A > .99f
                && _card.FindChildren("*", "Label", true, false).OfType<Label>()
                    .Any(label => label.IsVisibleInTree() && label.Text == info.Title)
                && Regex.Replace(VisibleBody(), @"\s+", "") == expected) return;
            await Settle(1);
        }
        throw new Exception(info.Title + ": hover did not finish rendering its current content. Visible="
            + _card.IsVisibleInTree() + "; Alpha=" + _card.Modulate.A + "; Body=" + VisibleBody());
    }

    private async Task Capture(string name, bool requireGrid = true)
    {
        for (var frame = 0; frame < 45 && (!_card.IsVisibleInTree() || _card.Modulate.A < .99f); frame++)
            await Settle(1);
        Check(_card.IsVisibleInTree() && _card.Modulate.A > .99f, name + ": hover fully rendered");
        var rect = _card.GetGlobalRect();
        var viewport = GetViewport().GetVisibleRect();
        Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= viewport.End.X + 1 && rect.End.Y <= viewport.End.Y + 1,
            name + ": complete card inside viewport");
        var footer = _card.FindChild("HoverFootprintFooter", true, false) as Control;
        if (requireGrid)
        {
            Check(footer?.IsVisibleInTree() == true && footer.GetIndex() == footer.GetParent().GetChildCount() - 1,
                name + ": grid footer is last");
            Check(!_card.FindChild("HeaderInventoryFootprint", true, false).Get("visible").AsBool(), name + ": no inventory grid under skill name");
        }
        Check(_card.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>()
            .All(scroll => scroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled), name + ": no internal scrolling");
        Check(_card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>()
            .Count(label => label.IsVisibleInTree()) == 1, name + ": one continuous body column");
        Check(!VisibleBody().Contains("可摆份数") && !VisibleBody().Contains("使用限制"), name + ": no placement or trailing limits section");
        Check(!VisibleBody().Contains("预估"), name + ": player wording has no preview prefix");
        Check(_card.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>()
            .Where(label => label.IsVisibleInTree()).All(label => label.GetThemeFontSize("normal_font_size") >= 16),
            name + ": long explanations retain a readable font size");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var screen = GetViewport().GetTexture().GetImage();
        var cropRect = new Rect2I((Vector2I)rect.Position.Floor(), (Vector2I)rect.Size.Ceil()).Intersection(new(Vector2I.Zero, screen.GetSize()));
        using var crop = screen.GetRegion(cropRect);
        var stem = Output + _resolution + "/" + name;
        Check(screen.SavePng(stem + "_full.png") == Error.Ok && crop.SavePng(stem + "_hover.png") == Error.Ok, name + ": native captures saved");
        _captures.Add(name);
    }

    private async Task ShowBoard(ActiveSkillData skill, string name, bool rotated = false)
    {
        _source.Hide(); _board.Show();
        Move(new(15, 15)); _board.EmitSignal(Control.SignalName.MouseExited);
        HoverInfoManager.HideAll(); await Settle(3);
        var placement = new CombatBoardPlacement
        {
            placement_id = name, source_key = skill.get_board_skill_key(), kind = CombatBoardPlacement.ObjectKind.Skill,
            cell = Vector2I.Zero, quarter_turns = rotated ? 1 : 0,
        };
        _board.Layout = new CombatBoardLayout { size = new(3, 3) };
        _board.Layout.placements.Add(placement); _board.QueueRedraw();
        Move(_board.GetGlobalRect().Position + Vector2.One * 30);
        _board._GuiInput(new InputEventMouseMotion { Position = new(30, 30) });
        await ToSignal(GetTree().CreateTimer(.16), SceneTreeTimer.SignalName.Timeout); await Settle();
        Check(_board._GetTooltip(new(30, 30)) == "", name + ": legacy popup suppressed");
        var info = CombatBoardPresentation.PlacementHover(_board.Layout, _catalog, placement, _owner);
        await WaitForHover(info);
        var expectedSize = CombatBoardGeometry.Footprint(placement, _catalog.FindSkill);
        Check(info.GridDiagrams?.Single().Cells.Count == expectedSize.X * expectedSize.Y, name + ": real rotated shape retained");
        var body = VisibleBody();
        var effect = PlayerInfoText.LayoutLongParagraphs(PlayerInfoText.Summary(CardPresentation.BoardHoverEffect(skill, _owner)));
        var calculationIndex = body.IndexOf("计算来源\n", StringComparison.Ordinal);
        var needsCalculations = PlayerInfoText.Mode == PlayerInfoText.InformationMode.Detailed
            && PlayerInfoText.CalculationSources(info.Body).Count > 0;
        Check((calculationIndex >= 0) == needsCalculations, name + ": detail setting controls final calculation block");
        if (needsCalculations)
            Check(body.IndexOf(effect, StringComparison.Ordinal) >= 0
                && calculationIndex > body.IndexOf(effect, StringComparison.Ordinal) + effect.Length,
                name + ": all effects precede calculation sources");
        _bodyBySkill[name] = body;
        Check(!body.Contains("合法") && !Regex.IsMatch(body, @"占格\s*\d|基础伤害|计算公式[：:]"), name + ": no obsolete wording");
        Check(!Regex.IsMatch(body, @"(?:^|\n)\s*[（(](.?:预估|对|造成)"), name + ": effect paragraph has no outer formula brackets");
        await Capture(name);
    }

    private void VerifyFormattingContract()
    {
        const string body = "保留真实效果与使用条件。";
        var cases = new (string Source, string Expected)[]
        {
            ("计算公式：min(3,4)＝3", "3 = min(3,4)"),
            ("计算公式：MIN(3,4)＝3", "3 = min(3,4)"),
            ("计算公式：取较小值(3,4)＝3", "3 = min(3,4)"),
            ("计算公式：取最小值（3，4）＝3", "3 = min（3，4）"),
            ("计算公式：min(max(3,4),5)＝4", "4 = min(max(3,4),5)"),
            ("计算公式：取最小值(max(3,4),取较大值(2,5))＝4", "4 = min(max(3,4),max(2,5))"),
            ("计算公式：化解：min(体魄[46.51]×0.25,神魂[28.65]×0.2)＝5", "化解：5 = min(体魄[46.51]×0.25,神魂[28.65]×0.2)"),
            ("\n\n计算来源\n19，计算：3+11+5", "19 = 3+11+5"),
            ("\n\n计算来源\n化解：19，计算：3+11+5", "化解：19 = 3+11+5"),
            ("\n\n计算来源\n19, 计算: min(19,20)", "19 = min(19,20)"),
            ("\n\n计算来源\n化解:19, 计算:min(19,20)", "化解：19 = min(19,20)"),
            ("\n\n计算来源\n19=min(19,20)", "19 = min(19,20)"),
            ("\n\n计算来源\n化解：19 = min(19,20)", "化解：19 = min(19,20)"),
            ("\n\n计算来源\n概率：50% = min(50%,80%)", "概率：50% = min(50%,80%)"),
            ("\n\n计算来源\n化解：19层 = min(19,20)", "化解：19层 = min(19,20)"),
            ("\n\n计算来源\n化解：（19 = min(19,20)）", "化解：19 = min(19,20)"),
            ("\n\n计算来源\n伤害：A = 7+体魄×0.2", "伤害：A = 7+体魄×0.2"),
            ("\n\n计算来源\n护盾：待结算值 = min(攻击者当前护盾值,本次抵消物伤)", "护盾：待结算值 = min(攻击者当前护盾值,本次抵消物伤)"),
            ("\n\n计算来源\n护盾：本次结算值 = min(攻击者当前护盾值,本次抵消物伤)", "护盾：本次结算值 = min(攻击者当前护盾值,本次抵消物伤)"),
        };
        foreach (var (source, expected) in cases)
        {
            var input = body + (source.StartsWith('\n') ? "" : "\n") + source;
            var detailed = PlayerInfoText.Detailed(input);
            Check(detailed == body + "\n\n计算来源\n" + expected, "canonical formula case: " + source);
            Check(PlayerInfoText.Summary(input) == body && PlayerInfoText.Summary(detailed) == body,
                "source migration retains result text and real conditions: " + expected);
            for (var repeat = 0; repeat < 3; repeat++)
            {
                detailed = PlayerInfoText.Detailed(detailed);
                Check(detailed == body + "\n\n计算来源\n" + expected, "formatting remains idempotent: " + expected + "; " + repeat);
            }
        }
        _formatCasesAudited = cases.Length;
        var combined = body + "\n\n计算来源\n化解：19，计算：3+11+5\n\n护盾：7 = min(7,8)\n\n化解：19 = 3+11+5";
        Check(PlayerInfoText.Detailed(combined) == body + "\n\n计算来源\n化解：19 = 3+11+5\n\n护盾：7 = min(7,8)",
            "old and canonical footer entries deduplicate with one blank line between calculations");
        const string shortFields = "关键词：应对\n消耗：体力1、法力0\n目标：自身\n整局限制：2次，同名共享\n次数：每回合1次\n出手方式：远程";
        Check(PlayerInfoText.LayoutLongParagraphs(shortFields) == shortFields,
            "compact costs targets counts and keywords keep their original line without inserted blank lines");
        const string paragraph = "效果：应对敌方物理攻击时，获得19层化解。\n化法追加：抵消物理伤害后，攻击者失去相应护盾。\n费用：体力1、法力0";
        const string laidOut = "效果：\n应对敌方物理攻击时，获得19层化解。\n化法追加：\n抵消物理伤害后，攻击者失去相应护盾。\n费用：体力1、法力0";
        Check(PlayerInfoText.LayoutLongParagraphs(paragraph) == laidOut,
            "long effect and rewrite prose begin below their semantic heading without universal blank rows");
        Check(PlayerInfoText.LayoutLongParagraphs(laidOut) == laidOut, "long paragraph layout is idempotent");
        const string styled = "[color=#d7c8a0][b]效果：[/b][/color]造成12点物理伤害。";
        Check(PlayerInfoText.LayoutLongParagraphs(styled) == "[color=#d7c8a0][b]效果：[/b][/color]\n造成12点物理伤害。",
            "styled headings preserve BBCode and complete effect prose");
        const string sourceFooter = "\n\n[color=#d7c8a0][b]计算来源[/b][/color]\n效果：19 = min(max(3,19),20)\n\n护盾：7 = min(7,8)";
        Check(PlayerInfoText.LayoutLongParagraphs(paragraph + sourceFooter) == laidOut + sourceFooter,
            "paragraph layout never splits a calculation name result or formula and retains item spacing");
    }

    private async Task ShowSource(ActiveSkillData skill, string name, string boardName)
    {
        Move(new(15, 15)); _board.EmitSignal(Control.SignalName.MouseExited); _board.Hide();
        HoverInfoManager.HideAll(); _source.Show(); await Settle(3);
        Move(_source.GetGlobalRect().GetCenter());
        HoverInfoManager.SetProvider(_source, () => CombatBoardPresentation.SkillHover(skill, _owner));
        await ToSignal(GetTree().CreateTimer(.16), SceneTreeTimer.SignalName.Timeout); await Settle();
        await WaitForHover(CombatBoardPresentation.SkillHover(skill, _owner));
        Check(Regex.Replace(VisibleBody(), @"\s+", "") == Regex.Replace(_bodyBySkill[boardName], @"\s+", ""),
            name + ": source and board show identical content");
        await Capture(name);
    }

    private async Task VerifyOtherInformation()
    {
        _board.Hide(); _source.Show(); HoverInfoManager.HideAll();
        Move(_source.GetGlobalRect().GetCenter());
        var examples = new[]
        {
            StoragePanel.ItemHover(GD.Load<ItemData>("res://resources/entities/items/兽皮.tres"), 1),
            AlchemyPillVisual.Hover(GD.Load<PillItemData>("res://resources/entities/items/alchemy/huichun_1.tres"), user: _owner),
            CharacterAttributePresentation.Build(_owner, "PrecisionRow", "精准"),
            HoverInfoManager.Structured("分组计算来源核对", null, "",
                new("效果", "获得8层周流。计算公式：体魄[10]×0.8＝8。"),
                new("后续效果", "获得5点护盾。计算公式：神魂[10]×0.5＝5。")),
        };
        foreach (var info in examples)
        {
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            HoverInfoManager.SetInfo(_source, info, true); await Settle();
            var text = VisibleBody();
            Check(Regex.Replace(text, @"\s+", "") == Regex.Replace(PlayerInfoText.Detailed(info.Body), @"\s+", ""),
                info.Title + ": structured reader places all calculations after all sections");
            Check(Regex.Matches(text, "计算来源").Count == 1, info.Title + ": one final calculation block");
            var rect = _card.GetGlobalRect(); var bounds = GetViewport().GetVisibleRect();
            Check(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= bounds.End.X + 1 && rect.End.Y <= bounds.End.Y + 1,
                info.Title + ": full shared information stays within window");
            await Capture("共用_" + info.Title + "_详细", requireGrid: false);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false); await Settle();
            Check(Regex.Replace(VisibleBody(), @"\s+", "") == Regex.Replace(PlayerInfoText.Summary(info.Body), @"\s+", ""),
                info.Title + ": open hover immediately switches to final results only");
            await Capture("共用_" + info.Title + "_简略", requireGrid: false);
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false); await Settle();
            Check(VisibleBody().Contains("计算来源\n"), info.Title + ": reopening detailed mode restores source lines");
        }
    }

    private async void Run()
    {
        var originalMode = PlayerInfoText.Mode;
        try
        {
            var size = OS.GetCmdlineUserArgs().Contains("--large") ? new Vector2I(1920, 1080) : new Vector2I(1280, 720);
            _resolution = size.X + "x" + size.Y;
            GetWindow().ContentScaleSize = size; GetWindow().Size = size;
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Output + _resolution));
            VerifyFormattingContract();
            var background = new ColorRect { Color = new("343a30"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _owner = new Character();
            _owner.load_from_data(new HumanData
            {
                entity_name = "悬停隔离样例", unique_id = "isolated-unified-skill-hover", physique = 10, intelligence = 10, soul = 10,
                base_physique = 10, base_intelligence = 10, base_soul = 10,
                body_proficiency = 10, magic_proficiency = 10, sword_proficiency = 10,
                base_body_proficiency = 10, base_magic_proficiency = 10, base_sword_proficiency = 10,
            });
            AddChild(_owner);
            _catalog = new CombatBoardCatalog();
            var root = ProjectSettings.GlobalizePath("res://");
            foreach (var path in System.IO.Directory.EnumerateFiles(root + "resources/activeskills", "*.tres", System.IO.SearchOption.AllDirectories))
            {
                if (!System.IO.File.ReadAllText(path).Contains("board_definition_version = 1")) continue;
                var uri = "res://" + System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
                var skill = GD.Load<ActiveSkillData>(uri);
                if (skill is not null && skill.get_board_skill_key() == uri) _catalog.skills.Add(skill);
            }
            Check(_catalog.skills.Count == 20, "all twenty current base skills loaded");
            var baseline = _catalog.skills.ToDictionary(skill => skill, skill => (skill.energy_cost, skill.mana_cost,
                skill.board_size, skill.board_max_placements, skill.calculate_damage(_owner)));
            foreach (var skill in _catalog.skills)
            {
                var info = CombatBoardPresentation.SkillHover(skill, _owner);
                Check(!info.Title.Contains('*') && info.Title == skill.skill_name, skill.skill_name + ": clean identity");
                Check(info.GridDiagrams?.Single().Cells.Count == skill.board_size.X * skill.board_size.Y,
                    skill.skill_name + ": actual board cells");
                Check(info.CompactGridFooter && info.Footprint is null && !info.InventoryFootprintBelowTitle,
                    skill.skill_name + ": footer only");
                Check(info.SingleColumn && !info.Body.Contains("可摆份数") && !info.Body.Contains("使用限制"),
                    skill.skill_name + ": continuous body without placement limit");
                Check(!info.Body.Contains("合法") && !Regex.IsMatch(info.Body, @"占格\s*\d"), skill.skill_name + ": clean short fields");
                foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
                {
                    PlayerInfoText.SetMode(mode, persist: false);
                    var text = PlayerInfoText.Display(info.Body);
                    var summary = PlayerInfoText.Summary(info.Body);
                    Check(!text.Contains("预估") && !text.Contains('≈') && PlayerInfoText.Summary(text) == summary,
                        skill.skill_name + ": both modes have identical final results");
                    if (mode == PlayerInfoText.InformationMode.Detailed)
                    {
                        Check(PlayerInfoText.Detailed(text) == text, skill.skill_name + ": repeated formatting preserves one final source block");
                        var parts = text.Split("\n\n计算来源\n", StringSplitOptions.None);
                        Check(parts[0] == summary && !parts[0].Contains('≈'), skill.skill_name + ": formulas are outside effect text");
                        Check(!text.Contains("，计算：") && !Regex.IsMatch(text, @"取(?:较|最)小值"),
                            skill.skill_name + ": current base source uses canonical equality and min spelling");
                    }
                    var prose = PlayerInfoText.LayoutLongParagraphs(text);
                    Check(PlayerInfoText.LayoutLongParagraphs(prose) == prose && Regex.Replace(prose, @"\s+", "") == Regex.Replace(text, @"\s+", ""),
                        skill.skill_name + ": global prose layout is idempotent and retains all effects and sources");
                    Check(!Regex.IsMatch(text, @"(?:^|\n)\s*[（(](.?:预估|对|造成)"), skill.skill_name + ": no whole-effect brackets");
                    if (mode == PlayerInfoText.InformationMode.Simple) Check(!text.Contains('≈'), skill.skill_name + ": summary has no formulas");
                }
            }
            var yingdi = _catalog.skills.Single(skill => skill.skill_name == "应敌");
            var limited = (ActiveSkillData)yingdi.Duplicate();
            limited.uses_per_battle = 2;
            limited.max_charges = 5;
            limited.uses_per_round = 1;
            limited.cooldown_rounds = 2;
            var limitedText = CombatBoardPresentation.SkillHover(limited, _owner).Body;
            Check(limitedText.Contains("消耗：体力1、法力0\n整局限制：2次，同名共享\n目标："),
                "battle cap follows cost and takes precedence over legacy charges");
            Check(!limitedText.Contains("每回合：") && !limitedText.Contains("冷却：") && !limitedText.Contains("可摆份数"),
                "only the whole-battle cap has a restriction field");
            limited.uses_per_battle = 0;
            Check(CombatBoardPresentation.SkillHover(limited, _owner).Body.Contains("整局限制：5次"), "legacy whole-battle charges retained");
            limited.max_charges = 0;
            Check(!CombatBoardPresentation.SkillHover(limited, _owner).Body.Contains("整局限制"), "unlimited skills have no cap placeholder");
            Check(Mathf.IsEqualApprox(yingdi.calculate_damage(_owner), 11.5f), "actual unrounded Yingdi is 11.5");
            Check(Mathf.RoundToInt(BattleActionResolver.preview_damage(_owner, yingdi)) == 12, "actual rounded Yingdi preview is twelve");
            var detail = PlayerInfoText.Detailed(CombatBoardPresentation.SkillHover(yingdi, _owner).Body);
            Check(detail.Contains("造成12点") && detail.Contains("\n\n计算来源\n伤害：12 = 7 +") && !detail.Contains("11.5")
                && !detail.Contains("预估") && !detail.Contains("基础伤害"), "one final result and one separate source row");
            _owner.SetMeta("forged_physical_bonus", .5f);
            var buffed = PlayerInfoText.Summary(CombatBoardPresentation.SkillHover(yingdi, _owner).Body);
            Check(buffed.Contains($"造成{Mathf.RoundToInt(BattleActionResolver.preview_damage(_owner, yingdi))}点"), "own damage modifiers retained");
            _owner.RemoveMeta("forged_physical_bonus");
            var inline = "获得8层周流（体魄[10]×0.8＝8），持续2回合（同名共享）。";
            Check(PlayerInfoText.Summary(inline) == "获得8层周流，持续2回合（同名共享）。", "legacy inline formulas move without removing real conditions");
            Check(PlayerInfoText.Detailed(inline).EndsWith("\n\n计算来源\n8 = 体魄[10]×0.8"), "legacy inline formula moves to its own result-first final line");
            var rounding = PlayerInfoText.Detailed("当前值：10 计算公式：向上取整(8×1.2)＝10");
            Check(rounding == "当前值：10\n\n计算来源\n10 = (8×1.2)", "formula terms and result survive without rounding jargon");
            var subtotal = PlayerInfoText.Detailed("造成14点伤害。计算公式：(7＋体魄[10]×0.2≈基础物伤9)×倍率[1.5]≈14。");
            Check(subtotal == "造成14点伤害。\n\n计算来源\n14 = (7＋体魄[10]×0.2)×倍率[1.5]",
                "legacy approximate subtotals become one result-first source without altering operands");
            var binding = new Label(); AddChild(binding);
            PlayerInfoText.Bind(binding, "获得8层周流。计算公式：体魄[10]×0.8＝8。");
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            Check(binding.Text == "获得8层周流。", "existing label switches immediately to result only");
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Detailed, persist: false);
            Check(binding.Text == "获得8层周流。\n\n计算来源\n8 = 体魄[10]×0.8", "existing label switches immediately to result-first final source line");
            binding.QueueFree();
            _card = GetNode<HoverInfoManager>("/root/HoverInfoManager").GetNode<Control>("SharedHoverInfoCard");
            _board = new BattleBoardView { Layout = new CombatBoardLayout { size = new(3, 3) }, Catalog = _catalog,
                CombatOwner = _owner, CellSize = 72, Position = new(40, size.Y - 270), Size = new(216, 216) };
            AddChild(_board);
            _source = new Button { Text = "招式来源", Position = new(size.X - 220, size.Y - 100), Size = new(180, 60) }; AddChild(_source);
            foreach (var mode in new[] { PlayerInfoText.InformationMode.Simple, PlayerInfoText.InformationMode.Detailed })
            {
                PlayerInfoText.SetMode(mode, persist: false);
                var suffix = mode == PlayerInfoText.InformationMode.Simple ? "简略" : "详细";
                foreach (var skillName in new[] { "应敌", "架招", "云岚三叠", "复命" })
                {
                    var skill = _catalog.skills.Single(skill => skill.skill_name == skillName);
                    await ShowBoard(skill, "盘面_" + skillName + "_" + suffix);
                    await ShowSource(skill, "来源_" + skillName + "_" + suffix, "盘面_" + skillName + "_" + suffix);
                }
            }
            PlayerInfoText.SetMode(PlayerInfoText.InformationMode.Simple, persist: false);
            await ShowBoard(_catalog.skills.Single(skill => skill.skill_name == "痛击"), "盘面_痛击_竖放", rotated: true);
            await VerifyCurrentRewrites();
            await VerifyOtherInformation();
            foreach (var skill in _catalog.skills)
            {
                var expected = baseline[skill];
                Check(expected == (skill.energy_cost, skill.mana_cost, skill.board_size, skill.board_max_placements, skill.calculate_damage(_owner)),
                    skill.skill_name + ": original gameplay fields unchanged");
            }
            var report = new { resolution = _resolution, checks = _checks, captures = _captures, player_save_read = false, player_save_written = false,
                resource_values_changed = false, formula_contract_cases = _formatCasesAudited,
                twenty_base_skills = _catalog.skills.Count, seven_rewrites = _rewritesAudited, rewrite_outputs = _rewriteOutputs };
            using (var file = Godot.FileAccess.Open(Output + _resolution + "/verification.json", Godot.FileAccess.ModeFlags.Write))
                file.StoreString(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"UNIFIED_SKILL_HOVER_OK: {_checks} checks; {_resolution}; {_captures.Count} native screenshots; no gameplay changes");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { PlayerInfoText.SetMode(originalMode, persist: false); }
    }
}

