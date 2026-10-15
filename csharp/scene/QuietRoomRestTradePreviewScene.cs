using Godot;

/// <summary>原生控件美术预览；临时角色和交易物品，不进入正式旅程，不发起行动或写存档。</summary>
public partial class QuietRoomRestTradePreviewScene : Control
{
    private const string Folder = "res://docs/美术规范/静室休息与交易统一_20261014/";
    private CultivationUI _room = null!;
    private RestPanel _rest = null!;
    private TradePanel _trade = null!;
    private Character _character = null!;
    private WorldNpcData _merchant = null!;
    private GameTimeMgr _clock = null!;
    private string _captureFolder = Folder;

    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (OS.GetCmdlineUserArgs().Contains("--compact"))
        {
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = new Vector2I(1280, 720);
            GetWindow().ContentScaleSize = new Vector2I(1280, 720);
            _captureFolder += "1280x720/";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_captureFolder));
        }
        _clock = GetNode<GameTimeMgr>("/root/GameTimeManager");
        _clock.year = 1; _clock.month = 3; _clock.day = 15; _clock.hour = 10;
        var data = new HumanData
        {
            entity_name = "苏醒者", cultivation_realm = "云息三层",
            spirit_root_type = (int)GongfaResource.Wuxing.METAL_WOOD_WATER,
            spirit_root_name = "三灵根", root_weights = [34, 33, 33, 0, 0],
            main_gongfa_path = "res://resources/gongfa/黄庭养形篇.tres",
            learned_gongfa_paths = ["res://resources/gongfa/黄庭养形篇.tres", "res://resources/gongfa/乘物游骸真经.tres"],
            ambient_lingqi_pressure = 180, current_world_stamina = 40, max_world_stamina = 100, has_spirit_core = true
        };
        _character = new Character(); AddChild(_character); _character.load_from_data(data); _character.Hide();
        _character.grant_intro_gongfa(GD.Load<GongfaResource>("res://resources/gongfa/黄庭养形篇.tres"));
        _character.add_item_to_inventory("res://resources/entities/items/灵石.tres", 12);
        _character.add_item_to_inventory("res://resources/entities/items/兽皮.tres", 3);
        _merchant = new WorldNpcData { npc_id = "material_preview", npc_name = "周满仓", formal_identity = "青溪里村民", cultivation_realm = "凡人" };
        _merchant.trade_inventory["res://resources/entities/items/兽骨.tres"] = 4;
        _merchant.trade_inventory["res://resources/entities/items/黄庭养形篇秘籍.tres"] = 1;
        _room = new CultivationUI(); AddChild(_room);
        _rest = new RestPanel(); AddChild(_rest);
        _trade = new TradePanel(); AddChild(_trade);
        ShowPage(0);
        if (!OS.GetCmdlineUserArgs().Contains("--capture-material-pages")) return;
        for (var page = 0; page < 6; page++)
        {
            ShowPage(page); await Capture($"0{page + 1}_{new[] { "吐纳", "研读", "突破", "淬体", "观法", "灵感" }[page]}.png");
        }
        ShowPage(0); Press(_room, "指定日期"); await Capture("07_吐纳日期.png");
        ShowPage(6); await Capture("08_休息白天.png");
        Press(_rest, "指定日期"); await Capture("09_休息日期.png");
        _clock.hour = 22; _rest.Refresh(); await Capture("10_休息夜晚.png");
        _rest.Open(_character, _clock, "青溪里 · 李宅", _ => { }, 8, "安心歇息，明日清晨再启程。");
        await Capture("11_剧情休息.png");
        ShowPage(7); await Capture("12_交易.png");
        var slot = Descendants<StorageItemSlot>(_trade).First(item => !item.is_empty && item.item_count > 1);
        slot.EmitSignal(StorageItemSlot.SignalName.clicked, slot); await Capture("13_交易选取数量.png");
        slot.EmitSignal(StorageItemSlot.SignalName.info_requested, slot, true); await Capture("14_交易悬停.png");
        _character.cultivation_realm = "云息十层"; ShowPage(0); await Capture("15_归元九息.png");
        _room.show_cultivation_overview(_character); await Capture("16_修行总览.png");
        GetTree().Quit();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        var index = (int)key.Keycode - (int)Key.Key1;
        if (index is >= 0 and <= 7) ShowPage(index);
        if (key.Keycode == Key.Escape) GetTree().Quit();
    }
    private void ShowPage(int page)
    {
        _room.close_panel(); _rest.Close(); _trade.close();
        if (page < 6)
        {
            _room.show_entity(_character);
            Press(_room, new[] { "吐纳修行", "功法研读", "闭关突破", "修命·淬体", "修性·观法", "灵感·瓶颈" }[page]);
        }
        else if (page == 6) _rest.Open(_character, _clock, "青溪里 · 李宅", _ => { });
        else _trade.show_for(_character, _merchant);
    }
    private static IEnumerable<T> Descendants<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Press(Node parent, string text) => Descendants<Button>(parent).First(button => button.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    private async System.Threading.Tasks.Task Capture(string name)
    {
        for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(_captureFolder + name));
    }
}
