using Godot;
using System.Reflection;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>七本目录与新增三本的事件、化法、行动快照及边界回归。</summary>
public partial class SevenBooksCombatSmoke : Node
{
    private const string Yu="玉髓回澜养真宝册", Bao="抱虚养晦通玄录", Cloud="舒卷天章";
    private readonly List<Resource> _keep=[];
    private int checks;
    private void Check(bool condition,string label) { if(!condition)throw new Exception(label);checks++; }
    private T Load<T>(string path) where T:Resource
    {var r=GD.Load<T>(path)??throw new Exception("资源加载失败："+path);_keep.Add(r);return r;}
    private ActiveSkillData Skill(string book,string name)=>Load<ActiveSkillData>($"res://resources/activeskills/{book}/{name}.tres");
    private Creature Unit(BattleManager manager,string book,int faction=1)
    {
        var unit=new Creature{Name="TestUnit"+manager.all_units.Count,entity_data=new HumanData
        { entity_name="秘籍验证",main_gongfa_path=book.Length>0?$"res://resources/gongfa/{book}.tres":"",faction=faction,
          max_health=1000,current_health=1000,max_stamina=100,current_stamina=100,max_mana=4,current_mana=0,
          physique=20,intelligence=20,soul=10,body_proficiency=8,magic_proficiency=8,sword_proficiency=8,
          precision=10000,dodge=0,critical=0,crit_resist=100000 },faction=faction,max_qi_blood=8};
        AddChild(unit);manager.all_units.Add(unit);
        if(faction==1)manager.player_units.Add(unit);else manager.enemy_units.Add(unit);
        var state=new BattleUnit();state.setup(unit,null);manager.battle_units[unit]=state;
        manager.gongfa_combat.Begin(unit);
        return unit;
    }
    private void Heart(BattleManager m,Creature u,string book)=>Check(m.gongfa_combat.Utility(u,Skill(book,"心法")),"心法入口："+book);
    private void Complete(BattleManager m,Creature u,ActiveSkillData s)
    { m.gongfa_combat.Before(u,s);m.gongfa_combat.Utility(u,s);m.gongfa_combat.AfterSkillResolved(u,s); }
    private static object? Invoke(object instance,string method,params object?[] args)=>instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(instance,args);
    public override void _Ready()=>CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            if(!GongfaAvailability.IsEnabled(Load<GongfaResource>($"res://resources/gongfa/{Yu}.tres")) ||
               !GongfaAvailability.IsEnabled(Load<GongfaResource>($"res://resources/gongfa/{Bao}.tres")))
            {GD.Print("SEVEN_BOOKS_COMBAT_SMOKE: skipped; new books await drawio review");GetTree().Quit();return;}
            foreach(var name in new[]{Yu,Bao,Cloud})
            {
                var book=Load<GongfaResource>($"res://resources/gongfa/{name}.tres");
                Check(book.chapters.Count==4,"四卷："+name);
                var expectedRewrites = name == Cloud ? 1 : 4;
                Check(book.chapters.Sum(c=>c.sub_deck_rewrites.Count)==expectedRewrites,"当前保留化法数量："+name);
                Check(book.chapters.Sum(c=>c.learn_permanent_bonuses.GetValueOrDefault(name==Cloud?"sword_proficiency":name==Yu?"body_proficiency":"magic_proficiency",0).AsSingle())==3,"造诣奖励合计3："+name);
                foreach(var chapter in book.chapters)
                {
                    Check(chapter.heart_method_description.Contains("主修额外效果"),"运行时完整心法："+chapter.chapter_name);
                    foreach(var rewrite in chapter.sub_deck_rewrites)
                    {
                        var result=(ActiveSkillData)rewrite.result_skill!;
                        Check(result.gongfa_rewrite_effect_ids.Count>0,"化法机制标记："+result.skill_name);
                        Check(!string.IsNullOrWhiteSpace(GongfaSkillRewriteRuntime.GetRewriteAddedEffectDescriptions(result)),"化法来源说明："+result.skill_name);
                    }
                }
                Check(GongfaAvailability.IsItemEnabled(Load<ItemData>($"res://resources/entities/items/{name}秘籍.tres")),"秘籍物品开放："+name);
                var snapshot=new BattleEntityData();
                typeof(StartMenu).GetMethod("ApplyTestLoadout",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,
                    [snapshot,"res://resources/basic_decks/公共基础技能池.tres",book.ResourcePath,"",book.chapters.SelectMany(c=>c.learned_active_skills).OfType<ActiveSkillData>().Select(s=>s.ResourcePath).ToArray()]);
                Check(snapshot.combat_passive_paths.Contains(book.heart_method_skill!.ResourcePath),"战斗测试自动心法："+name);
                Check(snapshot.combat_heart_method_upgrades.ContainsKey(name+"|"+book.chapters[3].heart_method_upgrade_id),"战斗测试第四卷："+name);
                var targets=book.chapters.SelectMany(c=>c.sub_deck_rewrites).Select(r=>((ActiveSkillData)r.source_skill!).ResourcePath).Distinct().ToArray();
                var learned=new HumanData {entity_name="化法绑定验证",cultivation_realm="云息九层",main_gongfa_path=book.ResourcePath,
                    learned_gongfa_paths=[book.ResourcePath],basic_deck_path="res://resources/basic_decks/公共基础技能池.tres",
                    combat_skill_slots_configured=true,combat_skill_slot_paths=new(targets),
                    combat_skill_references=new(targets.Select(CombatSkillReference.FromKey)),unlocked_skill_node_ids=["sword_cut_root"]};
                learned.learned_gongfa_chapters[book.ResourcePath]=new Godot.Collections.Array<string>(book.chapters.Select(c=>c.get_id()));
                var character=new Character();AddChild(character);character.load_from_data(learned);
                var choices=character.get_skill_rewrite_choices().Where(c=>c.Source.StartsWith(name,StringComparison.Ordinal)).ToArray();
                Check(choices.Length==expectedRewrites,"真实角色只提供现行化法来源："+name);
                foreach(var choice in choices)
                {
                    var available=character.get_skill_rewrite_candidates(choice.Id);
                    Check(available.Count==1,"精确匹配已装配技能："+choice.Title);
                    Check(character.set_skill_rewrite_binding(choice.Id,available[0].Key,out _),"真实化法绑定："+choice.Title);
                    Check(character.resolve_combat_skill(available[0].Key)!.gongfa_rewrite_effect_ids.Count>0,"绑定后生成运行副本："+choice.Title);
                }
                character.QueueFree();
            }
            var manager=new BattleManager{Name="BattleMgr"};AddChild(manager);manager.set_spectator_battle(true);
            var runtime=manager.gongfa_combat;
            var enemy=Unit(manager,"",2);
            var yu=Unit(manager,Yu);Heart(manager,yu,Yu);
            var wash=Skill(Yu,"涤髓");yu.qi_blood=4;
            Complete(manager,yu,wash);
            Check(yu.qi_blood==3&&yu.shield==25&&yu.current_health==1000,"满生命回落溢出转盾");
            yu.current_health=980;yu.shield=0;runtime.Fall(yu,1,true);
            Check(yu.current_health==1000&&yu.shield==15,"先补缺口，只转换剩余回复");
            yu.healing_received_bonus=.2f;yu.shield=0;runtime.Fall(yu,1,true);
            Check(yu.shield==30,"受疗加成只计入一次");yu.healing_received_bonus=0;
            yu.qi_blood=0;Check(!runtime.CanPlay(yu,wash),"涤髓气血不足禁止施展");
            yu.qi_blood=2;yu.shield=0;
            Complete(manager,yu,Skill(Yu,"化法/涤髓第4卷"));
            Check(yu.shield==50,"第四卷100%替代50%，不偷带未绑定的第一卷");
            yu.qi_blood=2;yu.shield=0;
            Complete(manager,yu,GongfaSkillRewriteRuntime.CopyWithMergedEffects(Skill(Yu,"化法/涤髓第4卷"),Skill(Yu,"化法/涤髓第1卷")));
            Check(yu.shield==55,"明确绑定第一及第四卷后化法累积");
            var before=yu.qi_blood;yu.shield=0;
            runtime.RecordShieldAbsorbed(yu,enemy,10,10);runtime.EndIncomingAttack(yu);
            Check(yu.qi_blood==before+1,"敌方耗尽护盾后主修补血");
            runtime.RecordShieldAbsorbed(yu,enemy,10,10);runtime.EndIncomingAttack(yu);
            Check(yu.qi_blood==before+1,"耗盾补血每回合只触发一次");
            runtime.BeginRound(yu);runtime.RecordShieldAbsorbed(yu,yu,10,10);runtime.EndIncomingAttack(yu);
            Check(yu.qi_blood==before+1,"友方与主动扣盾不补血");
            runtime.RecordShieldAbsorbed(yu,enemy,10,10);runtime.EndIncomingAttack(yu);
            Check(yu.qi_blood==before+2,"下一回合可重新触发");
            var guard=Skill(Yu,"化法/架招第3卷");yu.shield=0;
            runtime.Before(yu,guard);runtime.OnResponseTriggered(yu,guard);
            runtime.ResponseResolved(yu,enemy,1,0,false);runtime.ResponseResolved(yu,enemy,1,0,false);runtime.EndIncomingAttack(yu);
            Check(yu.shield==1,"多段化解汇总后统一向下取整");
            var spring=Skill(Yu,"鸣泉");yu.shield=5;yu.qi_blood=3;runtime.Before(yu,spring);yu.shield=0;runtime.AfterAttack(yu,spring,true);
            Check(yu.qi_blood==4,"鸣泉读取施展前护盾快照");
            Check(spring.uses_per_round==1,"鸣泉独立限次");
            runtime.Utility(yu,Load<ActiveSkillData>("res://resources/activeskills/太和感物养元宝册/太和感物养元.tres"));
            yu.current_health=1000;yu.qi_blood=4;yu.shield=0;
            var taiheGuard=Load<ActiveSkillData>("res://resources/activeskills/太和感物养元宝册/化法/架招.tres");
            runtime.Before(yu,taiheGuard);runtime.OnResponseTriggered(yu,taiheGuard);
            Check(yu.qi_blood==4&&yu.shield==25,"太和消耗气血走公共回复，随后应对补血");
            runtime.EndIncomingAttack(yu);
            runtime.Utility(yu,Load<ActiveSkillData>("res://resources/activeskills/百骸衍枝化生宝册/心法.tres"));
            yu.SetMeta("combat_heart_method_upgrades",new GDictionary{{"百骸衍枝化生宝册|baihai_c4",true}});
            runtime.BeginRound(yu);yu.qi_blood=7;yu.shield=0;
            Check(runtime.BloodArmCount(yu)==2,"玉髓主修配百骸双臂");
            runtime.Fall(yu,1,true);Check(runtime.BloodArmCount(yu)==1&&yu.shield==25,"回落换盾会失去第二只手臂");
            yu.shield=0;runtime.RecordShieldAbsorbed(yu,enemy,25,25);runtime.EndIncomingAttack(yu);
            Check(runtime.BloodArmCount(yu)==2,"敌方破盾补血后重新显化双臂");

