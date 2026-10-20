using Godot;

/// <summary>Native projected surface geometry. Vertex colours carry surface normals;
/// UV.x runs from the floating root to the fingertips. No illustrated textures or rig.</summary>
internal static class BattleBloodArmGeometry
{
    private readonly record struct Vertex(Vector3 Point, Vector3 Normal, Vector2 Uv);
    private readonly record struct Triangle(int A, int B, int C, float Depth);
    private sealed class Builder
    {
        private readonly List<Vertex> _vertices=[];
        private readonly List<Triangle> _triangles=[];

        private void Face(int a,int b,int c)
        {
            var normal=(_vertices[a].Normal+_vertices[b].Normal+_vertices[c].Normal)/3f;
            // A single translucent front shell avoids doubling the glass opacity.
            if(normal.Z<-.08f) return;
            _triangles.Add(new(a,b,c,(_vertices[a].Point.Z+_vertices[b].Point.Z+_vertices[c].Point.Z)/3));
        }

        public void Tube(Vector3[] centers,float[] widths,float[] depths,float from,float to,int slices=18)
        {
            var start=_vertices.Count;
            for(var row=0;row<centers.Length;row++)
            {
                var before=centers[Math.Max(0,row-1)];
                var after=centers[Math.Min(centers.Length-1,row+1)];
                var tangent=(after-before).Normalized();
                var side=new Vector3(-tangent.Y,tangent.X,0).Normalized();
                if(side.LengthSquared()<.01f) side=Vector3.Right;
                var depth=tangent.Cross(side).Normalized();
                for(var col=0;col<=slices;col++)
                {
                    var angle=col*Mathf.Tau/slices;
                    var c=Mathf.Cos(angle); var s=Mathf.Sin(angle);
                    var offset=side*(c*widths[row])+depth*(s*depths[row]);
                    var normal=(side*c/Mathf.Max(widths[row],1)+depth*s/Mathf.Max(depths[row],1)).Normalized();
                    _vertices.Add(new(centers[row]+offset,normal,new Vector2(Mathf.Lerp(from,to,row/(float)(centers.Length-1)),col/(float)slices)));
                }
            }
            for(var row=0;row<centers.Length-1;row++) for(var col=0;col<slices;col++)
            {
                var a=start+row*(slices+1)+col; var b=a+slices+1;
                Face(a,b,a+1);Face(a+1,b,b+1);
            }
        }

        public void Ellipsoid(Func<Vector3,Vector3> project,Vector3 center,Vector3 radii,float path)
        {
            const int rings=14,slices=24;
            var start=_vertices.Count;
            var origin=project(Vector3.Zero);
            for(var row=0;row<=rings;row++)
            {
                var latitude=Mathf.Pi*row/rings;
                for(var col=0;col<=slices;col++)
                {
                    var longitude=Mathf.Tau*col/slices;
                    var unit=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
                    // A softly rounded palm block, rather than a spherical mitten.
                    var powers=new Vector3(.63f,.78f,.80f);
                    static float SignedPower(float value,float power)=>Mathf.Sign(value)*Mathf.Pow(Mathf.Abs(value),power);
                    var rounded=new Vector3(SignedPower(unit.X,powers.X),SignedPower(unit.Y,powers.Y),SignedPower(unit.Z,powers.Z));
                    var localNormal=(new Vector3(SignedPower(unit.X,2-powers.X),SignedPower(unit.Y,2-powers.Y),SignedPower(unit.Z,2-powers.Z))/radii).Normalized();
                    var normal=(project(localNormal)-origin).Normalized();
                    _vertices.Add(new(project(center+rounded*radii),normal,new Vector2(path+(unit.Y+1)*.025f,col/(float)slices)));
                }
            }
            for(var row=0;row<rings;row++)for(var col=0;col<slices;col++)
            {
                var a=start+row*(slices+1)+col;var b=a+slices+1;
                Face(a,b,a+1);Face(a+1,b,b+1);
            }
        }

        public ArrayMesh Finish()
        {
            var vertices=new Vector3[_vertices.Count];
            var colors=new Color[_vertices.Count];
            var uv=new Vector2[_vertices.Count];
            for(var index=0;index<_vertices.Count;index++)
            {
                var v=_vertices[index];
                vertices[index]=new Vector3(v.Point.X,v.Point.Y,0);
                var n=v.Normal*.5f+Vector3.One*.5f;
                colors[index]=new Color(n.X,n.Y,n.Z,1);
                uv[index]=v.Uv;
            }
            // Projected depth order preserves the side view's overlapping finger planes.
            var indices=_triangles.OrderBy(triangle=>triangle.Depth)
                .SelectMany(triangle=>new[]{triangle.A,triangle.B,triangle.C}).ToArray();
            var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=vertices;
            arrays[(int)Mesh.ArrayType.Color]=colors;
            arrays[(int)Mesh.ArrayType.TexUV]=uv;
            arrays[(int)Mesh.ArrayType.Index]=indices;
            var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
            return mesh;
        }
    }

