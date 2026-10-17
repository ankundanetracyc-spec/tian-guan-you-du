using Godot;
using GDict = Godot.Collections.Dictionary;

/// <summary>静室中的单人修行面板。每一次操作均由玩家确认后才推进时间。</summary>
public partial class CultivationUI : Control
{
	private GodotObject? _target;
	private YuanxiPanel? _yuanxiPanel;
	private PanelContainer? _panel;
	private Label? _name, _realm, _power, _rate, _plan, _studyHint, _breakthroughHint;
	private OptionButton? _mode, _studyBook;
	private SpinBox? _strands, _days, _year, _month, _date;
	private Button? _cultivate, _study, _breakthrough;
	private VBoxContainer? _strandRow, _dayRow, _dateRow;
	private readonly List<GongfaResource> _studyCandidates = [];
	private PanelContainer? _overviewPanel;
	private Label? _overviewName, _overviewRealm, _overviewFocus, _overviewBodySummary, _overviewNatureSummary;
	private PanelContainer? _bodyPanel;
	private Label? _bodyTitle, _bodyName, _bodyRealm, _bodyMethodHint, _bodyRequirement, _bodyPreview, _bodyActive, _bodyResult, _bodyNourishHint, _bodyReadOnlyHint;
	private OptionButton? _bodyMethod, _bodyMaterial;
	private Button? _bodyStart, _bodyBack;
	private Control? _bodyMaterialRow;
	private bool _bodyReadOnly;
	private readonly List<BodyRefinementMethod> _bodyMethods = [];
	private readonly Dictionary<string, ProgressBar> _bodyProgressBars = [];
	private readonly Dictionary<string, Label> _bodyProgressLabels = [];
	private readonly Dictionary<string, Label> _bodyRewardLabels = [];
	private readonly Dictionary<string, Label[]> _bodyNodeLabels = [];
	private Texture2D? _bodyPartSigilSheet;
	private PanelContainer? _visualPanel;
	private Label? _visualName, _visualRealm, _visualSummary, _visualMethodHint, _visualResult;
	private OptionButton? _visualFocus, _visualMethod;
	private Button? _visualPractice;
	private readonly Dictionary<string, OptionButton> _visualSlots = [];
	private readonly Dictionary<string, Label> _visualDetails = [];
	/// <summary>正式突破与测试入口均由主场景接管，避免面板持有另一套牌局状态。</summary>
	public event Action<Character>? CondensationRequested;
	public event Action<Character>? CondensationTestRequested;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddToGroup("cultivation_ui_panel");
		ZIndex = 500;
		MouseFilter = MouseFilterEnum.Pass;
		build_room_shell();
		build_ui();
		build_overview_ui();
		build_body_ui();
		
