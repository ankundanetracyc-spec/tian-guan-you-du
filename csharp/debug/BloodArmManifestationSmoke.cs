using Godot;
using System.Reflection;

/// <summary>走战斗测试的真实配置及角色生成入口，检查气血达标后的显臂节点和材质。</summary>
public partial class BloodArmManifestationSmoke : Node2D
{
    private int checks;
    private void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks++;}
    public override void _Ready()=>CallDeferred(nameof(Run));
    private async void Run()
    {
        try
        {
            RenderingServer.SetDefaultClearColor(new Color(.16f,.18f,.17f));
            AddChild(new Node2D{Name="DeployLayer"});
            var manager=new BattleManager{Name="BattleMgr",deploy_layer="../DeployLayer"};AddChild(manager);
            var create=typeof(StartMenu).GetMethod("CreateTestUnit",BindingFlags.NonPublic|BindingFlags.Static)!;
            var loadout=typeof(StartMenu).GetMethod("ApplyTestLoadout",BindingFlags.NonPublic|BindingFlags.Static)!;
            var actorData=(BattleEntityData)create.Invoke(null,["显臂检查",1,0])!;
            var enemyData=(BattleEntityData)create.Invoke(null,["陪练",2,1])!;
            loadout.Invoke(null,[actorData,"","res://resources/gongfa/百骸衍枝化生宝册.tres","res://resources/gongfa/太和感物养元宝册.tres",new string[]{"res://resources/activeskills/催血.tres"}]);
            var packed=GD.Load<PackedScene>("res://tscn/battle/BattleTestCharacter.tscn");
            manager.setup_battle_from_packed(packed,packed,actorData,enemyData);
            manager.SetProcess(false);manager.battle_running=false;
            var actor=(Creature)manager.player_units[0];
            var enemy=(Creature)manager.enemy_units[0];
            actor.GlobalPosition=new Vector2(650,850);enemy.GlobalPosition=new Vector2(1320,850);
            Check(manager.gongfa_combat.HasPower(actor,"百骸衍枝化生"),"测试配置自动启动百骸心法");
            var doll=actor.GetNodeOrNull<BattlePaperDoll>("BattlePaperDoll");
            GD.Print($"BLOOD_ARM_DIAGNOSTIC: children={string.Join(',',actor.GetChildren().Select(n=>n.Name))}; doll={doll is not null}; side_ink={doll?.uses_side_ink()}");
            Check(doll is not null&&doll.uses_side_ink(),"实际角色具备水墨立绘节点");
            foreach(var (blood,count) in new[]{(2,0),(3,1),(7,2),(2,0)})
            {
                actor.qi_blood=blood;
                Check(CardRules.Number(actor,"qi_blood")==blood&&CardRules.Number(actor,"qi_blood_stacks")==blood
                    &&CardRules.Evaluate("气血层数×2",actor)==blood*2,$"气血{blood}公共读数与公式层数一致");
                Check(manager.gongfa_combat.BloodArmCount(actor)==count,$"气血{blood}结算手臂{count}");
                await ToSignal(GetTree().CreateTimer(.7f),SceneTreeTimer.SignalName.Timeout);
                var view=actor.GetNodeOrNull<BattleBloodQiArms>("BloodQiArms");
                GD.Print($"BLOOD_ARM_DIAGNOSTIC: blood={blood}; view={view is not null}; arms={view?.ArmCount}; visible={view?.Visible}");
                Check(view is not null&&view.ArmCount==count,$"气血{blood}持续显示正确显臂数量");
                if(count>0)
                {
                    var surface=view!.GetNode<Sprite2D>("QiArm0/BloodQiBody");
                    Check(surface.Texture is not null&&surface.IsVisibleInTree(),"血臂主体贴图已加载并在可见树中");
                    Check(((ShaderMaterial)surface.Material).GetShaderParameter("presence").AsSingle()>.9f,"血臂显化材质透明度正常");
                    if(OS.GetCmdlineUserArgs().Contains("--capture"))
                    {
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        var path=$"res://tmp/baihai_manifestation_{count}.png";
                        using var image=GetViewport().GetTexture().GetImage();image.SavePng(path);
                    }
                }
            }
            foreach(var (upgrade,threshold,second) in new[]{("",5,0),("baihai_c2",4,0),("baihai_c3",4,8),("baihai_c4",3,7)})
            {
                var upgrades=new Godot.Collections.Dictionary();
                if(upgrade.Length>0)upgrades[$"百骸衍枝化生宝册|{upgrade}"]=true;
                actor.SetMeta("combat_heart_method_upgrades",upgrades);
                actor.qi_blood=threshold-1;
                Check(manager.gongfa_combat.BloodArmCount(actor)==0&&actor.GetNode<BattleBloodQiArms>("BloodQiArms").ArmCount==0,$"{upgrade}门槛前不显臂");
                actor.qi_blood=threshold;
                Check(manager.gongfa_combat.BloodArmCount(actor)==1&&actor.GetNode<BattleBloodQiArms>("BloodQiArms").ArmCount==1,$"{upgrade}门槛当次显单臂");
                if(second>0)
                {
                    actor.qi_blood=second;
                    Check(manager.gongfa_combat.BloodArmCount(actor)==2&&actor.GetNode<BattleBloodQiArms>("BloodQiArms").ArmCount==2,$"{upgrade}双臂门槛当次显双臂");
                }
            }
            actor.shield=17;actor.blood_furnace_value=30;
            Check(CardRules.Number(actor,"shield")==17&&CardRules.Number(actor,"spent_furnace")==30,"同类战斗资源和别名读取实际存储");
            var combined=actor.GetMeta("combat_heart_method_upgrades").AsGodotDictionary();
            combined["太和感物养元宝册|taihe_c4"]=true;
            actor.SetMeta("combat_heart_method_upgrades",combined);
            Check(manager.gongfa_combat.HasPower(actor,"太和感物养元"),"辅修太和心法从测试配置自动生效");
            manager.gongfa_combat.BeginRound(actor);actor.qi_blood=0;
            var response=GD.Load<ActiveSkillData>("res://resources/activeskills/架招.tres")!;
            manager.gongfa_combat.OnResponseTriggered(actor,response);
            Check(actor.qi_blood==2&&manager.gongfa_combat.BloodArmCount(actor)==0,"太和第四卷首次应对获得2层气血，尚未达显臂门槛");
            manager.gongfa_combat.OnResponseTriggered(actor,response);
            Check(actor.qi_blood==3&&actor.GetNode<BattleBloodQiArms>("BloodQiArms").ArmCount==1,"太和后续应对获得气血后立即联动百骸显臂");
            GD.Print($"BLOOD_ARM_MANIFESTATION_SMOKE: {checks} checks passed");GetTree().Quit();
        }
        catch(Exception error){GD.PushError("BLOOD_ARM_MANIFESTATION_SMOKE: "+error);GetTree().Quit(1);}
    }
}
