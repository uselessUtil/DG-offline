using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
namespace DivineGateGodot;
// Cubic Hermite sampling preserves the source AnimationClip keys and tangents.
public static class BattleMotion
{
    static JsonNode? motions;
    public static float F(JsonNode? n,float fallback=0)=>n==null?fallback:n is JsonValue v && v.TryGetValue<string>(out var text)?text=="inf"?float.PositiveInfinity:text=="-inf"?float.NegativeInfinity:float.Parse(text,System.Globalization.CultureInfo.InvariantCulture):(float)n.GetValue<double>();
    public static bool On(JsonNode? n)=>n?["enabled"]?.GetValue<bool>()??false;
    static JsonNode? Clip(string name){motions??=JsonNode.Parse(FileAccess.GetFileAsString("res://Reference/battle/motions.json"));return motions?[name];}
    public static float Duration(string name)=>Math.Max(.05f,F(Clip(name)?["duration"],.5f));
    public static float Curve(JsonNode? curve,float t,string? axis=null,float fallback=0)
    {
        var keys=curve?["m_Curve"]?.AsArray();if(keys==null || keys.Count==0)return fallback;
        float Val(JsonNode? key,string member)=>F(axis==null?key?[member]:key?[member]?[axis]);
        if(t<=F(keys[0]?["time"]))return Val(keys[0],"value");
        for(int i=1;i<keys.Count;i++)if(t<=F(keys[i]?["time"])){
            var a=keys[i-1];var b=keys[i];float dt=F(b?["time"])-F(a?["time"]);if(dt<=0)return Val(b,"value");
            float u=(t-F(a?["time"]))/dt,u2=u*u,u3=u2*u;
            float left=Val(a,"outSlope"),right=Val(b,"inSlope");if(!float.IsFinite(left)||!float.IsFinite(right))return Val(a,"value");
            return (2*u3-3*u2+1)*Val(a,"value")+(u3-2*u2+u)*dt*left+(-2*u3+3*u2)*Val(b,"value")+(u3-u2)*dt*right;
        }
        return Val(keys[^1],"value");
    }
    public static Vector2 Sample(string name,string group,float t,string path="",bool scale=false)
    {
        var c=Clip(name)?["curves"]?[group]?.AsArray().FirstOrDefault(n=>n?["path"]?.GetValue<string>()==path)?["curve"];
        float f=scale?1:0;return new(Curve(c,t,"x",f),Curve(c,t,"y",f)*(scale?1:-1));
    }
    public static float Range(JsonNode? c,float time,float random,float fallback=0)
    {
        if(c==null)return fallback;float scalar=F(c["scalar"]);int state=(int)F(c["minMaxState"]);
        if(state==0)return scalar;float max=Curve(c["maxCurve"],time,null,1)*scalar;
        return state==1?max:Mathf.Lerp(Curve(c["minCurve"],time)*scalar,max,random);
    }
}
