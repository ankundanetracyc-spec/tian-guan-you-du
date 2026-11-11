using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Service = TalismanCraftingService;
using Plans = TalismanCraftingPlans;

public partial class TalismanCraftingScene
{
    private readonly Dictionary<string, string> _planDraftNames = new();
    private static string PlanModeName(string mode) => mode switch { "paper" => "制纸", "ink" => "研墨", _ => "画符" };

    private void OpenPlanBook() => OpenPlanBook(_mode);
    private void OpenPlanBook(string mode)
    {
        if (_busy) return;
        var sheet = OpenModal("配伍册", 1130, 800);
        int x = 30;
        foreach (string kind in new[] { "paper", "ink", "drawing" })
        {
            var tab = Button(PlanModeName(kind), () => OpenPlanBook(kind), 22);
            tab.Name = "PlanMode_" + kind;
            CraftingPaperStyle.ApplyButton(tab, fontSize: 22, selected: mode == kind);
            Place(sheet, tab, x, 96, 126, 43); x += 138;
        }
        var list = Scroll(sheet, 30, 159, 1070, 425);
        var entries = Plans.List(OwnerCharacter, mode);
        foreach (var entry in entries)
        {
            var row = new Control { Name = "PlanRow_" + entry.Id, CustomMinimumSize = new Vector2(1042, 122) };
            list.AddChild(row);
            Place(row, new ColorRect { Color = new Color(.53f, .64f, .48f, .12f), MouseFilter = MouseFilterEnum.Ignore }, 0, 0, 1042, 119);
            Place(row, Text(entry.Name, 27), 14, 9, 620, 37);
            var description = Text(entry.Summary, 20, true); description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Place(row, description, 14, 53, 620, 56);
            var load = Button("载入", () => LoadCraftingPlan(entry.Id), 22); load.Name = "PlanLoad_" + entry.Id;
            load.TooltipText = string.Join("\n", entry.Warnings);
            Place(row, load, 650, 36, 115, 46);
            var rename = Button("更名", () => OpenPlanRename(entry.Id, entry.Name, mode), 21); rename.Name = "PlanRename_" + entry.Id;
            Place(row, rename, 777, 36, 115, 46);
            var delete = Button("删除", () => OpenPlanDelete(entry.Id, entry.Name, mode), 21); delete.Name = "PlanDelete_" + entry.Id;
            Place(row, delete, 904, 36, 115, 46);
        }
        if (entries.Count == 0)
        {
            var empty = Text("尚无配伍", 26, true); empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.CustomMinimumSize = new Vector2(1030, 180); list.AddChild(empty);
        }
        Line(sheet, 30, 601, 1070);
        Place(sheet, Text("记下本批" + PlanModeName(_mode) + "配伍", 25), 30, 612, 1050, 39);
        var name = new LineEdit { Name = "PlanNameInput", PlaceholderText = "配伍名称", MaxLength = Plans.NameLimit,
            SelectAllOnFocus = true, Text = _planDraftNames.GetValueOrDefault(_mode, PlanModeName(_mode) + "配伍") };
        CraftingPaperStyle.ApplyInput(name, fontSize: 23);
        name.TextChanged += value => _planDraftNames[_mode] = value;
        Place(sheet, name, 30, 665, 783, 46);
        var save = Button("记下配伍", () => SaveCraftingPlan(sheet, name.Text), 23); save.Name = "SavePlanButton";
        save.Disabled = OwnerCharacter is null;
        Place(sheet, save, 835, 665, 264, 46);
        var notice = Text("", 20, true); notice.Name = "PlanBookNotice"; notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(sheet, notice, 30, 729, 1070, 44);
    }

    private void SaveCraftingPlan(Control sheet, string name)
    {
        var request = CurrentRequest();
        if (Plans.Save(OwnerCharacter, name, request, false, out string reason))
        { _planDraftNames[request.Mode] = name.Trim(); OpenPlanBook(request.Mode); return; }
        if (Plans.List(OwnerCharacter, request.Mode).Any(p => p.Name == name.Trim()))
        {
            var confirm = OpenModal("覆盖配伍", 790, 360);
            var message = Text("替换“" + name.Trim() + "”？", 26); message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Place(confirm, message, 30, 102, 730, 84);
            var accept = Button("确认覆盖", () =>
            {
                if (Plans.Save(OwnerCharacter, name, request, true, out string failure))
                { _planDraftNames[request.Mode] = name.Trim(); OpenPlanBook(request.Mode); }
                else PlanNotice(confirm, failure);
            }, 23);
            accept.Name = "PlanOverwriteConfirm"; Place(confirm, accept, 526, 266, 230, 49);
            Place(confirm, Button("取消", () => OpenPlanBook(request.Mode), 22), 30, 266, 150, 49);
            AddPlanNotice(confirm, 200, 730);
            return;
        }
        PlanNotice(sheet, reason);
    }

