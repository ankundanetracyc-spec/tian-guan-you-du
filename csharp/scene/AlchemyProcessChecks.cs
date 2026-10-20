using Godot;
using System;
using System.Linq;

/// <summary>节点流程的实际模型检查，与场景按钮检查共用同一模型。</summary>
public static class AlchemyProcessChecks
{
    // 保持构造炉况使用的Godot Resource存活，避免原生测试运行中提前析构脚本资源。
    private static readonly System.Collections.Generic.List<AlchemyPreviewModel> Fixtures=[];
    private static AlchemyPreviewModel Track(AlchemyPreviewModel model){Fixtures.Add(model);return model;}
    public static void Run()
    {
        int count=0;
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("节点炼丹："+name);GD.Print("ALCHEMY_PROCESS_OK "+name);count++;}
        void Near(double a,double b,string name)=>Check(Math.Abs(a-b)<1e-6,name);
        AlchemyPreviewModel Model()=>Track(new(){Attainment=1000,MainSpirit=1000,Mana=10000,Wanted=1});
        AlchemyPreviewModel.Dose Seed(AlchemyPreviewModel m,string utility,int id,double available=60,double raw=0,string texture="纤维")
        {
            var baseHerb=m.Herbs.First(h=>h.Name=="青髓芽");
            var craft=new MaterialCraftData{texture=texture,legacy_texture="木",element="",polarity=0};
            if(utility.Length>0)craft.utilities.Add(new MaterialUtilityGrade{utility_id=utility,grade=1});
            var herb=new AlchemyPreviewModel.Herb(baseHerb.Name,baseHerb.Item,craft);
            var d=new AlchemyPreviewModel.Dose(herb,id==1?"主药":"辅药",1){Id=id,Original=100,Raw=raw,Available=available,Slag=100-available-raw,MaxPrecision=1000,Mixed=1,Open=texture=="致密"?0:1,Extracted=100-raw};
            m.InFurnace.Add(d);return d;
        }
        void Tick(AlchemyPreviewModel m,AlchemyPreviewModel.Hand command=AlchemyPreviewModel.Hand.Hold){m.Action=command;string e=m.Advance();if(e.Length>0)throw new InvalidOperationException(e);m.AssertConservation();}
        var pending=Track(new AlchemyPreviewModel());
        pending.QueueHerb("青髓芽",.5);pending.QueueOperation(AlchemyPreviewModel.Hand.Care,0);
        Check(pending.Breath==0&&pending.Pending.Count==1&&pending.InFurnace.Count==0,"投料和灵识操作只安排，不推进");
        Tick(pending,AlchemyPreviewModel.Hand.Raise);Check(pending.Breath==1&&pending.InFurnace.Count==1&&pending.PendingOperations.Count==0,"一个炉令提交整息并清空临时安排");
        Near(pending.Mana,598,"0到1付新档2法力");
        Check(new[]{7,8,19,20,36,39,40,59,60}.Select(AlchemyProcessConfig.FireLimit).SequenceEqual(new[]{0,1,1,2,2,2,3,3,4}),"每20造诣增加一档火候，保留8点开炉门槛");
        pending.Heat=2;pending.Action=AlchemyPreviewModel.Hand.Raise;int cappedBreath=pending.Breath;double cappedMana=pending.Mana;
        Check(pending.Advance().Length>0&&pending.Breath==cappedBreath&&pending.Mana==cappedMana,"造诣36不可升至3档且不扣资源");
        pending.Attainment=60;
        pending.Heat=3;pending.Mana=600;Tick(pending,AlchemyPreviewModel.Hand.Raise);Near(pending.Mana,592,"3到4付8法力");
        pending.Mana=600;pending.Heat=3;Tick(pending);Near(pending.Mana,594,"守3付6法力");
        pending.Mana=600;pending.Heat=3;Tick(pending,AlchemyPreviewModel.Hand.Lower);Near(pending.Mana,594,"3到2仍付原档6法力");
        var clock=Model();for(int i=0;i<10;i++)Tick(clock);Near(clock.ElapsedHours,2,"十息等于一时辰两小时");
        var insufficient=Model();insufficient.Mana=0;Check(insufficient.Advance()=="","无火守炉可执行");int breath=insufficient.Breath;
        insufficient.Action=AlchemyPreviewModel.Hand.Raise;Check(insufficient.Advance().Length>0&&insufficient.Breath==breath&&insufficient.Mana==0,"法力不足整息不提交");
        var book=Track(new AlchemyPreviewModel());book.LibraryBooks.Clear();book.LearnFine();Check(!book.FineLearned,"无书不因造诣自动学会");
        book.LibraryBooks.Add(AlchemyProcessConfig.Tools["分流御药"].Book);book.LearnFine();Check(book.FineLearned&&book.StudyHours==2,"持书达标研读一时辰");
        book.Helper=true;Check(!book.CanUse(AlchemyPreviewModel.Hand.Fine),"副手不继承本人技巧");
        var flow=Model();var d1=Seed(flow,"通",1);Seed(flow,"通",2,0);
        flow.SignLink(1,2,"通");Tick(flow);var route=flow.SignedLinks.Single();
        Check(route.Built&&flow.NodeAvailable(2)==0,"新建通路本息不运输");
        Tick(flow);Near(route.LastFlow,4,"没有行也有每息4单位基础导流");
        Check(flow.NodeSlag(2)>0,"运输保留真实夹渣，不自动提纯");
        Seed(flow,"行",3);Check(!flow.SignFlowSupport(3,route).Contains("没有"),"独立行材料可以给其他材料通路供力");
        Tick(flow);Check(route.LastFlow>4&&route.LastFlow<=20,"行增流且受路线容量限制");
        Near(flow.SustainedLoad,2,"一条通加一条行占两个线位");
        var support=flow.SignedLinks.Single(l=>l.UtilityId=="行");flow.PauseLink(route);Check(support.Paused&&flow.SustainedLoad==0,"暂停通路联动停行并释放线位");
        var chain=Model();Seed(chain,"通",1);Seed(chain,"通",2,0);Seed(chain,"",3,0);
        chain.SignLink(1,2,"通");Tick(chain);chain.SignLink(2,3,"通"); // 节点2需先实际接到药，再签自己的通。
        Check(chain.SignedLinks.Count==1,"没有可用来源的中继节点不能先建结构");
        Tick(chain);chain.SignLink(2,3,"通");Tick(chain);Check(chain.NodeAvailable(3)==0,"新到与新建都不能同息串联");
        Tick(chain);Check(chain.NodeAvailable(3)>0,"已建中继下一息可转送且逐源守恒");
        var empty=Model();Seed(empty,"生",1);empty.QueueOperation(AlchemyPreviewModel.Hand.Gather);empty.Heat=1;Tick(empty);
        Check(empty.Pills.Count==0,"凝丹不能从远端节点隔空取药");
        var hub=Model();hub.SelectedMainHerbName="青髓芽";var main=Seed(hub,"生",1,80);
        hub.QueueOperation(AlchemyPreviewModel.Hand.Draw,0,1);hub.QueueOperation(AlchemyPreviewModel.Hand.Gather);hub.Heat=1;Tick(hub);
        Check(hub.NodeAvailable(0)>0&&hub.Pills.Count==0,"当息到货不能同息分丹");
        for(int i=0;i<3;i++){hub.QueueOperation(AlchemyPreviewModel.Hand.Draw,0,1);Tick(hub);}
        for(int i=0;i<14;i++){hub.QueueOperation(AlchemyPreviewModel.Hand.Circulate);hub.QueueOperation(AlchemyPreviewModel.Hand.Separate);hub.QueueOperation(AlchemyPreviewModel.Hand.Purge);hub.QueueOperation(AlchemyPreviewModel.Hand.Gather);Tick(hub);}
        hub.Heat=0;hub.QueueOperation(AlchemyPreviewModel.Hand.Output);
        Check(hub.Done&&hub.Pills.Count==1&&hub.Pills[0].Score>=20,"实际输送、分离排渣、分丹稳形到成丹全流程");
        // 十二功用逐一选择真实目标，比较施用前后的对应状态。
        foreach(string utility in AlchemyPreviewModel.Purposes.Keys)
        {
            if(utility is "通" or "行")continue;
            var m=Model();var target=Seed(m,"生",1,40);Seed(m,utility,2,60);
            if(utility=="破"){target.Raw=60;target.Slag=0;target.Open=0;target.Herb.Craft.texture="致密";m.Heat=1;}
            if(utility=="化"){target.Raw=60;target.Slag=0;target.Herb.Craft.texture="黏胶";m.Heat=Math.Max(1,target.Herb.Processing.Open-2);}
            if(utility=="生")target.Disorder=.3;
            if(utility=="镇")target.Agitation=40;
            if(utility=="护")m.Heat=target.Herb.Processing.Safe+2;
            if(utility=="藏")target.Herb.Craft.texture="气体";
            if(utility=="聚"){m.QueueOperation(AlchemyPreviewModel.Hand.Draw,0,1);Tick(m);}
            if(utility=="引")
            {
                target.Herb.Craft.utilities.Add(new MaterialUtilityGrade{utility_id="通",grade=1});Seed(m,"",3,0);m.SignLink(1,3,"通");Tick(m);
            }
            int dest=utility=="聚"?0:1;
            if(utility=="固"){m.QueueOperation(AlchemyPreviewModel.Hand.Draw,0,1);Tick(m);dest=0;}
            m.SignLink(2,dest,utility);Tick(m);
            Check(m.Contributions.Keys.Any(k=>k.EndsWith("·"+utility)),"真实目标施用与来源划账："+utility);
            m.AssertConservation();
        }
        var isolated=Model();Seed(isolated,"生",1);Seed(isolated,"护",2);Tick(isolated);
        Check(isolated.Contributions.Count==0,"未签线未安排时材料功用不全炉自动触发");
        var overload=new AlchemyPreviewModel{MainSpirit=1};overload.QueueHerb("青髓芽");Tick(overload);
        Check(overload.Done&&overload.Result.Contains("炸炉"),"无应急的真实超载炸炉");
        var zero=new AlchemyPreviewModel();zero.Action=AlchemyPreviewModel.Hand.Lower;Check(zero.Advance().Length>0&&zero.Breath==0,"零档不能敛焰");
        AlchemyPreviewModel FinishedBlend(string mainName,params (string Name,double Amount)[] assistants)
        {
            var model=Model();model.SelectedMainHerbName=mainName;
            var pill=new AlchemyPreviewModel.Pill{Index=1,Form=1,MainHerbName=mainName};model.Pills.Add(pill);
            void Add(string name,double amount,bool main)
            {
                var herb=model.Herbs.Single(h=>h.Name==name);
                var dose=new AlchemyPreviewModel.Dose(herb,main?"主药":"辅药",1){Id=model.InFurnace.Count+1,Raw=0,Allocated=amount,Expelled=100-amount,MaxPrecision=1000,Mixed=1};
                model.InFurnace.Add(dose);pill.Parts.Add(new(dose,amount));
            }
            Add(mainName,22,true);foreach(var (name,amount) in assistants)Add(name,amount,false);
            model.Finish();model.AssertConservation();return model;
        }
        var single=FinishedBlend("青髓芽");Check(single.Pills[0].Score==50,"单味满主药、满稳形与混匀仍只到50分");
        var strong=FinishedBlend("青木芯");Check(strong.Pills[0].Score<=50,"高品主药不能单独绕过配伍要求");
        var three=FinishedBlend("青髓芽",("回泉珠",6),("山蚕长丝",8));Check(three.Pills[0].Score>=90,"三味互补配伍可以达到高品质");
        var alternative=FinishedBlend("青髓芽",("月露菌汁",6),("凝露松脂",8),("凝霜叶",4));Check(alternative.Pills[0].Score>=90,"不同四味组合也可达高品质，不锁唯一丹方");
        var noConversion=FinishedBlend("青髓芽",("山蚕长丝",8));Check(noConversion.Pills[0].Score<=65,"缺化药性时不能靠稳固与封藏顶替");
        var noSeal=FinishedBlend("青髓芽",("回泉珠",6),("凝露松脂",8));Check(noSeal.Pills[0].Score<=75,"缺藏药性时不能到优效以上");
        var dust=FinishedBlend("青髓芽",("回泉珠",.00001),("山蚕长丝",.00001));Check(dust.Pills[0].Score<60,"象征性掺一点辅材不等于完成高品质配伍");
        var branches=Model();Seed(branches,"通",1,6);Seed(branches,"引",2);Seed(branches,"",3,0);Seed(branches,"",4,0);
        branches.SignLink(1,3,"通");branches.SignLink(1,4,"通");Tick(branches);
        var left=branches.SignedLinks[0];var right=branches.SignedLinks[1];branches.SignLink(2,1,"引");var distributor=branches.SignedLinks.Last();
        distributor.BranchWeights[left]=9;distributor.BranchWeights[right]=1;Tick(branches);
        Check(left.LastFlow>0&&right.LastFlow>0&&Math.Abs(left.LastFlow/right.LastFlow-9)<1e-6,"引的9比1设置实际改变分支流量，未凭空增加来源");
        var emptied=Model();Seed(emptied,"通",1,3);Seed(emptied,"",2,0);emptied.SignLink(1,2,"通");
        Check(emptied.LiveNodeIds().Contains(2),"空料接收节点有真实待入通路时保留");Tick(emptied);double boundBefore=emptied.InFurnace[0].Bound,slagBefore=emptied.Inventory.Sum(p=>p.Slag);Tick(emptied);
        Check(!emptied.LiveNodeIds().Contains(1)&&emptied.LiveNodeIds().Contains(2)&&emptied.SignedLinks.All(l=>l.Paused),"药性与原料彻底耗尽后退场，相关控制线释放灵识");
        Check(Math.Abs(emptied.InFurnace[0].Bound-boundBefore)<1e-6&&Math.Abs(emptied.Inventory.Sum(p=>p.Slag)-slagBefore)<1e-6,"耗尽退场保留已付工艺绑定和炉内残渣，不凭空抹账");
        var unrefined=Model();Seed(unrefined,"",1,0,100);Check(unrefined.LiveNodeIds().Contains(1),"药性尚未炼出但仍有原料时不退场");
        var emptyCycle=Model();Seed(emptyCycle,"通",1,0);Seed(emptyCycle,"通",2,0);emptyCycle.SignedLinks.Add(new(1,2,"通"){Built=true});emptyCycle.SignedLinks.Add(new(2,1,"通"){Built=true});
        Check(emptyCycle.LiveNodeIds().Count==0,"没有原料或药性供给的空环不能假装后续进料");
        foreach(bool assistant in new[]{false,true})
        {
            var course=new AlchemySimpleLesson(assistant);Fixtures.Add(course.Model);
            Check(course.Model.Breath==0&&course.Model.InFurnace.Count==0&&course.Model.Pending.Count==0,"九品基础教程从第0息空炉开始："+assistant);
            for(int step=0;step<90&&!course.Model.Done;step++)course.Step();
            Check(course.Model.Done&&course.Model.Pills.Count==1&&course.Model.Pills[0].Score>=20&&course.Model.Learned.Count==0&&course.Model.RepeatingOperations.Count==0,"基础炉务与功用线足以完成九品丹："+assistant+" stage="+course.Stage);
            GD.Print($"ALCHEMY_SIMPLE_RESULT assistant={assistant} breaths={course.Model.Breath} score={course.Model.Pills.FirstOrDefault()?.Score} mana={course.Model.Mana}");
        }
        GD.Print("ALCHEMY_BLEND_RESULTS single="+single.Pills[0].Score+" three="+three.Pills[0].Score+" alternative="+alternative.Pills[0].Score);
        GD.Print("ALCHEMY_PROCESS_CHECKS_PASSED "+count);
    }
}
