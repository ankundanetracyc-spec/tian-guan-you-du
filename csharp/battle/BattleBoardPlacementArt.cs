using Godot;

/// <summary>整份占形的边框、短刻度与完整图标；拖拽、落点和盘面复用。</summary>
internal static class BattleBoardPlacementArt
{
    internal static void Draw(CanvasItem target,CombatBoardPlacement item,CombatBoardCatalog catalog,Rect2 rect,float cellSize,
        GodotObject? owner,BattleManager? manager,bool dim,bool selected,bool armed,float scale=1,Color? tint=null,Color? border=null)
    {
        var color=tint??Colors.White;
        var line=border??(selected?new Color("a15d3d"):armed?new Color("648a87"):new Color("726653"));
        var local=new Rect2(-rect.Size/2,rect.Size);
        target.DrawSetTransform(rect.GetCenter(),0,Vector2.One*scale);
        target.DrawRect(local.Grow(-1),new Color(.20f,.17f,.12f,.07f));
        target.DrawRect(local.Grow(-1),line,false,selected||armed?2.5f:1.8f);
        // 只沿占形边缘留短刻度，内部不贯穿袍袖或气流。
        var tick=Mathf.Clamp(cellSize*.075f,3,6);
        for(var x=1;x<Mathf.RoundToInt(rect.Size.X/cellSize);x++)
        {
            var at=local.Position.X+x*cellSize;
            target.DrawLine(new(at,local.Position.Y+1),new(at,local.Position.Y+tick),line,1.2f);
            target.DrawLine(new(at,local.End.Y-tick),new(at,local.End.Y-1),line,1.2f);
        }
        for(var y=1;y<Mathf.RoundToInt(rect.Size.Y/cellSize);y++)
        {
            var at=local.Position.Y+y*cellSize;
            target.DrawLine(new(local.Position.X+1,at),new(local.Position.X+tick,at),line,1.2f);
            target.DrawLine(new(local.End.X-tick,at),new(local.End.X-1,at),line,1.2f);
        }
        var path=item.kind switch
        {
            CombatBoardPlacement.ObjectKind.Skill=>catalog.FindSkill(item.source_key)?.icon_path??"",
            CombatBoardPlacement.ObjectKind.Rewrite=>catalog.FindRewrite(item.source_key)?.icon_path??"",
            CombatBoardPlacement.ObjectKind.Pill=>BattleSupplyRules.Load(item.source_key)?.icon_path??"",
            CombatBoardPlacement.ObjectKind.Talisman=>TalismanCombatCatalog.Load(item.source_key)?.icon_path??"",
            _=>"res://textures/ui/combat/board_skill_icons/zhoutian_indicator.png"
        };
        var copy=false;
        if(item.kind==CombatBoardPlacement.ObjectKind.Skill&&catalog.FindSkill(item.source_key)is { skill_name:"复命" } fuming)
        {
            var display=FumingPresentation.Resolve(owner,fuming,manager);
            if(!ReferenceEquals(display,fuming)){path=display.icon_path;copy=true;}
        }
        if(BattleBoardView.Icon(path)is {} icon)
        {
            var rotated=item.kind is CombatBoardPlacement.ObjectKind.Skill or CombatBoardPlacement.ObjectKind.Pill or CombatBoardPlacement.ObjectKind.Talisman&&item.quarter_turns%2!=0;
            var draw=local.Grow(-3);
            if(rotated)
            {
                target.DrawSetTransform(rect.GetCenter(),Mathf.Pi/2,Vector2.One*scale);
                draw=new Rect2(-new Vector2(rect.Size.Y,rect.Size.X)/2,new Vector2(rect.Size.Y,rect.Size.X)).Grow(-3);
            }
            if(copy)
            {
                var ratio=Mathf.Min(draw.Size.X/icon.GetWidth(),draw.Size.Y/icon.GetHeight());
                var size=icon.GetSize()*ratio;draw=new Rect2(draw.GetCenter()-size/2,size);
            }
            var imageTint=color;imageTint.A*=dim?.35f:1;
            target.DrawTextureRect(icon,draw,false,imageTint);
        }
        if(item.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator)
        {
            target.DrawSetTransform(rect.GetCenter(),0,Vector2.One*scale);
            var direction=(Vector2)item.get_rewrite_offset();var end=direction*cellSize*.36f;
            var normal=new Vector2(-direction.Y,direction.X);var arrow=new Color("845b35");
            target.DrawLine(Vector2.Zero,end,arrow,2);
            target.DrawLine(end,end-direction*7+normal*4,arrow,2);
            target.DrawLine(end,end-direction*7-normal*4,arrow,2);
        }
        target.DrawSetTransform(rect.GetCenter(),0,Vector2.One*scale);
        BattleBoardSkin.DrawItemFrame(target,local.Grow(-2),cellSize*7/110,new Color(.76f,.78f,.76f,color.A));
        if(selected||armed||border is not null)target.DrawRect(local.Grow(-1),line,false,selected||armed?2.5f:1.8f);
        target.DrawSetTransform(Vector2.Zero);
    }
}