		build_visualization_ui();
		finish_room_shell();
		Hide();
	}

	public Rect2 get_ui_rect() => _room.Visible ? _room.GetGlobalRect() : _overviewPanel?.Visible == true ? _overviewPanel.GetGlobalRect() : _visualPanel?.Visible == true ? _visualPanel.GetGlobalRect() : _bodyPanel?.Visible == true ? _bodyPanel.GetGlobalRect() : _panel?.Visible == true ? _panel.GetGlobalRect() : GetGlobalRect();

	public void show_entity(GodotObject entity)
	{
		if (!GodotObject.IsInstanceValid(entity)) return;
		_yuanxiPanel?.Hide();
		_target = entity;
		_bodyReadOnly = false;
		_roomActions = true;
		set_date_defaults();
		refresh();
		_overviewPanel?.Hide();
		
		_bodyPanel?.Hide();
		_visualPanel?.Hide();
		select_room_page(0);
		Show();
		MoveToFront();
	}

	/// <summary>从宗门授业入口打开功法研读区；研读内容与时间成本仍使用现有静室系统。</summary>
	public void show_teaching_for(GodotObject entity)
	{
		show_entity(entity);
		select_room_page(1);
		_study?.GrabFocus();
	}

	private void build_ui() => build_room_ui();

	/// <summary>修命与修性的统一进度总览；只汇总已有养成数据。</summary>
	private void build_overview_ui()
	{
		_overviewPanel = new PanelContainer { Name = "CultivationOverviewPanel", MouseFilter = MouseFilterEnum.Stop, ZIndex = 30 };
		_overviewPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_overviewPanel.Theme = QuietRoomStyle.MakeTheme();
		_overviewPanel.AddThemeStyleboxOverride("panel", QuietRoomStyle.Paper(1, 36));
		_roomContent.AddChild(_overviewPanel);

		var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 16);
		_overviewPanel.AddChild(root);

		var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 52) };
		root.AddChild(head);
		head.AddChild(label("修行总览", 28, true));
		_overviewName = label("", 22, true); _overviewName.Hide(); root.AddChild(_overviewName);
		_overviewRealm = label("", 16); _overviewRealm.Hide(); root.AddChild(_overviewRealm);
		_overviewFocus = label("", 17, true); _overviewFocus.Hide(); root.AddChild(_overviewFocus);
		var intro = label("修命养形，修性观景。切换主修不清除已积累的修行进度。", 19);
		intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(intro);

		var cards = new HBoxContainer { CustomMinimumSize = new Vector2(0, 420) };
		cards.AddThemeConstantOverride("separation", 16);
		root.AddChild(cards);
		_overviewBodySummary = add_overview_card(cards, "修命", "六部位锻体", "锻体、养形与淬体共同打磨肉身；气血仍是可由多种来源运用的公共状态。", "查看修命进度", () => { if (_target is not null) show_body_overview(_target); });
		_overviewNatureSummary = add_overview_card(cards, "修性", "灵台三景·观变察势应物", "三景面向所有修士开放；本轮只细化日象·烈日，其余图景留配置接口。", "查看修性图景", () => { if (_target is not null) show_visualization_overview(_target); });
		_overviewPanel.Hide();
	}

	private Label add_overview_card(HBoxContainer parent, string title, string subtitle, string description, string buttonText, Action onPressed)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 320) };
		card.AddThemeStyleboxOverride("panel", section_style());
		parent.AddChild(card);
		var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 12);
		margin(card, 20, 20, 18, 18).AddChild(box);
		box.AddChild(label(title, 27, true));
		box.AddChild(label(subtitle, 18));
		var summary = label(description, 19);
		summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		summary.SizeFlagsVertical = SizeFlags.ExpandFill;
		box.AddChild(summary);
		var button = new Button { Text = buttonText, CustomMinimumSize = new Vector2(0, 46) };
		style_action_button(button);
		button.Pressed += onPressed;
		box.AddChild(button);
		return summary;
	}

	public void show_cultivation_overview(GodotObject entity)
	{
		if (!GodotObject.IsInstanceValid(entity) || _overviewPanel is null) return;
		_room.Show();
		_target = entity;
		_bodyReadOnly = false;
		refresh_cultivation_overview();
		_panel?.Hide(); _bodyPanel?.Hide();  _visualPanel?.Hide();
		_studyPage.Hide(); _breakPage.Hide(); _inspirationPage?.Hide(); _yuanxiPanel?.Hide();
		_roomBodyScroll.Hide(); _roomNatureScroll.Hide();
		foreach (var tab in _roomTabs) { tab.ButtonPressed = false; tab.Disabled = !_roomActions; }
		refresh_room_header();
		_overviewPanel.Show(); Show(); MoveToFront();
	}

	private void refresh_cultivation_overview()
	{
		if (_target is null || _overviewPanel is null) return;
		_overviewName!.Text = value("entity_name", "-").AsString();
		_overviewRealm!.Text = $"{value("cultivation_realm", "凡人").AsString()}　·　{value("spirit_root_name", "").AsString()}";
		_overviewFocus!.Text = "战斗构筑：公共基础技能池与已学秘籍；技能和心法效果按各自规则联动。";

		var body = call("get_body_refinement_state").AsGodotDictionary();
		var parts = body.GetValueOrDefault("parts", new Godot.Collections.Array()).AsGodotArray().Select(item => item.AsGodotDictionary()).ToList();
		var bodyAtFive = parts.Count(part => part.GetValueOrDefault("level", 0).AsInt32() >= 5);
		var bodyActive = body.GetValueOrDefault("active", false).AsBool();
		_overviewBodySummary!.Text = $"六部位：{bodyAtFive}/{parts.Count} 个部位达到五炼节点。\n{(bodyActive ? $"进行中：{body.GetValueOrDefault("method_name", "淬体").AsString()}" : "当前没有进行中的淬体。")}\n养形随日常吐纳积累，淬炼法主动推进。";

		var visualization = call("get_visualization_state").AsGodotDictionary();
		var scenes = visualization.GetValueOrDefault("scenes", new Godot.Collections.Array()).AsGodotArray().Select(item => item.AsGodotDictionary()).Where(item => item.GetValueOrDefault("equipped", false).AsBool()).ToList();
		var sceneSummary = scenes.Count == 0
			? "尚未装配三景。"
			: string.Join("\n", scenes.Select(scene => $"{scene.GetValueOrDefault("slot_name", "观景").AsString()}：{scene.GetValueOrDefault("name", "未名").AsString()}　{scene.GetValueOrDefault("rank", 1).AsInt32()}重"));
		_overviewNatureSummary!.Text = $"当前装配：\n{sceneSummary}\n三景进度与装配记录保留；本轮只细化日象·烈日，其余图景效果待定。";
	}

	/// <summary>修性的三景装配与静室观法；合观、显境只展示局外接口，不在此处生成战斗状态。</summary>
	private void build_visualization_ui()
	{
		_visualPanel = new PanelContainer { Name = "VisualizationPanel", MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
		_visualPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_visualPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.90f, .90f, .84f, .995f), ContentMarginLeft = 42, ContentMarginRight = 42, ContentMarginTop = 28, ContentMarginBottom = 28 });
		AddChild(_visualPanel);
		var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; root.AddThemeConstantOverride("separation", 12); _visualPanel.AddChild(root);
		var head = new HBoxContainer(); root.AddChild(head);
		head.AddChild(label("修性·灵台三景", 34, true));
		var overview = new Button { Text = "修行总览" }; style_small_button(overview); overview.Pressed += () => { if (_target is not null) show_cultivation_overview(_target); }; head.AddChild(overview);
		var back = new Button { Text = "返回修炼" }; style_small_button(back); back.Pressed += hide_visualization_page; head.AddChild(back);
		var close = new Button { Text = "收起" }; style_small_button(close); close.Pressed += close_panel; head.AddChild(close);
		_visualName = label("", 22); root.AddChild(_visualName);
		_visualRealm = label("", 16); root.AddChild(_visualRealm);
		_visualSummary = label("", 16); _visualSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart; root.AddChild(_visualSummary);

		var scenes = new PanelContainer(); scenes.AddThemeStyleboxOverride("panel", section_style()); root.AddChild(scenes);
		var sceneBox = new VBoxContainer(); sceneBox.AddThemeConstantOverride("separation", 10); margin(scenes, 18, 18, 16, 16).AddChild(sceneBox);
		sceneBox.AddChild(label("灵台三景", 24, true));
		sceneBox.AddChild(label("天象观变，地文察势，物华应物。每景独立成长；具体属性与战斗效果以完成配置的图景为准。", 14));
		foreach (var slot in VisualizationRules.Slots)
		{
			var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); sceneBox.AddChild(row);
			row.AddChild(label(VisualizationRules.GetSlotName(slot) + "：", 18));
			var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(310, 0) };
			picker.ItemSelected += _ => { if (_target is null) return; _target.Call("set_visualization_equipped", slot, picker.GetItemMetadata(picker.Selected).AsString()); refresh_visualization_ui(); };
			row.AddChild(picker); _visualSlots[slot] = picker;
			var detail = label("", 15, true); detail.Name = "VisualizationDetail_" + slot; detail.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(detail); _visualDetails[slot] = detail;
		}

		var actionPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; actionPanel.AddThemeStyleboxOverride("panel", section_style()); root.AddChild(actionPanel);
		var actionBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; actionBox.AddThemeConstantOverride("separation", 10); margin(actionPanel, 18, 18, 16, 16).AddChild(actionBox);
		actionBox.AddChild(label("温观与观法", 24, true));
		var focusRow = new HBoxContainer(); focusRow.AddChild(label("聚焦景：", 16)); _visualFocus = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _visualFocus.ItemSelected += _ => { if (_target is not null) _target.Call("set_visualization_focus", _visualFocus.GetItemMetadata(_visualFocus.Selected)); refresh_visualization_ui(); }; focusRow.AddChild(_visualFocus); actionBox.AddChild(focusRow);
		var methodRow = new HBoxContainer(); methodRow.AddChild(label("观法：", 16)); _visualMethod = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _visualMethod.ItemSelected += _ => refresh_visualization_method(); methodRow.AddChild(_visualMethod); actionBox.AddChild(methodRow);
		_visualMethodHint = label("", 15); _visualMethodHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _visualMethodHint.CustomMinimumSize = new Vector2(0, 54); actionBox.AddChild(_visualMethodHint);
		_visualPractice = new Button { Text = "行观一日", CustomMinimumSize = new Vector2(0, 46) }; style_action_button(_visualPractice); _visualPractice.Pressed += practice_visualization; actionBox.AddChild(_visualPractice);
		actionBox.AddChild(section_label("最近结算"));
		_visualResult = label("尚无观想结算记录。", 14, true); _visualResult.AutowrapMode = TextServer.AutowrapMode.WordSmart; actionBox.AddChild(_visualResult);
		_visualPanel.Hide();
	}

	/// <summary>
	/// 修命淬体面板独立于吐纳面板显示，避免把“修炼功法”和“淬体法”挤在同一张纵向表单里。
	/// 静室态的右侧负责选择法门、基础材料和启动一次淬体；底部入口复用面板但切换为只读查看态。
	/// </summary>
	private void build_body_ui()
	{
		_bodyPanel = new PanelContainer { Name = "BodyRefinementPanel", MouseFilter = MouseFilterEnum.Stop, ZIndex = 20 };
		_bodyPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_bodyPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(.91f, .88f, .79f, .995f), BorderColor = new Color(.25f, .22f, .16f, .96f),
			BorderWidthLeft = 0, BorderWidthRight = 0, BorderWidthTop = 0, BorderWidthBottom = 0,
			ContentMarginLeft = 38, ContentMarginRight = 38, ContentMarginTop = 26, ContentMarginBottom = 26
		});
		AddChild(_bodyPanel);
		_bodyPartSigilSheet = GD.Load<Texture2D>("res://textures/ui/qingxi/body_part_sigil_sheet_ink_v1.png");
		var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; root.AddThemeConstantOverride("separation", 12); _bodyPanel.AddChild(root);
		var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) }; root.AddChild(head);
		_bodyTitle = label("修命·淬体", 34, true); head.AddChild(_bodyTitle);
		var overview = new Button { Text = "修行总览" }; style_small_button(overview); overview.Pressed += () => { if (_target is not null) show_cultivation_overview(_target); }; head.AddChild(overview);
		_bodyBack = new Button { Text = "返回修炼" }; style_small_button(_bodyBack); _bodyBack.Pressed += hide_body_page; head.AddChild(_bodyBack);
		var close = new Button { Text = "收起" }; style_small_button(close); close.Pressed += close_panel; head.AddChild(close);
		_bodyName = label("", 22); root.AddChild(_bodyName);
		_bodyRealm = label("", 16); root.AddChild(_bodyRealm);

		var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; columns.AddThemeConstantOverride("separation", 18); root.AddChild(columns);
		var left = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; left.AddThemeStyleboxOverride("panel", section_style()); columns.AddChild(left);
		var leftMargin = margin(left, 14, 14, 14, 14); var leftBox = new VBoxContainer(); leftBox.AddThemeConstantOverride("separation", 9); leftMargin.AddChild(leftBox);
		leftBox.AddChild(label("六部位成长轴", 24, true));
		leftBox.AddChild(label("五炼、十炼、十五炼解锁节点，悬停查看奖励。", 15));
		var axes = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 515) }; axes.AddThemeConstantOverride("separation", 7); leftBox.AddChild(axes);
		foreach (var part in BodyRefinementRules.Parts) axes.AddChild(build_body_part_card(part));
		_bodyNourishHint = label("", 14); _bodyNourishHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _bodyNourishHint.CustomMinimumSize = new Vector2(0, 38); _bodyNourishHint.Name = "BodyNourishHint"; leftBox.AddChild(_bodyNourishHint);

		var right = new PanelContainer { CustomMinimumSize = new Vector2(520, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; right.AddThemeStyleboxOverride("panel", section_style()); columns.AddChild(right);
		var rightMargin = margin(right, 22, 22, 18, 18); var rightBox = new VBoxContainer(); rightBox.AddThemeConstantOverride("separation", 11); rightMargin.AddChild(rightBox);
		rightBox.AddChild(label("法门与淬体", 24, true));
		var methodRow = new HBoxContainer(); methodRow.AddChild(label("图录：", 16)); _bodyMethod = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _bodyMethod.ItemSelected += _ => { refresh_body_materials(); refresh_body_detail(); }; methodRow.AddChild(_bodyMethod); rightBox.AddChild(methodRow);
		_bodyMethodHint = label("", 16); _bodyMethodHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; _bodyMethodHint.CustomMinimumSize = new Vector2(0, 58); rightBox.AddChild(_bodyMethodHint);
		_bodyRequirement = label("", 15); _bodyRequirement.AutowrapMode = TextServer.AutowrapMode.WordSmart; rightBox.AddChild(_bodyRequirement);
		var materialRow = new HBoxContainer(); _bodyMaterialRow = materialRow; materialRow.AddChild(label("基础材料：", 16)); _bodyMaterial = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _bodyMaterial.ItemSelected += _ => refresh_body_detail(); materialRow.AddChild(_bodyMaterial); rightBox.AddChild(materialRow);
		_bodyPreview = label("", 15); _bodyPreview.AutowrapMode = TextServer.AutowrapMode.WordSmart; _bodyPreview.CustomMinimumSize = new Vector2(0, 50); rightBox.AddChild(_bodyPreview);
		rightBox.AddChild(section_label("当前状态"));
		_bodyActive = label("", 15); _bodyActive.AutowrapMode = TextServer.AutowrapMode.WordSmart; _bodyActive.CustomMinimumSize = new Vector2(0, 34); rightBox.AddChild(_bodyActive);
		rightBox.AddChild(section_label("最近结算"));
		_bodyResult = label("", 14); _bodyResult.AutowrapMode = TextServer.AutowrapMode.WordSmart; _bodyResult.CustomMinimumSize = new Vector2(0, 82); rightBox.AddChild(_bodyResult);
		_bodyStart = new Button { Text = "开始淬体", CustomMinimumSize = new Vector2(0, 50) }; style_action_button(_bodyStart); _bodyStart.Pressed += start_body_refinement; rightBox.AddChild(_bodyStart);
		_bodyReadOnlyHint = label("查看态：实际淬体请前往静室。", 13); _bodyReadOnlyHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; rightBox.AddChild(_bodyReadOnlyHint);
		_bodyReadOnlyHint.Hide();
		_bodyPanel.Hide();
	}

	private PanelContainer build_body_part_card(string part)
	{
		var card = new PanelContainer { CustomMinimumSize = new Vector2(0, 80) }; card.AddThemeStyleboxOverride("panel", QuietRoomStyle.Edge(false, 0));
		var content = margin(card, 10, 10, 5, 5); var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); content.AddChild(row);
		var icon = new TextureRect { Texture = body_part_sigil(part), CustomMinimumSize = new Vector2(42, 42), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddChild(icon);
		var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 3); row.AddChild(box);
		var head = new HBoxContainer(); box.AddChild(head); head.AddChild(label(BodyRefinementRules.GetName(part), 18, true)); var progressLabel = label("未炼", 14); progressLabel.HorizontalAlignment = HorizontalAlignment.Right; head.AddChild(progressLabel); _bodyProgressLabels[part] = progressLabel;
		var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) }; box.AddChild(bar); _bodyProgressBars[part] = bar;
		var nodes = new HBoxContainer(); nodes.AddThemeConstantOverride("separation", 4); box.AddChild(nodes);
		var nodeLabels = new Label[3]; var nodeLevels = new[] { 5, 10, 15 };
		for (var i = 0; i < nodeLevels.Length; i++)
		{
			var nodeLabel = label($"{nodeLevels[i]}炼", 14); nodeLabel.TooltipText = format_body_node(part, nodeLevels[i]); nodeLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill; nodeLabel.CustomMinimumSize = new Vector2(0, 22); nodeLabel.VerticalAlignment = VerticalAlignment.Center; nodes.AddChild(nodeLabel); nodeLabels[i] = nodeLabel;
		}
		_bodyNodeLabels[part] = nodeLabels;
		var reward = label("当前节点：尚无", 12); reward.AutowrapMode = TextServer.AutowrapMode.WordSmart; reward.Modulate = new Color(.35f, .28f, .20f, .82f); _bodyRewardLabels[part] = reward;
		return card;
	}

	private Texture2D? body_part_sigil(string part)
	{
		if (_bodyPartSigilSheet is null) return null;
		var index = System.Array.IndexOf(BodyRefinementRules.Parts, part);
		if (index < 0) return null;
		return new AtlasTexture { Atlas = _bodyPartSigilSheet, Region = new Rect2((index % 2) * 512, (index / 2) * 512, 512, 512) };
	}

	private static string format_body_node(string part, int level) => $"{level}炼　{BodyRefinementRules.GetRewardText(part, level)}";

	private static MarginContainer margin(Control parent, int left, int right, int top, int bottom)
	{
		var result = new MarginContainer(); result.AddThemeConstantOverride("margin_left", left); result.AddThemeConstantOverride("margin_right", right); result.AddThemeConstantOverride("margin_top", top); result.AddThemeConstantOverride("margin_bottom", bottom); parent.AddChild(result); return result;
	}

	private void show_body_page()
	{
		_bodyReadOnly = false; _roomActions = true; select_room_page(3); Show(); MoveToFront();
	}
	private void hide_body_page() { if (_bodyReadOnly) close_panel(); else select_room_page(0); }

	/// <summary>从主界面修命入口打开只读的淬炼总览；不提供材料槽和行动按钮。</summary>
	public void show_body_overview(GodotObject entity)
	{
		if (!GodotObject.IsInstanceValid(entity) || _bodyPanel is null) return;
		_target = entity; _bodyReadOnly = true; _roomActions = false; _roomPage = 3;
		select_room_page(3); Show(); MoveToFront();
	}

	/// <summary>从主界面修性入口打开三景图。</summary>
	private void show_visualization_page() { select_room_page(4); Show(); MoveToFront(); }
	private void hide_visualization_page() => select_room_page(0);

	/// <summary>供主界面修性入口调用；三景观想对所有云息修士开放。</summary>
	public void show_visualization_overview(GodotObject entity)
	{
		if (!GodotObject.IsInstanceValid(entity) || _visualPanel is null) return;
		_target = entity; _roomActions = false; _roomPage = 4; select_room_page(4); Show(); MoveToFront();
	}

	/// <summary>统一关闭修炼面板，供主场景返回键处理。</summary>
	public void close_panel()
	{
		_room.Hide();
		_yuanxiPanel?.Hide();
		_bodyReadOnly = false;
		_overviewPanel?.Hide(); _visualPanel?.Hide();  _bodyPanel?.Hide(); _panel?.Hide(); Hide();
	}

	private void refresh_visualization_ui()
	{
		if (_visualPanel is null || _target is null) return;
		_visualName!.Text = value("entity_name", "-").AsString();
		_visualRealm!.Text = $"{value("cultivation_realm", "凡人").AsString()}　·　修性面向所有修士开放";
		var state = call("get_visualization_state").AsGodotDictionary();
		var available = state.GetValueOrDefault("available", false).AsBool();
		var scenes = state.GetValueOrDefault("scenes", new Godot.Collections.Array()).AsGodotArray().Select(raw => raw.AsGodotDictionary()).ToList();
		_visualSummary!.Text = available
			? $"当前合观：{state.GetValueOrDefault("combination_name", "尚未合观").AsString()}。观法可推进聚焦景；图景属性与战斗效果以正式配置为准。{(state.GetValueOrDefault("manifest_ready", false).AsBool() ? "三景已具显境资格，显境效果待后续定义。" : "显境需当前三景皆达五十重。")}" 
			: "修性在云息期开放观法；闭关突破期间暂停。不要求装配主修秘籍。";
		foreach (var slot in VisualizationRules.Slots)
		{
			if (!_visualSlots.TryGetValue(slot, out var picker)) continue;
			var selectedId = state.GetValueOrDefault("equipped", new GDict()).AsGodotDictionary().GetValueOrDefault(slot, "").AsString();
			picker.Clear(); var selected = 0; var index = 0;
			foreach (var scene in scenes.Where(item => item.GetValueOrDefault("slot", "").AsString() == slot))
			{
				var id = scene.GetValueOrDefault("id", "").AsString();
				picker.AddItem(scene.GetValueOrDefault("name", id).AsString()); picker.SetItemMetadata(index, id);
				if (id == selectedId) selected = index;
				index++;
			}
			picker.Disabled = !available || picker.ItemCount == 0; if (picker.ItemCount > 0) picker.Select(selected);
			_visualDetails.TryGetValue(slot, out var detail);
			var current = scenes.FirstOrDefault(item => item.GetValueOrDefault("id", "").AsString() == selectedId);
			if (detail is null || current is null || current.Count == 0) continue;
			var rank = current.GetValueOrDefault("rank", 1).AsInt32(); var ratio = current.GetValueOrDefault("ratio", 0f).AsSingle();
			var nodes = string.Concat(Enumerable.Range(1, 10).Select(node => rank >= node * 5 ? "●" : "○"));
			detail.Text = $"{rank}重　{ratio:P0}　{nodes}\n图景属性与战斗效果：待按新方案配置";
		}
		var previousFocus = state.GetValueOrDefault("focus_id", "").AsString();
		_visualFocus!.Clear(); var focusSelected = 0; var focusIndex = 0;
		foreach (var scene in scenes.Where(item => item.GetValueOrDefault("equipped", false).AsBool()))
		{
			var id = scene.GetValueOrDefault("id", "").AsString(); _visualFocus.AddItem(scene.GetValueOrDefault("name", id).AsString()); _visualFocus.SetItemMetadata(focusIndex, id);
			if (id == previousFocus) focusSelected = focusIndex; focusIndex++;
		}
		_visualFocus.Disabled = !available || _visualFocus.ItemCount == 0; if (_visualFocus.ItemCount > 0) _visualFocus.Select(focusSelected);
		refresh_visualization_methods();
		var last = state.GetValueOrDefault("last_result", new GDict()).AsGodotDictionary();
		_visualResult!.Text = last.Count == 0 ? "尚无观想结算记录。" : $"上次：{last.GetValueOrDefault("scene_name", "观景").AsString()}，以{last.GetValueOrDefault("method_name", "观法").AsString()}温观 {format_duration_hours(last.GetValueOrDefault("duration_hours", 0).AsInt32())}，{last.GetValueOrDefault("before_rank", 1).AsInt32()}重 → {last.GetValueOrDefault("after_rank", 1).AsInt32()}重。";
	}

	private void refresh_visualization_methods()
	{
		if (_visualMethod is null || _visualFocus is null || _target is null) return;
		var focusId = _visualFocus.Selected >= 0 ? _visualFocus.GetItemMetadata(_visualFocus.Selected).AsString() : "";
		var previous = _visualMethod.Selected >= 0 ? _visualMethod.GetItemMetadata(_visualMethod.Selected).AsString() : "";
		var methods = call("get_visualization_methods", focusId).AsGodotDictionary().GetValueOrDefault("methods", new Godot.Collections.Array()).AsGodotArray();
		_visualMethod.Clear(); var selected = 0;
		for (var i = 0; i < methods.Count; i++)
		{
			var method = methods[i].AsGodotDictionary(); _visualMethod.AddItem(method.GetValueOrDefault("name", "观法").AsString()); _visualMethod.SetItemMetadata(i, method.GetValueOrDefault("id", "").AsString());
			if (_visualMethod.GetItemMetadata(i).AsString() == previous) selected = i;
		}
		if (_visualMethod.ItemCount == 0) { _visualMethod.AddItem("当前无适用观法"); _visualMethod.SetItemMetadata(0, ""); }
		_visualMethod.Select(selected); _visualMethod.Disabled = !call("can_use_visualization").AsBool() || methods.Count == 0;
		refresh_visualization_method();
	}

	private void refresh_visualization_method()
	{
		if (_visualMethodHint is null || _visualPractice is null || _visualMethod is null) return;
		var methodId = _visualMethod.Selected >= 0 ? _visualMethod.GetItemMetadata(_visualMethod.Selected).AsString() : "";
		var method = VisualizationCatalog.GetMethod(methodId);
		if (method is null) { _visualMethodHint.Text = "当前重数没有适用观法；探索、事件与后段观法会在后续内容中补入。"; _visualPractice.Disabled = true; return; }
		var focusId = _visualFocus?.Selected >= 0 ? _visualFocus.GetItemMetadata(_visualFocus.Selected).AsString() : "";
		var visualState = call("get_visualization_state").AsGodotDictionary();
		GDict? focusScene = visualState.GetValueOrDefault("scenes", new Godot.Collections.Array()).AsGodotArray().Select(raw => raw.AsGodotDictionary()).FirstOrDefault(scene => scene.GetValueOrDefault("id", "").AsString() == focusId);
		var multiplier = VisualizationRules.GetPracticeMultiplier(VisualizationRules.GetRank(focusScene?.GetValueOrDefault("progress", 1f).AsSingle() ?? 1f));
		_visualMethodHint.Text = $"{method.Description}\n适用：{method.MinRank}～{method.MaxRank}重；条件：{method.ConditionText}；耗时 {format_duration_hours(method.DurationHours)}，预计推进 {method.RankYield * multiplier:0.##} 重（五重节点效率 ×{multiplier:0.##}）。";
		_visualPractice.Text = $"行观 {format_duration_hours(method.DurationHours)}";
		_visualPractice.Disabled = !call("can_use_visualization").AsBool();
	}

	private void practice_visualization()
	{
		if (_target is null || _visualMethod is null || _visualMethod.Selected < 0) return;
		var methodId = _visualMethod.GetItemMetadata(_visualMethod.Selected).AsString(); var method = VisualizationCatalog.GetMethod(methodId);
		if (method is null) return;
		play_transition("静室观想", $"{method.Name}将耗时 {format_duration_hours(method.DurationHours)}，使所观之景更深一重。", "观想结束，所得已记入灵台。", () => { _target.Call("practice_visualization", methodId); refresh(); refresh_visualization_ui(); });
	}

	private void apply_body_view_mode()
	{
		if (_bodyReadOnly)
		{
			if (_bodyTitle is not null) _bodyTitle.Text = "修命·淬体总览 · 查看态";
			if (_bodyBack is not null) _bodyBack.Text = "返回角色";
			_bodyMaterialRow?.Hide();
			_bodyStart?.Hide();
			_bodyReadOnlyHint?.Show();
		}
		else
		{
			if (_bodyTitle is not null) _bodyTitle.Text = "静室·修命淬体 · 行动态";
			if (_bodyBack is not null) _bodyBack.Text = "返回修炼";
			_bodyMaterialRow?.Show();
			_bodyStart?.Show();
			_bodyReadOnlyHint?.Hide();
		}
	}

	private string selected_body_method_path()
	{
		if (_bodyMethod is null || _bodyMethod.Selected < 0 || _bodyMethod.Selected >= _bodyMethods.Count) return "";
		return _bodyMethods[_bodyMethod.Selected].ResourcePath;
	}

	private string selected_body_material_path() =>
		_bodyMaterial is null || _bodyMaterial.Selected < 0 ? "" : _bodyMaterial.GetItemMetadata(_bodyMaterial.Selected).AsString();

	private void refresh_body_ui()
	{
		if (_bodyPanel is null || _target is null) return;
		_bodyName!.Text = value("entity_name", "-").AsString();
		_bodyRealm!.Text = $"{value("cultivation_realm", "凡人").AsString()}　{value("spirit_root_name", "").AsString()}";
		var state = call("get_body_refinement_state").AsGodotDictionary();
		foreach (var entry in state.GetValueOrDefault("parts", new Godot.Collections.Array()).AsGodotArray())
		{
			var part = entry.AsGodotDictionary(); var id = part.GetValueOrDefault("id", "").AsString();
			if (!_bodyProgressBars.TryGetValue(id, out var bar)) continue;
			var level = part.GetValueOrDefault("level", 0).AsInt32(); var progress = part.GetValueOrDefault("progress", 0f).AsSingle(); var next = part.GetValueOrDefault("next_threshold", 0).AsInt32();
			bar.Value = part.GetValueOrDefault("ratio", 0f).AsSingle();
			var remaining = level >= 15 ? 0f : Mathf.Max(0f, next - progress);
			_bodyProgressLabels[id].Text = level >= 15 ? $"{level}炼　炼度 {progress:0.0}　已达当前上限" : level <= 0 ? $"未炼　炼度 {progress:0.0}/{next}　还需 {remaining:0.0}" : $"{level}炼　炼度 {progress:0.0}/{next}　还需 {remaining:0.0}";
			_bodyRewardLabels[id].Text = $"当前节点：{part.GetValueOrDefault("reward", "尚无").AsString()}";
			if (_bodyNodeLabels.TryGetValue(id, out var nodes))
			{
				var nodeLevels = new[] { 5, 10, 15 };
				for (var i = 0; i < nodes.Length; i++) nodes[i].AddThemeColorOverride("font_color", level >= nodeLevels[i] ? QuietRoomStyle.Teal : WorldHudStyle.SecondaryInk);
			}
		}
		var active = state.GetValueOrDefault("active", false).AsBool();
		_bodyActive!.Text = active ? $"当前淬体：{state.GetValueOrDefault("method_name", "淬体").AsString()}　余 {format_duration_hours(state.GetValueOrDefault("remaining_hours", 0).AsInt32())}" : "当前没有进行中的淬体。";
		var last = state.GetValueOrDefault("last_result", new GDict()).AsGodotDictionary();
		_bodyResult!.Text = last.Count == 0 ? "尚无淬体结算记录。" : format_body_result(last);
		if (_bodyNourishHint is not null)
		{
			var gongfa = value("main_gongfa").AsGodotObject() as GongfaResource;
			_bodyNourishHint.Text = gongfa is null ? "养形谱：尚未装配主修功法。" : $"养形谱：《{gongfa.gongfa_name}》；日常吐纳提供约 {gongfa.body_nourish_rate_per_day:0.##} 点养形炼度。\n分配：{format_body_nourish_weights(gongfa)}";
		}
		refresh_body_methods();
		_bodyMethod!.Disabled = !_bodyReadOnly && active;
		_bodyMaterial!.Disabled = _bodyReadOnly || active;
		refresh_body_detail();
		apply_body_view_mode();
	}

	private void refresh_body_methods()
	{
		if (_bodyMethod is null || _target is not Character human) return;
		var selectedPath = selected_body_method_path();
		_bodyMethods.Clear(); foreach (var method in human.get_known_body_refinement_methods()) if (!_bodyMethods.Any(item => item.ResourcePath == method.ResourcePath)) _bodyMethods.Add(method);
		_bodyMethod.Clear();
		if (_bodyMethods.Count == 0) { _bodyMethod.AddItem("暂无已掌握淬体法"); _bodyMethod.Disabled = true; return; }
		_bodyMethod.Disabled = false; var selected = 0;
		for (var i = 0; i < _bodyMethods.Count; i++) { _bodyMethod.AddItem(_bodyMethods[i].method_name); _bodyMethod.SetItemMetadata(i, _bodyMethods[i].ResourcePath); if (_bodyMethods[i].ResourcePath == selectedPath) selected = i; }
		_bodyMethod.Select(selected);
		refresh_body_materials();
	}

	private void refresh_body_materials()
	{
		if (_bodyMaterial is null || _target is null) return;
		if (_bodyReadOnly)
		{
			_bodyMaterialRow?.Hide();
			return;
		}
		var selectedPath = selected_body_material_path(); var methodPath = selected_body_method_path();
		_bodyMaterial.Clear();
		if (string.IsNullOrEmpty(methodPath)) { _bodyMaterial.AddItem("无可用淬体法"); _bodyMaterial.SetItemMetadata(0, ""); _bodyMaterial.Disabled = true; return; }
		var options = call("get_body_refinement_material_options", methodPath).AsGodotDictionary().GetValueOrDefault("options", new Godot.Collections.Array()).AsGodotArray();
		if (options.Count == 0) { _bodyMaterial.AddItem("不使用基础材料（效率 25%）"); _bodyMaterial.SetItemMetadata(0, ""); }
		else foreach (var raw in options) { var option = raw.AsGodotDictionary(); var path = option.GetValueOrDefault("item_path", "").AsString(); var name = option.GetValueOrDefault("item_name", "材料").AsString(); var count = option.GetValueOrDefault("count", 0).AsInt32(); _bodyMaterial.AddItem(path.Length == 0 ? name : $"{name} × {count}"); _bodyMaterial.SetItemMetadata(_bodyMaterial.ItemCount - 1, path); }
		var index = 0; for (var i = 0; i < _bodyMaterial.ItemCount; i++) if (_bodyMaterial.GetItemMetadata(i).AsString() == selectedPath) index = i;
		_bodyMaterial.Select(index); _bodyMaterial.Disabled = false;
	}

	private void refresh_body_detail()
	{
		if (_bodyMethodHint is null || _bodyRequirement is null || _bodyPreview is null || _bodyStart is null) return;
		var methodPath = selected_body_method_path();
		if (string.IsNullOrEmpty(methodPath)) { _bodyMethodHint.Text = "尚未选择淬体法。"; _bodyRequirement.Text = ""; _bodyPreview.Text = ""; _bodyStart.Disabled = true; return; }
		var preview = call("get_body_refinement_preview", methodPath, _bodyReadOnly ? "" : selected_body_material_path()).AsGodotDictionary();
		if (!preview.GetValueOrDefault("valid", false).AsBool()) { _bodyMethodHint.Text = _bodyMethods.FirstOrDefault(method => method.ResourcePath == methodPath)?.description ?? ""; _bodyRequirement.Text = preview.GetValueOrDefault("reason", "当前配置不可用。").AsString(); _bodyPreview.Text = ""; _bodyStart.Disabled = true; return; }
		_bodyMethodHint.Text = preview.GetValueOrDefault("description", "").AsString() + $"\n分配：{preview.GetValueOrDefault("allocation", "").AsString()}\n有效范围：{preview.GetValueOrDefault("effective_range", "").AsString()}";
		_bodyRequirement.Text = $"要求：{preview.GetValueOrDefault("requirement", "").AsString()}";
		var minHours = preview.GetValueOrDefault("duration_min_hours", 0).AsInt32(); var maxHours = preview.GetValueOrDefault("duration_max_hours", minHours).AsInt32();
		var minYield = preview.GetValueOrDefault("base_min", 0).AsInt32(); var maxYield = preview.GetValueOrDefault("base_max", minYield).AsInt32(); var efficiency = preview.GetValueOrDefault("efficiency", .25f).AsSingle();
		_bodyPreview.Text = _bodyReadOnly
			? $"时长：{format_duration_range(minHours, maxHours)}　·　基础炼度：{minYield}～{maxYield}\n查看态不预览材料与本次效率。"
			: $"时长：{format_duration_range(minHours, maxHours)}　·　基础炼度：{minYield}～{maxYield}\n本次基础效率：{efficiency:P0}　·　材料：{preview.GetValueOrDefault("material_name", "无").AsString()}";
		_bodyStart.Disabled = _bodyReadOnly || call("get_body_refinement_state").AsGodotDictionary().GetValueOrDefault("active", false).AsBool();
	}

	private void start_body_refinement()
	{
		if (_target is null || _bodyReadOnly) return;
		var methodPath = selected_body_method_path(); var materialPath = selected_body_material_path();
		var preview = call("get_body_refinement_preview", methodPath, materialPath).AsGodotDictionary();
		if (!preview.GetValueOrDefault("valid", false).AsBool()) { _bodyResult!.Text = preview.GetValueOrDefault("reason", "当前不能淬体。").AsString(); return; }
		var minHours = preview.GetValueOrDefault("duration_min_hours", 0).AsInt32(); var maxHours = preview.GetValueOrDefault("duration_max_hours", minHours).AsInt32();
		play_transition("淬体", $"{preview.GetValueOrDefault("method_name", "淬体").AsString()} 将持续 {format_duration_range(minHours, maxHours)}，炼度会按法门设定随机分配。", "淬体结束，炼度已经记入六部位。", () => { var result = call("start_body_refinement", methodPath, materialPath).AsGodotDictionary(); _bodyResult!.Text = result.GetValueOrDefault("success", false).AsBool() ? format_body_result(call("get_last_body_refinement_result").AsGodotDictionary()) : result.GetValueOrDefault("reason", "淬体未能开始。").AsString(); refresh(); refresh_body_ui(); });
	}

	private static string format_body_result(GDict result)
	{
		var lines = new List<string> { $"上次结算：有效炼度 {result.GetValueOrDefault("effective_yield", 0f).AsSingle():0.0}（基础 {result.GetValueOrDefault("base_yield", 0f).AsSingle():0.0}，效率 {result.GetValueOrDefault("efficiency", 0f).AsSingle():P0}）" };
		if (result.GetValueOrDefault("allocation", new GDict()).AsGodotDictionary() is { } allocation && allocation.Count > 0)
		{
			var values = new List<string>(); foreach (Variant key in allocation.Keys) values.Add($"{BodyRefinementRules.GetName(key.AsString())} +{allocation[key].AsSingle():0.0}"); lines.Add("部位分配：" + string.Join("、", values));
		}
		var ups = result.GetValueOrDefault("level_ups", new Godot.Collections.Array()).AsGodotArray(); if (ups.Count > 0) lines.Add("晋级：" + string.Join("、", ups.Select(item => { var entry = item.AsGodotDictionary(); return $"{entry.GetValueOrDefault("part", "").AsString()}{entry.GetValueOrDefault("to", 0).AsInt32()}炼"; })));
		return string.Join("\n", lines);
	}

	private static string format_body_nourish_weights(GongfaResource gongfa)
	{
		var sum = 0f;
		foreach (var part in BodyRefinementRules.Parts) sum += Mathf.Max(0f, gongfa.body_nourish_weights.GetValueOrDefault(part, 0f).AsSingle());
		if (sum <= 0f) return "六部位均分（当前秘籍未另设偏重）";
		var parts = new List<string>();
		foreach (var part in BodyRefinementRules.Parts)
		{
			var weight = Mathf.Max(0f, gongfa.body_nourish_weights.GetValueOrDefault(part, 0f).AsSingle());
			if (weight > 0f) parts.Add($"{BodyRefinementRules.GetName(part)} {weight / sum:P0}");
		}
		return string.Join("、", parts);
	}

	private Label label(string text, int size, bool expand = false)
	{
		var result = new Label { Text = text };
		result.AddThemeFontSizeOverride("font_size", size);
		result.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
		if (expand) result.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return result;
	}

	private static Label section_label(string text)
	{
		var result = new Label { Text = text };
		result.AddThemeFontSizeOverride("font_size", 20); result.AddThemeColorOverride("font_color", QuietRoomStyle.Ink);
		return result;
	}

	private static StyleBoxFlat section_style() => QuietRoomStyle.Edge(false, 0);
	private static void style_small_button(Button button) { button.AddThemeFontSizeOverride("font_size", 18); }
	private static void style_action_button(Button button) => QuietRoomStyle.Primary(button);

	private static SpinBox spin(int min, int max, int value) => new() { MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true, CustomMinimumSize = new Vector2(100, 0) };
	private Variant value(string key, Variant fallback = default) => GodotObject.IsInstanceValid(_target) ? _target!.Get(key) : fallback;
	private Variant call(string method, params Variant[] args) => GodotObject.IsInstanceValid(_target) && _target!.HasMethod(method) ? _target.Call(method, args) : default;

	private void set_date_defaults()
	{
		var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
		if (time is null || _year is null || _month is null || _date is null) return;
		_year.Value = time.year; _month.Value = time.month; _date.Value = Math.Min(30, time.day + 1);
		if (time.day == 30) { _date.Value = 1; _month.Value = time.month == 12 ? 1 : time.month + 1; if (time.month == 12) _year.Value = time.year + 1; }
	}

	private void update_mode_visibility()
	{
		var selected = _mode?.Selected ?? 0;
		if (_strandRow is not null) _strandRow.Visible = selected == 0;
		if (_dayRow is not null) _dayRow.Visible = selected == 1;
		if (_dateRow is not null) _dateRow.Visible = selected == 2;
	}

	private int selected_hours()
	{
		var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
		if (time is null) return 0;
		return (_mode?.Selected ?? 0) switch
		{
			1 => Mathf.Max(1, (int)_days!.Value * 24),
			2 => Math.Max(1, total_hours((int)_year!.Value, (int)_month!.Value, (int)_date!.Value, time.hour) - time.get_total_game_hours()),
			_ => 0
		};
	}

	private static int total_hours(int year, int month, int day, int hour) => (((year - 1) * 12 + (month - 1)) * 30 + (day - 1)) * 24 + hour;

	private static string format_date_after(GameTimeMgr time, int hours)
	{
		var total = time.get_total_game_hours() + Math.Max(0, hours);
		var dayIndex = total / 24; var hour = total % 24;
		var year = dayIndex / 360 + 1; var remainder = dayIndex % 360;
		return $"{year}年{remainder / 30 + 1}月{remainder % 30 + 1}日 {hour:00}时";
	}

	private static string format_duration_hours(int hours) => GameTimeText.Duration(hours);

	private static string format_duration_range(int minHours, int maxHours) => $"{format_duration_hours(minHours)}～{format_duration_hours(maxHours)}";

	private void refresh()
	{
		if (_target is null || _strands is null) return;
		_name!.Text = value("entity_name", "-").AsString();
		_realm!.Text = $"{value("cultivation_realm", "凡人").AsString()}　{value("spirit_root_name", "").AsString()}";
		var progress = call("get_next_realm_progress").AsGodotDictionary();
		var required = progress.GetValueOrDefault("required", 0).AsInt32();
		var current = progress.GetValueOrDefault("current", 0).AsInt32();
		_power!.Text = required > 0 ? $"{(progress.GetValueOrDefault("is_current_layer", false).AsBool() ? "本层 " : "累计 ")}{current} / {required} 缕" : $"{value("spirit_strands", 0).AsInt32()} 缕";
		var cultivationRate = _target is Character practitioner
			? practitioner.get_cultivation_rate_after_bottleneck()
			: call("get_cultivation_rate").AsSingle();
		_rate!.Text = $"实际修炼速率  {cultivationRate:0.####} 缕 / 小时";
		refresh_bottleneck_status();
		_strands.MaxValue = Math.Max(1, call("get_strands_until_next_realm").AsInt32());
		if (_strands.Value > _strands.MaxValue) _strands.Value = _strands.MaxValue;
		update_mode_visibility();
		refresh_study_choices();
		refresh_plan();
		update_study();
		update_breakthrough();
		if (_overviewPanel?.Visible == true) refresh_cultivation_overview();
		if (_bodyPanel?.Visible == true) refresh_body_ui();
		refresh_room_header(); refresh_room_study();
		_roomPowerBar.MaxValue = Math.Max(1, required); _roomPowerBar.Value = current;
	}

	private void refresh_plan()
	{
		if (_target is null || _plan is null || _cultivate is null) return;
		if (value("main_gongfa").AsGodotObject() is not GongfaResource)
		{
			_plan.Text = "尚未装配主修功法。请先前往角色页面，将已研读的功法设为主修后再吐纳修行。";
			_cultivate.Disabled = true;
			return;
		}
		var mode = _mode?.Selected ?? 0;
		var preview = mode switch
		{
			0 => call("get_world_cultivation_preview_for_strands", (int)_strands!.Value).AsGodotDictionary(),
			3 => call("get_world_cultivation_preview_for_strands", call("get_strands_until_next_realm").AsInt32()).AsGodotDictionary(),
			_ => call("get_world_cultivation_preview_for_hours", selected_hours()).AsGodotDictionary()
		};
		var hours = preview.GetValueOrDefault("hours", 0).AsInt32();
		var strands = preview.GetValueOrDefault("strands", 0).AsInt32();
		var staminaCost = preview.GetValueOrDefault("stamina_cost", 0f).AsSingle();
		var staminaLimited = preview.GetValueOrDefault("stamina_limited", false).AsBool();
		var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
		_plan.Text = time is null ? "" : $"预计灵力　{strands} 缕\n耗时　{format_duration_hours(hours)}\n精力消耗　{staminaCost:0.#} 点\n结束　{format_date_after(time, hours)}\n\n未满一缕的灵力在结束时散去。{(staminaLimited ? "\n精力不足，已按可持续时长预估。" : "")}";
		if (mode is 1 or 2 && strands == 0) _plan.Text += "\n本次时长不足以获得一整缕，可增加修炼天数或改用指定灵力。";
		_cultivate.Disabled = hours <= 0 || (_target is Character practitioner && !practitioner.can_cultivate());
	}

	private void cultivate()
	{
		if (_target is null) return;
		var mode = _mode?.Selected ?? 0;
		var wanted = mode == 0 ? (int)_strands!.Value : mode == 3 ? call("get_strands_until_next_realm").AsInt32() : 0;
		var preview = mode is 0 or 3 ? call("get_world_cultivation_preview_for_strands", wanted).AsGodotDictionary() : call("get_world_cultivation_preview_for_hours", selected_hours()).AsGodotDictionary();
		var hours = preview.GetValueOrDefault("hours", 0).AsInt32(); var strands = preview.GetValueOrDefault("strands", 0).AsInt32();
		var staminaCost = preview.GetValueOrDefault("stamina_cost", 0f).AsSingle();
		var requestedHours = selected_hours();
		play_transition("吐纳修行", $"灵力沿周天缓缓运转，预计耗时 {format_duration_hours(hours)}，消耗精力 {staminaCost:0.#} 点。", $"修炼结束，实得 {strands} 缕灵力。未满一缕的余数已散去。", () => { if (mode is 0 or 3) _target.Call("cultivate_with_world_stamina_for_strands", wanted); else _target.Call("cultivate_with_world_stamina_for_hours", requestedHours); refresh(); }, hours);
	}

	private void update_study()
	{
		if (_target is Character student && _study is not null && _studyHint is not null)
		{
			var book = selected_study_book();
			var chapter = book?.get_sorted_chapters().FirstOrDefault(c => !student.is_gongfa_chapter_learned(book, c.get_id()));
			if (book is null) { _studyHint.Text = "暂无可研读功法"; _study.Text = "开始研读"; _study.Disabled = true; return; }
			if (chapter is null && (book.chapters.Count > 0 || student.has_learned_gongfa(book)))
			{ _studyHint.Text = "全卷已研读"; _study.Text = "已读完"; _study.Disabled = true; return; }
			// Match SecretBook's existing first-volume access; later volumes use chapter requirements.
			var eligible = !student.has_learned_gongfa(book) || chapter is null || student.can_learn_gongfa_chapter(book, chapter);
			var hours = student.get_available_study_hours(24);
			var progress = chapter is null ? 0 : student.get_gongfa_chapter_study_progress(book, chapter.get_id());
			var required = chapter?.required_study_progress ?? book.required_study_progress;
			_studyHint.Text = $"{chapter?.chapter_name ?? "当前卷"}\n研读进度  {progress:0.0} / {required:0.0}" +
				(!eligible ? "\n尚未满足本卷研读条件" : $"\n耗时  {format_duration_hours(hours)}　精力  {hours * WorldStaminaRules.StudyCostPerGameHour:0.#}");
			_study.Text = !eligible ? "条件未满足" : hours <= 0 ? "精力不足" : "开始研读";
			_study.Disabled = !eligible || hours <= 0;
			return;
		}
	}

	private void refresh_study_choices()
	{
		if (_studyBook is null || _target is not Character human) return;
		var selectedPath = selected_study_book()?.ResourcePath ?? human.get_current_study_gongfa()?.ResourcePath ?? "";
		_studyCandidates.Clear();
		foreach (var book in human.get_known_gongfas()) if (!_studyCandidates.Any(item => item.ResourcePath == book.ResourcePath)) _studyCandidates.Add(book);
		if (human.get_pending_intro_gongfa() is { } pending && !_studyCandidates.Any(item => item.ResourcePath == pending.ResourcePath)) _studyCandidates.Add(pending);
		_studyBook.Clear();
		if (_studyCandidates.Count == 0) { _studyBook.AddItem("暂无可研读功法"); _studyBook.Disabled = true; return; }
		_studyBook.Disabled = false;
		var selected = 0;
		for (var i = 0; i < _studyCandidates.Count; i++) { _studyBook.AddItem($"《{_studyCandidates[i].gongfa_name}》"); if (_studyCandidates[i].ResourcePath == selectedPath) selected = i; }
		_studyBook.Select(selected);
	}

	private void select_study_book(long index)
	{
		if (_target is not Character human || index < 0 || index >= _studyCandidates.Count) return;
		_studyBook!.Select((int)index);
		update_study(); refresh_room_study();
	}
	private GongfaResource? selected_study_book() => _studyBook is not null && _studyBook.Selected >= 0 && _studyBook.Selected < _studyCandidates.Count ? _studyCandidates[_studyBook.Selected] : null;

	private void study(){if(_target is not Character reader || selected_study_book() is not { } book)return;var hours=reader.get_available_study_hours(24);if(hours<=0 || !reader.select_gongfa_for_study(book)){refresh();return;}var staminaCost=hours*WorldStaminaRules.StudyCostPerGameHour;play_transition("研读功法",$"研读 {format_duration_hours(hours)}，消耗精力 {staminaCost:0.#} 点。","研读结束。",()=>{reader.study_for_world_hours(hours);refresh();});}

    private void update_breakthrough()
	{
		if (_breakthrough is null || _breakthroughHint is null || _target is null) return;
		var realms = GetNodeOrNull<RealmManager>("/root/RealmManager");
		if (realms is null) { _breakthrough.Text = "闭关突破"; _breakthrough.Disabled = true; return; }
		var next = realms.get_next_realm_name(value("cultivation_realm", "").AsString());
		if (string.IsNullOrEmpty(next))
		{
			if (_target is Character completedYuanxi && completedYuanxi.is_yunxi_complete())
			{
				_breakthrough.Text = "准备铸玄基";
				_breakthroughHint.Text = "云息十层圆满，可准备铸玄基；后续修行暂未开放。";
			}
			else
			{
				_breakthrough.Text = "后续境界";
				_breakthroughHint.Text = "当前已达可显示的最高境界。";
			}
			_breakthrough.Disabled = true;
			return;
		}
		var pillHint = _target is Character pillCharacter ? pillCharacter.get_breakthrough_pill_hint(next) : "";
		if (!call("is_breakthrough_ready").AsBool()) { _breakthrough.Text = "闭关突破"; _breakthroughHint.Text = $"灵力圆满后，方可突破至 {next}。{(string.IsNullOrEmpty(pillHint) ? "" : $"\n{pillHint}")}"; _breakthrough.Disabled = true; return; }
		if (_target is Character condensationTarget && condensationTarget.requires_condensation_breakthrough())
		{
			_breakthroughHint.Text = "云息六层至七层改为八回合凝核牌局：每回合六张牌，必须对调两张，杂质达到 100 立即失败。";
			_breakthrough.Text = "进入凝核牌局"; _breakthrough.Disabled = false; return;
		}
		if (_target is Character missingPillTarget && !missingPillTarget.has_required_breakthrough_pill(next))
		{
			_breakthroughHint.Text = pillHint;
			_breakthrough.Text = "缺少突破丹";
			_breakthrough.Disabled = true;
			return;
		}
		var room = call("can_use_meditation_room_for_retreat").AsBool();
		var range = call("get_breakthrough_retreat_hour_range", room).AsGodotDictionary();
		var low = range.GetValueOrDefault("min_hours", 0).AsInt32(); var high = range.GetValueOrDefault("max_hours", 0).AsInt32();
		var time = GetNodeOrNull<GameTimeMgr>("/root/GameTimeManager");
		_breakthroughHint.Text = time is null ? pillHint : $"闭关突破至 {next} 约需 {format_duration_range(low, high)}，将在 {format_date_after(time, low)} 至 {format_date_after(time, high)} 之间结束。{(string.IsNullOrEmpty(pillHint) ? "" : $"\n{pillHint}")}";
		_breakthrough.Text = room ? "入静室闭关突破" : "闭关突破";
		_breakthrough.Disabled = false;
	}

	private void breakthrough()
	{
		if (_target is null) return;
		if (_target is Character condensationTarget && condensationTarget.requires_condensation_breakthrough())
		{
			close_panel(); CondensationRequested?.Invoke(condensationTarget); return;
		}
		var room = call("can_use_meditation_room_for_retreat").AsBool();
		var range = call("get_breakthrough_retreat_hour_range", room).AsGodotDictionary();
		var low = range.GetValueOrDefault("min_hours", 0).AsInt32(); var high = range.GetValueOrDefault("max_hours", 0).AsInt32();
		play_transition("闭关突破", $"心神归一，突破过程约需 {format_duration_range(low, high)}。", "闭关结束。你缓缓收功，境界变化已结算。", () => { _target.Call("start_breakthrough_retreat", room); refresh(); });
	}

	private void condensation_test()
	{
		if (_target is not Character testTarget) return;
		close_panel(); CondensationTestRequested?.Invoke(testTarget);
	}

	private void play_transition(string title, string plan, string complete, Action settle, int cultivationHours = 0)
	{
		void finish() { settle(); refresh_room_header(); }
		if (GetTree().GetFirstNodeInGroup("action_transition") is ActionTransitionPanel transition)
		{
			if (cultivationHours > 0) transition.play_cultivation(title, cultivationHours, complete, finish);
			else transition.play(title, plan, complete, finish);
			return;
		}
		finish();
	}
}
