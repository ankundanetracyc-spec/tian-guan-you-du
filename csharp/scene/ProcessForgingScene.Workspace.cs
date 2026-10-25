using Godot;

public partial class ProcessForgingScene
{
    private void BuildInputs()
    {
        var panel = Paper(_page, 24, 112, 510, 784);
        Put(panel, Text("投料", 27), 20, 14, 460, 38);
        Put(panel, Text("剑类 · 按岗位配置材料", 18), 20, 57, 460, 28);
        var groups = new[] { "身材", "质材", "性材", "和材" };
        for (var i = 0; i < groups.Length; i++)
        {
            var index = i; var group = groups[i];
            var tab = Btn(group, () => { _inputGroup = index; _scroll = 0; Refresh(); });
            if (i == _inputGroup) ForgingInkStyle.Button(tab, true);
            Put(panel, tab, 20 + i * 119, 99, 111, 43);
        }
        var explanation = _inputGroup switch { 0 => "主体、承接与辅体都属于身材。", 1 => "质材主要建立灵性承载。", 2 => "性材主要提供角色收益与效能。", _ => "和材主要调和材料之间的相克。" };
        Put(panel, Text(explanation, 17), 20, 151, 470, 30);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; _inputScroll = scroll;
        Put(panel, scroll, 17, 192, 478, 508);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; list.AddThemeConstantOverride("separation", 12); scroll.AddChild(list);
        var roles = _inputGroup == 0 ? new[] { "主体", "承接", "复合身材" } : new[] { groups[_inputGroup] };
        foreach (var role in roles)
        {
            var entries = _draft.Inputs.Where(input => input.Role == role).ToArray(); var slots = _model.Slots(role, _draft.Proficiency);
            var header = new HBoxContainer(); list.AddChild(header);
            var label = Text(DisplayText(role) + $"  {entries.Length}/{slots}", 21); label.SizeFlagsHorizontal = SizeFlags.ExpandFill; header.AddChild(label);
            var add = Btn("添加材料", () => Picker(role, null)); add.CustomMinimumSize = new(100, 34); add.Disabled = entries.Length >= slots; header.AddChild(add);
            if (slots == 0) { list.AddChild(Text("造诣32开放此岗位", 17)); continue; }
            if (entries.Length == 0) list.AddChild(Wrap(role is "主体" or "承接" ? "尚未投料 · 开炼前需配置" : "尚未投料 · 可选", 17));
            foreach (var input in entries)
            {
                var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", ForgingInkStyle.Paper(padding: 8, plain: true)); list.AddChild(card);
                var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 4); card.AddChild(box);
                var fact = _model.Snapshot(input.Material);
                var select = Btn(input.Material + $"  ›\n{fact.Element.DefaultIfEmptyName()} · {ProcessForgingTrial.PolarityLabel(fact.Polarity)} · 库存{_session.Stock[input.Material]}", () => Picker(role, input)); select.CustomMinimumSize = new(0, 48); select.AddThemeFontSizeOverride("font_size", 16);
                select.Icon = GD.Load<Texture2D>(_model.Materials[input.Material].icon_path); select.ExpandIcon = true; select.AddThemeConstantOverride("icon_max_width", 40);
                select.TooltipText = MaterialInfo(input.Material, role); box.AddChild(select);
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); box.AddChild(row);
                var qty = Text("份数", 18); qty.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(qty);
                var minus = Btn("－", () => { input.Quantity = Math.Max(1, input.Quantity - 1); Edit(); }); minus.CustomMinimumSize = new(42, 38); minus.Disabled = input.Quantity <= 1; row.AddChild(minus);
                var number = new LineEdit { Text = input.Quantity.ToString(), Alignment = HorizontalAlignment.Center, CustomMinimumSize = new(58, 38) }; row.AddChild(number);
                void CommitQuantity()
                {
                    if (int.TryParse(number.Text, out var count) && count is >= 1 and <= 8)
                    { if (count != input.Quantity) { input.Quantity = count; Edit(); } }
                    else number.Text = input.Quantity.ToString();
                }
                number.TextSubmitted += _ => CommitQuantity(); number.FocusExited += CommitQuantity;
                var plus = Btn("＋", () => { if (_draft.Inputs.Where(x => x.Material == input.Material).Sum(x => x.Quantity) < _session.Stock[input.Material]) input.Quantity = Math.Min(8, input.Quantity + 1); Edit(); }); plus.CustomMinimumSize = new(42, 38);
                plus.Disabled = input.Quantity >= 8 || _draft.Inputs.Where(x => x.Material == input.Material).Sum(x => x.Quantity) >= _session.Stock[input.Material]; row.AddChild(plus);
                _buttons["quantity-plus-" + role + "-" + input.Material] = plus;
                var remove = Btn("移除", () => { _draft.Inputs.Remove(input); Edit(); }); remove.CustomMinimumSize = new(62, 38); row.AddChild(remove);
            }
        }
        Put(panel, Btn("清空投料", () => { _draft.Inputs.Clear(); _scroll = 0; Edit(); }), 20, 729, 145, 40);
        Put(panel, Text("每岗每材1—8份 · 全炉由灵识限制", 16), 179, 735, 312, 30);
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, _scroll);
    }

    private void BuildProcess()
    {
        var yin = Paper(_page, 552, 682, 632, 214);
        Put(yin, Text("投入阴阳", 23), 22, 15, 585, 35);
        var value = _model.Inspect(_draft, _session.Stock).InputPolarity;
        var selected = Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero) + 3, 0, 6);
        var ornament = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/ui/forging_polarity_20261024/yin_yang_axis.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Put(yin, ornament, 44, 31, 544, 124);
        for (var i = 0; i < 7; i++)
        {
            var center = 52 + 88 * i;
            var tick = new ColorRect { Color = new Color("83765d"), MouseFilter = MouseFilterEnum.Ignore };
            Put(yin, tick, center - 0.5f, i == 3 ? 83 : 88, 1, i == 3 ? 20 : 10);
            var label = Text(ProcessForgingTrial.PolarityNames[i], 18);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            if (i == selected) label.AddThemeColorOverride("font_color", new Color("284d43"));
            Put(yin, label, center - 34, 129, 68, 26);
        }
        // 游标反映连续投入值，档名仍沿用现有七档取整规则。
        var cursorX = 52 + 528 * (float)((Math.Clamp(value, -3, 3) + 3) / 6);
        var cursor = new Polygon2D
        {
            Polygon = [new Vector2(-6, -10), new Vector2(6, -10), new Vector2(0, 0)],
            Color = new Color("35574e"), Position = new Vector2(cursorX, 81)
        };
        yin.AddChild(cursor);
        var needle = new ColorRect { Color = new Color("35574e"), MouseFilter = MouseFilterEnum.Ignore };
        Put(yin, needle, cursorX - 1, 83, 2, 22);
        var current = Text("当前投入 · " + ProcessForgingTrial.PolarityLabel(value), 18);
        current.HorizontalAlignment = HorizontalAlignment.Center;
        Put(yin, current, 22, 179, 584, 28);
    }

    private void BuildCosts()
    {
        var panel = Paper(_page, 1202, 112, 446, 784);
        Put(panel, Text("开炼用度", 27), 22, 14, 395, 38);
        var available = Text($"可支配灵识  {AvailableSpirit} / {SpiritCapacity}", 23); Put(panel, available, 22, 66, 395, 35);
        var demand = _model.SpiritDemand(_draft);
        Put(panel, Text($"本次需求    {demand}" + (demand > AvailableSpirit ? $" · 超出{demand - AvailableSpirit}" : ""), 22), 22, 109, 395, 34);
        var bar = WorldHudStyle.Bar(WorldHudStyle.StaminaInk); bar.MaxValue = Math.Max(1, AvailableSpirit); bar.Value = Math.Min(demand, AvailableSpirit); Put(panel, bar, 22, 155, 395, 12);
        available.TooltipText = $"总灵识{SpiritCapacity}，其他占用{OtherSpirit}；本次投料不会提前扣除。";
        var breakdown = ScrollList(panel, 22, 233, 395, 230);
        breakdown.AddChild(Text("逐材负担", 20));
        foreach (var load in _model.SpiritLoads(_draft))
        {
            var line = Text($"{RoleLabel(load.Input.Role)} · {load.Input.Material} ×{load.Input.Quantity}    {load.Total:0.##}", 17); breakdown.AddChild(line);
            line.TooltipText = $"{RoleLabel(load.Input.Role)}：材料基础{load.BaseLoad:0.##} + 功用{load.FunctionLoad:0.##}，岗位×{load.RoleMultiplier:0.##}，份数×{load.DoseMultiplier:0.##}。整炉合计后统一向上取整。";
        }
        var r = _model.Inspect(_draft, _session.Stock, SpiritCapacity, OtherSpirit);
        if (_forgingOwner is not null && !_forgingOwner.can_spend_world_stamina(r.Energy)) r.Errors.Add("当前精力不足。");
        Put(panel, Text($"投入{r.Units}份 · 工时{r.Hours}时辰 · 精力{r.Energy}", 18), 22, 485, 398, 32);
        Put(panel, Wrap(string.Join("\n", r.Errors.DefaultIfEmpty("投料齐备，可以开炼。")), 17), 22, 535, 395, 103);
        var submitted = _forgingOwner is null && _session.IsSubmitted(_model, _draft, _revision);
        var craft = Btn(submitted ? "已炼制 · 调整后再炼" : "开 炼", Craft); craft.Disabled = !r.CanSubmit || submitted; ForgingInkStyle.Button(craft, true); _buttons["craft"] = craft;
        Put(panel, craft, 22, 654, 395, 54);
        Put(panel, Wrap(_notice, 16), 22, 724, 395, 48);
    }
}

