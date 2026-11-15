using Godot;

/// <summary>对话角色立绘接入；单独放置以便主对话面板正在编辑时仍可扩展。</summary>
public partial class StoryDialoguePanel : Control
{
    private Control? _speakerPortrait;
    private PortraitRenderer? _speakerPortraitRenderer;
    private bool _portraitIsPlayer;
    private bool _portraitOnRight;
    private bool _portraitResizeConnected;
    private string _portraitSpeaker = "";

    public void show_player_speech(string player_name, string body, Character player, Action? on_closed = null)
    {
        show_player_speech(player_name, body, on_closed);
        show_character_portrait(player, false);
    }

    public void show_player_thought(string player_name, string body, Character player, Action? on_closed = null)
    {
        show_player_thought(player_name, body, on_closed);
        show_character_portrait(player, false);
    }

    public void show_npc_speech(string speaker, string body, WorldNpcData npc, Action? on_closed = null)
    {
        show_npc_speech(speaker, body, on_closed);
        show_world_npc_portrait(npc);
    }

    public void hide_speaker_portrait()
    {
        _portraitSpeaker = "";
        _speakerPortrait?.Hide();
        _speakerPortraitRenderer?.clear_portrait();
    }

    public override void _Process(double delta)
    {
        process_social_reveal(delta);
        if (_socialContent is not null) return;
        if (!Visible || _speaker is null) return;
        if (!_speakerOnRight)
        {
            if (!_portraitIsPlayer || _speaker.Text != _portraitSpeaker) hide_speaker_portrait();
            return;
        }
        if (_speaker.Text == _portraitSpeaker) return;
        show_world_npc_portrait(new WorldNpcData
        {
            npc_id = "dialogue_" + _speaker.Text,
            npc_name = _speaker.Text,
            gender = -1,
            current_age = 30,
            cultivation_realm = "凡人",
        });
    }

    private void show_character_portrait(Character character, bool on_right)
    {
        ensure_portrait_node();
        if (character.Get("entity_data").AsGodotObject() is not HumanData data)
        {
            hide_speaker_portrait();
            return;
        }
        _portraitIsPlayer = true;
        _portraitSpeaker = _speaker?.Text ?? "";
        var displayData = data.Duplicate(true) as HumanData ?? data;
        var gender = displayData.gender == 1 ? "female" : "male";
        var appearanceCatalog = GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
        if (appearanceCatalog?.allows_color_tint(gender, "skin") != true) displayData.appearance_skin_tone = -1;
        if (appearanceCatalog?.allows_color_tint(gender, "hair") != true) displayData.appearance_hair_color = -1;
        var portraitCatalog = GetNode<PortraitCatalog>("/root/PortraitCatalog");
        var context = portraitCatalog.get_context(displayData, gender, character.Get("current_age").AsInt32(), displayData.faction_name, character.Get("cultivation_realm").AsString());
        var requested = portraitCatalog.get_requested_appearance(displayData);
        if (displayData.appearance_hair_back_style < 0) requested["hair_back"] = "";
        _speakerPortraitRenderer!.configure(displayData, context, requested);
        if (!_speakerPortraitRenderer.has_required_layers()) { _speakerPortraitRenderer.clear_portrait(); _speakerPortrait?.Hide(); return; }
        finish_portrait_layout(on_right);
    }

    private void show_world_npc_portrait(WorldNpcData npc)
    {
        ensure_portrait_node();
        npc.ensure_appearance_identity();
        _portraitIsPlayer = false;
        _portraitSpeaker = npc.npc_name;
        _speakerPortraitRenderer!.show_world_npc(npc);
        if (!_speakerPortraitRenderer.has_required_layers()) { _speakerPortraitRenderer.clear_portrait(); _speakerPortrait?.Hide(); return; }
        finish_portrait_layout(true);
    }

    private void finish_portrait_layout(bool on_right)
    {
        if (_speakerPortrait is null || _speakerPortraitRenderer is null || !_speakerPortraitRenderer.has_required_layers())
        {
            _speakerPortrait?.Hide();
            return;
        }

        var viewport = GetViewportRect().Size;
        var dialogue = _dialogueFrame?.GetGlobalRect() ?? new Rect2(0, viewport.Y - _dialogueHeight, viewport.X, _dialogueHeight);
        float height = Mathf.Min(dialogue.Size.X * .30f, Mathf.Min(dialogue.Size.Y * 2.1f, viewport.Y * .46f));
        height = Mathf.Min(height, Mathf.Max(120.0f, dialogue.Position.Y - 36.0f));
        const float visible_portrait_ratio = 0.76f;
        float full_image_height = height / visible_portrait_ratio;
        float width = full_image_height * 2.0f / 3.0f;
        float top = Mathf.Max(0.0f, dialogue.Position.Y - height + 8f);
        _speakerPortrait.AnchorLeft = 0;
        _speakerPortrait.AnchorTop = 0;
        _speakerPortrait.AnchorRight = 0;
        _speakerPortrait.AnchorBottom = 0;
        var left = on_right ? dialogue.End.X - width - 28f : dialogue.Position.X + 28f;
        if (!on_right)
        {
            var roster = GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;
            if (roster?.IsVisibleInTree() == true && new Rect2(left, top, width, height).Intersects(roster.get_ui_rect()))
                left = Mathf.Max(left, roster_clearance());
        }
        _speakerPortrait.Position = new Vector2(left, top);
        _speakerPortrait.Size = new Vector2(width, height);
        _speakerPortraitRenderer.Position = Vector2.Zero;
        _speakerPortraitRenderer.Size = new Vector2(width, full_image_height);
        _portraitOnRight = on_right;
        _speakerPortrait.Show();
        _speakerPortrait.MoveToFront();
        layout_floating_choices();
    }

    private void ensure_portrait_node()
    {
        if (_speakerPortrait is null)
        {
            _speakerPortrait = new Control
            {
                Name = "SpeakerPortrait",
                MouseFilter = MouseFilterEnum.Ignore,
                ClipContents = true,
                ZAsRelative = false,
                ZIndex = 241,
                Visible = false,
            };
            AddChild(_speakerPortrait);
            _speakerPortraitRenderer = new PortraitRenderer
            {
                Name = "CurrentPortrait",
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _speakerPortrait.AddChild(_speakerPortraitRenderer);
        }
        if (!_portraitResizeConnected)
        {
            Resized += relayout_speaker_portrait;
            _portraitResizeConnected = true;
        }
    }

    private void relayout_speaker_portrait()
    {
        if (_speakerPortrait?.Visible == true) finish_portrait_layout(_portraitOnRight);
    }
}
