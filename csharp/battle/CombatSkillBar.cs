using Godot;
using Godot.Collections;

/// <summary>
/// 即时战斗固定技能栏。它只负责显示已装配的技能槽和发出输入信号，
/// 不创建牌堆、手牌或弃牌堆；凝核牌局仍由原卡牌界面单独处理。
/// </summary>
public partial class CombatSkillBar : Control
{
    private const int SlotCapacity = 10;
    // 技能槽是竖向长方形：费用在图标左上角，名称在框下方。
    // 费用和名称均由代码绘制，素材只负责等比显示图标窗口。
    // 10 个槽位尽量利用底板宽度，不再让技能只占中间一小段。
    internal const float SlotWidth = 118f;
    internal const float SlotHeight = 190f;
    // 素材本身约为 0.756 的竖向比例；这里给出同一比例的显示框，
    // 避免 Godot 在槽位里把水墨边框压扁或拉宽。
    // 框体留出上方费用与下方名称的空间；三者仍完整处于技能栏内。
    internal const float FrameWidth = 90f, FrameHeight = 119f;
    internal const float FrameTop = 32f;
    // 槽位只保留一圈低饱和度的纯色细边，不再使用带山水纹样的装饰框。
    // 人物档案的技能装配页也复用这份局内技能框，避免两处视觉分叉。
    internal const string SlotFramePath = "res://textures/ui/combat/combat_skill_slot_frame_generated_v1.png";

    [Signal] public delegate void skill_selectedEventHandler(ActiveSkillData skill, int slot_index);
    [Signal] public delegate void skill_target_selectedEventHandler(ActiveSkillData skill, Vector2 viewport_position, int slot_index);

