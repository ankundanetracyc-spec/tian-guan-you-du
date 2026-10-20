using Godot;
using System.Collections.Generic;

/// <summary>A helix around the authored limb. Its signed depth determines whether
/// each ribbon segment is behind or in front of the arm, rather than a flat halo.</summary>
public partial class BattleBloodQiOrbit : Node2D
{
    private readonly MeshInstance2D[] _surfaces=new MeshInstance2D[2];
    private readonly ArrayMesh[] _meshes=new ArrayMesh[2];
    private readonly List<Vector3>[] _vertices=[[],[]];
    private readonly List<Vector2>[] _uvs=[[],[]];
    private readonly List<Color>[] _colors=[[],[]];
    private readonly List<int>[] _indices=[[],[]];
    private ShaderMaterial _material=null!;
    private Vector2[] _path=[];
    private bool _left;
    private float _time,_activity;
    public void SetPath(Vector2[] path) => _path=path;
    public void Configure(bool left,Vector2[] path)
    {
        _left=left; _path=path;
        _material=new ShaderMaterial {Shader=GD.Load<Shader>("res://shaders/battle_blood_qi_orbit.gdshader")};
        _material.SetShaderParameter("ink_noise",BattleInkRibbon.SharedNoise);
        for(var i=0;i<2;i++)
        {
            _meshes[i]=new ArrayMesh();
            _surfaces[i]=new MeshInstance2D {Name=i==0?"BehindArm":"InFrontOfArm",Mesh=_meshes[i],Material=_material,ZIndex=i==0?-1:1};
            AddChild(_surfaces[i]);
        }
    }
    public void Advance(float seconds,float presence,float activity)
    {
        _time=seconds; _activity=activity;
        Modulate=new Color(1,1,1,presence);
        _material.SetShaderParameter("effect_time",seconds);
        for(var i=0;i<2;i++) {_vertices[i].Clear();_uvs[i].Clear();_colors[i].Clear();_indices[i].Clear();}
        for(var strand=0;strand<2;strand++)
        {
            const int steps=168;
            for(var sample=0;sample<steps;sample++)
            {
                var u0=sample/(float)steps;var u1=(sample+1f)/steps;
                var a=Sample(u0,strand);var b=Sample(u1,strand);
                // Split exactly at the front/back crossing. No section jumps layers.
                if(a.Depth*b.Depth<0)
                {
                    var crossing=Mathf.Lerp(u0,u1,Mathf.Abs(a.Depth)/(Mathf.Abs(a.Depth)+Mathf.Abs(b.Depth)));
                    AddStrip(u0,crossing,strand,a.Depth>=0?1:0);
                    AddStrip(crossing,u1,strand,b.Depth>=0?1:0);
                }
                else AddStrip(u0,u1,strand,(a.Depth+b.Depth)>=0?1:0);
            }
        }
        AddFlecks();
        for(var i=0;i<2;i++)
        {
            _meshes[i].ClearSurfaces();
            if(_vertices[i].Count==0) continue;
            var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=_vertices[i].ToArray();
            arrays[(int)Mesh.ArrayType.TexUV]=_uvs[i].ToArray();
            arrays[(int)Mesh.ArrayType.Color]=_colors[i].ToArray();
            arrays[(int)Mesh.ArrayType.Index]=_indices[i].ToArray();
            _meshes[i].AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        }
    }
    private Vector2 Center(float u)
    {
        var q=Mathf.Clamp(u,0,1)*(_path.Length-1);var i=Mathf.Min((int)q,_path.Length-2);var t=q-i;
        return _path[i].CubicInterpolate(_path[i+1],_path[Mathf.Max(0,i-1)],_path[Mathf.Min(_path.Length-1,i+2)],t);
    }
    private (Vector2 Point,float Depth,float Width) Sample(float u,int strand)
    {
        var tangent=(Center(Mathf.Min(1,u+.005f))-Center(Mathf.Max(0,u-.005f))).Normalized();
        var normal=new Vector2(-tangent.Y,tangent.X);
        var theta=u*Mathf.Tau*1.72f-_time*(1.65f+strand*.13f)+strand*2.55f+(_left?.3f:1.5f);
        var radius=(Mathf.Lerp(165,112,u)+Mathf.Sin(u*Mathf.Pi)*45)*(1+strand*.15f);
        radius*=1+.07f*Mathf.Sin(u*19-_time*1.4f+strand*2);
        var point=Center(u)+normal*(Mathf.Cos(theta)*radius)+tangent*(Mathf.Sin(theta)*radius*.23f);
        var taper=Mathf.Pow(Mathf.Sin(Mathf.Pi*Mathf.Clamp(u,.001f,.999f)),.65f);
        // A series of broad brush heads taper to zero, rather than unbroken wire loops.
        var pressure=Mathf.Pow(.5f+.5f*Mathf.Sin(u*19-_time*2+strand*2),1.6f);
        var width=(strand==0?50:25)*taper*pressure*(1+_activity*.4f);
        return(point,Mathf.Sin(theta),width);
    }
    private void AddStrip(float u0,float u1,int strand,int layer)
    {
        var a=Sample(u0,strand);var b=Sample(u1,strand);
        var tangent=(b.Point-a.Point).Normalized();var normal=new Vector2(-tangent.Y,tangent.X);
        var color=new Color(strand==2?.96f:.77f,.018f+strand*.012f,.052f+strand*.013f,layer==0?.47f:.83f);
        Quad(layer,a.Point-normal*a.Width,a.Point+normal*a.Width,b.Point-normal*b.Width,b.Point+normal*b.Width,
            new Vector2(u0,0),new Vector2(u0,1),new Vector2(u1,0),new Vector2(u1,1),color);
    }
    private void AddFlecks()
    {
        for(var i=0;i<16;i++)
        {
            var age=Mathf.PosMod(_time*(.19f+(i%3)*.022f)+i*.618034f,1);
            var s=Sample(age,i%3);var radial=(s.Point-Center(age)).Normalized();
            var point=s.Point+radial*(22+Mathf.Sin(age*Mathf.Pi)*50);
            var direction=(Sample(Mathf.Min(.999f,age+.01f),i%3).Point-s.Point).Normalized();
            var normal=direction.Orthogonal();var size=(3+i%4)*Mathf.Sin(age*Mathf.Pi);
            var color=new Color(.88f,.025f,.063f,Mathf.Sin(age*Mathf.Pi)*.74f);
            var layer=s.Depth>=0?1:0;
            Quad(layer,point-direction*size*2-normal*size*.45f,point-direction*size*2+normal*size*.45f,
                point+direction*size*2-normal*size*.10f,point+direction*size*2+normal*size*.10f,
                new Vector2(age,0),new Vector2(age,1),new Vector2(age+.02f,0),new Vector2(age+.02f,1),color);
        }
    }
    private void Quad(int layer,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud,Color color)
    {
        var start=_vertices[layer].Count;
        var vertices=_vertices[layer];var uv=_uvs[layer];var colors=_colors[layer];var indices=_indices[layer];
        vertices.Add(new Vector3(a.X,a.Y,0));vertices.Add(new Vector3(b.X,b.Y,0));
        vertices.Add(new Vector3(c.X,c.Y,0));vertices.Add(new Vector3(d.X,d.Y,0));
        uv.Add(ua);uv.Add(ub);uv.Add(uc);uv.Add(ud);
        colors.Add(color);colors.Add(color);colors.Add(color);colors.Add(color);
        indices.Add(start);indices.Add(start+1);indices.Add(start+2);
        indices.Add(start+2);indices.Add(start+1);indices.Add(start+3);
    }
    public override void _ExitTree()
    {
        foreach(var mesh in _meshes) mesh?.Dispose();
        _material?.Dispose();
    }
}
