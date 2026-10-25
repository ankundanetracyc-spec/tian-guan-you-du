using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class AlchemySlotBrewPreviewScene
{
    private VBoxContainer _ingredientLedgerRows = null!;
    private Label _ingredientLedgerCount = null!;
    private string _ingredientLedgerFingerprint = "";
    private void BuildIngredientLedger()
    {
        var panel = MakeInkSlip(vertical: true);
        panel.Name = "IngredientLedger";
        Place(_canvas, panel, new Vector2(20, 184), new Vector2(224, 580));
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        panel.AddChild(body);
        AddSidebarToggle(panel, true);
        var heading = QuietRoomStyle.Text("已投入药材", 23);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(heading);
        _ingredientLedgerCount = QuietRoomStyle.Text("", 14);
        _ingredientLedgerCount.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(_ingredientLedgerCount);
        body.AddChild(HSeparatorLine());
        var scroll = new ScrollContainer
        {
            Name = "IngredientLedgerScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        body.AddChild(scroll);
        _ingredientLedgerRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _ingredientLedgerRows.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(_ingredientLedgerRows);
    }

    private void RefreshIngredientLedger()
    {
        var entries = new List<(MaterialChoice Material, int Dose, int Slot)>();
        if (_main is not null) entries.Add((_main, _mainDose, -1));
        for (int i = 0; i < _assistants.Length; i++)
            if (_assistants[i] is { } material) entries.Add((material, _assistantDoses[i], i));
        entries = entries.OrderBy(e => (int)RoleFor(e.Slot)).ThenBy(e => e.Slot).ToList();
        var fingerprint = string.Join("|", entries.Select(e => $"{e.Material.Path}:{e.Dose}:{e.Slot}"));
        if (fingerprint == _ingredientLedgerFingerprint && _ingredientLedgerRows.GetChildCount() > 0) return;
        _ingredientLedgerFingerprint = fingerprint;
        ClearNotebookChildren(_ingredientLedgerRows);
        _ingredientLedgerCount.Text = $"{entries.Count}味 · {entries.Sum(e => e.Dose)}份";
        if (entries.Count == 0)
        {
            var empty = QuietRoomStyle.Text("尚未投料\n选入药材后在此查看材性。", 14);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _ingredientLedgerRows.AddChild(empty);
            return;
        }
        foreach (var entry in entries)
        {
            var material = entry.Material;
            var group = new VBoxContainer { Name = $"Ledger_{entry.Slot}" };
            group.AddThemeConstantOverride("separation", 4);
            _ingredientLedgerRows.AddChild(group);
            var role = entry.Slot < 0 ? "君" : RoleFor(entry.Slot) switch
            {
                AlchemySlotSimulation.SlotRole.Chen => "臣",
                AlchemySlotSimulation.SlotRole.Zuo => "佐", _ => "使"
            };
            var name = QuietRoomStyle.Text($"{role} · {material.Name} ×{entry.Dose}", 16);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            group.AddChild(name);
            var facts = QuietRoomStyle.Text($"品阶 {material.Craft.material_grade_name()}\n五行 {material.Craft.element} · 阴阳 {PolarityName(material.Craft.polarity)}", 13);
            group.AddChild(facts);
            var active = material.Craft.utilities
                .Where(u => u is not null && FunctionActsInSlot(entry.Slot, u.utility_id))
                .GroupBy(u => u.utility_id)
                .Select(g => $"{g.Key}{MaterialCraftData.utility_grade_name(g.Max(u => u.grade))}").ToArray();
            var functions = QuietRoomStyle.Text("生效 " + (active.Length == 0 ? "无本位功用" : string.Join("、", active)), 13);
            functions.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            functions.AddThemeColorOverride("font_color", new Color("51665a"));
            group.AddChild(functions);
            SetHover(group, MaterialTooltip(material, entry.Slot));
        }
    }
}
