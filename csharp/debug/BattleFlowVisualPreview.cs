using Godot;
using GArray=Godot.Collections.Array;
/// <summary>独立战斗流程视觉验收场景，不进入正式战斗或写入存档。</summary>
public partial class BattleFlowVisualPreview : Node
{
    public override async void _Ready()
    {
        try
        {
            if(OS.GetCmdlineUserArgs().Contains("--portrait-check")){await CheckPortraits();GetTree().Quit();return;}
            var view=new BattleFlowView();AddChild(view);
            var units=new List<BattleEntityData>();
            var names=new[]{"林清远","叶轻舟","苏墨","陈云川","白芷"};
            for(int side=1;side<=2;side++)for(int i=0;i<5;i++)units.Add(new BattleEntityData{uid=$"preview-{side}-{i}",entity_name=side==1?names[i]:$"试炼盗匪·{i+1}",faction=side,entity_type=0,portrait_data=new HumanData{gender=i%2,appearance_seed=121+i+side*17},current_age=24,cultivation_realm="云息一层",max_health=190+i*3,current_health=190+i*3,initiative_speed=100+i*4,dun_speed=100});
            var folder="res://docs/美术规范/战斗流程概念_20261009/实机验收";
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(folder));
            async System.Threading.Tasks.Task Capture(string name)
            {
                for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(folder+"/"+name+".png");
            }
            view.Briefing("山谷遭遇",units,false,"撤退：可尝试",()=>{});await Capture("战前_五对五");
            view.Briefing("山谷遭遇",units.Where(u=>u.faction==2||u.uid=="preview-1-0").ToList(),false,"撤退：可尝试",()=>{});await Capture("战前_单对五");
            var loot=new GArray();
            foreach(var path in System.IO.Directory.GetFiles(ProjectSettings.GlobalizePath("res://resources/entities/items"),"*.tres",System.IO.SearchOption.AllDirectories).Take(60))
            {
                var res="res://"+System.IO.Path.GetRelativePath(ProjectSettings.GlobalizePath("res://"),path).Replace('\\','/');
                if(GD.Load<Resource>(res) is ItemData)loot.Add(new Godot.Collections.Dictionary{{"item_path",res},{"count",3},{"source","视觉验收示例"}});
            }
            view.Results("战斗胜利",units,units,loot,false,()=>{});await Capture("胜利_滚动战利品");
            view.Results("战斗失败",units,units,[],false,()=>{});await Capture("失败_五人");
            GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
    private async System.Threading.Tasks.Task CheckPortraits()
    {
        var canvas=new Control{Size=new(1672,941)};AddChild(canvas);
        canvas.AddChild(new ColorRect{Size=new(1672,941),Color=new Color("1b3031")});
        var catalog=GetNode<PortraitCatalog>("/root/PortraitCatalog");
        void Caption(string value,Vector2 position)
        {
            var label=new Label{Text=value,Position=position};label.AddThemeFontSizeOverride("font_size",22);canvas.AddChild(label);
        }
        Caption("原尺寸立绘等比缩小 / 96头像 / 192头像：脸型与五官位置应保持一致",new(24,8));
        for(int i=0;i<3;i++)
        {
            var data=new HumanData{gender=i==2?1:0,appearance_seed=1200+i,appearance_face_width=i==1?.9f:1f,appearance_face_height=i==1?1.1f:1f,appearance_eye_scale=i==1?1.15f:i==2?.9f:1,appearance_eye_offset_y=i==0?0:i==1?10:-10};
            var context=catalog.get_context(data,data.gender==1?"female":"male",24,"","云息");
            var requested=catalog.get_requested_appearance(data);if(data.appearance_hair_back_style<0)requested["hair_back"]="";
            var y=52+i*292;
            var full=new PortraitRenderer{Size=new(1024,1536),Scale=Vector2.One*.16f,Position=new(36,y)};canvas.AddChild(full);full.configure(data,context,requested);
            Caption($"眼偏移 {data.appearance_eye_offset_y} / 脸宽 {data.appearance_face_width} 高 {data.appearance_face_height}",new(250,y+220));
            foreach(var size in new[]{96,192})
            {
                var crop=new PortraitRenderer{Size=new(size,size),Position=new(size==96?280:450,y)};crop.set_headshot_mode(true);canvas.AddChild(crop);crop.configure(data,context,requested);
                var composition=crop.GetChildren().OfType<Control>().Single();
                if(!Mathf.IsEqualApprox(composition.Scale.X,composition.Scale.Y))throw new Exception("头像组合发生非等比缩放");
                var faceGroup=composition.GetNode<Control>("FaceComposition");
                var expectedHead=data.gender==1?new Vector2(519,414):new Vector2(517,411);
                var expectedEyes=data.gender==1?new Vector2(519,379):new Vector2(516.5f,374.5f);
                if(!faceGroup.PivotOffset.IsEqualApprox(expectedHead))throw new Exception("面部锚点未读取实际骨架");
                if(!faceGroup.GetNode<TextureRect>("Eye").PivotOffset.IsEqualApprox(expectedEyes))throw new Exception("双眼缩放中心不在原图双眼中点");
                foreach(var layer in composition.FindChildren("*","TextureRect",true,false).OfType<TextureRect>())
                {
                    var original=full.FindChildren("*","TextureRect",true,false).OfType<TextureRect>().Single(l=>l.Name==layer.Name);
                    if(!layer.Scale.IsEqualApprox(original.Scale)||!layer.PivotOffset.IsEqualApprox(original.PivotOffset)||!layer.Position.IsEqualApprox(original.Position))throw new Exception("头像改变了原立绘部件变换："+layer.Name);
                }
            }
        }
        for(int i=0;i<12;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://docs/美术规范/战斗流程概念_20261009/实机验收/头像比例检查_20261010.png");
        // 实际生成器数据，不只验证默认脸型；先配置后由容器改变尺寸。
        using var generator=new CharacterGenerator();
        for(int index=0;index<48;index++)
        {
            var data=generator.generate("云息",1);
            var renderer=new PortraitRenderer();renderer.set_headshot_mode(index%2==0);canvas.AddChild(renderer);
            var context=catalog.get_context(data,data.gender==1?"female":"male",24,"","云息");renderer.configure(data,context,catalog.get_requested_appearance(data));
            renderer.Size=index%2==0?new Vector2(80,112):new Vector2(330,500);
            var composition=renderer.GetNode<Control>("PortraitComposition");
            if(!Mathf.IsEqualApprox(composition.Scale.X,composition.Scale.Y))throw new Exception("容器布局造成非等比组合缩放");
            var group=composition.GetNode<Control>("FaceComposition");
            var eyes=group.GetNode<TextureRect>("Eye");
            var expected=data.gender==1?new Vector2(519,379):new Vector2(516.5f,374.5f);
            if(!eyes.PivotOffset.IsEqualApprox(expected))throw new Exception("随机角色眼睛锚点异常");
            if(eyes.Scale!=Vector2.One*data.appearance_eye_scale)throw new Exception("眼睛被重复缩放");
            renderer.QueueFree();
        }
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GD.Print("PORTRAIT_CHECK_OK: native rig anchors + 48 generated characters + late container resize + uniform composition");
    }

}

