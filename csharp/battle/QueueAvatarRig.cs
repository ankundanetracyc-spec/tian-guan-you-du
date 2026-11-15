using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// First queue-avatar outfit: a continuous skin bound to a real 2D skeleton.
/// The shared mesh keeps neck/collar, shoulder/sleeve and waist joins continuous.
/// This is a single approved outfit, not a generic substitute for other clothes/hair.
/// </summary>
public partial class QueueAvatarRig : Node2D
{
    public const string ArtworkPath = "res://textures/characters/queue_avatar_design_20261103/yuxu_neimen_shouzhen_v3_ink.png";
    public const float SourceVisibleHeight = 1526f;
    private static readonly Vector2 FootOrigin = new(495.5f, 1531);
    private readonly List<Bone2D> _bones = new();
    private Node2D? _presentation;
    private Skeleton2D? _skeleton;
    private float _factor = 1f;
    private bool _facingLeft;
    private bool _paused;
    private double _time;
    public const float PalmStrikeDuration = .58f;
    private Polygon2D? _skin;
    private Sprite2D? _punchPose;
    private Sprite2D? _chopPose;
    private string _attackKind = "chop";
    private QueueAvatarHair? _sharedHair;
    private readonly List<ShaderMaterial> _headMasks = new();
    private Vector2 _punchNeck = new(500,245);
    private Vector2 _chopNeck = new(500,245);
    private bool _palmActive;
    private float _palmTime;
    public bool PalmStrikeActive => _palmActive;
    public float PalmStrikeTime => _palmTime;
    public bool UsesPalmKeyPoses => _punchPose != null && _chopPose != null;
    public string AttackKind => _attackKind;
    public string PalmPoseKind => _palmActive && _palmTime >= .27f && _palmTime < .42f
        ? "contact" : "idle";

    public Rect2 LocalVisibleBounds => new(
        new Vector2(-315.5f * _factor, -SourceVisibleHeight * _factor),
        new Vector2(631 * _factor, SourceVisibleHeight * _factor));
    public Rect2 LocalBounds => LocalVisibleBounds;
    public Vector2 ChestAnchor => MapPoint(new Vector2(510, 410));
    public Vector2 HeadAnchor => MapPoint(new Vector2(536, 148));
    public Vector2 PalmStrikeContactAnchor => MapPoint(new Vector2(1107, 325));
    public Vector2 HandAnchor => PalmPoseKind == "contact" ? PalmStrikeContactAnchor
        : MapPoint(new Vector2(704, 861));
    public bool IdlePaused => _paused;
    public int RigBoneCount => _bones.Count;
    public double IdleTime => _time;

    public void Configure(float visibleHeight)
    {
        Name = "QueueAvatarRig";
        _factor = Mathf.Max(1, visibleHeight) / SourceVisibleHeight;
        if (_presentation == null) Build();
        ApplyScale();
    }

    public void SetFacing(bool facingLeft)
    {
        _facingLeft = facingLeft;
        ApplyScale();
    }

    public void SetIdlePaused(bool paused) => _paused = paused;
    public bool ConfigureHair(QueueAvatarHair.Selection selection)
    {
        if (_presentation == null) return false;
        if (_sharedHair == null)
        {
            _sharedHair = new QueueAvatarHair { Name="SharedHeadAndHair" };
            _presentation.AddChild(_sharedHair);
        }
        bool ready = _sharedHair.Configure(selection);
        foreach (var material in _headMasks) material.SetShaderParameter("hide_head",ready);
        UpdateSharedHairTransform();
        return ready;
    }
    public void SetAttackKind(string kind)
    {
        _attackKind = kind == "punch" ? "punch" : "chop";
        Pose();
    }
    public void SetPalmStrikeTime(float seconds)
    {
        _palmActive = seconds >= 0 && seconds < PalmStrikeDuration;
        _palmTime = Mathf.Clamp(seconds, 0, PalmStrikeDuration);
        Pose();
    }
    public void ClearPalmStrike()
    {
        if (!_palmActive) return;
        _palmActive = false;
        _palmTime = 0;
        Pose();
    }
    public void SetIdleTime(double seconds)
    {
        _time = seconds;
        Pose();
    }

    public override void _Process(double delta)
    {
        AdvanceIdle(delta);
    }

    public void AdvanceIdle(double delta)
    {
        if (_paused || !IsVisibleInTree()) return;
        _time += delta;
        Pose();
    }

    private Vector2 MapPoint(Vector2 source)
    {
        Vector2 p = (source - FootOrigin) * _factor;
        return p;
    }

    private void ApplyScale()
    {
        if (_presentation != null)
            _presentation.Scale = new Vector2(_facingLeft ? -_factor : _factor, _factor);
    }

