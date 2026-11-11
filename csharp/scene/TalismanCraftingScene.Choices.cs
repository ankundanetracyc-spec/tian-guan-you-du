using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using static TalismanCraftingCatalog;
using Service = TalismanCraftingService;

public partial class TalismanCraftingScene
{
    private void BuildDrawingLeft()
    {
        var left = Paper(_page, 24, 118, 400, 797); left.Name = "TalismanLeftPanel";
        var heading = Text("纸墨与符谱", 28); heading.Name = "TalismanLeftHeading";
        Place(left, heading, 24, 86, 350, 42);
        var recipe = Button((Service.Recipes().FirstOrDefault(r => r.Id == _recipe)?.Name ?? "选择符谱") + "　　换谱 ›", OpenRecipes, 26);
        recipe.Name = "ChooseRecipeButton"; Place(left, recipe, 22, 139, 355, 67);
        void Heading(string title, float y, string kind)
        {
            Place(left, Text(title, 23), 23, y, 225, 36);
            var choose = Button("选择 ›", () => OpenProducts(kind), 19); ClearStyle(choose);
            choose.Name = "ChooseProduct_" + kind; Place(left, choose, 282, y, 95, 35);
        }
        Heading("符纸", 222, "paper");
        var paper = Service.OwnedProducts(OwnerCharacter, "paper").FirstOrDefault(p => p.Path == _paper);
        if (paper is not null) ProductMini(left, paper, 22, 263, 355, 130);
        Heading("符墨", 407, "ink");
        var inkList = Scroll(left, 22, 449, 355, 176);
        foreach (var selected in _inks.ToArray())
        {
            var ink = Service.OwnedProducts(OwnerCharacter, "ink").FirstOrDefault(p => p.Path == selected.Key);
            if (ink is null)
            {
                var missing = new Control { Name = "MissingInkCard_" + inkList.GetChildCount(), CustomMinimumSize = new Vector2(335, 110) };
                inkList.AddChild(missing);
                Place(missing, Text("符墨不足或已失效", 22), 10, 8, 315, 35);
                Place(missing, Text($"需要 {selected.Value} 份", 20, true), 10, 55, 212, 36);
                var discard = Button("移除", () => { _inks.Remove(selected.Key); RequestRefresh(); }, 19);
                discard.Name = "RemoveMissingInk_" + inkList.GetChildCount(); Place(missing, discard, 247, 55, 78, 36);
                continue;
            }
            var card = new Control { CustomMinimumSize = new Vector2(335, 180) }; inkList.AddChild(card);
            ProductMini(card, ink, 0, 0, 335, 134);
            AddStepper(card, "InkDose_" + ink.Item.definition_id, selected.Value, 91, 140, value =>
            {
                if (value <= 0) _inks.Remove(ink.Path); else _inks[ink.Path] = value;
                RequestRefresh();
            }, Math.Min(99, ink.Count));
            var remove = Button("移除", () => { _inks.Remove(ink.Path); RequestRefresh(); }, 17);
            Place(card, remove, 263, 140, 64, 36);
        }
        Heading("符笔", 633, "brush");
        var brush = Service.OwnedProducts(OwnerCharacter, "brush").FirstOrDefault(p => p.Path == _brush);
        if (brush is not null) ProductMini(left, brush, 22, 675, 355, 109);
        else
        {
            var buy = Button("备置符笔", () => OpenKnowledge("brush"), 21); buy.Name = "GetBrushButton";
            Place(left, buy, 24, 693, 350, 57);
        }
    }

