using Godot;
using Godot.Collections;

/// <summary>现行秘籍技能、心法与公共战斗状态的局内结算。状态按单位保存，战斗结束释放。</summary>
public sealed class GongfaCombatRuntime
{
    private readonly BattleManager manager;
    private readonly System.Collections.Generic.Dictionary<ulong,State> states=[];
    private sealed class State
    {
        public HashSet<string> Powers=[]; public HashSet<string> Used=[];
        public int Falls, FallsThisRound, Discounts, ConsumedWounds, SpentYouhai, BlockedRemainder, LastType=-1, RedSunFurnaceSpent; public bool YouhaiTriggeredThisRound, NeijingWoundTriggeredThisRound, Bottom, Boost, YouhaiEmpoweredMountain; public ActiveSkillData? RedSunDamageSkill; public string RedSunDamageSkillKey="";
        public ActiveSkillData? PendingCard, PendingResponseCard; public float PendingMomentum; public int PendingResponseBlockBonus; public bool ResponseTriggered, LingShuFajinEmpowered; public string LingShuFajinSkillKey=""; public HashSet<string> ResolvedResponseEffects=[];
    }
    private State For(GodotObject u){if(!states.TryGetValue(u.GetInstanceId(),out var s))states[u.GetInstanceId()]=s=new();return s;}
    public GongfaCombatRuntime(BattleManager owner)=>manager=owner;
    public void Reset()=>states.Clear();
    public void Begin(GodotObject u){var s=For(u);s.BlockedRemainder=0;s.Powers.Clear();s.Used.Clear();s.Falls=s.FallsThisRound=s.Discounts=s.ConsumedWounds=s.SpentYouhai=0;s.LastType=-1;s.YouhaiTriggeredThisRound=s.NeijingWoundTriggeredThisRound=false;s.PendingCard=s.PendingResponseCard=null;s.PendingMomentum=0;s.PendingResponseBlockBonus=0;s.ResponseTriggered=false;s.LingShuFajinEmpowered=false;s.LingShuFajinSkillKey="";s.ResolvedResponseEffects.Clear();s.RedSunDamageSkill=null;s.RedSunDamageSkillKey="";s.RedSunFurnaceSpent=0;s.Bottom=s.Boost=s.YouhaiEmpoweredMountain=false;}
    public void BeginRound(GodotObject u){var s=For(u);s.FallsThisRound=0;s.YouhaiTriggeredThisRound=s.NeijingWoundTriggeredThisRound=false;s.LastType=-1;s.PendingCard=null;s.PendingMomentum=0;s.RedSunDamageSkill=null;s.RedSunDamageSkillKey="";s.RedSunFurnaceSpent=0;s.YouhaiEmpoweredMountain=false;}
    private static bool Main(GodotObject u,string book)=>(u is Character c && c.main_gongfa?.gongfa_name==book) || (u is WorldEntity e && e.entity_data is HumanData h && h.main_gongfa_path.Contains(book));
    public bool CanPlay(GodotObject u,ActiveSkillData card)
    {
        var s=For(u);var qi=CardRules.Number(u,"qi_blood");var you=Stacks(u,"游骸");
        if((HasEffect(card,"chiyang_c4_zhuming")||HasEffect(card,"chiyang_c4_hanshan"))&&!s.Powers.Contains("赤阳参同"))return false;
        if((HasEffect(card,"chiyang_c4_zhuming")||HasEffect(card,"chiyang_c4_hanshan"))&&CardRules.Number(u,"blood_furnace_value")<20)return false;
        return card.skill_name switch
        {
            "抱一"=>s.Falls>0&&!s.Used.Contains("抱一"),
            "伏火"=>qi>=1&&!s.Used.Contains("伏火"),
            "大冶"=>qi>=2,
            "偃月"=>CardRules.Number(u,"blood_furnace_value")>=2,
            "归藏"=>s.Falls>=2&&!s.Used.Contains("归藏"),
            "归元"=>qi==0,
            "托身" or "解形"=>you>0,
            "冯虚"=>s.SpentYouhai>=2,
            "去故"=>(manager.is_realtime_battle()?manager.count_wounds(u):manager.hand_cards(u).Count(IsWound))>=2,
            "引筋"=>manager.count_wounds(u)>0,
            "朱明"=>CardRules.Number(u,"blood_furnace_value")>=20,
            _=>true
        };
    }
    public int Cost(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);var discount=!CardRules.IsResponse(c)&&s.Discounts>0?1:0;
        if(c.skill_name=="催血"&&s.Boost)discount++;
        return Math.Max(0,c.get_energy_cost()-discount);
    }
    public void Before(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);s.Used.Add(c.skill_name);
        s.RedSunDamageSkill=null;s.RedSunDamageSkillKey="";s.RedSunFurnaceSpent=0;
        s.PendingMomentum=Momentum(u,c);s.PendingCard=c;s.LastType=(int)c.skill_type;
        if(!CardRules.IsResponse(c)&&s.Discounts>0)s.Discounts--;
        s.YouhaiEmpoweredMountain=HasEffect(c,"chengwu_c2_fajin")&&c.skill_name=="发劲"&&Stacks(u,"游骸")>0&&SpendYouhai(u);
        if(CardRules.IsResponse(c))
        {
            s.PendingResponseCard=null;s.PendingResponseBlockBonus=0;s.ResponseTriggered=false;s.ResolvedResponseEffects.Clear();
        }
    }
    /// <summary>只在队首应对确实匹配来袭并触发时结算化法，避免未触发的应对提前消耗资源。</summary>
    public void OnResponseTriggered(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);s.PendingResponseCard=c;s.PendingResponseBlockBonus=0;s.ResponseTriggered=true;s.ResolvedResponseEffects.Clear();
        if(HasEffect(c,"chiyang_c2_xieli")&&s.Powers.Contains("赤阳参同"))
        {
            var spent=SpendFurnace(u,20,true);
            s.PendingResponseBlockBonus+=spent/2;
            RecordRedSunFurnaceSpend(u,spent);
        }
        if(HasEffect(c,"buxu_c2_xieli")&&CardRules.Number(u,"qi_blood")==0)
            s.PendingResponseBlockBonus++;
        if(HasEffect(c,"yinfu_c1_chengji"))
            s.PendingResponseBlockBonus+=Math.Min(2,Stacks(u,"借力")/2);
    }
    /// <summary>在攻击技能开始结算前处理需要消耗伤势的灵枢化法。</summary>
    public void PrepareAttack(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);s.LingShuFajinEmpowered=false;s.LingShuFajinSkillKey="";
        if(c.skill_name!="引筋"||manager.count_wounds(u)<=0)return;
        s.LingShuFajinEmpowered=ConsumeWounds(u,1)>0;
        if(s.LingShuFajinEmpowered)s.LingShuFajinSkillKey=SkillKey(c);
    }
    public void PrepareRedSunAttack(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);s.RedSunDamageSkill=null;s.RedSunDamageSkillKey="";s.RedSunFurnaceSpent=0;
        if(!s.Powers.Contains("赤阳参同")||u is not Creature creature)return;
        var available=Math.Max(0,creature.blood_furnace_value);
        var spent=HasEffect(c,"chiyang_c3_fajin")&&c.skill_name=="发劲"?Math.Min(10,available):
            (HasEffect(c,"chiyang_c4_zhuming")||HasEffect(c,"chiyang_c4_hanshan"))&&c.skill_name=="朱明"&&available>=20?available:0;
        if(spent<=0)return;
        creature.blood_furnace_value-=spent;
        RecordRedSunFurnaceSpend(u,spent);
        s.RedSunDamageSkill=c;s.RedSunDamageSkillKey=SkillKey(c);s.RedSunFurnaceSpent=spent;
    }
    public float Momentum(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);
        if(!c.keywords.Contains("乘势"))return 0;
        return MomentumConditionChecker.check(c.momentum_condition_type,new Dictionary{{"last_skill_type",s.LastType}})?CardRules.Evaluate(c.momentum_effect_params,u):0;
    }
    public float SettlingMomentum(GodotObject u,ActiveSkillData c)=>For(u).PendingCard==c?For(u).PendingMomentum:Momentum(u,c);
    public void Fall(GodotObject u,int count,bool active,float rate=3)
    {
        if(u is not Creature creature)return;
        rate+=creature.get_combat_tempering_total("fall_heal_rate_pp");
        var s=For(u);var before=creature.qi_blood;var spent=Math.Min(Math.Max(0,count),before);
        if(spent==0)return;creature.qi_blood-=spent;
        if(active && s.Powers.Contains("赤阳参同"))creature.blood_furnace_value=Math.Min(creature.blood_furnace_value_cap,creature.blood_furnace_value+Mathf.RoundToInt(spent*rate));
        else Heal(creature,spent*rate);
        if(active)
        {
            s.Falls++;
            s.FallsThisRound++;
            if(s.Powers.Contains("黄庭养形"))
            {
                if(s.FallsThisRound==1)Add(u,"身法",1);
                if(HasHeartUpgrade(u,"黄庭养形篇","huangting_c2")&&s.FallsThisRound<=2)Add(u,"身法",1);
                if(s.FallsThisRound==1&&HasHeartUpgrade(u,"黄庭养形篇","huangting_c4"))Add(u,"身法",1);
                if(s.FallsThisRound==1&&Main(u,"黄庭养形篇"))Energy(u,1);
            }
            if(s.Powers.Contains("乘物游骸")&&!s.YouhaiTriggeredThisRound)
            {
                Add(u,"游骸",1);
                if(HasHeartUpgrade(u,"乘物游骸真经","chengwu_c2"))Add(u,"身法",1);
                if(HasHeartUpgrade(u,"乘物游骸真经","chengwu_c4"))Add(u,"身法",1);
                s.YouhaiTriggeredThisRound=true;
            }
        }
        if(active&&before==1&&creature.qi_blood==0&&s.Powers.Contains("补虚行血"))
        {
            s.Bottom=true;
            if(!s.Boost)s.Boost=true;
            if(HasHeartUpgrade(u,"补虚行血诀","buxu_c3"))Add(u,"身法",1);
        }
    }
    public bool Utility(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);
        if(c.skill_type==ActiveSkillData.SkillType.POWER && new[]{"黄庭养形","赤阳参同","内景损益","灵枢经筋","阴符应物","乘物游骸","补虚行血"}.Contains(c.skill_name))
        {
            s.Powers.Add(c.skill_name);
            // 运行时能力与状态图标共用一份永久状态，让效果在血条上方可见。
            AddPowerStatus(u,c);
            return true;
        }
        switch(c.skill_name)
        {
            case "平息":
                if(HasEffect(c,"chiyang_c1_pingxi")&&s.Powers.Contains("赤阳参同"))
                {
                    var spent=SpendFurnace(u,10,true);
                    RecordRedSunFurnaceSpend(u,spent);
                    if(spent>0&&u is Creature furnaceUser)Heal(furnaceUser,spent/2f);
                }
                var qiBefore=CardRules.Number(u,"qi_blood");
                Fall(u,1,true);
                var qiAfter=CardRules.Number(u,"qi_blood");
                if(qiAfter<qiBefore&&HasEffect(c,"chengwu_c3_pingxi"))Add(u,"身法",1);
                if(qiBefore==1&&qiAfter==0&&HasEffect(c,"buxu_c3_pingxi"))Add(u,"身法",1);
                if(qiAfter<qiBefore&&HasEffect(c,"huangting_c2_pingxi"))Add(u,"身法",1);
                if(HasEffect(c,"lingshu_c3_pingxi"))
                {
                    ConsumeWounds(u,1);
                }
                return true;
            case "返照":
                var fanzhaoBefore=CardRules.Number(u,"qi_blood");Fall(u,1,true);Add(u,"身法",2);
                if(HasEffect(c,"huangting_c1_fanzhao")&&fanzhaoBefore>CardRules.Number(u,"qi_blood")&&CardRules.Number(u,"qi_blood")==0)Add(u,"身法",1);
                return true;
            case "伏火": Fall(u,1,true,6);return true;
            case "大冶": Fall(u,2,true,7);return true;
            case "偃月":
                var furnaceSpent=SpendFurnace(u,20,true);
                Add(u,"身法",furnaceSpent/2);
                return true;
            case "蜕形": Fall(u,1,true);Add(u,"身法",2);return true;
            case "周行":
                Gain(u,1);if(s.Falls>0)Add(u,"身法",1);
                if(HasEffect(c,"huangting_c3_zhouxing")&&s.Falls>=2)Add(u,"身法",1);
                return true;
            case "归藏": Add(u,"身法",3);s.Discounts=2;return true;
            case "还阳":
                var yangBefore=CardRules.Number(u,"qi_blood");Gain(u,yangBefore==0?2:1);
                if(HasEffect(c,"buxu_c1_huanyang")&&yangBefore==0&&CardRules.Number(u,"qi_blood")>yangBefore&&u is Creature yangUser)Heal(yangUser,3);
                return true;
            case "催血":
                if(HasEffect(c,"lingshu_c2_cuixue")&&manager.count_wounds(u)>0)
                {
                    var consumed=ConsumeWounds(u,1);
                    if(consumed>0&&u is Creature lingShuUser)Heal(lingShuUser,3);
                }
                if(HasEffect(c,"chengwu_c1_cuixue")&&Stacks(u,"身法")>=10&&Stacks(u,"游骸")==0)Add(u,"游骸",1);
                Gain(u,s.Boost?2:1);
                if(s.Boost&&Main(u,"补虚行血诀"))Add(u,"身法",2);
                if(s.Bottom&&HasEffect(c,"buxu_c4_cuixue"))Add(u,"身法",1);
                if(s.Falls>0&&HasEffect(c,"huangting_c4_cuixue"))Add(u,"身法",1);
                s.Boost=false;return true;
            case "余温":
                var beforeWen=CardRules.Number(u,"qi_blood");
                if(beforeWen>0){Fall(u,1,true);Add(u,"身法",1);}
                else{Gain(u,1);Add(u,"身法",2);}
                return true;
            case "归元":
                var beforeGuiyuan=CardRules.Number(u,"qi_blood");
                Gain(u,beforeGuiyuan==0?3:1);
                Add(u,"身法",2);
                if(HasEffect(c,"buxu_c4_guiyuan"))Add(u,"身法",1);
                return true;
            case "抱一":
                var baoYiAtCast=CardRules.Number(u,"qi_blood");Gain(u,1);
                if(HasEffect(c,"huangting_c2_baoyi")&&baoYiAtCast==0&&CardRules.Number(u,"qi_blood")>baoYiAtCast&&u is Creature baoYiUser)Heal(baoYiUser,3);
                return true;
            case "托身": SpendYouhai(u);Add(u,"身法",2);return true;
            case "淬厉":
                manager.add_wound_card(u,"内伤",2);Add(u,"身法",1);
                if(HasEffect(c,"neijing_c1_cuili")&&u is Creature cuiliUser&&cuiliUser.get_status_stacks("内伤")>=2)Add(u,"借力",1);
                return true;
            case "去故":
                var removed=ConsumeWounds(u,3);Add(u,"借力",removed);
                if(HasEffect(c,"lingshu_c1_qugu")&&removed>=2)Add(u,"身法",1);
                return true;
            default:return false;
        }
    }
    public bool EvadeWithYouhai(GodotObject u,GodotObject attacker,ActiveSkillData card)
    {
        if(card.damage_type!="physical"||Stacks(u,"游骸")==0)return false;

        // 可追溯的攻击来源按肉身根基判定；来源缺失或无法读取身躯属性时直接抵挡。
        // 妖兽等没有单独体道造诣字段的实体，其该项按0计入总和。
        if(TryBodyFoundation(u,out var defenderTotal)&&TryBodyFoundation(attacker,out var attackerTotal)
            &&attackerTotal>=defenderTotal*2f)return false;

        SpendYouhai(u);
        return true;
    }
    public int Segments(GodotObject u,ActiveSkillData card)
    {
        if(card.skill_name=="冯虚")return 1+For(u).SpentYouhai/2;
        if(card.skill_name=="百骸")return 1+manager.count_wounds(u)/3;
        return card.skill_name=="发劲"&&HasEffect(card,"chengwu_c4_fajin")&&For(u).SpentYouhai>0?2:1;
    }
    public ActiveSkillData ForDamageSegment(ActiveSkillData card,int segment)
    {
        if(segment==0||card.skill_name!="发劲"||!HasEffect(card,"chengwu_c4_fajin"))return card;
        var extra=card.Duplicate() as ActiveSkillData??card;
        extra.base_damage=0;
        extra.damage_bonuses.Clear();
        extra.damage_formula="round(4 + 体魄×0.20 + 敏捷×0.15 + 体道造诣×0.15)";
        return extra;
    }
    public float DamageMultiplier(GodotObject u,ActiveSkillData c)
    {
        if(c.damage_type!="physical")return 1;
        var s=For(u);var borrowed=Stacks(u,"借力");var perStackBonus=(u is Creature creature?creature.get_combat_tempering_total("borrowed_force_damage_pp_per_stack"):0f)/100f;
        float multiplier=c.skill_name=="合劲"
            ? (borrowed>0&&perStackBonus>0?(1f+borrowed*(.08f+perStackBonus))/(1f+borrowed*.08f):1f)
            : 1f+borrowed*(.05f+perStackBonus);
        if(s.RedSunDamageSkillKey.Length>0&&s.RedSunDamageSkillKey==SkillKey(c)&&s.RedSunFurnaceSpent>0)
        {
            var perFurnacePoint = Main(u,"赤阳参同经") ? .015f : .01f;
            multiplier*=1+s.RedSunFurnaceSpent*perFurnacePoint;
        }
        if(HasEffect(c,"chengwu_c2_fajin")&&s.YouhaiEmpoweredMountain)multiplier*=1.5f;
        if(HasEffect(c,"neijing_c3_fajin"))multiplier*=1+Math.Min(20,manager.count_wounds(u)/2*5)/100f;
        if(HasEffect(c,"neijing_c4_tongji"))multiplier*=1+Math.Min(8,manager.count_wounds(u))*.02f;
        if(HasEffect(c,"lingshu_c4_yinjing")&&s.LingShuFajinEmpowered&&s.LingShuFajinSkillKey==SkillKey(c))multiplier*=1.2f;
        if(HasEffect(c,"yinfu_c2_xieshi"))multiplier+=borrowed*.02f;
        if(HasEffect(c,"yinfu_c4_hejin")&&borrowed>=3)multiplier*=1.1f;
        if(s.Powers.Contains("内景损益"))
        {
            var woundCount=manager.count_wounds(u);
            var heartMethodBonus=.03f;
            if(HasHeartUpgrade(u,"内景损益篇","neijing_c3"))heartMethodBonus+=.01f;
            if(Main(u,"内景损益篇"))heartMethodBonus+=.01f;
            multiplier*=1+woundCount*heartMethodBonus;
        }
        return multiplier;
    }
    public void AfterAttack(GodotObject u,ActiveSkillData card)
    {
        if(card.skill_name=="解形")SpendYouhai(u);
        if(card.skill_name is "撼山" or "发劲")For(u).YouhaiEmpoweredMountain=false;
        var state=For(u);state.RedSunDamageSkill=null;state.RedSunDamageSkillKey="";state.RedSunFurnaceSpent=0;state.LingShuFajinEmpowered=false;state.LingShuFajinSkillKey="";
        if(card.damage_type=="physical"&&card.skill_name!="挟势"&&u is Creature creature)creature.consume_status_stacks("借力",creature.get_status_stacks("借力"));
    }
    public void ResponseResolved(GodotObject u,int blocked,int healthLost,bool chengji)
    {
        var state=For(u);
        if(chengji&&healthLost==0)Add(u,"借力",1);
        if(state.Powers.Contains("阴符应物"))
        {
            var threshold=4-(Main(u,"阴符应物经")?1:0)-(HasHeartUpgrade(u,"阴符应物经","yinfu_c3")?1:0);
            state.BlockedRemainder+=Math.Max(0,blocked);
            Add(u,"借力",state.BlockedRemainder/threshold);
            state.BlockedRemainder%=threshold;
        }
        var response=state.PendingResponseCard;
        if(response is null)return;
        if(blocked>0&&HasEffect(response,"yinfu_c3_xieli")&&state.ResolvedResponseEffects.Add("yinfu_c3_xieli"))Add(u,"借力",1);
        if(healthLost>0&&HasEffect(response,"neijing_c2_xieli")&&state.ResolvedResponseEffects.Add("neijing_c2_xieli"))Add(u,"外伤",2);
    }
    public void OnHealthDamageReceived(GodotObject u,int damage)
    {
        if(damage<=0)return;
        var state=For(u);
        if(state.Powers.Contains("内景损益")&&HasHeartUpgrade(u,"内景损益篇","neijing_c2")&&!state.NeijingWoundTriggeredThisRound)
        {
            Add(u,"外伤",1);
            state.NeijingWoundTriggeredThisRound=true;
        }
    }
    public int AdditionalResponseBlock(GodotObject u,ActiveSkillData card)=>For(u).ResponseTriggered&&ReferenceEquals(For(u).PendingResponseCard,card)?For(u).PendingResponseBlockBonus:0;
    public void EndIncomingAttack(GodotObject u)
    {
        var state=For(u);state.PendingResponseCard=null;state.PendingResponseBlockBonus=0;state.ResponseTriggered=false;state.ResolvedResponseEffects.Clear();
    }
    private bool SpendYouhai(GodotObject u)
    {
        if(u is not Creature c||c.get_status_stacks("游骸")<=0)return false;
        c.consume_status_stacks("游骸",1);
        var state=For(u);
        u.SetMeta("spent_youhai",++state.SpentYouhai);
        if(state.Powers.Contains("乘物游骸")&&Main(u,"乘物游骸真经"))Add(u,"身法",3);
        return true;
    }
    private int ConsumeWounds(GodotObject u,int requested)
    {
        var consumed=manager.consume_hand_wounds(u,requested);
        if(consumed<=0)return 0;
        var state=For(u);
        state.ConsumedWounds+=consumed;
        if(state.Powers.Contains("灵枢经筋"))
        {
            Add(u,"借力",1);
            if(Main(u,"灵枢经筋篇"))Add(u,"借力",1);
        }
        if(HasHeartUpgrade(u,"灵枢经筋篇","lingshu_c2")&&consumed>=2)Add(u,"身法",1);
        if(HasHeartUpgrade(u,"灵枢经筋篇","lingshu_c4")&&consumed>=3)Gain(u,1);
        return consumed;
    }
    private static void RecordRedSunFurnaceSpend(GodotObject u,int spent)
    {
        if(spent>0&&HasHeartUpgrade(u,"赤阳参同经","chiyang_c2"))Add(u,"身法",1);
        if(spent>0&&HasHeartUpgrade(u,"赤阳参同经","chiyang_c4"))Add(u,"身法",1);
    }
    private static int SpendFurnace(GodotObject u,int maximum,bool evenOnly)
    {
        if(u is not Creature creature)return 0;
        var available=Math.Max(0,creature.blood_furnace_value);
        var spent=Math.Min(Math.Max(0,maximum),available);
        if(evenOnly)spent-=spent%2;
        creature.blood_furnace_value-=spent;
        return spent;
    }
    private static bool TryBodyFoundation(GodotObject? unit,out float total)
    {
        total=0;
        if(!GodotObject.IsInstanceValid(unit)||!HasReadableProperty(unit!,"physique")||!HasReadableProperty(unit!,"agility"))return false;
        total=CardRules.Number(unit,"physique")+CardRules.Number(unit,"agility")+CardRules.Number(unit,"body_proficiency");
        return true;
    }
    private static bool HasReadableProperty(GodotObject unit,string key)
    {
        if(unit.GetPropertyList().Any(property=>property["name"].AsString()==key))return true;
        return unit is WorldEntity entity&&entity.entity_data is { } data&&data.GetPropertyList().Any(property=>property["name"].AsString()==key);
    }
    public static bool IsWound(ActiveSkillData c)=>c.skill_name=="伤势";
    private static bool HasEffect(ActiveSkillData skill,string effectId)=>GongfaSkillRewriteRuntime.HasEffect(skill,effectId);
    private static string SkillKey(ActiveSkillData skill)=>$"{skill.skill_name}|{skill.source_gongfa_name}|{string.Join(",",skill.gongfa_rewrite_effect_ids.OrderBy(id=>id,StringComparer.Ordinal))}";
    private static int Stacks(GodotObject u,string id)=>u is Creature c?c.get_status_stacks(id):0;
    private static bool HasHeartUpgrade(GodotObject u,string book,string id)
    {
        var raw=u.GetMeta("combat_heart_method_upgrades",new Dictionary());
        if(raw.VariantType!=Variant.Type.Dictionary)return false;
        return raw.AsGodotDictionary().ContainsKey($"{book}|{id}");
    }
    private static void Gain(GodotObject u,int n){if(u is Creature c)c.gain_qi_blood(n);}
    private static void Energy(GodotObject u,int n){if(u is Creature c)c.current_stamina=Math.Min(c.max_stamina,c.current_stamina+n);}
    private static void Heal(Creature c,float rate)=>c.apply_healing(Mathf.RoundToInt(c.max_health*rate/100));
    private static void Add(GodotObject u,string id,int n)
    {
        if(n<=0||u is not Creature c)return;
        var effect=ResourceLoader.Exists($"res://resources/battle/statuses/{id}.tres")?GD.Load<StatusEffect>($"res://resources/battle/statuses/{id}.tres"):new StatusEffect{effect_id=id,name=id,max_stacks=99,duration_default=-1};
        if(effect is not null)c.add_status(effect,-1,n);
    }
    private static void AddPowerStatus(GodotObject u,ActiveSkillData card)
    {
        if(u is not Creature creature)return;
        var description=card.description;
        var raw=u.GetMeta("combat_heart_method_upgrades",new Dictionary());
        if(raw.VariantType==Variant.Type.Dictionary)
        {
            var upgrades=raw.AsGodotDictionary();
            var prefix=card.source_gongfa_name+"|";
            if(upgrades.TryGetValue(prefix+"current_description",out var current))description=current.AsString();
        }
        var tempering=creature.get_combat_tempering_summary(card.source_gongfa_name);
        if(tempering.Length>0)description+="\n\n修持（仅战斗内）\n"+tempering;
        var effect=new StatusEffect{effect_id="心法:"+card.skill_name,name=card.skill_name,description=description,max_stacks=1,duration_default=-1};
        creature.add_status(effect,-1,1,"power:"+card.skill_name,1,card.source_gongfa_name);
    }
}
