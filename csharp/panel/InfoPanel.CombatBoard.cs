using Godot;

public partial class InfoPanel
{
    private CharacterCombatBoardPage? _combatBoardPage;
    private void build_character_battle(Creature unit)
    {
        if(unit is not Character actor){_combatBoardPage=null;build_character_legacy_battle(unit);return;}
        clear_skill_rewrite_drag_state();var page=_characterPages[2];CharacterClear(page);
        _characterSkillList=null;_characterSkillSlots=null;_characterSkillRewritePanel=null;_showSkillNodeNetwork=false;
        _combatBoardPage=new();page.AddChild(_combatBoardPage);CharacterRect(_combatBoardPage,0,0,1382,795);
        _combatBoardPage.Setup(actor,_npc_card is null&&CombatBoardPreparationService.CanManage(actor),CharacterPaper(padding:26));
    }
}
