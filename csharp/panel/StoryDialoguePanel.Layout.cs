using Godot;

public partial class StoryDialoguePanel
{
    private float roster_clearance()
    {
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        return roster?.IsVisibleInTree() == true ? roster.get_ui_rect().End.X + 18f : 28f;
    }

    private void layout_bottom_dialogue()
    {
        if (!IsInsideTree() || _dialogueFrame is null || _body is null) return;
        var viewport = GetViewportRect().Size;
        var outer = viewport.X < 1500 ? 24f : 34f;
        var width = Mathf.Min(1500f, viewport.X * .76f);
        _dialogueHeight = dialogue_height_for_width(width, viewport.Y);
        var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
        // 只有名单真的延伸到对话高度时才收窄，始终保持画面中心线不变。
        if (roster?.IsVisibleInTree() == true && roster.get_ui_rect().End.Y > viewport.Y - outer - _dialogueHeight)
        {
            width = Mathf.Max(280f, Mathf.Min(width, viewport.X - 2f * roster_clearance()));
            _dialogueHeight = dialogue_height_for_width(width, viewport.Y);
        }
        var left = (viewport.X - width) * .5f;
        _dialogueFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _dialogueFrame.Position = new Vector2(left, viewport.Y - outer - _dialogueHeight);
        _dialogueFrame.Size = new Vector2(width, _dialogueHeight);
        relayout_speaker_portrait();
        layout_floating_choices();
    }

    private float dialogue_height_for_width(float width, float viewportHeight)
    {
        if (_body is null) return 166f;
        var textWidth = Mathf.Max(200f, width - 56f);
        var font = _body.GetThemeFont("font");
        var textSize = font.GetMultilineStringSize(_body.Text, HorizontalAlignment.Left, textWidth,
            _body.GetThemeFontSize("font_size"), -1,
            TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive);
        var minimumHeight = Mathf.Clamp(viewportHeight * .19f, 166f, 218f);
        var maximumHeight = Mathf.Max(166f, Mathf.Min(310f, viewportHeight * .37f));
        return Mathf.Clamp(Mathf.Max(minimumHeight, textSize.Y + 108f), 166f, maximumHeight);
    }

    private void layout_floating_choices()
    {
        if (_responseScroll is null || !_bottomChoosing || _bottomChoices is null || _dialogueFrame is null) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Min(Mathf.Clamp(viewport.X * .24f, 280f, 350f), Mathf.Max(1f, viewport.X - 56f));
        var dialogueWidth = Mathf.Min(1500f, viewport.X * .76f);
        var right = Mathf.Clamp((viewport.X + dialogueWidth) * .5f - 12f, width + 8f, viewport.X - 8f);
        var left = right - width;
        var desiredHeight = _bottomChoices.GetCombinedMinimumSize().Y;
        // 横向锚点保持固定；纵向随对话栏的实际上沿移动，末项贴住上沿。
        var bottomLimit = Mathf.Max(1f, _dialogueFrame.Position.Y);
        var topLimit = Mathf.Min(Mathf.Max(16f, viewport.Y * .12f), Mathf.Max(0f, bottomLimit - 1f));
        var height = Mathf.Min(desiredHeight, Mathf.Max(1f, bottomLimit - topLimit));
        var top = bottomLimit - height;
        _responseScroll.Position = new Vector2(left, top);
        _responseScroll.Size = new Vector2(width, height);
    }

    public Rect2 get_dialogue_rect() => _dialogueFrame?.GetGlobalRect() ?? new Rect2();
    public Rect2 get_response_rect() => _responseScroll?.IsVisibleInTree() == true ? _responseScroll.GetGlobalRect() : new Rect2();
}
