using Godot;
using System;
using System.Collections.Generic;

public partial class CondensationCardGamePanel
{
    private bool _openingSwapPending, _openingHelpVisible;

    private void BindCondensationHover(Control anchor, string name, bool includeCharacterBonus = true, int index = -1, bool immediate = false)
    {
        var card = CondensationCardGame.Definition(name);
        var effect = CondensationCardText.Details(name, includeCharacterBonus ? _game : null, index);
        HoverInfoManager.SetInfo(anchor, new HoverInfoManager.HoverInfoContent(
            card.Name, effect, CondensationUiArt.Icon(CardIconIndex(name)), card.Kind, ShowRules: false), immediate);
    }

    private void ShowOpeningSwap()
    {
        if (_game is not { Round: 1, SwapOpen: true } game || !_openingSwapPending) return;
        _openingHelpVisible = false;
        HoverInfoManager.HideAll();
        var box = Modal("起手换牌", 1450, 700);
        box.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var background = Art(box, CondensationUiArt.Opening, new Rect2(0, 0, 1450, 700));
        box.MoveChild(background, 0);
        box.GetChild<Label>(1).Position = new Vector2(65, 22);
        Text(box, "是否更换起手牌？选中要换掉的牌，最多两张。", 23, new Rect2(65, 78, 1180, 42), Cream);
        var selectedText = Text(box, "", 21, new Rect2(40, 530, 700, 36), Cream);
        var selections = new List<(Panel Border, Label Mark)>();
        Button? confirm = null;

        void UpdateSelection()
        {
            selectedText.Text = $"已选择 {_swapSelection.Count} / 2 张";
            for (var n = 0; n < selections.Count; n++)
            {
                var selected = _swapSelection.Contains(n);
                selections[n].Border.Visible = selected;
                selections[n].Mark.Visible = selected;
            }
            if (confirm is not null) confirm.Disabled = _swapSelection.Count == 0;
        }

        for (var i = 0; i < game.Hand.Count; i++)
        {
            var index = i;
            var name = game.Hand[i];
            var definition = CondensationCardGame.Definition(name);
            var x = 65 + i * 224;
            var card = new Button { Name = $"OpeningCard{i}", Position = new Vector2(x, 145), Size = new Vector2(200, 310), MouseDefaultCursorShape = CursorShape.PointingHand };
            ClearButton(card); box.AddChild(card);
            Art(card, CondensationUiArt.Card, new Rect2(0, 0, 200, 310));
            Text(card, name, 31, new Rect2(12, 13, 176, 48), Ink, true);
            Text(card, definition.Kind, 17, new Rect2(12, 58, 176, 28), Ink, true);
            Art(card, CondensationUiArt.Icon(CardIconIndex(name)), new Rect2(22, 83, 156, 117));
            Text(card, CondensationCardText.Face(name, game, index), 16, new Rect2(17, 201, 166, 106), Ink, true);
            var border = new Panel { Size = card.Size, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            border.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("e8c576"), BorderWidthLeft = 5, BorderWidthTop = 5, BorderWidthRight = 5, BorderWidthBottom = 5 });
            card.AddChild(border);
            var mark = Text(box, "◆ 已选择", 21, new Rect2(x, 468, 200, 35), new Color("f1d18a"), true);
            selections.Add((border, mark));
            BindCondensationHover(card, name, true, index);
            card.Pressed += () =>
            {
                if (!_swapSelection.Remove(index) && _swapSelection.Count < 2) _swapSelection.Add(index);
                UpdateSelection();
            };
        }

        ButtonAt(box, "玩法说明", new Rect2(40, 620, 200, 48), ShowHelp);
        ButtonAt(box, "不换，直接开始", new Rect2(770, 620, 260, 48), () => FinishOpeningSwap(false, selectedText));
        confirm = ButtonAt(box, "换牌并开始", new Rect2(1070, 620, 300, 48), () => FinishOpeningSwap(true, selectedText));
        UpdateSelection();
    }

    private void FinishOpeningSwap(bool exchange, Label message)
    {
        if (!_openingSwapPending || _game is not { Round: 1, SwapOpen: true } game) return;
        if (exchange)
        {
            if (_swapSelection.Count == 0) return;
            var error = game.Swap(_swapSelection);
            if (error != "") { message.Text = error; return; }
        }
        game.StartActions();
        _openingSwapPending = _openingHelpVisible = false;
        _swapSelection.Clear();
        HoverInfoManager.HideAll();
        HideModal(); Refresh();
    }
}
