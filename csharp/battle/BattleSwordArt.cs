using Godot;
using System.Text.Json;

/// <summary>Approved sword art and registrations. Reads presentation data only.</summary>
public static class BattleSwordArt
{
    public const string ArtRoot="res://textures/characters/side_ink_sword_actions_20261024_v5/";
    public const string FemaleArtRoot="res://textures/characters/side_ink_sword_female_20261025_v1/";
    public const string HurtArtRoot="res://textures/characters/side_ink_sword_hurt_20261025_v1/";
    public const string IdleArtRoot="res://textures/characters/side_ink_sword_idle_right_20261024_v3/";
    public const string FxRoot="res://textures/vfx/sword_action_sequences_20261024_v1/";
    public sealed record Pose(Texture2D Body,Texture2D HandFront,Vector2 Grip,float Angle);
    public readonly record struct Weapon(Texture2D Icon,Vector2 Grip,Vector2 Tip);
    private static readonly Dictionary<string,Pose> Poses=[];
    private static SpriteFrames? _frames;
    public static string PoseKey(ActiveSkillData skill)=>skill.skill_name switch
        {"斩剑"=>"cut","格剑"=>"parry","养锋"=>"nurture","尽锋"=>"finish",_=>""};

    public static bool TryPose(string key,out Pose? pose,bool female=false)
    {
        var root=female?FemaleArtRoot:key=="hurt"?HurtArtRoot:key=="idle"?IdleArtRoot:ArtRoot;
        var cacheKey=root+key;
        if(Poses.TryGetValue(cacheKey,out pose))return true;
        pose=null;
        if(!ResourceLoader.Exists(root+key+"_body_back.png")||!ResourceLoader.Exists(root+key+"_hand_front.png"))return false;
        var body=GD.Load<Texture2D>(root+key+"_body_back.png");
        var front=GD.Load<Texture2D>(root+key+"_hand_front.png");
        if(body is null||front is null||!Godot.FileAccess.FileExists(root+"layer_layout.json"))return false;
        try
        {
            using var document=JsonDocument.Parse(Godot.FileAccess.GetFileAsString(root+"layer_layout.json"));
            if(!document.RootElement.GetProperty("poses").TryGetProperty(key,out var item))return false;
            var grip=item.GetProperty("grip_canvas");
            pose=new(body,front,new(grip[0].GetSingle(),grip[1].GetSingle()),Mathf.DegToRad(item.GetProperty("sword_angle_degrees").GetSingle()));
            Poses[cacheKey]=pose;return true;
        }
        catch(JsonException){return false;}
    }

    public static Weapon WeaponFor(string path)
    {
        bool broad=path.Contains("broad_sword"),spined=path.Contains("spined_sword");
        return new(GD.Load<Texture2D>(path),broad?new(.866f,.521f):spined?new(.854f,.551f):new(.874f,.512f),
            broad?new(.023f,.522f):spined?new(.037f,.549f):new(.024f,.505f));
    }
    public static Weapon DefaultWeapon()=>WeaponFor("res://textures/ui/forged_swords_20261023_v1/slender_sword.png");
    public static Weapon SelectWeapon(BattleEntityData data,out bool equipped)
    {
        foreach(var item in data.equipped_artifacts)
            if(item is not null&&ForgedArtifactInventory.TryRead(item,out var sword)&&sword.Category=="剑")
            {equipped=true;return WeaponFor(ForgedArtifactPresentation.BodySpritePath(sword));}
        if(data.trial_forged_equipment_json.Length>0)
            try
            {
                if(JsonSerializer.Deserialize<ProcessForgingTrial.Artifact>(data.trial_forged_equipment_json) is {Category:"剑"} trial)
                {equipped=true;return WeaponFor(ForgedArtifactPresentation.BodySpritePath(trial));}
            }
            catch(JsonException){/* Missing presentation snapshot keeps the ordinary sword prop. */}
        foreach(var entry in data.equipment_paths)
            if(!string.IsNullOrWhiteSpace(entry.Value.AsString())&&GD.Load<EquipmentData>(entry.Value.AsString()) is {weapon_type:(int)Equipment.WeaponType.SWORD})
            {equipped=true;return DefaultWeapon();}
        equipped=false;return DefaultWeapon();
    }
    public static SpriteFrames Frames()
    {
        if(_frames is not null)return _frames;
        _frames=new SpriteFrames();_frames.RemoveAnimation("default");
        foreach(var (name,count) in new[]{("cut",24),("parry",18),("nurture",36),("finish",24),("contact",18)})
        {
            _frames.AddAnimation(name);_frames.SetAnimationLoopMode(name,SpriteFrames.LoopMode.None);_frames.SetAnimationSpeed(name,30);
            for(var i=0;i<count;i++)
            {
                var page=GD.Load<Texture2D>(FxRoot+$"{name}_atlas_{i/16}.png");
                if(page is null)break;
                _frames.AddFrame(name,new AtlasTexture{Atlas=page,Region=new Rect2(i%4*768,i%16/4*768,768,768)});
            }
        }
        return _frames;
    }
}
