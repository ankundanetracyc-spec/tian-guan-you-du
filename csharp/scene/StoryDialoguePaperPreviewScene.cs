using Godot;

/// <summary>Standalone presentation samples. No world, probe rolls, charges, battle or formal save is opened.</summary>
public partial class StoryDialoguePaperPreviewScene : Control
{
    public const string NpcSpeech = "道友，探人根底前，总该先打声招呼吧？";
    public const string Narration = "你没能探清对方的底细，反倒惊动了对方。";
    public const int SampleHour = (2 * 30 + 11) * 24 + 10;
    public static readonly string[] ReplyLabels = ["是我唐突", "说明来意", "何必动气", "收回灵识，告辞"];
    private StoryDialoguePaperPreviewPanel _dialogue = null!;
    private WorldNpcData _npc = null!;
    private Character _player = null!;
    private Control _canvas = null!, _headerCanvas = null!;
    public bool ExitOnClose { get; set; } = true;
    public StoryDialoguePaperPreviewPanel Dialogue => _dialogue;
    public Control DesignCanvas => _canvas;
    public int LastChoice { get; private set; } = -1;
    public int CancelCount { get; private set; }
    public int ContinueCount { get; private set; }
    public int CurrentCase { get; private set; }

    private static T Put<T>(Control parent, T child, float x, float y, float width, float height) where T : Control
    { parent.AddChild(child); child.Position = new(x, y); child.Size = new(width, height); return child; }
    private static Label Text(string caption)
    {
        var label = CraftingPaperStyle.Text(caption, 22); label.VerticalAlignment = VerticalAlignment.Center; return label;
    }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Theme = CraftingPaperStyle.MakeTheme();
        _canvas = QuietRoomStyle.Canvas(this); _canvas.Name = "DialoguePreviewCanvas";
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://textures/baishi/interiors_20261105/inn_lobby.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Put(_canvas, background, 0, 0, 1672, 941);
        _npc = new WorldNpcData
        {
            npc_id = "dialogue_paper_preview_luheng", npc_name = "陆衡", faction_id = "baopu_shan", formal_identity = "行脚修士",
            cultivation_realm = "云息二层", current_age = 20, gender = 0, appearance_seed = 684201
        };
        var human = new HumanData { entity_name = "沈照微", gender = 0, current_age = 18, cultivation_realm = "云息三层", appearance_seed = 684201, faction_name = "玉虚仙宗" };
        _player = new Character { entity_name = human.entity_name, entity_data = human };
        _dialogue = new StoryDialoguePaperPreviewPanel { Name = "PaperStoryDialogue" }; AddChild(_dialogue);
        BuildHeader(); ShowCase(1);
    }
    private void BuildHeader()
    {
        _headerCanvas = QuietRoomStyle.Canvas(this);
        _headerCanvas.Name = "DialogueHeaderCanvas"; _headerCanvas.ZIndex = 300; _headerCanvas.MouseFilter = MouseFilterEnum.Ignore;
        var header = new Control { Name = "DialoguePreviewHeader", ZIndex = 300, MouseFilter = MouseFilterEnum.Ignore };
        Put(_headerCanvas, header, 0, 0, 1672, 90);
        Panel Paper(float x, float width)
        {
            var panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", CraftingPaperStyle.Paper(0, true, .97f));
            return Put(header, panel, x, 18, width, 46);
        }
        Paper(24, 280);
        var place = Text("白石城 · 客栈"); place.HorizontalAlignment = HorizontalAlignment.Center;
        Put(header, place, 36, 18, 256, 46);
        Paper(1094, 412);
        var date = Text(LetterMailbox.Date(SampleHour)); date.Name = "DialogueDate";
        date.HorizontalAlignment = HorizontalAlignment.Right; Put(header, date, 1106, 18, 388, 46);
        var back = new Button { Name = "DialogueReturn", Text = "返回" }; PaperButtonFeedback.Apply(back);
        Put(header, back, 1534, 18, 114, 46);
        Put(back, new QuietRoomTriangle { Right = true, MouseFilter = MouseFilterEnum.Ignore }, 91, 20, 5, 7);
        back.Pressed += ClosePreview;
    }
    public void ShowCase(int selected)
    {
        CurrentCase = selected; LastChoice = -1;
        if (selected == 0)
            _dialogue.show_npc_speech(_npc.npc_name, "道友，灵识越界了。收回去吧。", _npc, () => ContinueCount++);
        else if (selected == 2)
            _dialogue.show_narration(Narration, () => ContinueCount++);
        else if (selected == 3)
            _dialogue.show_narration(string.Join("\n\n", Enumerable.Repeat(Narration, 30)), () => ContinueCount++);
        else if (selected == 5)
            ShowPlayerReply(0);
        else
        {
            int count = selected == 4 ? 16 : 4;
            var options = Enumerable.Range(0, count).Select(index => (ReplyLabels[index % 4], (Action)(() => Choose(index)))).ToArray();
            _dialogue.show_bottom_choices(_npc.npc_name, NpcSpeech, _npc, options, () => CancelCount++);
            _dialogue.SkinResponses();
        }
        // present() raises the modal dialogue; keep the preview return reachable above it.
        _headerCanvas.MoveToFront();
        _dialogue.AlignPortrait(); _dialogue.CallDeferred(nameof(StoryDialoguePaperPreviewPanel.AlignPortrait));
    }
    private void Choose(int index)
    {
        LastChoice = index;
        if (index % 4 == 3) return;
        ShowPlayerReply(index % 4);
    }
    private void ShowPlayerReply(int index)
    {
        string reply = index == 0 ? "是我唐突，抱歉。" : index == 2 ? "不过看一眼，何必动气。" : "只是想确认道友的修为，并无别的意思。";
        _dialogue.show_player_speech("沈照微", reply, _player, () => ContinueCount++);
        _headerCanvas.MoveToFront();
        _dialogue.AlignPortrait(); _dialogue.CallDeferred(nameof(StoryDialoguePaperPreviewPanel.AlignPortrait));
    }
    public void ClosePreview()
    {
        _dialogue.close();
        if (ExitOnClose) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GetTree().Quit(); }
        else Hide();
    }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape) ClosePreview();
        else if (key.Keycode is >= Key.Key1 and <= Key.Key6) ShowCase((int)key.Keycode - (int)Key.Key1);
        else return;
        GetViewport().SetInputAsHandled();
    }
    public override void _ExitTree()
    { if (_player is not null && GodotObject.IsInstanceValid(_player)) _player.Free(); }
}