    public static ArrayMesh Create(bool left,float punch,float guard)
    {
        var builder=new Builder();
        Vector3[] resting=left
            ? [new(0,0,0),new(-260,-20,-25),new(-470,-190,0),new(-475,-410,25),new(-300,-660,15),new(-200,-745,0)]
            : [new(0,0,0),new(225,-5,30),new(390,-170,70),new(365,-410,40),new(220,-650,15),new(150,-725,0)];
        Vector3[] extended=[new(0,0,0),new(180,10,0),new(360,40,25),new(530,5,20),new(700,-25,10),new(790,-40,0)];
        Vector3[] blocking=[new(0,0,0),new(180,-65,5),new(300,-230,30),new(375,-415,20),new(440,-570,10),new(455,-670,0)];
        var controls=new Vector3[resting.Length];
        for(var index=0;index<controls.Length;index++) controls[index]=resting[index].Lerp(blocking[index],guard).Lerp(extended[index],punch);
        float[] widths=[12,158,128,146,96,70];
        float[] depths=[10,97,84,88,61,48];
        var samples=Smooth(controls,widths,depths,7);
        builder.Tube(samples.Points,samples.Widths,samples.Depths,0,.69f,24);

        var wrist=controls[^1];
        var idleForward=left?new Vector2(.88f,-.47f):new Vector2(-.80f,-.60f);
        var forward=idleForward.Lerp(new Vector2(.12f,-.99f),guard).Lerp(new Vector2(1,-.02f),punch).Normalized();
        var side=new Vector2(forward.Y,-forward.X);
        var yaw=Mathf.Lerp(left?-1.03f:1.08f,left?-.60f:.68f,punch);
        yaw=Mathf.Lerp(yaw,left?-.80f:.86f,guard);
        const float handScale=1.48f;
        var palm=wrist+new Vector3(forward.X,forward.Y,0)*115;
        var handedness=left?1f:-1f;
        Vector3 Project(Vector3 point)
        {
            point*=handScale;
            var x=point.X*handedness;
            var horizontal=x*Mathf.Cos(yaw)+point.Z*Mathf.Sin(yaw);
            var depth=-x*Mathf.Sin(yaw)+point.Z*Mathf.Cos(yaw);
            return palm+new Vector3(side.X*horizontal+forward.X*point.Y,side.Y*horizontal+forward.Y*point.Y,depth);
        }
        builder.Ellipsoid(Project,new Vector3(0,0,0),new Vector3(130,95,45),.72f);
        // Four independent three-phalange fingers form a cupped, side-facing ready hand.
        for(var digit=0;digit<4;digit++)
        {
            float[] lengths=[134,155,145,112];
            var length=lengths[digit];
            var radius=digit==3?23:27;
            var basePoint=new Vector3(-83+digit*56,digit==1?80:digit==3?50:69,0);
            var angles=new[]{Mathf.Lerp(24+digit*7,78+digit*2,punch),Mathf.Lerp(85+digit*9,153+digit*2,punch),Mathf.Lerp(146+digit*8,216+digit*2,punch)};
            for(var joint=0;joint<angles.Length;joint++) angles[joint]=Mathf.Lerp(angles[joint],8+joint*15+digit*3,guard);
            var joints=new Vector3[4];joints[0]=basePoint;
            float[] fractions=[.46f,.32f,.22f];
            for(var joint=0;joint<3;joint++)
            {
                var angle=Mathf.DegToRad(angles[joint]);
                joints[joint+1]=joints[joint]+new Vector3((digit-1.5f)*guard*3,Mathf.Cos(angle)*length*fractions[joint],Mathf.Sin(angle)*length*fractions[joint]);
            }
            var finger=Smooth(joints,[radius,radius*.96f,radius*.80f,2],[radius*.88f,radius*.86f,radius*.74f,2],4);
            builder.Tube(finger.Points.Select(Project).ToArray(),finger.Widths.Select(value=>value*handScale).ToArray(),finger.Depths.Select(value=>value*handScale).ToArray(),.80f,1,18);
        }
        Vector3[] thumb=[new(-71,-32,10),new(-129,6,24),new(-132,58,51),new(-79,87,78)];
        Vector3[] closedThumb=[new(-71,-32,10),new(-115,15,37),new(-49,48,106),new(10,25,105)];
        for(var i=0;i<thumb.Length;i++)
        {
            thumb[i]=thumb[i].Lerp(closedThumb[i],punch);
            thumb[i].X-=guard*i*12;
        }
        var thumbSurface=Smooth(thumb,[39,36,29,3],[29,28,23,2],5);
        builder.Tube(thumbSurface.Points.Select(Project).ToArray(),thumbSurface.Widths.Select(value=>value*handScale).ToArray(),thumbSurface.Depths.Select(value=>value*handScale).ToArray(),.76f,.98f,18);
        return builder.Finish();
    }

    private readonly record struct Samples(Vector3[] Points,float[] Widths,float[] Depths);
    private static Samples Smooth(Vector3[] points,float[] widths,float[] depths,int steps)
    {
        var centers=new List<Vector3>();var radii=new List<float>();var thickness=new List<float>();
        for(var section=0;section<points.Length-1;section++)for(var step=0;step<steps;step++)
        {
            var t=step/(float)steps;var t2=t*t;var t3=t2*t;
            var a=points[Math.Max(0,section-1)];var b=points[section];var c=points[section+1];var d=points[Math.Min(points.Length-1,section+2)];
            centers.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
            radii.Add(Mathf.Lerp(widths[section],widths[section+1],t));
            thickness.Add(Mathf.Lerp(depths[section],depths[section+1],t));
        }
        centers.Add(points[^1]);radii.Add(widths[^1]);thickness.Add(depths[^1]);
        return new(centers.ToArray(),radii.ToArray(),thickness.ToArray());
    }
}
