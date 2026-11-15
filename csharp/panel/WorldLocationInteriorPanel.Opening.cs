using Godot;

public partial class WorldLocationInteriorPanel
{
    public event Action<string>? QingxiOpeningActionRequested;
    private Control? _qingxiOpeningActions;
    private Button? _qingxiTalk;

    /// <summary>溪边开场沿用白石与玉虚的场景绳签，剧情交互无需从人物名单触发。</summary>
    public void set_qingxi_opening_action(string action)
    {
        if (_qingxiOpeningActions is null)
        {
            _qingxiOpeningActions = new Control { Name = "QingxiOpeningActions", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_qingxiOpeningActions);
            _qingxiOpeningActions.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var rope = new Control { Name = "ActionRope", MouseFilter = MouseFilterEnum.Ignore };
            rope.SetScript(GD.Load<Script>("res://scripts/ui/SceneActionRope.gd"));
            _qingxiOpeningActions.AddChild(rope);
            rope.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var scene = GD.Load<PackedScene>("res://tscn/panel/SceneActionButton.tscn");
            Button Make(string id, string title)
            {
                var tag = scene.Instantiate<Button>(); tag.Name = "Action_" + id; tag.Text = title;
                tag.SetMeta("action_id", id); _qingxiOpeningActions.AddChild(tag);
                tag.Pressed += () => QingxiOpeningActionRequested?.Invoke(id);
                return tag;
            }
            _qingxiTalk = Make("qingxi.talk", "与方远说话");
        }
        _qingxiTalk!.Visible = action == "qingxi.talk";
        _qingxiOpeningActions.Visible = action.Length > 0;
        if (_qingxiOpeningActions.Visible) _qingxiOpeningActions.MoveToFront();
    }
}
