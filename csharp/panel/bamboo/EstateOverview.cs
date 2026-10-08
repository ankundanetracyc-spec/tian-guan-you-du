using Godot;
using Godot.Collections;

public partial class EstateOverview : Control
{
    public event Action<string>? SlipOpened;
    public event Action<string>? ResidentSelected;
    public event Action? BackHillTrialRequested;
    public event Action? MeditationRequested;
    public event Action? RestRequested;
    public event Action<string>? TaskRequested;
    private readonly Dictionary _unlocked=[], _buttons=[]; private int _stage; private bool _back_hill_trial_available, _meditation_available, _rest_available, _residents_expanded=true; private string _guided="", _opened_location=""; private Control? _layer, _location_page, _quest_panel, _quest_frame; private Label? _label, _location_title; private TextureRect? _background, _location_background; private VBoxContainer? _residents, _task_list; private Button? _location_action, _meditation_action, _rest_action, _resident_toggle;
    private static readonly (string id,string name,Vector2 pos)[] Features=[("li_residence","李宅",new(.39f,.44f)),("village","槐荫坪",new(.65f,.44f)),("farmland","田陌",new(.72f,.66f)),("back_hill","后山",new(.66f,.22f))];
    private static readonly (string id,string location,string name,string identity,string realm)[] Residents=[("li_head","li_residence","李守拙","里正","云息六层"),("shopkeeper","village","周满仓","掌柜","凡人"),("farmer","farmland","陈素秋","田农","凡人"),("watchman","village","吴承安","更夫","凡人")];
    public override void _Ready(){MouseFilter=MouseFilterEnum.Pass;_layer=GetNodeOrNull<Control>("FeatureLayer");_label=GetNodeOrNull<Label>("StageLabel");_background=GetNodeOrNull<TextureRect>("Background");_location_page=GetNodeOrNull<Control>("LocationPage");_location_background=GetNodeOrNull<TextureRect>("LocationPage/Background");_location_title=GetNodeOrNull<Label>("LocationPage/LocationTitle");GetNodeOrNull<Control>("LocationPage/ResidentsPanel")?.Hide();_location_action=GetNodeOrNull<Button>("LocationPage/LocationAction");_meditation_action=GetNodeOrNull<Button>("LocationPage/MeditationAction");_rest_action=GetNodeOrNull<Button>("LocationPage/RestAction");_quest_panel=GetNodeOrNull<Control>("QuestPanel");_quest_frame=GetNodeOrNull<Control>("QuestFrameArt");_task_list=GetNodeOrNull<VBoxContainer>("QuestPanel/Margin/VBox/TaskList");if(GetNodeOrNull<Button>("LocationPage/BackButton") is { } back)back.Pressed+=close_location;if(_location_action is not null)_location_action.Pressed+=open_location_action;if(_meditation_action is not null)_meditation_action.Pressed+=()=>MeditationRequested?.Invoke();if(_rest_action is not null)_rest_action.Pressed+=()=>RestRequested?.Invoke();Resized+=Layout;SetStageArt();Rebuild();set_quests([]);}
    public void set_unlocked_slips(Array<string> ids){_unlocked.Clear();foreach(var id in ids)_unlocked[id]=true;Rebuild();}
    public override void _Process(double delta){var inResidence=_opened_location=="li_residence"&&_location_page?.Visible==true;if(_meditation_action is not null)_meditation_action.Visible=_meditation_available&&inResidence;if(_rest_action is not null)_rest_action.Visible=_rest_available&&inResidence;}
    public void set_village_stage(int stage){_stage=stage;SetStageArt();} public void set_guided_slip(string id){_guided=id;StyleGuided();}
    public void set_back_hill_trial_available(bool available){_back_hill_trial_available=available;if(_location_action is not null)_location_action.Visible=_opened_location=="back_hill"&&available;}
    public void set_meditation_available(bool available){_meditation_available=available;}
    public void set_rest_available(bool available){_rest_available=available;}
    public void open_location_view(string location){open_location(location);}
    public void set_quests(IEnumerable<JournalPanel.Entry> tasks)
    {
        if (_task_list is null) return;
        foreach (var child in _task_list.GetChildren()) child.QueueFree();
        foreach (var task in tasks.Take(2))
        {
            var preview = task.Body.Length > 29 ? task.Body[..26] + "…" : task.Body;
            var button = new Button
            {
                Text = $"{task.Title}　·　{task.State}\n{preview}",
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 48),
                TooltipText = "点击查看任务详情"
            };
            button.AddThemeFontSizeOverride("font_size", 15);
            button.AddThemeColorOverride("font_color", new Color("30291f"));
            button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(.91f, .86f, .72f, .94f), BorderColor = new Color(.45f, .34f, .18f, .78f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6
            });
            button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(.98f, .92f, .77f, .98f), BorderColor = new Color(.65f, .49f, .23f, .95f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 12, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6
            });
            button.Pressed += () => TaskRequested?.Invoke(task.Title);
            _task_list.AddChild(button);
        }
        CallDeferred(nameof(refresh_quest_layout));
    }
    private void refresh_quest_layout(){if(_quest_panel is null)return;const float width=500f;const float height=220f;_quest_panel.AnchorTop=.25f;_quest_panel.AnchorBottom=.25f;_quest_panel.OffsetLeft=-width;_quest_panel.OffsetRight=-18;_quest_panel.OffsetTop=0;_quest_panel.OffsetBottom=height;if(_quest_frame is not null){_quest_frame.AnchorTop=.25f;_quest_frame.AnchorBottom=.25f;_quest_frame.OffsetLeft=-width-28;_quest_frame.OffsetRight=8;_quest_frame.OffsetTop=0;_quest_frame.OffsetBottom=height+22;}if(_quest_panel.GetNodeOrNull<MarginContainer>("Margin") is { } margin){margin.AddThemeConstantOverride("margin_left",50);margin.AddThemeConstantOverride("margin_top",69);margin.AddThemeConstantOverride("margin_right",28);margin.AddThemeConstantOverride("margin_bottom",16);}if(_quest_panel.FindChild("Header",true,false) is Label header)header.Hide();if(_task_list is not null)_task_list.AddThemeConstantOverride("separation",6);}
    private void SetStageArt(){if(_background is not null)_background.Texture=GD.Load<Texture2D>("res://textures/village_overview/qingxi_territory_overview_v5.png");if(_label is not null)_label.Text="青溪里";}
    private void Rebuild(){if(_layer is null)return;foreach(var child in _layer.GetChildren())child.QueueFree();_buttons.Clear();foreach(var feature in Features)if(_unlocked.ContainsKey(feature.id)){var button=new Button{Text=string.Join("\n",feature.name.ToCharArray()),CustomMinimumSize=new Vector2(54,132),TooltipText="进入"+feature.name};button.AddThemeFontSizeOverride("font_size",18);button.AddThemeColorOverride("font_color",new Color("31291f"));var normal=new StyleBoxFlat{BgColor=new Color("eee5d3d9"),BorderColor=new Color("655a47"),BorderWidthLeft=1,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=1,CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6};var hover=normal.Duplicate() as StyleBoxFlat;hover!.BgColor=new Color("fff6dfff");button.AddThemeStyleboxOverride("normal",normal);button.AddThemeStyleboxOverride("hover",hover);var residentCount=Residents.Count(item=>item.location==feature.id);if(residentCount>0){var badge=new Label{Text=residentCount.ToString(),Position=new Vector2(34,-8),Size=new Vector2(25,25),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};badge.AddThemeFontSizeOverride("font_size",14);badge.AddThemeColorOverride("font_color",Colors.White);badge.AddThemeStyleboxOverride("normal",new StyleBoxFlat{BgColor=new Color(.55f,.18f,.15f,.96f),CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12});button.AddChild(badge);}button.Pressed+=()=>{open_location(feature.id);SlipOpened?.Invoke(feature.id);};_layer.AddChild(button);_buttons[feature.id]=new Dictionary{{"button",button},{"pos",feature.pos}};}Layout();StyleGuided();}
    private void open_location(string location){if(_location_page is null)return;hide_world_roster_overlay();_opened_location=location;var here=Residents.Where(item=>item.location==location).ToArray();show_shared_residents(here,location);var locationName=Features.FirstOrDefault(item=>item.id==location).name;if(_location_title is not null)_location_title.Text=locationName;if(_location_action is not null){_location_action.Visible=location=="back_hill"&&_back_hill_trial_available;_location_action.Text="进入后山试炼";}if(_location_background is not null){var backgrounds=new System.Collections.Generic.Dictionary<string,string>{{"li_residence","res://textures/village_overview/qingxi_li_residence_v3.png"},{"village","res://textures/village_overview/qingxi_gathering_spot_v1.png"},{"farmland","res://textures/village_overview/qingxi_farmland_v2.png"},{"back_hill","res://textures/village_overview/qingxi_back_hill_v2.png"}};_location_background.Texture=GD.Load<Texture2D>(backgrounds.GetValueOrDefault(location,"res://textures/village_overview/qingxi_li_residence_v3.png"));}_location_page.Show();_location_page.MoveToFront();_quest_frame?.MoveToFront();_quest_panel?.MoveToFront();}
    private void show_shared_residents((string id,string location,string name,string identity,string realm)[] residents,string location){var roster=GetTree().GetFirstNodeInGroup("cell_character_list") as CellCharacterList;if(roster is null)return;var rows=new List<(string label,Action action)>();foreach(var resident in residents){var captured=resident;var display=string.IsNullOrWhiteSpace(captured.identity)?captured.name:$"{captured.name}（{captured.identity}）";rows.Add(($"{display}　·　{captured.realm}",()=>ResidentSelected?.Invoke(captured.id)));}roster.show_action_rows(rows,$"qingxi:{location}");}
    private void toggle_residents(){_residents_expanded=!_residents_expanded;apply_resident_expanded_state();}
    private void apply_resident_expanded_state(){if(_resident_toggle is not null){_resident_toggle.Text=_residents_expanded?"▼":"▲";_resident_toggle.TooltipText=_residents_expanded?"收起角色列表":"展开角色列表";}if(_residents is not null){foreach(var child in _residents.GetChildren())if(child is Label label)label.Hide();_residents.Visible=_residents_expanded;}}
    private void hide_world_roster_overlay(){foreach(var item in GetTree().GetNodesInGroup("cell_character_list")){if(item is Control control)control.Hide();}}
    private static StyleBoxFlat transparent_button_style(Color? background=null){return new StyleBoxFlat{BgColor=background??new Color(0,0,0,0)};}
    private void close_location(){_location_page?.Hide();hide_world_roster_overlay();}
    private void open_location_action(){if(_opened_location!="back_hill")return;close_location();BackHillTrialRequested?.Invoke();}
    private void Layout(){if(_layer is null)return;foreach(var value in _buttons.Values){var entry=value.AsGodotDictionary();if(entry["button"].AsGodotObject() is Control button){var p=entry["pos"].AsVector2();button.Position=new Vector2(_layer.Size.X*p.X-button.Size.X*.5f,_layer.Size.Y*p.Y-button.Size.Y*.5f);}}}
    private void StyleGuided(){foreach(var key in _buttons.Keys){var entry=_buttons[key].AsGodotDictionary();if(entry["button"].AsGodotObject() is Button button)button.Modulate=key.AsString()==_guided?new Color("fff7ce"):Colors.White;}}
}
