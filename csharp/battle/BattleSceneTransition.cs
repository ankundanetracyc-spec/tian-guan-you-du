using Godot;
using System.Threading.Tasks;
/// <summary>跨场景保留的水墨遮罩；透明时也拦截点击，防止过渡中重复操作。</summary>
public partial class BattleSceneTransition : CanvasLayer
{
    private ColorRect _veil=null!;
    private ShaderMaterial _ink=null!;
    public override void _Ready()
    {
        Layer=240;ProcessMode=ProcessModeEnum.Always;
        _ink=new ShaderMaterial{Shader=new Shader{Code="""
shader_type canvas_item;
uniform float amount : hint_range(0.0,1.0)=0.0;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p);vec2 f=fract(p);f=f*f*(3.0-2.0*f);return mix(mix(hash(i),hash(i+vec2(1.,0.)),f.x),mix(hash(i+vec2(0.,1.)),hash(i+vec2(1.,1.)),f.x),f.y);}
void fragment(){float n=noise(UV*9.0)*.65+noise(UV*31.0)*.25+noise(UV*83.0)*.10;float alpha=smoothstep(n-.16,n+.16,amount*1.4-.2);COLOR=vec4(vec3(.065,.105,.105)+noise(UV*150.0)*.012,alpha);}
"""}};
        _veil=new ColorRect{Material=_ink,MouseFilter=Control.MouseFilterEnum.Stop};AddChild(_veil);_veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
    public async Task Ink(float amount,double seconds)
    {
        var from=_ink.GetShaderParameter("amount").AsSingle();
        var tween=CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenMethod(Callable.From<float>(value=>_ink.SetShaderParameter("amount",value)),from,amount,seconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween,Tween.SignalName.Finished);
    }
}