    private Bone2D Bone(string name, Node2D parent, Vector2 sourcePoint, Vector2 parentSource)
    {
        var bone = new Bone2D { Name = name, Position = sourcePoint - parentSource };
        bone.SetAutocalculateLengthAndAngle(false);
        bone.SetLength(80);
        bone.SetBoneAngle(-Mathf.Pi / 2);
        bone.Rest = bone.Transform;
        parent.AddChild(bone);
        _bones.Add(bone);
        return bone;
    }

    private void Build()
    {
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _presentation = new Node2D { Name = "OutfitSkin" };
        AddChild(_presentation);
        _skeleton = new Skeleton2D { Name = "Skeleton2D" };
        _presentation.AddChild(_skeleton);
        var root = Bone("Ground", _skeleton, FootOrigin, FootOrigin);
        var chest = Bone("Chest", root, new(500, 600), FootOrigin);
        Bone("HeadNeck", chest, new(500, 245), new(500, 600));
        Bone("NearSleeve", chest, new(330, 330), new(500, 600));
        Bone("FarSleeve", chest, new(640, 350), new(500, 600));
        var robe = Bone("RobeUpper", root, new(500, 645), FootOrigin);
        Bone("RobeLower", robe, new(500, 1010), new(500, 645));
        Bone("BeltTails", root, new(505, 610), FootOrigin);
        Bone("HairKnot", _bones[2], new(470, 80), new(500, 245));

        // One indexed grid, rather than independently cropped sprites: shared edges
        // have exactly the same weights and cannot open visible seams in motion.
        const int columns = 32;
        const int rows = 64;
        var vertices = new Vector2[(columns + 1) * (rows + 1)];
        var uv = new Vector2[vertices.Length];
        var weights = new float[_bones.Count][];
        for (int b = 0; b < weights.Length; b++) weights[b] = new float[vertices.Length];
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            int i = y * (columns + 1) + x;
            var p = new Vector2(x * 1024f / columns, y * 1536f / rows);
            vertices[i] = p - FootOrigin;
            uv[i] = p;
            var w = WeightsAt(p);
            for (int b = 0; b < weights.Length; b++) weights[b][i] = w[b];
        }
        var triangles = new Godot.Collections.Array();
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int i = y * (columns + 1) + x;
            triangles.Add(new[] { i, i + 1, i + columns + 2 });
            triangles.Add(new[] { i, i + columns + 2, i + columns + 1 });
        }
        var skin = new Polygon2D
        {
            Name = "ContinuousWeightedSkin",
            Texture = GD.Load<Texture2D>(ArtworkPath),
            Polygon = vertices,
            UV = uv,
            Polygons = triangles,
            Skeleton = new NodePath("../Skeleton2D"),
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        };
        _presentation.AddChild(skin);
        _skin = skin;
        skin.Material = CreateHeadMask(new Vector4(455,230,547,281));
        for (int b = 0; b < _bones.Count; b++)
            skin.AddBone(_skeleton.GetPathTo(_bones[b]), weights[b]);
        BuildPalmKeyPoses();
        Pose();
    }

    private void BuildPalmKeyPoses()
    {
        if (_presentation == null) return;
        _punchPose = CreateKeyPose("LeftPunchPose", "res://textures/characters/queue_avatar_design_20261103/yuxu_left_punch_v7.png");
        _chopPose = CreateKeyPose("LeftKnifehandPose", "res://textures/characters/queue_avatar_design_20261103/yuxu_left_chop_v7.png");
    }

    private Sprite2D? CreateKeyPose(string name, string path)
    {
        if (_presentation == null || !ResourceLoader.Exists(path)) return null;
        var texture = GD.Load<Texture2D>(path);
        if (texture == null) return null;
        // Explicit production alignment is updated from the authored pose's feet;
        // the existing actor's layout bounds never change with an attack pose.
        var sprite = new Sprite2D {
            Name=name, Texture=texture, Centered=false, Visible=false,
            Position=-FootOrigin, TextureFilter=TextureFilterEnum.LinearWithMipmaps,
            Material=CreateHeadMask(new Vector4(455,230,547,281))
        };
        _presentation.AddChild(sprite);
        return sprite;
    }

    private void PosePalmKeyPose()
    {
        string kind = PalmPoseKind;
        bool punch = kind == "contact" && _attackKind == "punch" && _punchPose != null;
        bool chop = kind == "contact" && _attackKind == "chop" && _chopPose != null;
        if (_skin != null) _skin.Visible = !punch && !chop;
        if (_punchPose != null) _punchPose.Visible = punch;
        if (_chopPose != null) _chopPose.Visible = chop;
        UpdateSharedHairTransform();
    }

    private ShaderMaterial CreateHeadMask(Vector4 neckLine)
    {
        var material = new ShaderMaterial { Shader=new Shader { Code=@"shader_type canvas_item;
uniform bool hide_head=false;
uniform vec4 neck_line=vec4(455.0,230.0,547.0,281.0);
varying vec4 vertex_modulate;
void vertex(){vertex_modulate=COLOR;}
void fragment(){
 vec4 pixel=texture(TEXTURE,UV);
 vec2 p=UV/ TEXTURE_PIXEL_SIZE;
 float y=mix(neck_line.y,neck_line.w,clamp((p.x-neck_line.x)/(neck_line.z-neck_line.x),0.0,1.0));
 if(hide_head && p.y<y) pixel.a=0.0;
 COLOR=pixel*vertex_modulate;
}" } };
        material.SetShaderParameter("neck_line",neckLine);
        _headMasks.Add(material);
        return material;
    }

    private void UpdateSharedHairTransform()
    {
        if (_sharedHair == null || _presentation == null || _bones.Count<3) return;
        if (PalmPoseKind == "contact")
            _sharedHair.Transform=new Transform2D(0,(_attackKind=="punch"?_punchNeck:_chopNeck)-FootOrigin);
        else
            _sharedHair.Transform=_presentation.GlobalTransform.AffineInverse()*_bones[2].GlobalTransform;
    }
    private static float Smooth(float a, float b, float value)
    {
        float t = Mathf.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static float[] WeightsAt(Vector2 p)
    {
        var w = new float[9];
        float upper = 1 - Smooth(530, 710, p.Y);
        float head = 1 - Smooth(200, 285, p.Y);
        float near = (1 - Smooth(330, 410, p.X)) * Smooth(290, 470, p.Y) * (1 - Smooth(900, 1020, p.Y));
        float far = Smooth(600, 660, p.X) * Smooth(320, 510, p.Y) * (1 - Smooth(920, 1020, p.Y));
        // Hands and their cuffs use the same sleeve weights; no shrinking hands.
        near = Mathf.Max(near, (1-Smooth(420, 455, p.X))*Smooth(640,710,p.Y)*(1-Smooth(785,825,p.Y)));
        float sleeves = Mathf.Clamp(near + far, 0, 1);
        w[3] = near;
        w[4] = far;
        w[2] = head * (1 - sleeves);
        float hair = (1 - Smooth(55, 110, p.Y)) * .42f;
        w[8] = w[2] * hair;
        w[2] -= w[8];
        w[1] = upper * (1 - head) * (1 - sleeves);
        float lower = (1 - upper) * (1 - sleeves);
        // All footwear is rigidly weighted to Ground. The robe sway fades before
        // boot tops so the feet never skate or bounce with the breathing cycle.
        float cloth = 1 - Smooth(1370, 1450, p.Y);
        float hem = Smooth(850, 1200, p.Y);
        float tails = (1-Smooth(45,85,Mathf.Abs(p.X-503))) * Smooth(650,720,p.Y) * (1-Smooth(1010,1090,p.Y)) * .75f;
        w[7] = lower * tails;
        w[5] = lower * (1-tails) * cloth * (1-hem);
        w[6] = lower * (1-tails) * cloth * hem;
        w[0] = lower * (1-tails) * (1-cloth);
        // Godot's canvas skinning consumes four influences per vertex.
        var order = new int[] { 0,1,2,3,4,5,6,7,8 };
        Array.Sort(order, (a,b) => w[b].CompareTo(w[a]));
        for (int n=4; n<order.Length; n++) w[order[n]]=0;
        float sum=0;
        foreach (float value in w) sum+=value;
        if (sum < .0001f) w[0]=1;
        else for (int n=0;n<w.Length;n++) w[n]/=sum;
        return w;
    }

    private void Pose()
    {
        if (_bones.Count == 0) return;
        float t = (float)_time;
        float breath = Mathf.Sin(t * Mathf.Tau / 4.8f);
        foreach (var bone in _bones) bone.Transform = bone.Rest;
        _bones[1].Position += new Vector2(Mathf.Sin(t * .71f) * 1.6f, -breath * 3.8f);
        _bones[1].Rotation = Mathf.DegToRad(Mathf.Sin(t * .71f) * .16f);
        _bones[1].Scale = new Vector2(1 + breath * .0025f, 1 + breath * .0012f);
        _bones[2].Rotation = Mathf.DegToRad(Mathf.Sin(t * .63f - .25f) * .24f);
        _bones[3].Rotation = Mathf.DegToRad(Mathf.Sin(t * .95f - .7f) * .52f);
        _bones[4].Rotation = Mathf.DegToRad(Mathf.Sin(t * .86f + .9f) * .42f);
        _bones[5].Rotation = Mathf.DegToRad(Mathf.Sin(t * .78f - .8f) * .14f);
        _bones[6].Rotation = Mathf.DegToRad(Mathf.Sin(t * .78f - 1.35f) * .29f);
        _bones[7].Rotation = Mathf.DegToRad(Mathf.Sin(t * .89f - 1.1f) * .6f);
        _bones[8].Rotation = Mathf.DegToRad(Mathf.Sin(t * .63f - .65f) * .25f);
        PosePalmKeyPose();
    }
}
