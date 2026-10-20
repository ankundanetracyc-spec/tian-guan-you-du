using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>旧手动基建演示炉局；当前玩家教程使用自动跑炉与独立功用课程。</summary>
public static class AlchemyFactoryScenario
{
    // 0:两味基础运输；1:五味分流增流；2:八味高品质配伍。
    public static AlchemyPreviewModel Create(int lesson=2)
    {
        var m=new AlchemyPreviewModel{Wanted=1};
        m.SelectFormula("sprout_gentle");m.SelectMainHerb("青髓芽");
        void Tick(AlchemyPreviewModel.Hand hand=AlchemyPreviewModel.Hand.Hold)
        {
            m.Action=hand;string error=m.Advance();
            if(error.Length>0||m.Done)throw new InvalidOperationException("教学炉局准备失败："+error+" "+m.Result);
        }
        var ingredients=new List<(string Name,double Amount)>{("青髓芽",.8),("风络藤丝",.4)};
        if(lesson>=1)ingredients.AddRange([("溪纹藻丝",.4),("乌羽片",.4),("雾铃花",.3)]);
        if(lesson>=2)ingredients.AddRange([("回泉珠",.4),("凝露松脂",.3),("凝霜叶",.3)]);
        foreach(var (name,amount) in ingredients)
        {
            if(m.Pending.Sum(d=>d.Original)/100+amount>m.FeedLimit)Tick(AlchemyPreviewModel.Hand.Raise);
            string queued=m.QueueHerb(name,amount);
            if(!m.Pending.Any(d=>d.Herb.Name==name))throw new InvalidOperationException(queued);
        }
        Tick(AlchemyPreviewModel.Hand.Raise);
        while(m.Heat<2)Tick(AlchemyPreviewModel.Hand.Raise);
        for(int guard=0;m.InFurnace.Any(d=>d.Raw>.001)&&guard<20;guard++)Tick();
        while(m.Heat>1)Tick(AlchemyPreviewModel.Hand.Lower);
        int Id(string name)=>m.InFurnace.Single(d=>d.Herb.Name==name).Id;
        int feed=Id("风络藤丝");
        m.SignLink(feed,0,"通");
        if(lesson>=1){int relay=Id("溪纹藻丝");m.SignLink(feed,relay,"通");m.SignLink(relay,0,"通");}
        Tick();
        foreach(var route in m.SignedLinks.Where(l=>l.UtilityId=="通"))route.FlowOpen=false;
        var mainFeed=new AlchemyPreviewModel.Operation(AlchemyPreviewModel.Hand.Draw,feed,Id("青髓芽"));
        m.PendingOperations.Add(mainFeed);Tick();
        if(lesson>=1)
        {
            var trunk=m.SignedLinks.Single(l=>l.UtilityId=="通"&&l.SourceDoseId==feed&&l.TargetDoseId!=0);
            m.SignFlowSupport(Id("乌羽片"),trunk);m.SignLink(Id("雾铃花"),feed,"引");
            var guide=m.SignedLinks.Single(l=>l.UtilityId=="引");
            foreach(var l in m.SignedLinks.Where(l=>l.UtilityId=="通"&&l.SourceDoseId==feed))guide.BranchWeights[l]=l==trunk?3:1;
        }
        if(lesson>=2){m.SignLink(Id("回泉珠"),0,"聚");m.SignLink(Id("凝露松脂"),0,"固");}
        foreach(var l in m.SignedLinks)
        {
            if(l.UtilityId=="通"){l.FlowOpen=true;l.TargetAmount=l.SourceDoseId==feed?(l.TargetDoseId==0?45:120):170;}
            else l.Repeat=true;
        }
        m.RepeatingOperations.Add(mainFeed);m.PendingOperations.Add(mainFeed);
        if(lesson>=2)
        {
            m.RepeatingOperations.Add(new(AlchemyPreviewModel.Hand.Draw,feed,Id("回泉珠")));
            m.RepeatingOperations.Add(new(AlchemyPreviewModel.Hand.Draw,feed,Id("凝霜叶")));
        }
        m.Action=AlchemyPreviewModel.Hand.Hold;
        m.Explanation=lesson switch
        {
            0=>"两味运输：青髓芽以基本牵药送至风络藤丝，再沿通路缓流入汇合位。先看懂来源、通路与每息到货。",
            1=>"五味调流：风络藤丝分成直汇与溪纹藻丝中继两支，乌羽片给首段增流，雾铃花控制分支权重。可暂停行、修改比例，比较实际流量。",
            _=>"八味配伍：青髓芽供生机，回泉珠的化与凝霜叶的藏实际送入主干。乌羽片增流，雾铃花分支，回泉珠聚药，松脂稳形。高品质需生、固、化、藏实际到丹体。"
        };
        m.LastFeedback=m.Explanation;m.AssertConservation();return m;
    }
}
