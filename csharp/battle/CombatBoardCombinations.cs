using Godot;

/// <summary>局部配置的坐标变换；只处理占形，不支付费用或改战斗运行态。</summary>
internal static class CombatBoardCombinations
{
    internal static CombatBoardLayout Normalize(CombatBoardLayout saved,CombatBoardCatalog catalog)
    {
        var layout=saved.CopyConfiguration();
        if(layout.placements.Count==0){layout.size=Vector2I.One;layout.unlocked_cells=[Vector2I.Zero];return layout;}
        var origin=new Vector2I(layout.placements.Min(p=>p.cell.X),layout.placements.Min(p=>p.cell.Y));
        foreach(var item in layout.placements)item.cell-=origin;
        var cells=layout.placements.SelectMany(p=>CombatBoardGeometry.Cells(p,CombatBoardGeometry.Footprint(p,catalog.FindSkill))).ToArray();
        if(cells.Length==0){layout.size=Vector2I.One;layout.unlocked_cells=[Vector2I.Zero];return layout;}
        layout.size=new(Math.Max(1,cells.Max(c=>c.X)+1),Math.Max(1,cells.Max(c=>c.Y)+1));
        layout.unlocked_cells=new(Enumerable.Range(0,layout.size.Y).SelectMany(y=>Enumerable.Range(0,layout.size.X).Select(x=>new Vector2I(x,y))));
        return layout;
    }

    internal static CombatBoardLayout Rotate(CombatBoardLayout saved,CombatBoardCatalog catalog)
    {
        var layout=Normalize(saved,catalog);var height=layout.size.Y;
        bool Rotatable(CombatBoardPlacement p)=>p.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator
            ||p.kind==CombatBoardPlacement.ObjectKind.Skill&&catalog.FindSkill(p.source_key)?.board_rotation_allowed==true
            ||p.kind==CombatBoardPlacement.ObjectKind.Pill&&BattleSupplyRules.Load(p.source_key)?.inventory_can_rotate==true
            ||p.kind==CombatBoardPlacement.ObjectKind.Talisman&&TalismanCombatCatalog.Load(p.source_key)?.inventory_can_rotate==true;
        if(layout.placements.Any(p=>{var shape=CombatBoardGeometry.Footprint(p,catalog.FindSkill);return shape.X!=shape.Y&&!Rotatable(p);}))return layout;
        foreach(var p in layout.placements)
        {
            var shape=CombatBoardGeometry.Footprint(p,catalog.FindSkill);p.cell=new(height-p.cell.Y-shape.Y,p.cell.X);
            if(p.kind is CombatBoardPlacement.ObjectKind.Rewrite or CombatBoardPlacement.ObjectKind.ZhouTianIndicator)p.rewrite_direction=(p.rewrite_direction+1)%4;
            else if(Rotatable(p))p.quarter_turns=(p.quarter_turns+1)%2;
        }
        return Normalize(layout,catalog);
    }
}