    private readonly List<ActiveSkillData> skills = [];
    private readonly List<Button> buttons = [];
    private static readonly Color PaperInk = new("303f38");
    private readonly List<Control> slotFrames = [];
    private float slotScale = 1f;
    private HBoxContainer? slots;
    private HBoxContainer? energyPips;
    private HBoxContainer? manaPips;
    private Label? energyValue;
    private Label? manaValue;
    private Label? queueLabel;
    private PanelContainer? panel;
    private NinePatchRect? barArtwork;
    private ActiveSkillData? hoveredSkill;
    private Control? hoveredSkillAnchor;
    private string lastSkillHoverText = "";
    private readonly System.Collections.Generic.Dictionary<ActiveSkillData, (int State, string Body, string Metadata)> hoverCache = [];
    private static readonly string[] HoverAttributes = CardRules.Attributes.Values.Concat(new[] { "qi_blood", "blood_furnace_value", "punch_frame", "current_health" }).Distinct().ToArray();
    private double uiRefreshRemaining;
    private double hoverRefreshRemaining;
    private Vector2 lastViewportSize = Vector2.Zero;
    private ActiveSkillData? targetingSkill;
    private int targetingSlot = -1;
    private WorldEntity? currentUnit;
    private int lastEnergy = -1, lastMaxEnergy = -1, lastMana = -1, lastMaxMana = -1;
    private float lastEnergyProgress = -1f, lastManaProgress = -1f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        barArtwork = GetNodeOrNull<NinePatchRect>("BarArtwork");
        CreateBar();
        CreateCompactResources();
        FitBarArtworkToBottom();
        Hide();
    }

    private void CreateBar()
    {
        panel = new PanelContainer { Name = "SkillBarPanel", MouseFilter = MouseFilterEnum.Pass, ZIndex = 30, ClipContents = true };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        // 两侧留出空间给后续的战斗辅助按钮，不让技能栏强行占满底部。
        panel.AnchorLeft = .15f; panel.AnchorRight = .85f;
        // 竖向槽位需要比旧横向槽多一些高度；保留上方战场可视区域，
        // 同时让资源两行和放大的槽位在 16:9/720p 下都能完整容纳。
        // 底板向上扩展一点，给槽位和资源信息留出完整的安全区。
        panel.AnchorTop = .66f; panel.OffsetTop = 0; panel.OffsetBottom = 0;
        // 底板已经由 CombatSkillBar.tscn 中的水墨素材提供。
        // 这里不再叠加一层脚本暗框，否则会把素材的留白和山水压成一块黑底。
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10
        });
        AddChild(panel);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 50), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var resourceCluster = new VBoxContainer { CustomMinimumSize = new Vector2(300, 50), MouseFilter = MouseFilterEnum.Ignore };
        resourceCluster.AddThemeConstantOverride("separation", 2);
        resourceCluster.AddChild(CreateResourceRow("体力", 6, new Color(.31f, .78f, .43f), out energyPips, out energyValue));
        resourceCluster.AddChild(CreateResourceRow("法力", 3, new Color(.30f, .57f, .94f), out manaPips, out manaValue));
        header.AddChild(resourceCluster);
        resourceCluster.Hide();
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        header.AddChild(spacer);
        queueLabel = new Label { Text = "技能队列 0/3", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(120, 0), MouseFilter = MouseFilterEnum.Ignore };
        queueLabel.AddThemeFontSizeOverride("font_size", 16); queueLabel.AddThemeColorOverride("font_color", new Color(.73f, .76f, .68f)); header.AddChild(queueLabel);
        header.Hide();
        slots = new HBoxContainer { Name = "Slots", Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, SlotHeight), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        slots.AddThemeConstantOverride("separation", 1); column.AddChild(slots);
    }

    private static HBoxContainer CreateResourceRow(string caption, int max, Color activeColor, out HBoxContainer pips, out Label value)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(292, 23), MouseFilter = MouseFilterEnum.Ignore };
        var label = new Label { Text = caption, CustomMinimumSize = new Vector2(44, 0), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 15); label.AddThemeColorOverride("font_color", new Color(.90f, .91f, .84f)); row.AddChild(label);
        pips = new HBoxContainer { Name = caption == "体力" ? "EnergyPips" : "ManaPips", CustomMinimumSize = new Vector2(caption == "体力" ? 128 : 64, 18), MouseFilter = MouseFilterEnum.Ignore };
        pips.AddThemeConstantOverride("separation", 4);
        for (var i = 0; i < max; i++)
        {
            // 每一格本质是一个 0—1 的小进度条：既保留圣水槽的分段感，
            // 又能显示恢复中的小数进度，而不是等到整数跳变才变化。
            var pip = new ProgressBar { Name = $"Pip{i}", CustomMinimumSize = new Vector2(18, 14), MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
            pip.AddThemeStyleboxOverride("background", PipBackground(activeColor));
            pip.AddThemeStyleboxOverride("fill", PipFill(activeColor));
            pips.AddChild(pip);
        }
        row.AddChild(pips);
        value = new Label { Text = $"0/{max}", CustomMinimumSize = new Vector2(48, 0), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        value.AddThemeFontSizeOverride("font_size", 14); value.AddThemeColorOverride("font_color", activeColor.Lightened(.18f)); row.AddChild(value);
        return row;
    }

    private static StyleBoxFlat PipBackground(Color activeColor) => new()
    {
        BgColor = new Color(.035f, .05f, .05f, .92f),
        BorderColor = activeColor.Darkened(.28f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    private static StyleBoxFlat PipFill(Color activeColor) => new()
    {
        BgColor = activeColor,
        BorderColor = activeColor.Lightened(.22f),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    private static void RefreshPips(HBoxContainer? pips, int current, int max, float recoveryProgress)
    {
        if (pips is null) return;
        var total = Mathf.Clamp(current + Mathf.Clamp(recoveryProgress, 0, 1), 0, max);
        for (var i = 0; i < pips.GetChildCount(); i++)
            if (pips.GetChild(i) is ProgressBar pip) pip.Value = Mathf.Clamp(total - i, 0, 1);
    }

    public void set_battle_unit(GodotObject unit)
    {
        currentUnit = unit as WorldEntity;
        HideSkillHover();
        RefreshSlots();
    }

    public void set_loadout(Array<ActiveSkillData> values)
    {
        hoverCache.Clear();
        skills.Clear();
        foreach (var value in values)
            if (value is not null && (value.combat_empty_slot || Character.is_combat_skill_slot_eligible(value)) && !skills.Contains(value) && skills.Count < SlotCapacity) skills.Add(value);
        RefreshSlots();
    }

    public void refresh_slots() { uiRefreshRemaining = 0; hoverRefreshRemaining = 0; RefreshSlots(); }

    // 保留原技能栏的美术与几何，仅切换原槽位和底板的可见性。
    public void set_actions_visible(bool visible, bool selectingTargets=false)
    {
        if(_actionsVisible!=visible||_selectingTargets!=selectingTargets){_actionsVisible=visible;_selectingTargets=selectingTargets;lastViewportSize=Vector2.Zero;FitBarArtworkToBottom();}
        if (slots is not null) slots.Visible = visible;
        if (barArtwork is not null) barArtwork.Visible = visible;
        if (queueLabel is not null) queueLabel.Visible = false;
        if (!visible) HideSkillHover();
    }
    private bool _actionsVisible=true;
    private bool _selectingTargets;

    private void CreateCompactResources()
    {
        AddCompactResource("EnergyChip", "icon_energy", out energyValue);
        AddCompactResource("ManaChip", "icon_mana", out manaValue);
    }

    private void AddCompactResource(string name, string icon, out Label value)
    {
        var chip = new Panel { Name = name, Size = new(100,40), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 31 };
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("e4d9bf"), BorderColor = new Color("787b6d"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 });
        // 火纹/水焰下部墨色较重，向上作 2 px 光学补偿，与数字视觉中心对齐。
        var group=new HBoxContainer{Position=new(6,0),Size=new(88,40),Alignment=BoxContainer.AlignmentMode.Center,MouseFilter=MouseFilterEnum.Ignore};group.AddThemeConstantOverride("separation",4);chip.AddChild(group);
        var iconBox=new Control{CustomMinimumSize=new(24,40),MouseFilter=MouseFilterEnum.Ignore};group.AddChild(iconBox);
        var image = BattleUiArt.Icon(icon, new(24,24)); image.Position = new(0,6); iconBox.AddChild(image);
        value = new Label { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        value.AddThemeFontSizeOverride("font_size",18); value.AddThemeColorOverride("font_color",PaperInk);
        group.AddChild(value); AddChild(chip);
    }

    public void begin_targeting(ActiveSkillData skill, int slot)
    {
        targetingSkill = skill; targetingSlot = slot;
        for (var i = 0; i < buttons.Count; i++) buttons[i].Modulate = i == slot ? new Color(.90f, .97f, .84f) : new Color(.62f, .62f, .62f);
    }

    public void clear_targeting()
    {
        targetingSkill = null; targetingSlot = -1;
        foreach (var button in buttons) button.Modulate = Colors.White;
        (GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager)?.cancel_skill_targeting();
    }

    public override void _Process(double delta)
    {
        FitBarArtworkToBottom();
        if (!Visible) return;
        var manager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        if (manager is null || currentUnit is null || !GodotObject.IsInstanceValid(currentUnit)) { HideSkillHover(); return; }
        // 战斗状态会改变技能说明；只更新内容，不追随鼠标重排浮层。
        hoverRefreshRemaining -= delta;
        if (hoverRefreshRemaining <= 0) { hoverRefreshRemaining = .1; RefreshSkillHover(manager); }
        uiRefreshRemaining -= delta;
        if (uiRefreshRemaining > 0) return;
        uiRefreshRemaining = .1;
        var currentEnergy = currentUnit.Get("current_stamina").AsInt32();
        var maxEnergy = currentUnit.Get("max_stamina").AsInt32();
        var currentMana = currentUnit.Get("current_mana").AsInt32();
        var maxMana = manager.get_effective_mana_capacity(currentUnit);
        var planningBudget = manager.is_planning_phase() && currentUnit == manager.get_main_player_unit();
        var actualEnergy = currentEnergy;
        var actualMana = currentMana;
        if (planningBudget)
        {
            var budget = manager.get_player_queue_resource_budget();
            currentEnergy = budget.X;
            currentMana = budget.Y;
        }
        UpdateResourcePreview(energyValue, planningBudget, currentEnergy, actualEnergy, "体力");
        UpdateResourcePreview(manaValue, planningBudget, currentMana, actualMana, "法力");
        var energyProgress = 0f;
        var manaProgress = 0f;
        if (currentEnergy != lastEnergy || maxEnergy != lastMaxEnergy || Mathf.Abs(energyProgress - lastEnergyProgress) >= .01f)
        {
            lastEnergy = currentEnergy; lastMaxEnergy = maxEnergy; lastEnergyProgress = energyProgress;
            if (energyValue is not null) energyValue.Text = $"{currentEnergy}/{maxEnergy}";
            RefreshPips(energyPips, currentEnergy, maxEnergy, energyProgress);
        }
        if (currentMana != lastMana || maxMana != lastMaxMana || Mathf.Abs(manaProgress - lastManaProgress) >= .01f)
        {
            lastMana = currentMana; lastMaxMana = maxMana; lastManaProgress = manaProgress;
            if (manaValue is not null) manaValue.Text = $"{currentMana}/{maxMana}";
            RefreshScaledPips(manaPips, currentMana, maxMana);
        }
        var queueCount = manager.get_filled_action_slot_count(currentUnit);
        var queueCapacity = manager.get_action_slot_capacity(currentUnit);
        var mayPlan = manager.can_player_plan();
        queueLabel!.Text = "";queueLabel.Hide();
        for (var i = 0; i < skills.Count && i < buttons.Count; i++)
        {
            var queuedCount = manager.get_battle_unit(currentUnit).skill_queue.Count(entry => !entry.completed && entry.kind == BattleQueuedSkill.ActionKind.Skill && entry.skill == skills[i]);
            var queued = queuedCount > 0;
            var cooldown = manager.get_skill_cooldown(currentUnit, skills[i]);
            var ready = manager.is_skill_ready(currentUnit, skills[i]);
            var usesLabel = buttons[i].GetNodeOrNull<Label>("ChargeText");
            if (usesLabel is not null)
            {
                usesLabel.Text = manager.get_skill_uses_remaining(currentUnit, skills[i]);
                if (manager.get_skill_cooldown_rounds(currentUnit, skills[i]) > 0)
                    usesLabel.Text = manager.get_skill_round_cooldown_text(currentUnit, skills[i]).Replace("冷却中，", "");
                usesLabel.Visible = !string.IsNullOrWhiteSpace(usesLabel.Text);
            }
            buttons[i].Disabled = !mayPlan || queueCount >= queueCapacity || !ready;
            HoverInfoManager.SetTooltip(buttons[i], "");
            var cooldownVisual = buttons[i].GetNodeOrNull<SkillCooldownVisual>("CooldownVisual");
            if (cooldownVisual is not null)
            {
                cooldownVisual.Remaining = 0;
                cooldownVisual.Duration = 0;
                cooldownVisual.QueueRedraw();
            }
            ApplySlotVisual(i, queued, targetingSkill == skills[i], buttons[i].Disabled);
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible || slots?.Visible != true || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        var index = key.Keycode switch { Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4, Key.Key6 => 5, Key.Key7 => 6, Key.Key8 => 7, Key.Key9 => 8, Key.Key0 => 9, _ => -1 };
        if (index < 0 || index >= skills.Count || index >= buttons.Count || buttons[index].Disabled) return;
        EmitSkill(skills[index], index); GetViewport().SetInputAsHandled();
    }

    private static void UpdateResourcePreview(Label? value, bool planning, int amount, int actual, string resource)
    {
        if (value is null) return;
        value.AddThemeColorOverride("font_color", amount < 0 ? new Color("a2382c") : PaperInk);
        // 整个底板可悬停；图标和数字仍作为一组居中，不增加常驻文字。
        if (value.GetParent()?.GetParent() is Control chip)
        {
            chip.MouseFilter = MouseFilterEnum.Pass;
            HoverInfoManager.SetTooltip(chip, planning
                ? $"{resource}队列预算：{amount}（当前实际 {actual}）\n应对按触发费用预留，未触发则按普通分支付费，未配置则空放不扣费。\n已计入休整、吐纳与明确的无条件返还；负数表示预算缺口。\n实际执行时，费用不足的分支跳过且不扣费。"
                : $"当前实际{resource}：{actual}");
        }
    }

    private void RefreshSlots()
    {
        if (slots is null) return;
        HideSkillHover();
        BattleUiArt.ClearChildren(slots);
        buttons.Clear();
        slotFrames.Clear();
        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            var button = CreateSlot(skill, index);
            buttons.Add(button); AddScaledSlot(button);
        }
        for (var index = skills.Count; index < SlotCapacity; index++)
        {
            var empty = new Panel { CustomMinimumSize = new Vector2(SlotWidth, SlotHeight), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
            // 空槽只显示素材边框，不绘制暗色矩形底。
            empty.AddThemeStyleboxOverride("panel", SlotStyle(Colors.Transparent, Colors.Transparent, 0));
            var frame = CreateFrame(); frame.Modulate = new Color(1f, 1f, 1f, .65f); empty.AddChild(frame);
            // 空槽只保留低亮度框线，不再额外放文字，避免十个槽位产生无效噪音。
            AddScaledSlot(empty);
        }
    }

    private void AddScaledSlot(Control content)
    {
        var wrapper = new Control { CustomMinimumSize = new Vector2(SlotWidth, SlotHeight) * slotScale,
            MouseFilter = MouseFilterEnum.Ignore };
        content.Size = new Vector2(SlotWidth, SlotHeight);
        content.Scale = Vector2.One * slotScale;
        wrapper.AddChild(content);
        slots!.AddChild(wrapper);
    }

    private static Panel CreateFrame()
    {
        var frame = new Panel { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 3 };
        var style = SlotStyle(Colors.Transparent, new Color("697264"), 1);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 3;
        frame.AddThemeStyleboxOverride("panel", style);
        // 显式给出尺寸和位置，不依赖原图尺寸；KeepAspectCentered 保证竖向
        // 框素材不会被压扁或拉宽。
        frame.Position = new Vector2((SlotWidth - FrameWidth) * .5f, FrameTop);
        frame.Size = new Vector2(FrameWidth, FrameHeight);
        return frame;
    }

    private Button CreateSlot(ActiveSkillData skill, int index)
    {
        var button = new Button { Disabled = skill.combat_empty_slot, Name = $"Skill{index}", CustomMinimumSize = new Vector2(SlotWidth, SlotHeight), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, SizeFlagsVertical = SizeFlags.ShrinkCenter, Text = "", Flat = true, ClipText = true, ClipContents = true, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        // 按钮只承担点击区域；视觉上的边框由第三张竖向水墨素材承担。
        button.AddThemeStyleboxOverride("normal", SlotStyle(Colors.Transparent, Colors.Transparent, 0));
        button.AddThemeStyleboxOverride("hover", SlotStyle(Colors.Transparent, Colors.Transparent, 0));
        button.AddThemeStyleboxOverride("pressed", SlotStyle(Colors.Transparent, Colors.Transparent, 0));
        button.AddThemeStyleboxOverride("disabled", SlotStyle(Colors.Transparent, Colors.Transparent, 0));

        // 图标铺在框线下方；装配页也复用这段绘制逻辑，保证两处显示同一张完整图。
        var icon = CreateSkillIcon(skill);
        button.AddChild(icon);
        var frame = CreateFrame(); frame.Modulate = new Color(1f, 1f, 1f, .78f); button.AddChild(frame); slotFrames.Add(frame);
        // 小型费用叠在图标左上角，名称位于框下方。
        var costManager = GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager;
        var displaySkill = currentUnit is not null ? costManager?.get_next_action_skill(currentUnit, skill) ?? skill : skill;
        var displayEnergy = currentUnit is not null && costManager is not null ? costManager.get_skill_energy_cost(currentUnit, displaySkill) : displaySkill.energy_cost;
        var displayMana = currentUnit is not null && costManager is not null ? costManager.get_skill_mana_cost(currentUnit, displaySkill) : displaySkill.mana_cost;
        var cost = new Control { Position = new Vector2((SlotWidth-FrameWidth)*.5f+4,FrameTop+3), Size = new Vector2(FrameWidth-8,20), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        var costX=0f;
        foreach(var (symbol,amount) in new[]{("icon_energy",displayEnergy),("icon_mana",displayMana)})
        {
            if(amount<=0)continue;
            var costIcon=BattleUiArt.Icon(symbol,new(16,16));costIcon.Position=new(costX,1);cost.AddChild(costIcon);
            var number=new Label{Text=amount.ToString(),Position=new(costX+18,0),Size=new(20,20),VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};
            number.AddThemeFontSizeOverride("font_size",13);number.AddThemeColorOverride("font_color",PaperInk);cost.AddChild(number);costX+=36;
        }
        button.AddChild(cost);
        var uses = new Label { Name = "ChargeText", Text = "", Position = new Vector2(28, 139), Size = new Vector2(SlotWidth - 56, 16), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4, ClipText = true };
        uses.AddThemeFontSizeOverride("font_size", 11); uses.AddThemeColorOverride("font_color", PaperInk); button.AddChild(uses);
        var name = new Label { Text = skill.skill_name, Position = new Vector2(4, 160), Size = new Vector2(SlotWidth - 8, 18), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4, ClipText = true };
        name.AddThemeFontSizeOverride("font_size", 16); name.AddThemeColorOverride("font_color", PaperInk); button.AddChild(name);
        // CD 不再在技能名称下占用文字行；改为覆盖图标的纵向亮度进度。
        var cooldownVisual = new SkillCooldownVisual
        {
            Name = "CooldownVisual",
            Position = new Vector2((SlotWidth - FrameWidth) * .5f, FrameTop),
            Size = new Vector2(FrameWidth, FrameHeight),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        button.AddChild(cooldownVisual);
        var hotkeyText = index == 9 ? "0" : (index + 1).ToString();
        var hotkey = new Label { Text = hotkeyText, Position = new Vector2(86, 132), Size = new Vector2(16, 16), HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4 };
        hotkey.AddThemeFontSizeOverride("font_size", 11); hotkey.AddThemeColorOverride("font_color", PaperInk); button.AddChild(hotkey);
        button.MouseEntered += () => ShowSkillHover(skill);
        button.MouseExited += HideSkillHover;
        button.Pressed += () => EmitSkill(skill, index);
        return button;
    }

    private void ShowSkillHover(ActiveSkillData skill)
    {
        hoveredSkill = skill;
        var index = skills.IndexOf(skill);
        hoveredSkillAnchor = index >= 0 && index < buttons.Count ? buttons[index] : null;
        lastSkillHoverText = "";
        if (hoveredSkillAnchor is null || !_actionsVisible || !Visible) return;
        RefreshSkillHover(GetTree().GetFirstNodeInGroup("battle_mgr") as BattleManager);
    }

    private void HideSkillHover()
    {
        if (hoveredSkillAnchor is not null) HoverInfoManager.HideFor(hoveredSkillAnchor);
        hoveredSkill = null;
        hoveredSkillAnchor = null;
        lastSkillHoverText = "";
    }

    private void RefreshSkillHover(BattleManager? manager)
    {
        if (!_actionsVisible || !Visible || hoveredSkillAnchor is null || !GodotObject.IsInstanceValid(hoveredSkillAnchor) ||
            hoveredSkill is null || currentUnit is null || !GodotObject.IsInstanceValid(currentUnit)) return;
        var skill = hoveredSkill;
        var state = SkillHoverState(skill, manager);
        if (hoverCache.TryGetValue(skill, out var cached) && cached.State == state)
        {
            ApplySkillHoverContent(skill, cached.Body, cached.Metadata);
            return;
        }
        var effect = CardPresentation.CombatEffectText(skill, currentUnit);
        var target = CardPresentation.CombatTargetText(manager?.get_next_action_skill(currentUnit, skill) ?? skill, effect);
        var displaySkill = manager?.get_next_action_skill(currentUnit, skill) ?? skill;
        var energyCost = manager is null ? displaySkill.energy_cost : manager.get_skill_energy_cost(currentUnit, displaySkill);
        var manaCost = manager is null ? displaySkill.mana_cost : manager.get_skill_mana_cost(currentUnit, displaySkill);
        var cost = skill.skill_type == ActiveSkillData.SkillType.POWER
            ? ""
            : energyCost == 0 && manaCost == 0
                ? "费用：无"
                : $"费用：体力 {energyCost}；法力 {manaCost}";
        if (CardRules.IsResponse(skill))
        {
            var normal = CardRules.NormalCast(skill);
            cost = $"应对触发费用：体力 {energyCost}；法力 {manaCost}\n未应对费用：" + (normal is null ? "无（空放）"
                : $"体力 {manager?.get_skill_energy_cost(currentUnit, normal) ?? normal.energy_cost}；法力 {manager?.get_skill_mana_cost(currentUnit, normal) ?? normal.mana_cost}");
        }
        var useLimit = CardPresentation.CombatUseLimitText(skill, currentUnit, manager);
        var detail = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(cost)) detail.Add(cost);
        detail.Add($"目标：{target}");
        detail.Add($"使用次数：{useLimit}");
        var roundCooldown = CardPresentation.CombatCooldownText(skill, currentUnit, manager);
        if (!string.IsNullOrWhiteSpace(roundCooldown)) detail.Add(roundCooldown);
        detail.Add($"效果：\n{effect}");
        var calculations = CardPresentation.CombatCalculationDetails(skill, currentUnit);
        if (!string.IsNullOrWhiteSpace(calculations)) detail.Add($"\n数值推演：\n{calculations}");
        var body = string.Join("\n\n", detail);
        var keywords = skill.tags.Count > 0 ? string.Join("、", skill.tags) : "无";
        var metadata = $"类型：{skill.get_card_category()}　关键词：{keywords}";
        hoverCache[skill] = (state, body, metadata);
        ApplySkillHoverContent(skill, body, metadata);
    }

    private int SkillHoverState(ActiveSkillData skill, BattleManager? manager)
    {
        var hash = new HashCode();
        hash.Add(currentUnit!.GetInstanceId());
        hash.Add(GetViewportRect().Size);
        hash.Add(skill.description);
        hash.Add(skill.energy_cost); hash.Add(skill.mana_cost); hash.Add(skill.damage_formula); hash.Add(skill.stacks_formula); hash.Add(skill.base_damage);
        hash.Add(skill.cooldown_rounds);
        hash.Add(skill.cooldown_rounds > 0 ? manager?.get_skill_round_cooldown_text(currentUnit, skill) : "");
        foreach (var key in HoverAttributes) hash.Add(CardRules.Number(currentUnit, key));
        foreach (var bonus in skill.damage_bonuses)
            if (bonus is not null) { hash.Add(bonus.attribute); hash.Add(bonus.multiplier); hash.Add(CardRules.Number(currentUnit, bonus.attribute)); }
        hash.Add(manager?.get_next_action_skill(currentUnit, skill).artifact_is_summon);
        hash.Add(manager?.get_skill_energy_cost(currentUnit, skill));
        hash.Add(manager?.get_skill_mana_cost(currentUnit, skill));
        hash.Add(manager?.get_skill_uses_remaining(currentUnit, skill));
        hash.Add(manager?.gongfa_combat.DamageMultiplier(currentUnit, skill));
        foreach (var method in new[] { "get_blood_furnace_physical_damage_multiplier", "get_gongfa_physical_damage_multiplier", "get_species_physical_damage_multiplier" })
            if (currentUnit.HasMethod(method)) hash.Add(currentUnit.Call(method).AsSingle());
        if (currentUnit is Creature creature)
        {
            hash.Add(creature.get_combat_tempering_physical_damage_multiplier());
            foreach (var status in creature.statuses)
                if (status.AsGodotObject() is StatusInstance instance) { hash.Add(instance.effect?.effect_id); hash.Add(instance.stacks); }
        }
        if (CardRules.NormalCast(skill) is { } normal && normal != skill)
        {
            hash.Add(normal.GetInstanceId()); hash.Add(normal.description);
            hash.Add(normal.damage_formula); hash.Add(normal.stacks_formula); hash.Add(normal.base_damage);
            hash.Add(manager?.get_skill_energy_cost(currentUnit, normal));
            hash.Add(manager?.get_skill_mana_cost(currentUnit, normal));
            hash.Add(manager?.gongfa_combat.DamageMultiplier(currentUnit, normal));
        }
        return hash.ToHashCode();
    }

    private void ApplySkillHoverContent(ActiveSkillData skill, string body, string metadata)
    {
        if (hoveredSkillAnchor is null || !GodotObject.IsInstanceValid(hoveredSkillAnchor)) return;
        var contentKey = $"{skill.GetInstanceId()}|{metadata}|{body}";
        if (contentKey != lastSkillHoverText)
        {
            lastSkillHoverText = contentKey;
            var icon = string.IsNullOrWhiteSpace(skill.icon_path) || !ResourceLoader.Exists(skill.icon_path)
                ? null
                : ResourceLoader.Load<Texture2D>(skill.icon_path);
            HoverInfoManager.SetInfo(hoveredSkillAnchor,
                new HoverInfoManager.HoverInfoContent(skill.skill_name, body, icon, metadata));
        }
    }

    /// <summary>
    /// 创建战斗技能图标。scale 只放大局内槽位的完整几何尺寸；
    /// 所有来源图标都完整等比显示在窗口中，不能把方图或竖图裁成左上角的一块。
    /// 没有 icon_path 的技能仍然返回空纹理，交给外层只显示空槽框。
    /// </summary>
    internal static SkillIconVisual CreateSkillIcon(ActiveSkillData skill, float scale = 1f)
    {
        var slotWidth = SlotWidth * scale;
        var frameWidth = FrameWidth * scale;
        var frameHeight = FrameHeight * scale;
        return CreateSkillIconVisual(skill, new Vector2(frameWidth, frameHeight), new Vector2((slotWidth - frameWidth) * .5f, FrameTop * scale));
    }

    /// <summary>
    /// 统一创建完整图标窗口。这里不使用 TextureRect 的 Covered 模式，
    /// 由 SkillIconVisual 明确以“缩小至完整可见”为唯一规则。
    /// </summary>
    internal static SkillIconVisual CreateSkillIconVisual(ActiveSkillData skill, Vector2 size, Vector2 position = default)
    {
        return new SkillIconVisual
        {
            Name = "Icon",
            Texture = string.IsNullOrWhiteSpace(skill.icon_path) ? null : ResourceLoader.Load<Texture2D>(skill.icon_path),
            Position = position,
            Size = size,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 1,
            ClipContents = true
        };
    }

    private static string FormatCost(ActiveSkillData skill)
    {
        return $"体力{skill.energy_cost} · 法力{skill.mana_cost}";
    }

    private void FitBarArtworkToBottom()
    {
        if (barArtwork is null) return;
        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;
        if (lastViewportSize.DistanceSquaredTo(viewportSize) < .5f) return;
        lastViewportSize = viewportSize;

        // 九宫格只延展纸面中芯，边角保持比例，底部留少量呼吸空间。
        const float leftAnchor = .085f;
        const float rightAnchor = .915f;
        // 让槽位名称与冷却信息在底边框之上保留安全距离；
        // 高度略增而不改变底栏的横向占用范围。
        var displayHeight = viewportSize.Y * .25f;
        var bottomGap = viewportSize.Y * .025f;
        barArtwork.AnchorLeft = leftAnchor;
        barArtwork.AnchorRight = rightAnchor;
        barArtwork.AnchorTop = 1f;
        barArtwork.AnchorBottom = 1f;
        barArtwork.OffsetTop = -displayHeight-bottomGap;
        barArtwork.OffsetBottom = -bottomGap;
        var resourceY=_actionsVisible?viewportSize.Y-displayHeight-bottomGap+8:689*BattleUiArt.Scale(GetViewport())-44;
        // 选目标时沿用技能栏左沿，避开左侧十项清单；执行时再对齐执行底板。
        var resourceX=_actionsVisible||_selectingTargets?viewportSize.X*leftAnchor+24:BattleUiArt.Origin(GetViewport()).X+124*BattleUiArt.Scale(GetViewport());
        GetNode<Control>("EnergyChip").Position = new(resourceX,resourceY);
        GetNode<Control>("ManaChip").Position = new(resourceX+110,resourceY);

        if (panel is null) return;
        // 资源区位于底板上沿之前；槽位行从底板可见区起始处开始，
        // 这样费用（上）、技能框（中）、名称（下）都会完整落在技能栏内。
        // 槽位行比底板顶线低 18 像素，费用不再与水墨边线重叠；
        // 加高后的底板会在槽位文字下方留下稳定的安全区。
        var panelTop = Mathf.Max(0f, viewportSize.Y - displayHeight-bottomGap+26);
        panel.AnchorLeft = leftAnchor;
        panel.AnchorRight = rightAnchor;
        panel.AnchorTop = 0f;
        panel.AnchorBottom = 1f;
        panel.OffsetTop = panelTop;
        panel.OffsetBottom = -bottomGap-8;
        slotScale = Mathf.Max(.1f, Mathf.Min(
            (viewportSize.X * (rightAnchor-leftAnchor)-42) / (SlotCapacity*SlotWidth),
            (displayHeight-64) / SlotHeight));
        if (slots is not null) slots.CustomMinimumSize = new Vector2(0,SlotHeight*slotScale);
        RefreshSlots();
    }

    private void EmitSkill(ActiveSkillData skill, int index)
    {
        // 切换到另一项技能时只退出目标选择，不吞掉这次点击；
        // 已排队技能可再次点击加入额外行动；取消与调整顺序统一在规划队列卡片上操作。
        if (targetingSkill == skill) { clear_targeting(); return; }
        if (targetingSkill is not null) clear_targeting();
        EmitSignal(SignalName.skill_selected, skill, index);
    }

    public void emit_target(ActiveSkillData skill, Vector2 viewportPosition, int slot) => EmitSignal(SignalName.skill_target_selected, skill, viewportPosition, slot);

    private void ApplySlotVisual(int index, bool queued, bool targeting, bool disabled)
    {
        if (index < 0 || index >= slotFrames.Count || index >= buttons.Count) return;
        var frame = slotFrames[index];
        frame.Modulate = queued
            ? new Color(.92f, .98f, .86f, 1f)
            : disabled ? new Color(.56f, .60f, .55f, .65f) : targeting ? new Color(.55f, .80f, .65f, 1f) : Colors.White;
        buttons[index].Modulate = targeting ? new Color(.90f, .97f, .84f) : Colors.White;
    }

    private static StyleBoxFlat SlotStyle(Color background, Color border, int width) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4
    };
}

/// <summary>
/// 技能图标的唯一缩放口径：原图完整、等比、居中显示。
/// 不裁切、不以左上角为原点放大，也不要求填满竖向技能槽。
/// </summary>
public sealed partial class SkillIconVisual : Control
{
    public Texture2D? Texture { get; set; }

    public override void _Draw()
    {
        if (Texture is null) return;
        var source = Texture.GetSize();
        if (source.X <= 0 || source.Y <= 0 || Size.X <= 0 || Size.Y <= 0) return;

        // 仅在图标窗口内留出极窄的安全边距；窗口本身位于槽位框内，
        // 因此此处不会侵占费用和名称的空间。
        var bounds = new Rect2(new Vector2(2, 2), new Vector2(Mathf.Max(1, Size.X - 4), Mathf.Max(1, Size.Y - 4)));
        var scale = Mathf.Min(bounds.Size.X / source.X, bounds.Size.Y / source.Y);
        var drawSize = source * scale;
        var drawRect = new Rect2(bounds.Position + (bounds.Size - drawSize) * .5f, drawSize);
        DrawTextureRect(Texture, drawRect, false, Colors.White);
    }
}

/// <summary>技能冷却遮罩：冷却开始时整张图标偏暗，恢复时从上向下逐步亮起。</summary>
public sealed partial class SkillCooldownVisual : Control
{
    public float Remaining { get; set; }
    public float Duration { get; set; }

    public override void _Draw()
    {
        if (Duration <= 0.01f || Remaining <= 0.01f) return;
        var ratio = Mathf.Clamp(Remaining / Duration, 0f, 1f);
        // 顶部先恢复亮度，底部保留暗区；冷却结束时遮罩完全消失。
        var readyHeight = Size.Y * (1f - ratio);
        var darkRect = new Rect2(0, readyHeight, Size.X, Size.Y - readyHeight);
        DrawRect(darkRect, new Color(.015f, .025f, .025f, .70f), true);
        DrawLine(new Vector2(0, readyHeight), new Vector2(Size.X, readyHeight), new Color(.88f, .88f, .72f, .82f), 1.5f, true);
    }
}
