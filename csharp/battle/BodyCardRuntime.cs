using Godot;
using Godot.Collections;

/// <summary>现有锻躯卡表的局内条件、心法与公共资源。状态按单位保存，战斗结束释放。</summary>
public sealed class BodyCardRuntime
{
    private const float YouhaiPassiveCooldownDurationFrames = 360f;
    private readonly BattleManager manager;
    private readonly System.Collections.Generic.Dictionary<ulong,State> states=[];
    private sealed class State
    {
        public HashSet<string> Powers=[]; public HashSet<string> Used=[];
        public int Falls, Discounts, ConsumedWounds, SpentYouhai, LastType=-1; public float YouhaiPassiveCooldownFrames; public bool Bottom, Boost, BurnTriggered, YouhaiEmpoweredMountain;
        public ActiveSkillData? PendingCard; public float PendingMomentum;
    }
    private State For(GodotObject u){if(!states.TryGetValue(u.GetInstanceId(),out var s))states[u.GetInstanceId()]=s=new();return s;}
    public BodyCardRuntime(BattleManager owner)=>manager=owner;
    public void Reset()=>states.Clear();
    public void Begin(GodotObject u){var s=For(u);s.Used.Clear();s.Falls=s.Discounts=s.ConsumedWounds=0;s.LastType=-1;s.YouhaiPassiveCooldownFrames=0;s.PendingCard=null;s.PendingMomentum=0;s.Bottom=s.Boost=s.BurnTriggered=s.YouhaiEmpoweredMountain=false;}
    public void Tick(GodotObject u,float frames)
    {
        if(frames<=0||!GodotObject.IsInstanceValid(u))return;
        var s=For(u);s.YouhaiPassiveCooldownFrames=Math.Max(0f,s.YouhaiPassiveCooldownFrames-frames);
    }
    private static bool Main(GodotObject u,string book)=>(u is Character c && c.main_gongfa?.gongfa_name==book) || (u is WorldEntity e && e.entity_data is HumanData h && h.main_gongfa_path.Contains(book));
    public bool CanPlay(GodotObject u,ActiveSkillData card)
    {
        var s=For(u);var qi=CardRules.Number(u,"qi_blood");var you=Stacks(u,"游骸");
        return card.skill_name switch
        {
            "抱一"=>s.Falls>0&&!s.Used.Contains("抱一"),
            "伏火"=>qi>=1&&!s.Used.Contains("伏火"),
            "归藏"=>s.Falls>=2&&!s.Used.Contains("归藏"),
            "托身" or "解形"=>you>0,
            "冯虚"=>s.SpentYouhai>=2,
            "去故"=>(manager.is_realtime_battle()?manager.count_wounds(u):manager.hand_cards(u).Count(IsWound))>=2,
            _=>true
        };
    }
    public int Cost(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);var discount=!CardRules.IsResponse(c)&&IsBody(c)&&s.Discounts>0?1:0;
        if(c.skill_name=="催血"&&s.Boost)discount++;
        return Math.Max(0,c.get_energy_cost()-discount);
    }
    public void Before(GodotObject u,ActiveSkillData c)
    {
        var s=For(u);s.Used.Add(c.skill_name);
        s.PendingMomentum=Momentum(u,c);s.PendingCard=c;s.LastType=(int)c.skill_type;
        if(!CardRules.IsResponse(c)&&IsBody(c)&&s.Discounts>0)s.Discounts--;
        s.YouhaiEmpoweredMountain=c.skill_name=="撼山"&&Stacks(u,"游骸")>0&&SpendYouhai(u);
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
        var s=For(u);var before=creature.qi_blood;var spent=Math.Min(Math.Max(0,count),before);
        if(spent==0)return;creature.qi_blood-=spent;
        if(active && s.Powers.Contains("赤阳参同"))creature.blood_furnace_value=Math.Min(creature.blood_furnace_value_cap,creature.blood_furnace_value+Mathf.RoundToInt(spent*rate));
        else Heal(creature,spent*rate);
        if(active)
        {
            s.Falls++;
            if(s.Powers.Contains("黄庭养形"))
            {
                if(s.Falls==1)Add(u,"身法",1);
                if(s.Falls==2&&Main(u,"黄庭养形篇"))Energy(u,1);
            }
            if(s.Powers.Contains("乘物游骸")&&s.YouhaiPassiveCooldownFrames<=0)
            {
                Add(u,"游骸",1);
                s.YouhaiPassiveCooldownFrames=YouhaiPassiveCooldownDurationFrames;
            }
        }
        if(before==1&&creature.qi_blood==0&&!s.Bottom&&s.Powers.Contains("补虚行血")){s.Bottom=true;s.Boost=true;}
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
            case "平息": Fall(u,1,true);return true;
            case "返照": Fall(u,1,true);Add(u,"身法",2);return true;
            case "伏火": Fall(u,1,true,6);return true;
            case "蜕形": Fall(u,1,true);Add(u,"身法",2);return true;
            case "周行": Gain(u,1);if(s.Falls>0)Add(u,"身法",1);return true;
            case "归藏": Add(u,"身法",3);s.Discounts=2;return true;
            case "还阳": Gain(u,CardRules.Number(u,"qi_blood")==0?2:1);return true;
            case "催血":
                Gain(u,s.Boost?2:1);
                if(s.Boost&&Main(u,"补虚行血诀"))Add(u,"身法",1);
                s.Boost=false;return true;
            case "抱一": Gain(u,1);return true;
            case "托身": SpendYouhai(u);Add(u,"身法",2);return true;
            case "淬厉": manager.add_wound_card(u,"内伤",2);Add(u,"身法",1);return true;
            case "去故":
                var removed=manager.consume_hand_wounds(u,3);Add(u,"借力",removed);
                if(removed>0&&s.ConsumedWounds==0&&s.Powers.Contains("灵枢经筋"))Add(u,"借力",1);
                s.ConsumedWounds+=removed;
                if(removed>=3&&s.Powers.Contains("灵枢经筋")&&Main(u,"灵枢经筋篇"))Gain(u,1);
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
    public int Segments(GodotObject u,ActiveSkillData card)=>card.skill_name=="冯虚"?1+For(u).SpentYouhai/2:1;
    public float DamageMultiplier(GodotObject u,ActiveSkillData c)
    {
        if(c.damage_type!="physical")return 1;
        var s=For(u);float multiplier=c.skill_name=="合劲"?1:1+Stacks(u,"借力")*.05f;
        if(c.skill_name=="撼山"&&s.YouhaiEmpoweredMountain)multiplier*=1.5f;
        if(s.Powers.Contains("内景损益")&&IsBody(c))multiplier*=1+manager.count_wounds(u)*(Main(u,"内景损益篇")?.05f:.03f);
        return multiplier;
    }
    public void AfterAttack(GodotObject u,ActiveSkillData card)
    {
        if(card.skill_name=="解形")SpendYouhai(u);
        if(card.skill_name=="撼山")For(u).YouhaiEmpoweredMountain=false;
        if(u is Creature c)
        {
            if(card.skill_name=="偃月")c.blood_furnace_value=Math.Max(0,c.blood_furnace_value-20);
            if(card.skill_name=="朱明")c.blood_furnace_value=0;
        }
        if(card.damage_type=="physical"&&card.skill_name!="挟势"&&u is Creature creature)creature.consume_status_stacks("借力",creature.get_status_stacks("借力"));
    }
    public ActiveSkillData? FurnaceFollowup(GodotObject u,ActiveSkillData card)
    {
        var s=For(u);
        var spent=card.skill_name=="偃月"?Math.Min(20,CardRules.Number(u,"blood_furnace_value")):card.skill_name=="朱明"?CardRules.Number(u,"blood_furnace_value"):0;
        if(spent<=0||s.BurnTriggered||!s.Powers.Contains("赤阳参同")||!Main(u,"赤阳参同经"))return null;
        if(spent<20)return null;
        s.BurnTriggered=true;
        return new ActiveSkillData{skill_name="赤阳参同追击",base_damage=4+spent*.20f,damage_bonuses=[new DamageBonus{attribute="physique",multiplier=.25f},new DamageBonus{attribute="body_proficiency",multiplier=.15f}],damage_type="physical"};
    }
    public void ResponseResolved(GodotObject u,int blocked,bool unharmed,bool chengji)
    {
        if(chengji&&unharmed)Add(u,"借力",1);
        if(For(u).Powers.Contains("阴符应物"))Add(u,"借力",blocked/(Main(u,"阴符应物经")?3:4));
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
    public static bool IsBody(ActiveSkillData c)=>c.source_gongfa_name is "黄庭养形篇" or "赤阳参同经" or "内景损益篇" or "灵枢经筋篇" or "阴符应物经" or "乘物游骸真经" or "补虚行血诀" || c.skill_name is "发劲" or "催血" or "平息" or "卸力" or "撼山";
    private static int Stacks(GodotObject u,string id)=>u is Creature c?c.get_status_stacks(id):0;
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
        var effect=new StatusEffect{effect_id="心法:"+card.skill_name,name=card.skill_name,description=card.description,max_stacks=1,duration_default=-1};
        creature.add_status(effect,-1,1,"power:"+card.skill_name,1,card.source_gongfa_name);
    }
}
