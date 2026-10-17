using Godot;
using System;
using System.Linq;

/// <summary>仅在显式 --alchemy-check 下运行的炼丹原型检查。</summary>
public static class AlchemyPreviewChecks
{
    public static void Run()
    {
        int count=0;
        void Check(bool condition,string label){if(!condition)throw new InvalidOperationException("炼丹检查失败："+label);count++;GD.Print("ALCHEMY_CHECK_OK "+label);}
        AlchemyPreviewModel Wood(){var m=new AlchemyPreviewModel();m.QueueHerb("青木芯");return m;}
        var m=Wood();Check(m.Pending.Count==1&&m.InFurnace.Count==0,"拖拽先待投");m.Advance();Check(m.InFurnace.Count==1&&m.Pending.Count==0,"推进后入炉");
        m.Heat=2;for(int i=0;i<8;i++)m.Advance();Check(m.InFurnace[0].Released>.9,"木质需要实际受热炼化");
        Check(Math.Abs(m.InFurnace[0].Balance-100)<1e-6,"单来源物质守恒");
        m.Temperature=90;m.Heat=4;m.QueueHerb("苦苔根");m.Advance();Check(m.InFurnace.Last().Destroyed>0,"过早投入木质发生热伤");
        m.Temperature=55;m.Heat=2;m.FineLearned=true;m.Action=AlchemyPreviewModel.Hand.Fine;for(int i=0;i<4;i++)m.Advance();
        Check(m.InFurnace.All(d=>Math.Abs(d.Balance-100)<1e-6),"多功用消耗不重复计算物质");
        var wood=m.InFurnace.First(d=>d.Herb.Name=="青木芯");wood.Exposure=3;wood.Disorder=0;Check(wood.Readiness("通")>wood.Readiness("生"),"同份药性高品能力取得要求更高");
        double earlier=m.CarryRisk;m.Action=AlchemyPreviewModel.Hand.Separate;m.Advance();Check(m.CarryRisk<earlier,"分离降低预计夹带");
        double slag=m.Slag;m.Action=AlchemyPreviewModel.Hand.Purge;m.Advance();Check(m.Slag<slag,"排出真实残渣");Check(m.InFurnace.All(d=>Math.Abs(d.Balance-100)<1e-6),"排出后物质守恒");
        var one=Wood();var many=Wood();many.QueueHerb("青木芯");Check(many.Count==one.Count*2,"主药量决定理论产量");Check(many.Preview().Spirit>one.Preview().Spirit&&many.Preview().Mana>=one.Preview().Mana,"大炉增加控制及供火负担");
        var low=Wood();low.Attainment=7;Check(low.Advance().Length>0&&low.Breath==0,"九品造诣门槛8");Check(!low.LearnFine().StartsWith("已"),"技术学习资格");
        var idle=Wood();idle.Attainment=32;Check(!idle.FineLearned,"造诣达标不自动学会");idle.Action=AlchemyPreviewModel.Hand.Fine;Check(idle.Advance().Length>0,"未学习不能施展");
        var high=Wood();high.Attainment=3000;Check(Math.Abs(high.Preview().Spirit-one.Preview().Spirit)<1e-6,"造诣不凭空打折灵识");
        var overload=Wood();overload.Attainment=200;for(int i=0;i<23;i++)overload.QueueHerb("青木芯");overload.Advance();Check(overload.Done&&overload.Result.Contains("炸炉"),"超出灵识容量炸炉");
        var helper=Wood();helper.Helper=true;helper.Recover=true;helper.Mana=40;helper.Advance();Check(helper.Mana==65&&helper.HelperMana<110,"副手顶炉与本人回气");
        var potion=Wood();potion.Mana=20;potion.TakePotion=true;potion.Advance();Check(potion.Mana>20&&potion.Potions==1&&!potion.TakePotion,"服药只执行一次");
        var early=Wood();early.Action=AlchemyPreviewModel.Hand.Output;early.Advance();Check(early.Pills.All(p=>p.Score<20),"提前出炉有明确失败判定");
        var finished=Wood();finished.Pending.Clear();var d=new AlchemyPreviewModel.Dose(finished.Herbs.First(h=>h.Name=="青木芯"),"主药",1){Raw=0,Available=72,Slag=0,Expelled=28,Exposure=20};finished.InFurnace.Add(d);
        // 加入阴性可用部分使试方组成达标，不能靠减产修正错误组成。
        finished.InFurnace.Add(new(finished.Herbs.First(h=>h.Name=="苦苔根"),"辅药",2){Raw=0,Available=30,Expelled=70,Exposure=20});finished.Fusion=1;finished.Shape=1;finished.Uniform=.6;finished.Concentration=1;finished.Temperature=55;finished.Finish();
        Check(finished.Pills.Count==3&&finished.Pills.All(p=>p.Score>=20),"逐颗成丹试算");Check(finished.Pills.Select(p=>p.MainAmount).Distinct().Count()>1,"分配状态带来实际颗间差异");
        finished.Wanted=1;finished.Finish();Check(finished.Pills[0].Score<=100,"减产单颗封顶100");
        Check(new[]{19,20,50,51,75,76,90,91,100}.Select(AlchemyPreviewModel.Tier).SequenceEqual(new[]{"未成丹","薄效","薄效","良效","良效","优效","优效","极效","极效"}),"评级边界");
        // 隔离一项真实材料功用，对照有/无该能力时的实际炉内变化；不改材料资源。
        AlchemyPreviewModel Scenario(string utility,bool enabled)
        {
            var t=new AlchemyPreviewModel();
            AlchemyPreviewModel.Herb Isolate(AlchemyPreviewModel.Herb h,string id)
            {
                var craft=new MaterialCraftData{element=h.Craft.element,polarity=h.Craft.polarity,texture=h.Craft.texture};
                if(enabled&&id.Length>0) craft.utilities.Add(new MaterialUtilityGrade{utility_id=id,grade=h.Grade(id)});
                return new(h.Name,h.Item,craft);
            }
            var main=t.Herbs.First(h=>h.Name=="青木芯");
            var blank=new MaterialCraftData{element=main.Craft.element,polarity=0,texture=main.Craft.texture};
            t.InFurnace.Add(new(new(main.Name,main.Item,blank),"主药",1){Raw=0,Available=70,Slag=10,Expelled=20,Exposure=50});
            var source=t.Herbs.First(h=>h.Grade(utility)>0);
            t.InFurnace.Add(new(Isolate(source,utility),"辅药",2){Raw=0,Available=60,Expelled=40,Exposure=50});
            t.Temperature=55;t.Heat=2;
            if(utility is "破" or "化") t.InFurnace.Add(new(t.Herbs.First(h=>h.Name=="凝露松脂"),"辅药",3){Raw=80,Expelled=20});
            if(utility is "聚" or "引" or "固")t.Action=AlchemyPreviewModel.Hand.Gather;
            if(utility=="固")t.Concentration=.5;
            if(utility=="镇")t.Agitation=25;
            if(utility=="护"){t.Temperature=90;t.Heat=4;}
            if(utility=="生")t.InFurnace[0].Disorder=.3;
            if(utility=="明")t.Action=AlchemyPreviewModel.Hand.Separate;
            t.Advance();return t;
        }
        foreach(string u in AlchemyPreviewModel.Purposes.Keys)
        {
            var with=Scenario(u,true);var without=Scenario(u,false);
            double Metric(AlchemyPreviewModel x)=>u switch
            {
                "破"=>x.InFurnace.Last().Open,"化"=>-x.InFurnace.Last().Raw,
                "通"=>x.Connection,"行"=>x.Fusion,"聚"=>x.Concentration,"引"=>x.Uniform,"固"=>x.Shape,
                "镇"=>-x.Agitation,"护"=>-x.InFurnace[0].Destroyed,"生"=>-x.InFurnace[0].Disorder,
                "明"=>x.InFurnace[0].Separation,_=>-x.InFurnace[0].Escaped
            };
            Check(Metric(with)>Metric(without)+1e-8&&with.Contributions.Keys.Any(k=>k.EndsWith("·"+u)),"功用实际对照："+u);
        }
        var noWork=Scenario("行",true);noWork.Fusion=noWork.Uniform=1;double used=noWork.InFurnace.Sum(d=>d.Spent);noWork.Advance();
        Check(Math.Abs(noWork.InFurnace.Sum(d=>d.Spent)-used)<1e-8,"已混匀且分配均匀时行不空耗");
        var demoted=Wood();demoted.LearnFine();demoted.Attainment=8;demoted.Action=AlchemyPreviewModel.Hand.Fine;
        Check(demoted.Advance().Length>0&&demoted.Breath==0,"学习资格与当前施展门槛分别检查");
        GD.Print($"ALCHEMY_CHECKS_PASSED {count}");
    }
}
