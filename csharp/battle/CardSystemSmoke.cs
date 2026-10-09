using Godot;
using Godot.Collections;
using System.Reflection;

/// <summary>独立场景回归：真实资源、公式、手牌时序及应对结算。</summary>
public partial class CardSystemSmoke : Node
{
    private int checks;
    public override void _Ready()=>CallDeferred(nameof(Run));
    private void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    public async void Run()
    {
        try
        {
            var manager=new BattleManager();AddChild(manager);
            var bodyDeck=GD.Load<BasicDeckResource>("res://resources/basic_decks/锻躯路.tres") ?? throw new Exception("锻躯基础技能库资源");
            var bodyPaths=bodyDeck.get_deck_skills();
            Check(bodyPaths.Count==13,"锻躯基础技能库总张数");
            Check(bodyPaths.Count(path=>path=="res://resources/activeskills/发劲.tres")==5,"锻躯基础技能库发劲数量");
            Check(bodyPaths.Count(path=>path=="res://resources/activeskills/催血.tres")==2,"锻躯基础技能库催血数量");
            Check(bodyPaths.Count(path=>path=="res://resources/activeskills/平息.tres")==1,"锻躯基础技能库平息数量");
            Check(bodyPaths.Count(path=>path=="res://resources/activeskills/卸力.tres")==4,"锻躯基础技能库卸力数量");
            Check(bodyPaths.Count(path=>path=="res://resources/activeskills/撼山.tres")==1,"锻躯基础技能库撼山数量");
            var bodyTiming=new[]{
                (Path:"res://resources/activeskills/发劲.tres",Name:"发劲",Energy:1,Preparation:144,Recovery:96,Cooldown:240,Target:2,Category:"active"),
                (Path:"res://resources/activeskills/催血.tres",Name:"催血",Energy:1,Preparation:144,Recovery:96,Cooldown:360,Target:0,Category:"active"),
                (Path:"res://resources/activeskills/平息.tres",Name:"平息",Energy:1,Preparation:144,Recovery:96,Cooldown:360,Target:0,Category:"active"),
                (Path:"res://resources/activeskills/卸力.tres",Name:"卸力",Energy:1,Preparation:96,Recovery:48,Cooldown:240,Target:0,Category:"response"),
                (Path:"res://resources/activeskills/撼山.tres",Name:"撼山",Energy:3,Preparation:480,Recovery:192,Cooldown:720,Target:2,Category:"active")
            };
            foreach(var expected in bodyTiming)
            {
                var skill=GD.Load<ActiveSkillData>(expected.Path) ?? throw new Exception(expected.Name+"资源");
                Check(skill.skill_name==expected.Name,expected.Name+"显示名");
                Check(skill.energy_cost==expected.Energy,expected.Name+"精力费用");
                Check(skill.mana_cost==0&&skill.spirit_occupy==0,expected.Name+"法力与灵识费用");
                Check(skill.preparation_frames==expected.Preparation&&skill.recovery_frames==expected.Recovery,expected.Name+"准备收招时长");
                Check(skill.cooldown_frames==expected.Cooldown,expected.Name+"冷却时长");
                Check(skill.target_type==expected.Target&&skill.slot_category==expected.Category,expected.Name+"目标与槽位类型");
            }
            var defender=new Creature{entity_data=new HumanData{physique=12,agility=8,body_proficiency=9,max_health=100,current_health=100,max_stamina=6,current_stamina=3},faction=1};AddChild(defender);
            var attacker=new Creature{entity_data=new HumanData{physique=8,max_health=100,current_health=100,max_stamina=6,current_stamina=3},faction=2};AddChild(attacker);
            Check(CardRules.Number(defender,"body_proficiency")==9,"读取角色数据中的造诣");
            var response=GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres").Duplicate() as ActiveSkillData ?? throw new Exception("架招资源");
            Check(CardRules.Block(response,defender)==7,"架招卡表公式");
            var unload=GD.Load<ActiveSkillData>("res://resources/activeskills/卸力.tres");
            Check(CardRules.Block(unload,defender)==9,"卸力卡表公式");
            Check(CardPresentation.Face(response,defender).Contains("7"),"牌面计算数值");
            Check(!CardPresentation.Face(response,defender).Contains("floor"),"牌面无公式");
            Check(CardPresentation.Details(response,defender).Contains("floor"),"右侧化解公式");
            Check(!CardPresentation.Details(response,defender).Contains("精力："),"右侧无费用栏");
            var deck=new BattleDeckManager();AddChild(deck);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            // 回归场景只验证凝核/旧卡牌底座；即时战斗已由固定技能槽接管。
            typeof(BattleManager).GetField("_realtimeMode",flags)?.SetValue(manager,false);
            var decks=(Dictionary)typeof(BattleManager).GetField("_decks",flags)!.GetValue(manager)!;decks[defender]=deck;
            var hands=(Dictionary)typeof(BattleManager).GetField("_hands",flags)!.GetValue(manager)!;
            var attack=GD.Load<ActiveSkillData>("res://resources/activeskills/应敌.tres").Duplicate() as ActiveSkillData ?? throw new Exception("应敌资源");
            hands[defender]=new Array<ActiveSkillData>{attack,response};manager.active_unit=defender;
            manager._finish_current_action(defender);
            Check(manager.hand_cards(defender).Count==2,"结束回合整手保留");
            Check(deck.discard_pile.Count==0,"结束回合不弃牌");
            Check(manager.try_auto_response(defender,attacker),"手牌自动应对");
            Check(defender.current_stamina==2,"应对支付精力");
            Check(defender.get_status_stacks("化解")==7,"应对实际公式层数");
            Check(manager.hand_cards(defender).Count==1&&deck.discard_pile.Contains(response),"应对从手牌移到弃牌");
            deck.draw_pile.Add(response);
            Check(!manager.try_auto_response(defender,attacker),"不得从抽牌堆偷取应对牌");
            hands[defender]=new Array<ActiveSkillData>{response};defender.current_stamina=0;
            Check(!manager.try_auto_response(defender,attacker),"费用不足不应对");
            Check(!CardRules.ResponseMatches(GD.Load<ActiveSkillData>("res://resources/activeskills/避锋.tres"),"soul"),"物理应对条件");
            var hold=new ActiveSkillData{skill_name="测试保留",keywords=["保留"]};
            hands[defender]=new Array<ActiveSkillData>{attack,hold};
            deck.draw_pile.Clear();deck.discard_pile.Clear();for(var i=0;i<6;i++)deck.draw_pile.Add(new ActiveSkillData{skill_name="新牌"});
            var next=(Array<ActiveSkillData>)typeof(BattleManager).GetMethod("StartHand",flags)!.Invoke(manager,[defender,deck])!;
            Check(next.Contains(hold)&&!next.Contains(attack)&&deck.discard_pile.Contains(attack),"下回合弃旧抽新，保留跨清理");
            var ethereal=new ActiveSkillData{skill_name="测试虚无",keywords=["虚无"]};
            Check(deck.discard_hand_at_turn_end([attack,ethereal,response]).Count==3,"兼容接口结束回合整手保留");
            var cleaned=deck.discard_hand_at_turn_start([ethereal,response,hold]);
            Check(cleaned.Count==1&&cleaned.Contains(hold)&&deck.exhausted_pile.Contains(ethereal)&&deck.discard_pile.Contains(response),"开始回合虚无消耗、应对弃置、保留留下");
            var power=GD.Load<ActiveSkillData>("res://resources/activeskills/黄庭养形篇/抱一.tres");
            Check(!manager.body_cards.CanPlay(defender,power),"抱一回落条件");
            defender.max_qi_blood=3;defender.qi_blood=1;defender.current_health=80;
            manager.body_cards.Fall(defender,1,true);
            Check(defender.current_health==83&&manager.body_cards.CanPlay(defender,power),"气血回落与条件联动");
            manager.body_cards.Before(defender,power);
            Check(!manager.body_cards.CanPlay(defender,power),"每回合限一次");
            var momentum=new ActiveSkillData{skill_name="乘势测试",keywords=["乘势"]};
            manager.body_cards.Begin(defender);
            Check(manager.body_cards.Momentum(defender,momentum)==0,"乘势没有前置攻击不生效");
            manager.body_cards.Before(defender,attack);
            Check(Mathf.IsEqualApprox(manager.body_cards.Momentum(defender,momentum),.43f),"乘势按属性计算会心加成");
            manager.body_cards.Before(defender,momentum);
            Check(Mathf.IsEqualApprox(manager.body_cards.SettlingMomentum(defender,momentum),.43f),"乘势结算保存前牌条件");
            var actor=new Character();AddChild(actor);
            actor.apply_battle_data(new BattleEntityData{max_health=100,current_health=100,body_proficiency=17,sword_proficiency=13,magic_proficiency=11});
            Check(CardRules.Number(actor,"body_proficiency")==17&&CardRules.Number(actor,"sword_proficiency")==13&&CardRules.Number(actor,"magic_proficiency")==11,"战斗快照保留造诣");
            foreach(var file in System.IO.Directory.EnumerateFiles(ProjectSettings.GlobalizePath("res://resources/activeskills"),"*.tres",SearchOption.AllDirectories))
            {
                var skill=GD.Load<ActiveSkillData>(ProjectSettings.LocalizePath(file));
                if(skill is null)throw new Exception(file);
                var face=CardPresentation.Face(skill,defender);var details=CardPresentation.Details(skill,defender);
                Check(!string.IsNullOrWhiteSpace(face),skill.skill_name+"牌面非空");
                Check(!System.Text.RegularExpressions.Regex.IsMatch(face,@"\b(round|floor|max|min)\("),skill.skill_name+"牌面不得出现公式："+face);
                var visual=GD.Load<PackedScene>("res://tscn/panel/BattleCard.tscn").Instantiate<SkillCard>();AddChild(visual);visual.setup(skill,defender,0);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var label=visual.GetNode<Label>("EffectShade/Effect");
                Check(label.GetLineCount()*label.GetLineHeight()<=label.Size.Y+2,skill.skill_name+"牌面文字超出显示区");
                Check(visual.GetNode<Control>("EffectShade").Position.Y>=50,skill.skill_name+"牌面描述挤入标题");
                visual.QueueFree();
            }
            var beastTiming=new[]
            {
                (Path:"res://resources/activeskills/妖兽/灰背野猪/拱撞.tres",Name:"拱撞",Energy:1,Preparation:144,Recovery:96,Cooldown:240,Target:2,Category:"active"),
                (Path:"res://resources/activeskills/妖兽/灰背野猪/拱身戒备.tres",Name:"拱身架招",Energy:0,Preparation:96,Recovery:48,Cooldown:240,Target:0,Category:"response"),
                (Path:"res://resources/activeskills/妖兽/林地凶狼/撕咬.tres",Name:"撕咬",Energy:1,Preparation:144,Recovery:96,Cooldown:240,Target:2,Category:"active"),
                (Path:"res://resources/activeskills/妖兽/林地凶狼/游身架招.tres",Name:"游身架招",Energy:0,Preparation:96,Recovery:48,Cooldown:240,Target:0,Category:"response")
            };
            foreach(var expected in beastTiming)
            {
                var skill=GD.Load<ActiveSkillData>(expected.Path)??throw new Exception(expected.Name+"资源");
                Check(skill.skill_name==expected.Name,expected.Name+"显示名");
                Check(skill.energy_cost==expected.Energy&&skill.mana_cost==0&&skill.spirit_occupy==0,expected.Name+"三资源费用");
                Check(skill.preparation_frames==expected.Preparation&&skill.recovery_frames==expected.Recovery&&skill.cooldown_frames==expected.Cooldown,expected.Name+"实时三时序");
                Check(skill.target_type==expected.Target&&skill.slot_category==expected.Category,expected.Name+"目标与槽位类型");
            }
            var boar=new Beast{entity_data=new LivingData{physique=12,agility=5,intelligence=2,soul=2,max_health=100,current_health=100},faction=(int)WorldEntity.Faction.ENEMY,beast_deck=GD.Load<BasicDeckResource>("res://resources/beast_decks/灰背野猪.tres")};
            var beastAttacker=new Creature{entity_data=new LivingData{physique=8,agility=8,intelligence=5,soul=5,max_health=100,current_health=100},faction=(int)WorldEntity.Faction.PLAYER};
            AddChild(boar);AddChild(beastAttacker);boar.physique=12;boar.agility=5;boar.intelligence=2;boar.soul=2;boar.current_health=100;beastAttacker.current_health=100;
            var beastLoadout=(Array<ActiveSkillData>)typeof(BattleManager).GetMethod("LoadCombatSkills",flags)!.Invoke(manager,[boar])!;
            Check(beastLoadout.Count==2&&beastLoadout.Any(skill=>skill.skill_name=="拱撞")&&beastLoadout.Any(skill=>skill.skill_name=="拱身架招"),"妖兽技能库接入固定技能槽");
            manager.player_units.Add(beastAttacker);manager.enemy_units.Add(boar);manager.all_units.Add(beastAttacker);manager.all_units.Add(boar);
            var attackerState=new BattleUnit();attackerState.setup(beastAttacker,null);var boarState=new BattleUnit();boarState.setup(boar,null);manager.battle_units[beastAttacker]=attackerState;manager.battle_units[boar]=boarState;
            var beastAttackCard=GD.Load<ActiveSkillData>("res://resources/activeskills/发劲.tres")!.Duplicate() as ActiveSkillData??throw new Exception("玩家测试攻击");
            var boarAttack=beastLoadout.First(skill=>skill.skill_name=="拱撞");var beastResponseCard=beastLoadout.First(skill=>skill.skill_name=="拱身架招");
            attackerState.skill_queue.Add(new BattleQueuedSkill(beastAttackCard,boar));boarState.skill_queue.Add(new BattleQueuedSkill(boarAttack,beastAttacker));
            var combatSkills=(System.Collections.Generic.Dictionary<GodotObject,Array<ActiveSkillData>>)typeof(BattleManager).GetField("_combatSkills",flags)!.GetValue(manager)!;combatSkills[boar]=beastLoadout;
            var enemyTimers=(System.Collections.Generic.Dictionary<GodotObject,float>)typeof(BattleManager).GetField("_enemyPlanTimers",flags)!.GetValue(manager)!;enemyTimers[boar]=0;
            typeof(BattleManager).GetMethod("PlanEnemyQueue",flags)!.Invoke(manager,[boar,0f]);
            Check(boarState.skill_queue.Count==1&&boarState.skill_queue[0].skill==beastResponseCard,"妖兽受击前置备架招");
            boarState.skill_queue[0].phase=BattleQueuedSkill.Phase.HoldingResponse;
            Check(manager.try_auto_response(boar,beastAttacker,"physical"),"妖兽架招实时触发");
            Check(boar.get_status_stacks("化解")==7,"妖兽架招动态化解公式");
            GD.Print($"CARD_SYSTEM_SMOKE: {checks} checks passed");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