    private void ProductMini(Control parent, Service.ProductEntry entry, float x, float y, float width, float height)
    {
        var card = Place(parent, new Control { Name = "ProductInfo_" + entry.Item.definition_id }, x, y, width, height);
        HoverInfoManager.SetProvider(card, () => StoragePanel.ItemHover(entry.Item, entry.Count, entry.Path, OwnerCharacter));
        Place(card, new ColorRect { Color = new Color(.53f, .64f, .48f, .14f), MouseFilter = MouseFilterEnum.Ignore }, 0, 0, width, height);
        Place(card, Image(entry.Item.icon_path), 4, 8, 79, height - 16);
        Place(card, Text($"{entry.Item.entity_name}　×{entry.Count}", 23), 92, 5, width - 99, 34);
        var fields = TalismanItemPresentation.Fields(entry.Item);
        if (entry.Snapshot.Kind is "paper" or "brush")
        {
            float fy = 41;
            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                Place(card, Text(field.Key + " " + field.Value, 18, true), 92 + i % 2 * 124, fy + i / 2 * 30, 127, 30);
            }
        }
        else if (entry.Snapshot.Kind == "ink")
        {
            var utility = Text(fields[0].Key + " " + fields[0].Value, 18, true);
            utility.AutowrapMode = TextServer.AutowrapMode.WordSmart; Place(card, utility, 92, 41, width - 100, 31);
            for (int i = 1; i < fields.Count; i++)
            {
                var field = fields[i];
                Place(card, Text(field.Key + " " + field.Value, 18, true), 92 + (i - 1) % 2 * 124, 73 + (i - 1) / 2 * 28, 128, 28);
            }
        }
    }

    private Control OpenModal(string title, float width = 1130, float height = 806)
    {
        CloseModal();
        var shade = new ColorRect { Name = "CraftingModal", Color = new Color(.13f, .19f, .14f, .47f),
            MouseFilter = MouseFilterEnum.Stop, Size = new Vector2(1672, 941) };
        _canvas.AddChild(shade); _modal = shade;
        var sheet = Paper(shade, (1672 - width) / 2, (941 - height) / 2, width, height, true, .96f);
        if (title.Length > 0)
        {
            Place(sheet, Text(title, 30), 29, 18, width - 145, 50);
            var close = Button("×", () => { if (!_busy) CloseModal(); }, 26); close.Name = "CloseCraftingModal";
            Place(sheet, close, width - 71, 21, 42, 42);
            Line(sheet, 28, 78, width - 56);
        }
        return sheet;
    }

    private void CloseModal()
    {
        if (_modal is null) return;
        _canvas.RemoveChild(_modal); _modal.QueueFree(); _modal = null;
    }

    private void OpenMaterialPicker()
    {
        var sheet = OpenModal("选取材料");
        var search = new LineEdit { Name = "MaterialSearch", PlaceholderText = "名称、功用", SelectAllOnFocus = true };
        Place(sheet, search, 30, 94, 258, 43);
        string texture = "全部";
        float x = 305;
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 13); grid.AddThemeConstantOverride("v_separation", 13);
        var scroll = Place(sheet, new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }, 29, 156, 1072, 558);
        scroll.AddChild(grid);
        var owned = new CheckBox { Name = "OwnedMaterialsOnly", Text = "仅有库存", ButtonPressed = true };
        Place(sheet, owned, 914, 94, 184, 43);
        var status = Text("", 20, true); Place(sheet, status, 31, 746, 805, 37);
        void Fill()
        {
            foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            int shown = 0;
            foreach (var entry in DiscoverMaterials().Where(m => IsEligible(m, _mode))
                .Where(m => texture == "全部" || m.Texture == texture)
                .Where(m => !owned.ButtonPressed || m.Stock(OwnerCharacter) > 0)
                .Where(m => string.IsNullOrWhiteSpace(search.Text) || (m.Name + m.UtilityText).Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                shown++;
                var cell = new Control { CustomMinimumSize = new Vector2(337, 178) }; grid.AddChild(cell);
                Place(cell, new ColorRect { Color = new Color(.53f, .64f, .48f, .17f), MouseFilter = MouseFilterEnum.Ignore }, 0, 0, 337, 176);
                Place(cell, Image(entry.IconPath), 7, 11, 75, 104);
                Place(cell, Text(entry.Name, 24), 92, 10, 235, 35);
                Place(cell, Text($"{entry.ElementText} · {entry.Texture} · {entry.Stock(OwnerCharacter)}份", 18, true), 92, 49, 238, 30);
                var utilities = Text(entry.UtilityText, 18, true); utilities.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                Place(cell, utilities, 92, 80, 235, 42);
                bool selected = Materials.ContainsKey(entry.Path);
                var use = Button(selected ? "已选 · 移除" : "选入本批", () =>
                {
                    SelectMaterial(entry.Path, selected ? 0 : 1); Fill();
                }, 20);
                use.Name = "PickMaterial_" + entry.Id; use.Disabled = entry.Stock(OwnerCharacter) <= 0;
                CraftingPaperStyle.ApplyButton(use, fontSize: 20, selected: selected);
                Place(cell, use, 12, 131, 313, 36);
            }
            if (shown == 0) grid.AddChild(Text("没有符合条件的材料。", 24, true));
            status.Text = $"本批 {Materials.Count} 味 · {Materials.Values.Sum()} 份";
        }
        var filters = _mode == "paper" ? new[] { "全部", "纤维", "液体", "黏胶" } : new[] { "全部", "致密", "颗粒", "液体", "黏胶" };
        var filterButtons = new Dictionary<string, Button>();
        foreach (string filter in filters)
        {
            var button = Button(filter, () =>
            {
                texture = filter;
                foreach (var (key, btn) in filterButtons) CraftingPaperStyle.ApplyButton(btn, fontSize: 20, selected: texture == key);
                Fill();
            }, 20);
            button.Name = "TextureFilter_" + filter; filterButtons[filter] = button;
            Place(sheet, button, x, 94, 97, 43); x += 102;
        }
        CraftingPaperStyle.ApplyButton(filterButtons["全部"], fontSize: 20, selected: true);
        search.TextChanged += _ => Fill(); owned.Toggled += _ => Fill();
        var done = Button("完成选材", CloseModal, 22); done.Name = "FinishMaterialSelection";
        Place(sheet, done, 899, 739, 199, 46); Fill();
    }

    private void OpenProducts(string kind)
    {
        var sheet = OpenModal(kind == "paper" ? "选择符纸" : kind == "ink" ? "选择符墨" : "选择符笔");
        var entries = Service.OwnedProducts(OwnerCharacter, kind);
        var list = Scroll(sheet, 28, 100, 1074, 603);
        foreach (var entry in entries)
        {
            var row = new Control { CustomMinimumSize = new Vector2(1048, 171) }; list.AddChild(row);
            ProductMini(row, entry, 0, 0, 794, 164);
            bool selected = kind == "paper" ? _paper == entry.Path : kind == "brush" ? _brush == entry.Path : _inks.ContainsKey(entry.Path);
            var choose = Button(selected ? "已选" : "选用", () =>
            {
                if (kind == "paper") _paper = entry.Path;
                else if (kind == "brush") _brush = entry.Path;
                else if (!_inks.Remove(entry.Path)) _inks[entry.Path] = 1;
                RequestRefresh();
                if (kind == "ink") OpenProducts(kind); else CloseModal();
            }, 22);
            choose.Name = "PickProduct_" + entry.Item.definition_id;
            CraftingPaperStyle.ApplyButton(choose, fontSize: 22, selected: selected);
            Place(row, choose, 824, 54, 193, 49);
        }
        if (entries.Count == 0) list.AddChild(Text("行囊中暂无成品。", 26, true));
        var done = Button("完成选择", CloseModal, 22); done.Name = "FinishProductSelection";
        Place(sheet, done, 900, 738, 198, 46);
    }

    private void OpenRecipes()
    {
        var sheet = OpenModal("符谱", 1050, 640);
        float y = 109;
        foreach (var recipe in Service.Recipes())
        {
            var row = Place(sheet, new Control(), 30, y, 990, 218);
            Place(row, Image(TalismanCombatCatalog.IconPath(recipe.Id)), 9, 6, 95, 189);
            Place(row, Text(recipe.Name, 29), 129, 3, 600, 43);
            var description = Text(recipe.Description, 22, true); description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Place(row, description, 129, 51, 616, 42);
            var effect = Text(TalismanCombatCatalog.TierName(0) + "：" + TalismanCombatCatalog.EffectText(recipe.Id, 0), 23);
            effect.AutowrapMode = TextServer.AutowrapMode.WordSmart; Place(row, effect, 129, 101, 616, 52);
            Place(row, Text($"使用：体力{TalismanCombatCatalog.EnergyCost} · 符箓1张", 20, true), 129, 163, 616, 33);
            var detail = Button("符效", () => OpenRecipeEffects(recipe.Id), 22); detail.Name = "RecipeEffects_" + recipe.Id;
            Place(row, detail, 809, 43, 151, 46);
            var use = Button(_recipe == recipe.Id ? "已选" : "选用", () => { _recipe = recipe.Id; CloseModal(); RequestRefresh(); }, 23);
            use.Name = "Recipe_" + recipe.Id; use.Disabled = !Service.IsRecipeKnown(OwnerCharacter, recipe.Id);
            CraftingPaperStyle.ApplyButton(use, fontSize: 23, selected: _recipe == recipe.Id);
            Place(row, use, 809, 118, 151, 46);
            Line(row, 0, 214, 990); y += 241;
        }
    }

    private void OpenRecipeEffects(string recipeId)
    {
        var sheet = OpenModal(TalismanCombatCatalog.Name(recipeId), 1000, 650);
        Place(sheet, Image(TalismanCombatCatalog.IconPath(recipeId)), 28, 123, 143, 345);
        for (int tier = 0; tier < TalismanCombatCatalog.TierNames.Length; tier++)
        {
            var name = Text(TalismanCombatCatalog.TierName(tier), 25); name.Name = "RecipeTier_" + tier;
            Place(sheet, name, 206, 113 + tier * 78, 126, 51);
            var effect = Text(TalismanCombatCatalog.EffectText(recipeId, tier), 24, true); effect.Name = "RecipeEffect_" + tier;
            Place(sheet, effect, 344, 113 + tier * 78, 623, 51);
            Line(sheet, 206, 174 + tier * 78, 760);
        }
        Place(sheet, Text($"使用：体力{TalismanCombatCatalog.EnergyCost} · 符箓1张", 22, true), 31, 572, 656, 46);
        var back = Button("返回符谱", OpenRecipes, 23); back.Name = "RecipeEffectsBack";
        Place(sheet, back, 727, 572, 238, 46);
    }

    private void OpenMaterialDetails(MaterialEntry entry)
    {
        var sheet = OpenModal(entry.Name, 760, 496);
        Place(sheet, Image(entry.IconPath), 33, 119, 158, 182);
        Place(sheet, Text(entry.ElementText + " · " + entry.Texture, 25), 220, 116, 500, 48);
        var body = Text(entry.UtilityText + "\n\n" + entry.Item.description, 23, true); body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(sheet, body, 220, 185, 500, 269);
    }

    private void OpenRelations()
    {
        var sheet = OpenModal("本批配伍", 1000, 760);
        var list = Scroll(sheet, 31, 104, 938, 611);
        var quote = Service.Evaluate(OwnerCharacter, CurrentRequest());
        foreach (string text in quote.Blockers.Concat(quote.Notes).Distinct())
        {
            var note = Text("· " + text, 23, true); note.AutowrapMode = TextServer.AutowrapMode.WordSmart; note.CustomMinimumSize = new Vector2(905, 43); list.AddChild(note);
        }
        if (_mode != "drawing")
        {
            var analysis = Analyze(_mode, CurrentRequest().Materials, OwnerCharacter, _mode == "ink" ? _retained : null);
            list.AddChild(Text("材性关系", 26));
            foreach (var relation in analysis.Relations) list.AddChild(Text(relation.Text, 22, true));
        }
    }

    private void OpenKnowledge() => OpenKnowledge("");
    private void OpenKnowledge(string focus)
    {
        var sheet = OpenModal("符工录", 1100, 825);
        var list = Scroll(sheet, 29, 98, 1042, 626);
        list.AddChild(Text("符笔", 27));
        foreach (var offer in Service.BrushOffers())
        {
            var buyRow = new Control { CustomMinimumSize = new Vector2(1007, 104) }; list.AddChild(buyRow);
            Place(buyRow, Text(offer.Name, 26), 10, 3, 700, 36);
            var description = Text(offer.Description, 20, true); description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Place(buyRow, description, 10, 44, 731, 55);
            var buy = Button($"备置 · {offer.Price}灵石", () =>
            {
                if (OwnerCharacter is null) return;
                if (OwnerCharacter.TryBuyTalismanBrush(offer.Id, out string reason)) { Notice(offer.Name + "已入行囊。"); OpenKnowledge(focus); RequestRefresh(); }
                else ShowModalNotice(sheet, reason);
            }, 22);
            buy.Name = offer.Id == "qingzhu" ? "BuyTalismanBrushButton" : "BuyBrush_" + offer.Id; buy.Disabled = OwnerCharacter is null;
            Place(buyRow, buy, 785, 20, 202, 48);
        }
        foreach (string mode in new[] { "paper", "ink", "drawing" })
        {
            list.AddChild(Text(mode == "paper" ? "制纸" : mode == "ink" ? "研墨" : "画符", 27));
            foreach (var method in Service.Methods(mode))
            {
                var row = new Control { CustomMinimumSize = new Vector2(1007, 118) }; list.AddChild(row);
                Place(row, Text(method.Name, 25), 10, 5, 685, 37);
                var desc = Text(method.Description + "　" + method.Tradeoff, 20, true); desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                Place(row, desc, 10, 45, 751, 65);
                bool known = Service.IsMethodKnown(OwnerCharacter, method.Id);
                var learn = Button(known ? "已掌握" : $"研习 · 造诣{method.RequiredProficiency:0}", () =>
                {
                    if (OwnerCharacter is null) return;
                    if (OwnerCharacter.TryLearnTalismanMethod(method.Id, out string reason)) { OpenKnowledge(method.Id); RequestRefresh(); }
                    else ShowModalNotice(sheet, reason);
                }, 20);
                learn.Name = "Learn_" + method.Id; learn.Disabled = known || OwnerCharacter is null;
                Place(row, learn, 792, 25, 197, 51);
            }
        }
        if (OwnerCharacter?.entity_data is HumanData human && human.talisman_records.Count > 0)
        {
            list.AddChild(Text("制作记录", 27));
            foreach (string record in human.talisman_records.AsEnumerable().Reverse().Take(12))
            {
                var text = Text(record.Split('\n')[0], 20, true); text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                text.CustomMinimumSize = new Vector2(990, 50); list.AddChild(text);
            }
        }
        Place(sheet, Text($"研习用度　精力{Service.LearningEnergy:0.#} · {Service.LearningHours:0.##}时辰", 20, true), 30, 731, 1035, 33);
        Place(sheet, Text("", 20, true), 30, 767, 1035, 34).Name = "ModalNotice";
        if (focus.Length > 0)
        {
            var target = sheet.FindChild(focus == "brush" ? "BuyTalismanBrushButton" : "Learn_" + focus, true, false) as Control;
            if (target is not null && list.GetParent() is ScrollContainer scroll)
                Callable.From(() => { if (GodotObject.IsInstanceValid(scroll) && GodotObject.IsInstanceValid(target)) scroll.EnsureControlVisible(target); }).CallDeferred();
        }
    }

    private static void ShowModalNotice(Control sheet, string message)
    {
        if (sheet.FindChild("ModalNotice", true, false) is Label notice) { notice.Text = message; notice.AddThemeColorOverride("font_color", CraftingPaperStyle.Danger); }
    }

    private void OpenHelp()
    {
        var sheet = OpenModal("符工", 925, 660);
        var text = Text("制符纸\n纤维与液料结纸，胶料可助结合。\n\n研墨\n致密、颗粒或液料取蕴，选留至多四项功用。\n\n画符\n依符谱配好纸、墨、笔，选一种写法。\n\n符道造诣\n影响保留、配伍驾驭和技艺用度。", 25, true);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Place(sheet, text, 34, 104, 855, 510);
    }

    private void ShowOutcome(Service.Outcome outcome)
    {
        var sheet = OpenModal("鉴看", 1100, 800);
        var list = Scroll(sheet, 30, 104, 1040, 555);
        int index = 0;
        foreach (var product in outcome.Products)
        {
            var row = new Control { CustomMinimumSize = new Vector2(1012, 270) }; list.AddChild(row);
            Place(row, Image(product.Item.icon_path), 14, 5, 225, 245);
            Place(row, Text($"{product.Item.entity_name}　×{product.Count}", 31), 265, 6, 700, 49);
            if (product.Snapshot.Kind == "talisman")
            {
                var tier = Text(TalismanCombatCatalog.TierName(product.Snapshot.Tier), 27, true); tier.Name = "ResultTier_" + index;
                Place(row, tier, 268, 69, 713, 42);
                var effect = Text(TalismanCombatCatalog.EffectText(product.Snapshot.RecipeId, product.Snapshot.Tier), 26);
                effect.Name = "ResultEffect_" + index; effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                Place(row, effect, 268, 123, 713, 79);
                Place(row, Text($"使用：体力{TalismanCombatCatalog.EnergyCost} · 符箓1张", 22, true), 268, 213, 713, 36);
            }
            else
            {
                float y = 69;
                foreach (var field in Service.PlayerFields(product.Snapshot))
                {
                    Place(row, Text(field.Key, 23), 268, y, 244, 38);
                    Place(row, Text(field.Value, 25, true), 525, y, 463, 38);
                    Line(row, 269, y + 39, 714); y += 44;
                }
            }
            index++;
        }
        Place(sheet, Text(outcome.Summary, 22, true), 31, 665, 1020, 34);
        string technique = Service.Methods(_mode).FirstOrDefault(m => m.Id == (_mode == "drawing" ? _writing : _mode == "paper" ? _paperMethod : _inkMethod))?.Name ?? "";
        var work = Text($"{technique} · 符道{outcome.Quote.Proficiency:0.#} · 精力{outcome.Quote.EnergyCost:0.#} · {outcome.Quote.DurationHours:0.##}时辰", 20, true);
        work.Name = "ResultWorkSummary"; Place(sheet, work, 31, 704, 1020, 29);
        var again = Button("继续制作", () => { CloseModal(); _toast.Text = ""; Refresh(); }, 24); again.Name = "ContinueCraftButton";
        Place(sheet, again, 834, 742, 230, 43);
        Place(sheet, Text("已入行囊", 21, true), 32, 742, 500, 43);
    }
}
