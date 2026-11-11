using Godot;

public static class CombatBoardOwnedSources
{
    public sealed record Entry(string Key, string Category, string Name, string Source, string Detail,
        string Icon, Vector2I Shape, bool Configurable, HoverInfoManager.HoverInfoContent Hover,string Keywords="")
    {
        public bool Matches(string query)=>query.Length==0||$"{Name} {Source} {Keywords}".Contains(query,StringComparison.OrdinalIgnoreCase);
    }
    public static List<Entry> Read(Character actor)
    {
        var catalog = CombatBoardPreparationService.Catalog(actor); var result = new List<Entry>();
        var learnedChapters = actor.get_known_gongfas().SelectMany(book => book.get_sorted_chapters()
            .Where(chapter => actor.is_gongfa_chapter_learned(book, chapter.get_id()))
            .SelectMany(chapter => chapter.get_learned_skills(book == actor.main_gongfa)).OfType<ActiveSkillData>());
        var skills = catalog.skills.Concat(learnedChapters).Concat(actor.active_skills.OfType<ActiveSkillData>()).Concat(actor.get_unlocked_skill_node_paths()
            .Concat((actor.entity_data as HumanData)?.legacy_learned_combat_skill_paths ?? [])
            .Where(path => ResourceLoader.Exists(path)).Select(path => ResourceLoader.Load<ActiveSkillData>(path)).OfType<ActiveSkillData>());
        foreach (var skill in skills.Where(s => s.skill_type != ActiveSkillData.SkillType.POWER)
            .DistinctBy(s => s.has_board_definition() ? s.get_board_skill_key() : Character.combat_skill_key(s)))
        {
            var key = skill.has_board_definition() ? skill.get_board_skill_key() : Character.combat_skill_key(skill);
            var original = catalog.FindSkill(key) ?? skill; var available = catalog.FindSkill(key) is not null;
            var shape = original.has_board_definition() ? original.get_board_footprint(0) : Vector2I.Zero;
            var info = original.has_board_definition() ? CombatBoardPresentation.SkillHover(original, actor)
                : new HoverInfoManager.HoverInfoContent(original.skill_name, CardPresentation.Face(original, actor));
            result.Add(new(key,"招式",original.skill_name,string.IsNullOrWhiteSpace(original.source_gongfa_name)?"已学招式":original.source_gongfa_name,
                $"消耗：体力{original.energy_cost}"+(original.mana_cost>0?$"、法力{original.mana_cost}":""),original.icon_path,shape,available,info,string.Join(" ",original.get_presentation_tags())));
        }
        foreach (var choice in actor.get_skill_rewrite_choices().Where(c => c.BoardDefinitionVersion > 0 && c.LegacyArtifact is null))
            result.Add(new(choice.Id,"化法",choice.Title,choice.Source,"已学会"+(choice.Available?"":" · 当前未启用"),choice.BoardIconPath,
                Vector2I.One,catalog.FindRewrite(choice.Id)is not null,new(choice.Title,choice.Description,Load(choice.BoardIconPath),choice.Source)));
        foreach (var path in actor.pack_items.Distinct())
        {
            var count=actor.get_inventory_total_count(path); if(count<=0||!ResourceLoader.Exists(path))continue;
            var item=ResourceLoader.Load<ItemData>(path); if(item is null)continue;
            var category=item is PillItemData||item.item_category=="丹药"?"丹药":item is TalismanProductItemData||item.item_category=="符箓"?"符箓":"";
            if(category.Length==0)continue;
            var pill=BattleSupplyRules.Load(path);var talisman=TalismanCombatCatalog.Load(path);
            var info=pill is not null?AlchemyPillVisual.Hover(pill,user:actor):talisman is not null?TalismanCombatCatalog.Hover(talisman):new(item.entity_name,item.description,Load(item.icon_path));
            result.Add(new(path,category,item.entity_name,"行囊",$"持有{count}{(category=="丹药"?"颗":"张")}",item.icon_path,
                pill?.get_inventory_size()??talisman?.get_inventory_size()??Vector2I.Zero,pill is not null||talisman is not null,info));
        }
        if(actor.entity_data is HumanData human)
            foreach(var artifact in human.artifact_inventory.Where(a=>a is not null))
                result.Add(new("artifact:"+artifact.instance_id,"法宝",artifact.artifact_name,"行囊","持有1件 · 暂不可配招",artifact.form?.icon_path??"",
                    Vector2I.Zero,false,new(artifact.artifact_name,$"{artifact.artifact_tier} · {artifact.form?.form_name??"法宝"}",Load(artifact.form?.icon_path??""))));
        return result;
    }
    public static Texture2D? Load(string path)=>path.Length>0&&ResourceLoader.Exists(path)?ResourceLoader.Load<Texture2D>(path):null;
}

/// <summary>只读占形图，始终取真实定义，不提供配置资格或结算。</summary>
public partial class CombatBoardOwnedIcon : Control
{
    public Texture2D? Icon; public Vector2I Shape;
    public override void _Draw()
    {
        var area=new Rect2(Vector2.Zero,Size);
        if(Shape.X>0&&Shape.Y>0)
        {
            var cell=Math.Min(Size.X/Shape.X,Size.Y/Shape.Y);
            area=new Rect2((Size-(Vector2)Shape*cell)/2,(Vector2)Shape*cell);
            DrawTextureRect(BattleBoardSkin.Part("cell_face"),area,false);
            if(Icon is not null)DrawTextureRect(Icon,area.Grow(-4),false);
            BattleBoardSkin.DrawItemFrame(this,area,2.5f);
        }
        else if(Icon is not null)DrawTextureRect(Icon,area,false);
    }
}
