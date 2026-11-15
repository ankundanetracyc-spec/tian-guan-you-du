using System;
using System.Collections.Generic;

/// <summary>节点网 v0.1 参数，见 docs/plans/百工技艺/炼丹/炼丹节点网初始数值表_20261018.md。</summary>
public static class AlchemyProcessConfig
{
    public const int MinimumAttainment=8,MainMana=600,HelperMana=420,MainSpirit=24,HelperSpirit=18,HelperAttainment=30;
    public const int PotionRestore=60,RecoverRestore=30,SecondsPerBreath=720;
    public const double HoursPerBreath=.2,SpiritPerSignedLink=1,Unit=100,MinMain=12,MaxMain=22,CoreReferencePower=1;
    public const double AutomaticProcessingMultiplier=3;
    public const double TestUsableYield=.8; // 只供旧兼容检查，新流程逐类读取 Yield。
    public static readonly double[] Strength=[0,1,1.65,2.9,5.4,10.7,22.6,50.9,121,304];
    public static readonly double[] Precision=[0,10,16,25,38,57,85,126,186,275];
    public static readonly Dictionary<string,double> FixedDefinitionScale=[];
    public sealed record Texture(double Open,double Safe,double Rate);
    public static readonly Dictionary<string,Texture> TestProfiles=new()
    {
        ["木"]=new(24,72,25),["液"]=new(8,63,70),["石"]=new(48,108,15),["金"]=new(58,126,12),
        ["晶"]=new(43,99,19),["玉"]=new(36,94,21),["砂"]=new(29,91,30),["气"]=new(4,49,85),
        ["布"]=new(19,69,33),["羽"]=new(17,66,37),["甲"]=new(39,103,19),["骨"]=new(44,113,16)
    };
    public sealed record Processing(int Open,int OptimalLow,int OptimalHigh,int Safe,double Rate,double Yield,bool Dense);
    public static Processing Profile(MaterialCraftData craft)
    {
        int b=Math.Max(1,craft.material_grade_value())+1;
        Processing profile = craft.texture switch
        {
            "纤维"=>new(Math.Max(1,b-1),b,b+1,b+2,20,.85,false),
            "致密"=>new(b+1,b+2,b+3,b+4,12,.75,true),
            "颗粒"=>new(b,b+1,b+2,b+3,25,.80,false),
            "黏胶"=>new(Math.Max(1,b-1),b,b+1,b+2,18,.90,false),
            "液体"=>new(Math.Max(1,b-2),Math.Max(1,b-1),b,b+1,40,.95,false),
            "气体"=>new(Math.Max(1,b-2),Math.Max(1,b-1),b,b,50,.90,false),
            _=>throw new InvalidOperationException("炼丹加工分类缺项："+craft.texture)
        };
        // 新档位压缩为旧火候窗口的三分之一，保留加工类别的先后关系。
        int Tier(int value)=>(value+2)/3;
        return profile with { Open=Tier(profile.Open),OptimalLow=Tier(profile.OptimalLow),OptimalHigh=Tier(profile.OptimalHigh),Safe=Tier(profile.Safe) };
    }
    public sealed record Tool(int Learn,double Load,double PrecisionCap,double Bonus,string Help,string Book="");
    public static readonly Dictionary<string,Tool> Tools=new()
    {
        ["守炉"]=new(0,1,54,0,"保持当前火候，按当前档付法力；结算整套安排一息"),
        ["添焰"]=new(0,1,54,0,"火候升1档，支付新档法力"),
        ["敛焰"]=new(0,1,54,0,"火候降1档，仍支付减火前档位法力"),
        ["搅和"]=new(0,2,54,0,"安排目标药团混匀；随后用炉令推进"),
        ["分离"]=new(0,2,54,0,"安排分离药性与药渣，减少夹带"),
        ["排渣"]=new(0,1,54,0,"安排排出所选节点80%现有药渣"),
        ["凝丹"]=new(0,1,54,0,"由汇合药团分丹、塑形；不从其他节点自动搬药"),
        ["出炉"]=new(0,0,54,0,"安全档位结束本炉并逐颗判定，不额外推进一息"),
        ["分流御药"]=new(28,1,90,20,"精细照料，提升本息取得精度","牵丝控药篇"),
        ["分批缓投"]=new(18,.5,54,0,"单息投料上限+1份，投料负担×0.8","分炉投药诀"),
        ["精细分离"]=new(42,1.5,110,30,"分离精度+30，并额外增加0.05分离度","辨滓录"),
        ["断络止乱"]=new(60,0,54,0,"开息前预备一次应急：付20法力，暂停最多4线，放弃1项未执行操作","断络救炉诀"),
        ["照料"]=new(0,2,54,0,"照料选定批次本息加工，不额外增加炼化量"),
        ["牵药"]=new(0,2,54,0,"将选定来源少量药性送到目标，下一息才能再流出"),
        ["回流照管"]=new(90,2,180,60,"保存有明确停止条件的回流安排，逐息支付负担","回流复炼篇"),
        ["分丹持形"]=new(110,2,180,60,"凝丹上限+2颗，提升分配精度","分丹成形录"),
        ["百络精御"]=new(300,4,400,150,"精细照料与多目标操作，仍需材料原有功用","百络御药篇")
    };
    public static int FeedLimit(int a)=>a<8?0:1+(int)Math.Floor(Math.Sqrt((a-8)/8d));
    public static int PillLimit(int a)=>a<8?0:2+(int)Math.Floor(Math.Sqrt((a-8)/4d));
    public static double InputLimit(int a)=>Math.Min(10,FeedLimit(a));
    public static double BasePrecision(int a)=>18+4*Math.Sqrt(Math.Max(0,a-8));
    public static int FireLimit(int a)=>a<MinimumAttainment?0:1+a/20;
    public static int MaintenanceMana(int h)=>h<=0?0:(int)Math.Ceiling(h+h*h/4d);
    public static int CommandMana(int before,AlchemyPreviewModel.Hand order,int fireIndex=0)
        =>(int)Math.Ceiling(MaintenanceMana(order==AlchemyPreviewModel.Hand.Raise?before+1:before)*(fireIndex==0?1:.85));
    public static double Healing(int q)
    {
        if(q<20)return 0;int[] x=[20,50,75,90,100];double[] y=[12,18,29,43,62];
        for(int i=1;i<x.Length;i++)if(q<=x[i])return y[i-1]+(y[i]-y[i-1])*(q-x[i-1])/(x[i]-x[i-1]);return 62;
    }
}