    private void OpenPlanRename(string id, string previous, string mode)
    {
        var sheet = OpenModal("更名", 790, 360);
        var name = new LineEdit { Name = "PlanRenameInput", Text = previous, MaxLength = Plans.NameLimit, SelectAllOnFocus = true };
        CraftingPaperStyle.ApplyInput(name, fontSize: 24); Place(sheet, name, 30, 108, 730, 49);
        AddPlanNotice(sheet, 185, 730);
        var accept = Button("确认更名", () =>
        {
            if (Plans.Rename(OwnerCharacter, id, name.Text, out string reason)) OpenPlanBook(mode);
            else PlanNotice(sheet, reason);
        }, 23);
        accept.Name = "PlanRenameConfirm"; Place(sheet, accept, 526, 266, 230, 49);
        Place(sheet, Button("取消", () => OpenPlanBook(mode), 22), 30, 266, 150, 49);
    }

    private void OpenPlanDelete(string id, string name, string mode)
    {
        var sheet = OpenModal("删除配伍", 790, 360);
        var message = Text("删除“" + name + "”？", 26); message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(sheet, message, 30, 102, 730, 84);
        AddPlanNotice(sheet, 200, 730);
        var accept = Button("确认删除", () =>
        {
            if (Plans.Delete(OwnerCharacter, id, out string reason)) OpenPlanBook(mode);
            else PlanNotice(sheet, reason);
        }, 23);
        accept.Name = "PlanDeleteConfirm"; Place(sheet, accept, 526, 266, 230, 49);
        Place(sheet, Button("取消", () => OpenPlanBook(mode), 22), 30, 266, 150, 49);
    }

    private static void AddPlanNotice(Control sheet, float y, float width)
    {
        var notice = Text("", 20, true); notice.Name = "PlanBookNotice"; notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(sheet, notice, 30, y, width, 49);
    }

    private static void PlanNotice(Control sheet, string message)
    {
        if (sheet.FindChild("PlanBookNotice", true, false) is Label note)
        { note.Text = message; note.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger); }
    }

    private void LoadCraftingPlan(string id)
    {
        if (_busy) return;
        if (!Plans.TryLoad(OwnerCharacter, id, out var request, out string reason))
        { if (_modal?.FindChild("PlanBookNotice", true, false) is Label note) note.Text = reason; return; }
        ApplyCraftingPlan(request);
    }

    /// <summary>载入需求量，即便不足也保留草稿；开工仍走真实库存与资格预检。</summary>
    public void ApplyCraftingPlan(Service.Request request)
    {
        if (_busy || request.Mode is not ("paper" or "ink" or "drawing")) return;
        if (request.Mode == "drawing")
        {
            _paper = request.PaperPath; _brush = request.BrushPath; _recipe = request.RecipeId;
            _writing = request.WritingId; _quantity = request.Quantity; _inks.Clear();
            foreach (var s in request.InkSelections) _inks[s.Path] = _inks.GetValueOrDefault(s.Path) + s.Quantity;
        }
        else
        {
            var materials = request.Mode == "paper" ? _paperMaterials : _inkMaterials;
            materials.Clear();
            foreach (var s in request.Materials) materials[s.Path] = materials.GetValueOrDefault(s.Path) + s.Quantity;
            if (request.Mode == "paper") _paperMethod = request.TechniqueId;
            else
            {
                _inkMethod = request.TechniqueId; _retained.Clear();
                foreach (string utility in request.RetainedUtilities) _retained.Add(utility);
            }
        }
        _mode = request.Mode;
        if (_toast is not null) _toast.Text = "";
        CloseModal();
        if (_page is not null) Refresh();
    }
}
