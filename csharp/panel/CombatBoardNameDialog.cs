using Godot;

/// <summary>等高按钮、固定标签列和伸展输入；长名称只在输入框内滚动，提示由正文独立滚动。</summary>
public partial class CombatBoardNameDialog : PopupPanel
{
    public LineEdit NameInput=null!;
    private Label _error=null!,_hint=null!;
    private VBoxContainer _body=null!;private Button _confirm=null!;
    private Func<string,bool,string?> _submit=null!;private Func<string,bool>? _exists;
    private bool _rename;
    public void Open(string title,string caption,string initial,bool rename,Func<string,bool,string?> submit,Func<string,bool>? exists=null)
    {
        Name="CombatBoardNameDialog";Transient=true;Exclusive=true;TransparentBg=true;Borderless=true;_submit=submit;_exists=exists;_rename=rename;
        AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        var paper=new PanelContainer{Theme=CraftingPaperStyle.MakeTheme()};AddChild(paper);paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        paper.AddThemeStyleboxOverride("panel",CraftingPaperStyle.Paper(padding:32,brightness:1));
        var root=new VBoxContainer();root.AddThemeConstantOverride("separation",20);paper.AddChild(root);
        var heading=new HBoxContainer();heading.AddThemeConstantOverride("separation",24);root.AddChild(heading);heading.AddChild(CombatBoardUi.Text(title,26));
        var close=CombatBoardUi.Button("×",Hide,font:25);close.Name="CloseNameDialog";close.CustomMinimumSize=new(44,44);heading.AddChild(close);
        var scroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};root.AddChild(scroll);
        _body=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};_body.AddThemeConstantOverride("separation",16);scroll.AddChild(_body);
        var field=new HBoxContainer();field.AddThemeConstantOverride("separation",16);_body.AddChild(field);
        var label=CombatBoardUi.Text(caption,21);label.CustomMinimumSize=new(88,48);label.SizeFlagsHorizontal=Control.SizeFlags.Fill;label.VerticalAlignment=VerticalAlignment.Center;field.AddChild(label);
        NameInput=new LineEdit{Name="PresetNameInput",Text=initial,CustomMinimumSize=new(0,48),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};CraftingPaperStyle.ApplyInput(NameInput,21,16);field.AddChild(NameInput);
        _error=CombatBoardUi.Text("",18,wrap:true);_error.Hide();_error.AddThemeColorOverride("font_color",new Color("985344"));_body.AddChild(_error);
        _hint=CombatBoardUi.Text("",18,true,true);_body.AddChild(_hint);
        root.AddChild(new HSeparator());var footer=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.End};footer.AddThemeConstantOverride("separation",14);root.AddChild(footer);
        var back=CombatBoardUi.Button("返回",Hide,font:21);back.Name="CancelNameDialog";back.CustomMinimumSize=new(132,48);footer.AddChild(back);
        _confirm=CombatBoardUi.Button("",Confirm,true,21);_confirm.Name="ConfirmPresetName";_confirm.CustomMinimumSize=new(132,48);footer.AddChild(_confirm);
        NameInput.TextChanged+=_=>{_error.Hide();UpdateHint();};NameInput.TextSubmitted+=_=>Confirm();
        PopupHide+=()=>QueueFree();CloseRequested+=Hide;UpdateHint();
        var view=GetTree().Root.Size;PopupCentered(new(Math.Min(720,view.X-32),Math.Min(350,view.Y-32)));NameInput.GrabFocus();if(rename)NameInput.SelectAll();
    }
    private void UpdateHint()
    {
        var overwrite=!_rename&&_exists?.Invoke(NameInput.Text.Trim())==true;
        _hint.Text=_rename?"只修改这项记录的名称。":overwrite?"已有同名记录，确认后将覆盖它。":"保存当前配置的位置、朝向与来源。";
        _confirm.Text=_rename?"确认改名":overwrite?"确认覆盖":"确认保存";
    }
    private void Confirm()
    {
        var name=NameInput.Text.Trim();var reason=name.Length==0?"请填写名称。":_submit(name,!_rename&&_exists?.Invoke(name)==true);
        if(reason is not null){_error.Text=reason;_error.Show();return;}Hide();
    }
}