            var bao=Unit(manager,Bao);Heart(manager,bao,Bao);
            var wait=Skill(Bao,"守寂");Complete(manager,bao,wait);
            Check(manager.get_battle_qi(bao)==2&&bao.shield==11,"零法力守寂产炁，主修首次额外1");
            Complete(manager,bao,wait);Check(manager.get_battle_qi(bao)==3,"第二次只产炁1");
            Complete(manager,bao,wait);Check(manager.get_battle_qi(bao)==3,"每回合第三次不再产炁");
            runtime.BeginRound(bao);manager.get_battle_unit(bao).set_qi(0);bao.current_mana=0;
            runtime.Before(bao,wait);bao.current_mana=2;runtime.Utility(bao,wait);runtime.AfterSkillResolved(bao,wait);
            Check(manager.get_battle_qi(bao)==2,"后续回法不修改支付后零法力快照");
            runtime.BeginRound(bao);manager.get_battle_unit(bao).set_qi(0);bao.current_mana=1;
            runtime.Before(bao,wait);bao.current_mana=0;runtime.Utility(bao,wait);runtime.AfterSkillResolved(bao,wait);
            Check(manager.get_battle_qi(bao)==0,"支付后非零，后续归零不补触发");
            runtime.BeginRound(bao);bao.max_mana=1;bao.current_mana=0;Complete(manager,bao,wait);
            Check(bao.current_mana==1&&manager.get_battle_qi(bao)==0,"基础与主修合并产炁，正常周天清炁回法");
            bao.max_mana=10;bao.current_mana=0;runtime.BeginRound(bao);manager.get_battle_unit(bao).set_qi(0);
            var guardBao=Skill(Bao,"化法/架招第3卷");runtime.Before(bao,guardBao);runtime.OnResponseTriggered(bao,guardBao);
            runtime.ResponseResolved(bao,enemy,1,0,false);runtime.ResponseResolved(bao,enemy,1,0,false);runtime.EndIncomingAttack(bao);
            Check(manager.get_battle_qi(bao)==1,"架招化法独立触发，按回合限一次");
            runtime.AfterSkillResolved(bao,guardBao);Check(manager.get_battle_qi(bao)==1,"应对不触发零法力心法");
            var burn=Unit(manager,"斫基损契玄经");Heart(manager,burn,Bao);
            runtime.Utility(burn,Load<ActiveSkillData>("res://resources/activeskills/斫基损契玄经/斫基损契.tres"));
            burn.max_mana=3;burn.SetMeta("combat_heart_method_upgrades",new GDictionary{{"斫基损契玄经|zhuo_ji_c3",true}});
            var scatter=Skill(Bao,"散照");Invoke(manager,"Pay",burn,scatter);runtime.Before(burn,scatter);runtime.AfterAttack(burn,scatter,true);runtime.AfterSkillResolved(burn,scatter);
            Check(burn.max_mana==2&&manager.get_battle_qi(burn)==2,"斫基燃烧补费与抱虚辅修分别产炁");
            Check(manager.get_skill_mana_cost(burn,scatter)==0,"斫基主修记录新增技能费用转换");
            var auto=Unit(manager,"玄枢含章经");Heart(manager,auto,Bao);
            runtime.Utility(auto,Load<ActiveSkillData>("res://resources/activeskills/玄枢含章经/玄枢含章.tres"));
            auto.max_mana=1;auto.current_mana=0;Complete(manager,auto,wait);Complete(manager,auto,wait);
            var pending=(System.Collections.Generic.Dictionary<GodotObject,int>)typeof(BattleManager).GetField("_pendingZhouTianAutoCasts",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(manager)!;
            Check(auto.current_mana==1&&pending.GetValueOrDefault(auto)==1,"抱虚产炁触发玄枢原有自动出招入口");

            var cloud=Unit(manager,Cloud);Heart(manager,cloud,Cloud);
            cloud.SetMeta("combat_heart_method_upgrades",new GDictionary{{Cloud+"|shujuan_c2",true},{Cloud+"|shujuan_c4",true}});
            var slash=Load<ActiveSkillData>("res://resources/activeskills/剑修基础/斩剑.tres");
            var intent=Load<StatusEffect>("res://resources/battle/statuses/剑意.tres");cloud.add_status(intent,-1,50);
            Check(cloud.get_status_stacks("剑意")==10,"剑意全局截断为10层");
            runtime.Before(cloud,slash);cloud.consume_status_stacks("剑意",10);
            Check(runtime.CloudFollowUpLimit(cloud,slash,true)==2,"行动开始满剑意，清空后仍保留本次两段");
            Check(runtime.CloudFollowUpLimit(cloud,slash,false)==0,"首段未命中不追加");
            var segment=runtime.CreateCloudFollowUp(cloud,slash);
            Check(segment.combat_is_gongfa_bonus_damage_segment&&segment.combat_is_cloud_segment&&segment.special_effects.Count==0,"追加段不复制原特殊效果");
            Check(Mathf.IsEqualApprox(segment.calculate_damage(cloud),12),"追加伤害读取初始剑意");
            cloud.add_status(intent,-1,5);runtime.Before(cloud,slash);
            Check(runtime.CloudFollowUpLimit(cloud,slash,true)==1,"主修门槛减少1层");
            ((HumanData)cloud.entity_data!).main_gongfa_path=$"res://resources/gongfa/{Yu}.tres";runtime.Before(cloud,slash);
            Check(runtime.CloudFollowUpLimit(cloud,slash,true)==0,"辅修仍需6层剑意");
            cloud.add_status(intent,-1,1);runtime.Before(cloud,slash);Check(runtime.CloudFollowUpLimit(cloud,slash,true)==1,"辅修六层生效");
            var triple=Skill(Cloud,"云岚三叠");cloud.add_status(intent,-1,10);runtime.Before(cloud,triple);
            Check(runtime.CloudFollowUpLimit(cloud,triple,true)==3,"云岚使用自身三段，不叠加固定两段");
            Check(Mathf.IsEqualApprox(runtime.CloudFollowUpChance(cloud,triple,0),.8f)&&Mathf.IsEqualApprox(runtime.CloudFollowUpChance(cloud,triple,1),.6f)&&Mathf.IsEqualApprox(runtime.CloudFollowUpChance(cloud,triple,2),.4f),"递减概率与80%上限");
            Check(runtime.CloudFollowUpChance(cloud,triple,-1)==0&&runtime.CloudFollowUpChance(cloud,triple,3)==0,"概率索引越界返回0");
            var info=CardPresentation.Face(triple,cloud);
            Check(!info.Contains("以C概率")&&!info.Contains("以D、E概率")&&info.Contains('%'),"技能详情解析概率字母与百分比");
            Check(Mathf.IsEqualApprox(CardRules.Evaluate("clamp(40% + 6×5%,40%,80%)",cloud),.7f),"百分比公式与clamp可计算");
            var inert=Unit(manager,"");inert.max_mana=0;manager.get_battle_unit(inert).set_qi(int.MaxValue-1);
            manager.gain_battle_qi(inert,10,out var overflowCycle);
            Check(manager.get_battle_qi(inert)==int.MaxValue&&!overflowCycle,"零容量积炁加法防溢出");
            Check(!GongfaAvailability.IsSkillPathEnabled("res://resources/activeskills/黄庭养形篇/化法/抱一.tres"),"归档目录内未被卷章引用的旧变体也关闭");
            runtime.BeginRound(cloud);cloud.shield=int.MaxValue;runtime.Before(cloud,wait);runtime.Utility(cloud,wait);
            Check(cloud.shield==int.MaxValue,"新增护盾加算防溢出");

            // 真实 SkillExecutor 调用，检查公用支付、次数和追加执行链。
            ((HumanData)cloud.entity_data!).main_gongfa_path=$"res://resources/gongfa/{Cloud}.tres";
            var executor=manager.GetChildren().OfType<SkillExecutor>().Single();
            var baseDamage=BattleActionResolver.preview_damage(cloud,slash);
            GD.Seed(20261025);var observed=false;
            for(var i=0;i<8&&!observed;i++)
            {
                enemy.current_health=1000;enemy.shield=0;cloud.consume_status_stacks("剑意",10);cloud.add_status(intent,-1,10);
                runtime.Before(cloud,slash);executor.execute_skill(cloud,slash,enemy);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                observed=1000-enemy.current_health>baseDamage;
            }
            Check(observed,"实际攻击执行了流云追加段");
            GD.Print($"SEVEN_BOOKS_COMBAT_SMOKE: {checks} checks passed");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
