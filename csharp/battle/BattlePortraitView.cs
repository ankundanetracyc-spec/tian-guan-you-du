using Godot;

/// <summary>共用人物立绘渲染器裁切头像；不重新抽取外观，不使用示意头像。</summary>
public partial class BattlePortraitView : Control
{
    private BattleEntityData _data=null!;
    public void Setup(BattleEntityData data) { _data=data; CallDeferred(nameof(LoadPortrait)); }
    public static EntityData Capture(EntityData source)
    {
        EntityData copy=source is HumanData human?new HumanData{gender=human.gender}:new EntityData();
        foreach(var property in source.GetPropertyList())
        {
            var name=property["name"].AsString();
            if(name.StartsWith("appearance_") || name is "faction_name" or "battle_portrait_path" or "icon_path")copy.Set(name,source.Get(name));
        }
        return copy;
    }
    private void LoadPortrait()
    {
        if(IsQueuedForDeletion())return;
        ClipContents=true;MouseFilter=MouseFilterEnum.Ignore;
        if(_data.entity_type==(int)WorldEntity.EntityType.CHARACTER && _data.portrait_data is HumanData source)
        {
            var data=(HumanData)source.Duplicate(true);
            var gender=data.gender==1?"female":"male";
            var mods=GetNodeOrNull<AppearanceModCatalog>("/root/AppearanceModCatalog");
            if(mods?.allows_color_tint(gender,"skin")!=true)data.appearance_skin_tone=-1;
            if(mods?.allows_color_tint(gender,"hair")!=true)data.appearance_hair_color=-1;
            var catalog=GetNodeOrNull<PortraitCatalog>("/root/PortraitCatalog");
            if(catalog is not null)
            {
                var renderer=new PortraitRenderer{Size=Size,MouseFilter=MouseFilterEnum.Ignore};
                renderer.set_headshot_mode(true);AddChild(renderer);renderer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                var requested=catalog.get_requested_appearance(data);
                if(data.appearance_hair_back_style<0)requested["hair_back"]="";
                renderer.configure(data,catalog.get_context(data,gender,_data.current_age,data.faction_name,_data.cultivation_realm),requested);
                if(renderer.has_required_layers())return;
                renderer.Hide();renderer.QueueFree();
            }
        }
        var path=_data.battle_portrait_path;
        if (_data.entity_type == (int)WorldEntity.EntityType.CHARACTER)
            path = _data.gender == 1 ? BattlePaperDoll.FemaleStaticPortraitPath : BattlePaperDoll.DefaultPortraitPath;
        var wolf=_data.entity_type==(int)WorldEntity.EntityType.BEAST && (_data.entity_name.Contains("狼") || path==BattlePaperDoll.ForestWolfPortraitPath);
        if(wolf)path=BattlePaperDoll.ForestWolfPortraitPath;
        // 旧测试野猪引用人形皮影，不能把它当作真实兽类头像。
        if(_data.entity_type==(int)WorldEntity.EntityType.BEAST && BattlePaperDoll.IsHumanPlaceholderPath(path))path="";
        if(!string.IsNullOrEmpty(path)&&ResourceLoader.Exists(path))
        {
            var texture=GD.Load<Texture2D>(path);
            if(texture is not null)
            {
                var bounds=texture.GetImage().GetUsedRect();
                var beast=_data.entity_type==(int)WorldEntity.EntityType.BEAST;
                var region=beast?new Rect2(Vector2.Zero,texture.GetSize()):new Rect2(bounds.Position,new Vector2(bounds.Size.X,Mathf.Min(bounds.Size.Y,bounds.Size.X)));
                var image=new TextureRect{Texture=new AtlasTexture{Atlas=texture,Region=region},Size=Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};AddChild(image);return;
            }
        }
        var missing=new Label{Text="暂无立绘",Size=Size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};
        missing.AddThemeFontSizeOverride("font_size",12);missing.AddThemeColorOverride("font_color",BattleUiArt.Text);AddChild(missing);
    }
}
