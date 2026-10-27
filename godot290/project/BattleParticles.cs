using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using static DivineGateGodot.BattleMotion;
namespace DivineGateGodot;
// Source particle data and textures are retained. Projection is 2D; mesh,
// sub-emitter and stretched-billboard parity are tracked in the conversion report.
public partial class BattleParticles : Node2D
{
    static JsonNode? definitions;
    readonly List<Emitter> emitters=new();
    public float Duration {get;private set;}
    public static int Anchor(int id){definitions??=JsonNode.Parse(FileAccess.GetFileAsString("res://Reference/battle/effects.json"));return (int)F(definitions?[id.ToString()]?["anchor"]);}
    public void Setup(int id)
    {
        definitions??=JsonNode.Parse(FileAccess.GetFileAsString("res://Reference/battle/effects.json"));
        foreach(var n in definitions?[id.ToString()]?["nodes"]?.AsArray()??new JsonArray()){
            if(n==null)continue;var e=new Emitter();e.Setup(n,emitters.Count+1);AddChild(e);emitters.Add(e);Duration=Math.Max(Duration,e.Duration);
        }
    }
    public void Seek(float t){foreach(var e in emitters)e.Seek(t);}
}
public partial class Emitter : Node2D
{
    JsonNode data=null!,p=null!;
    Texture2D texture=null!;
    readonly List<(float Birth,float Life,float R,float S,Vector2 Pos,Vector2 Speed)> particles=new();
    float time,scale=480;
    public float Duration {get;private set;}
    public void Setup(JsonNode n,int seed)
    {
        data=n;p=n["particle"]!;texture=LegacyView.Texture(n["material"]!["texture"]!.GetValue<string>());
        string shader=n["material"]?["shader"]?.GetValue<string>()??"";
        Material=new CanvasItemMaterial{BlendMode=shader.Contains("Add",StringComparison.OrdinalIgnoreCase)?CanvasItemMaterial.BlendModeEnum.Add:CanvasItemMaterial.BlendModeEnum.Mix};
        var matrix=n["matrix"]!.AsArray();Position=new(F(matrix[0]?[3])*scale,-F(matrix[1]?[3])*scale);
        Scale=new(MathF.Sqrt(MathF.Pow(F(matrix[0]?[0]),2)+MathF.Pow(F(matrix[1]?[0]),2)),MathF.Sqrt(MathF.Pow(F(matrix[0]?[1]),2)+MathF.Pow(F(matrix[1]?[1]),2)));
        var random=new Random(seed);var initial=p["InitialModule"]!;var emission=p["EmissionModule"]!;var shape=p["ShapeModule"]!;
        if(!On(emission))return;
        var births=new List<float>();float length=F(p["lengthInSec"]),delay=F(p["startDelay"]);
        for(int b=0;b<(int)F(emission["m_BurstCount"]);b++)for(int i=0;i<(int)F(emission["cnt"+b]);i++)births.Add(F(emission["time"+b]));
        float rate=Range(emission["rate"],0,.5f);if(rate>0)for(int i=0;i<Math.Min(500,length*rate);i++)births.Add(i/rate);
        foreach(float birth in births){
            float r=(float)random.NextDouble(),life=Range(initial["startLifetime"],birth/Math.Max(length,.001f),r);if(life<=0)continue;
            float angle=(float)random.NextDouble()*Mathf.Tau;Vector2 dir=Vector2.FromAngle(angle),pos=Vector2.Zero;
            if(On(shape)){
                int type=(int)F(shape["type"]);float radius=F(shape["radius"]);
                pos=type==5?new Vector2(((float)random.NextDouble()-.5f)*F(shape["boxX"]),((float)random.NextDouble()-.5f)*F(shape["boxY"])):dir*radius*MathF.Sqrt((float)random.NextDouble());
            }
            float speed=Range(initial["startSpeed"],0,r);float size=Range(initial["startSize"],0,r);
            particles.Add((birth+delay,life,r,size,pos,dir*speed));Duration=Math.Max(Duration,birth+delay+life);
        }
    }
    static Color Packed(JsonNode? n){uint v=n?["rgba"]?.GetValue<uint>()??uint.MaxValue;return new Color((v&255)/255f,((v>>8)&255)/255f,((v>>16)&255)/255f,((v>>24)&255)/255f);}
    static Color Gradient(JsonNode? g,float t)
    {
        if(g==null)return Colors.White;
        Color result=Colors.White;
        for(int alpha=0;alpha<2;alpha++){
            int count=(int)F(g[alpha==0?"m_NumColorKeys":"m_NumAlphaKeys"]);if(count==0)continue;
            Color value=Packed(g["key0"]);string prefix=alpha==0?"ctime":"atime";
            for(int i=1;i<count;i++){
                float a=F(g[prefix+(i-1)])/65535,b=F(g[prefix+i])/65535;Color v=Packed(g["key"+i]);
                if(t<=b){value=Packed(g["key"+(i-1)]).Lerp(v,b>a?Math.Clamp((t-a)/(b-a),0,1):1);break;}value=v;
            }
            if(alpha==0){result.R=value.R;result.G=value.G;result.B=value.B;}else result.A=value.A;
        }
        return result;
    }
    public void Seek(float value){time=value;QueueRedraw();}
    public override void _Draw()
    {
        var initial=p["InitialModule"]!;
        foreach(var part in particles){
            float age=time-part.Birth;if(age<0 || age>part.Life)continue;float u=age/part.Life;
            float size=part.S*(On(p["SizeModule"])?Range(p["SizeModule"]?["curve"],u,part.R,1):1)*scale;
            if(size<=.01f)continue;
            Vector2 pos=(part.Pos+part.Speed*age)*scale;
            var velocity=p["VelocityModule"];if(On(velocity))pos+=new Vector2(Range(velocity?["x"],u,part.R),-Range(velocity?["y"],u,part.R))*age*scale;
            var force=p["ForceModule"];if(On(force))pos+=new Vector2(Range(force?["x"],u,part.R),-Range(force?["y"],u,part.R))*age*age*.5f*scale;
            var start=initial["startColor"]!;int mode=(int)F(start["minMaxState"]);Color color=mode==0?Packed(start["maxColor"]):Gradient(start["maxGradient"],part.R);
            if(On(p["ColorModule"]))color*=Gradient(p["ColorModule"]?["gradient"]?["maxGradient"],u);
            float rotation=Range(initial["startRotation"],0,part.R);if(On(p["RotationModule"]))rotation+=Range(p["RotationModule"]?["curve"],u,part.R)*age;
            DrawSetTransform(pos,rotation);
            var uv=p["UVModule"];int x=On(uv)?Math.Max(1,(int)F(uv?["tilesX"],1)):1,y=On(uv)?Math.Max(1,(int)F(uv?["tilesY"],1)):1;
            int frame=On(uv)?Math.Clamp((int)(Range(uv?["frameOverTime"],u,part.R)*x*y),0,x*y-1):0;
            DrawTextureRectRegion(texture,new Rect2(-size/2,-size/2,size,size),new Rect2(frame%x*texture.GetWidth()/x,frame/x*texture.GetHeight()/y,texture.GetWidth()/x,texture.GetHeight()/y),color);
        }
        DrawSetTransform(Vector2.Zero);
    }
}
