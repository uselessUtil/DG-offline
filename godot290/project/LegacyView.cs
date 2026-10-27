using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DivineGateGodot;

// NGUI data is decoded offline. This renderer uses Godot drawing/input only.
// The Unity engine, MonoBehaviour lifecycle and Android plugins are not loaded.
public partial class LegacyView : Node2D
{
    public static JsonObject Index = null!;
    static readonly Dictionary<string,Texture2D> Textures = new();
    static readonly Dictionary<string,FontFile> Fonts = new();
    public readonly Dictionary<string,string> Labels = new();
    public readonly Dictionary<string,bool> Active = new();
    public readonly Dictionary<string,Vector2> Positions = new();
    public readonly Dictionary<string,Vector2> Scales = new();
    public readonly Dictionary<string,string> MeshTextures = new();
    public Action<string>? Clicked;
    public string Resource = "";
    public float CanvasHeight = 1136;
    JsonObject tree=null!;
    readonly List<(float depth,int order,Action draw)> draws=new();
    readonly List<(string path,Transform2D transform,Rect2 rect,Vector2[][]? triangles)> hits=new();
    string? pressed;
    public static float F(JsonNode? n,float d=0)=>n==null?d:float.Parse(n.ToJsonString(),System.Globalization.CultureInfo.InvariantCulture);
    public static int I(JsonNode? n,int d=0)=>n==null?d:n.GetValueKind()==System.Text.Json.JsonValueKind.True?1:n.GetValueKind()==System.Text.Json.JsonValueKind.False?0:n.GetValue<int>();
    public static string S(JsonNode? n,string d="")=>n==null?d:n.GetValue<string>();
    static bool B(JsonNode? n,bool d=false)=>n==null?d:n.GetValueKind()==System.Text.Json.JsonValueKind.True || (n.GetValueKind()==System.Text.Json.JsonValueKind.Number && I(n)!=0);
    static Vector2 V(JsonNode? n)=>n==null?Vector2.Zero:new(F(n["x"]),-F(n["y"]));
    static Color C(JsonNode? n)=>n==null?Colors.White:new(F(n["r"],1),F(n["g"],1),F(n["b"],1),F(n["a"],1));
    public static Texture2D? Texture(string path)
    {
        if(path=="")return null;
        if(!Textures.TryGetValue(path,out var t)) { t=GD.Load<Texture2D>("res://Reference/"+path);if(t==null)return null;Textures[path]=t; }
        return t;
    }
    public void LoadScene(string resource)
    {
        Resource=resource;
        Positions.Clear();Scales.Clear();MeshTextures.Clear();
        tree=JsonNode.Parse(FileAccess.GetFileAsString("res://Reference/"+S(Index["scenes"]![resource])))!.AsObject();
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(tree==null)return;
        draws.Clear();hits.Clear();
        Walk(tree,new Transform2D(0,new Vector2(320,CanvasHeight/2)),0,"",true);
        foreach(var draw in draws.OrderBy(x=>x.depth).ThenBy(x=>x.order))draw.draw();
        DrawSetTransformMatrix(Transform2D.Identity);
    }
    void Walk(JsonObject n,Transform2D parent,float z,string path,bool root=false)
    {
        string name=S(n["name"]);path+="/"+name;
        bool enabled=root||B(n["active"]);
        foreach(var pair in Active)if(path==pair.Key || name==pair.Key)enabled=pair.Value;
        if(!enabled)return;
        var components=n["components"]!.AsArray();
        Vector2 p=V(n["position"]);var scale=new Vector2(F(n["scale"]?["x"],1),F(n["scale"]?["y"],1));
        if(Positions.TryGetValue(name,out var overridePos))p=overridePos;
        if(Scales.TryGetValue(name,out var overrideScale))scale=overrideScale;
        var q=n["rotation"]!;float angle=-Mathf.Atan2(2*(F(q["w"])*F(q["z"])+F(q["x"])*F(q["y"])),1-2*(F(q["y"])*F(q["y"])+F(q["z"])*F(q["z"])));
        foreach(var c in components)if(S(c?["type"])=="CameraAlignUI" && I(c?["m_Enabled"],1)!=0) {
            int anchor=I(c?["m_Position"]);p.Y+=(anchor==1?-1:anchor==2?1:0)*(CanvasHeight-960)/2;
        }
        if(Mathf.IsZeroApprox(scale.X)||Mathf.IsZeroApprox(scale.Y))return;
        var t=parent*new Transform2D(angle,scale,0,p);z+=F(n["position"]?["z"]);
        var mesh=components.FirstOrDefault(c=>S(c?["type"])=="MeshFilter" && c?["mesh"]!=null);
        var renderer=components.FirstOrDefault(c=>S(c?["type"])=="MeshRenderer");
        if(mesh!=null && renderer!=null && B(renderer["m_Enabled"],true)) {
            string texture=MeshTextures.TryGetValue(name,out var image)?image:S(renderer["textures"]?[0]);var m=Index["meshes"]![S(mesh["mesh"])]!;
            Add(-z,()=>DrawMesh(t,m,Texture(texture)));
        }
        foreach(var item in components) {
            var c=item!.AsObject();if(I(c["m_Enabled"],1)==0)continue;
            string type=S(c["type"]);
            if(type.StartsWith("UI") && type.EndsWith("Sprite")) {
                string a=S(c["atlas"]),sprite=S(c["mSpriteName"]);
                if(a=="" || sprite=="")continue;
                var atlas=Index["atlases"]![a]!;var s=atlas["sprites"]!.AsArray().FirstOrDefault(x=>S(x?["name"])==sprite);
                if(s!=null)Add(-z+F(c["mDepth"])*0.001f,()=>DrawSprite(t,c,s,Texture(S(atlas["texture"])),type));
            }
            if(type=="UILabel") {
                string text=Labels.TryGetValue(path,out var value)?value:Labels.TryGetValue(name,out value)?value:S(c["mText"]);
                Add(-z+F(c["mDepth"])*0.001f,()=>DrawLabel(t,c,text));
            }
            // The original footer background only blocks clicks falling
            // through the menu; it is not a navigation button.
            if(type=="BoxCollider" && name!="footer_collide" && B(c["m_Enabled"],true)) {
                Vector2 size=new(F(c["m_Size"]?["x"]),F(c["m_Size"]?["y"]));
                hits.Add((path,t,new Rect2(V(c["m_Center"])-size/2,size),null));
            }
            if(type=="MeshCollider" && c["mesh"]!=null && B(c["m_Enabled"],true)) {
                var m=Index["meshes"]![S(c["mesh"])]!;var vertices=m["vertices"]!.AsArray();var ids=m["indices"]!.AsArray();
                var triangles=new Vector2[ids.Count/3][];
                for(int i=0;i<triangles.Length;i++) {triangles[i]=new Vector2[3];for(int j=0;j<3;j++){var v=vertices[I(ids[i*3+j])]!;triangles[i][j]=new(F(v[0]),-F(v[1]));}}
                hits.Add((path,t,new Rect2(),triangles));
            }
        }
        foreach(var ch in n["children"]!.AsArray())Walk(ch!.AsObject(),t,z,path);
    }
    void Add(float depth,Action draw)=>draws.Add((depth,draws.Count,draw));
    public void AddLabel(string parentName,string text,Vector2 position,float size)
    {
        JsonObject? parent=null,template=null;
        void Search(JsonObject n) {
            if(S(n["name"])==parentName)parent=n;
            if(template==null && n["components"]!.AsArray().Any(c=>S(c?["type"])=="UILabel"))template=n;
            foreach(var child in n["children"]!.AsArray())Search(child!.AsObject());
        }
        Search(tree);if(parent==null || template==null)throw new InvalidOperationException("Label parent/template missing");
        var label=template.DeepClone().AsObject();label["name"]="GodotPoolDescription";label["active"]=true;
        label["position"]=new JsonObject{["x"]=position.X,["y"]=-position.Y,["z"]=-160};
        label["scale"]=new JsonObject{["x"]=size,["y"]=size,["z"]=1};label["children"]=new JsonArray();
        var c=label["components"]!.AsArray().First(x=>S(x?["type"])=="UILabel")!.DeepClone();
        c["mText"]=text;c["mPivot"]=4;
        var fontKey=Index["fonts"]!.AsObject().OrderByDescending(x=>x.Value!["font"]!["mSaved"]!.AsArray().Count).First().Key;c["font"]=fontKey;
        label["components"]=new JsonArray(c);parent["children"]!.AsArray().Add(label);QueueRedraw();
    }
    static Vector2 Pivot(int pivot)=>new((pivot%3)*.5f,(pivot/3)*.5f);
    void DrawSprite(Transform2D t,JsonObject c,JsonNode s,Texture2D? texture,string type)
    {
        if(texture==null)return;
        var r=s["outer"]!;Rect2 src=new(F(r["x"]),F(r["y"]),F(r["width"]),F(r["height"]));
        Vector2 pivot=Pivot(I(c["mPivot"],4));Rect2 dst=new(-pivot,Vector2.One);
        DrawSetTransformMatrix(t);Color color=C(c["mColor"]);
        if(type=="UISlicedSprite") {
            var inner=s["inner"]!;float l=F(inner["x"])-src.Position.X,top=F(inner["y"])-src.Position.Y;
            float right=src.End.X-F(inner["x"])-F(inner["width"]),bottom=src.End.Y-F(inner["y"])-F(inner["height"]);
            float w=t.X.Length(),h=t.Y.Length();if(w==0||h==0)return;
            float[] dx={dst.Position.X,dst.Position.X+l/w,dst.End.X-right/w,dst.End.X};
            float[] dy={dst.Position.Y,dst.Position.Y+top/h,dst.End.Y-bottom/h,dst.End.Y};
            float[] sx={src.Position.X,src.Position.X+l,src.End.X-right,src.End.X};
            float[] sy={src.Position.Y,src.Position.Y+top,src.End.Y-bottom,src.End.Y};
            for(int y=0;y<3;y++)for(int x=0;x<3;x++)if(dx[x+1]>dx[x] && dy[y+1]>dy[y])
                DrawTextureRectRegion(texture,new Rect2(dx[x],dy[y],dx[x+1]-dx[x],dy[y+1]-dy[y]),new Rect2(sx[x],sy[y],sx[x+1]-sx[x],sy[y+1]-sy[y]),color);
        } else if(type=="UITiledSprite") {
            float w=t.X.Length(),h=t.Y.Length();
            for(float y=0;y<h;y+=src.Size.Y)for(float x=0;x<w;x+=src.Size.X) {
                float cw=Math.Min(src.Size.X,w-x),ch=Math.Min(src.Size.Y,h-y);
                DrawTextureRectRegion(texture,new Rect2(dst.Position+new Vector2(x/w,y/h),new Vector2(cw/w,ch/h)),new Rect2(src.Position,new Vector2(cw,ch)),color);
            }
        } else DrawTextureRectRegion(texture,dst,src,color);
    }
    void DrawLabel(Transform2D t,JsonObject c,string text)
    {
        string key=S(c["font"]);if(key==""||text=="")return;
        if(!Fonts.TryGetValue(key,out var font)) { font=GD.Load<FontFile>("res://Reference/fonts/"+key+".fnt");if(font==null)return;Fonts[key]=font; }
        int size=I(Index["fonts"]![key]!["font"]!["mSize"],32);
        text=Regex.Replace(text,@"\[(?:[0-9a-fA-F]{6}|-)\]","");
        Vector2 pivot=Pivot(I(c["mPivot"],4));string[] lines=text.Replace("\r","").Split('\n');
        float height=lines.Length*size;
        t=t.ScaledLocal(new Vector2(1f/size,1f/size));DrawSetTransformMatrix(t);
        float y=-pivot.Y*height+font.GetAscent(size);Color color=C(c["mColor"]);
        foreach(string line in lines) {
            float width=font.GetStringSize(line,HorizontalAlignment.Left,-1,size).X;Vector2 pos=new(-width*pivot.X,y);
            if(I(c["mEffectStyle"])!=0)DrawStringOutline(font,pos,line,HorizontalAlignment.Left,-1,size,2,C(c["mEffectColor"]));
            DrawString(font,pos,line,HorizontalAlignment.Left,-1,size,color);y+=size;
        }
    }
    void DrawMesh(Transform2D t,JsonNode m,Texture2D? tex)
    {
        if(tex==null || m["uv"]==null)return;
        var vertices=m["vertices"]!.AsArray();var uv=m["uv"]!.AsArray();var ids=m["indices"]!.AsArray();
        DrawSetTransformMatrix(t);
        for(int i=0;i+2<ids.Count;i+=3) {
            var points=new Vector2[3];var uvs=new Vector2[3];
            for(int j=0;j<3;j++) { int n=I(ids[i+j]);points[j]=new(F(vertices[n]![0]),-F(vertices[n]![1]));uvs[j]=new(F(uv[n]![0]),1-F(uv[n]![1])); }
            DrawPolygon(points,new[]{Colors.White},uvs,tex);
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(!IsVisibleInTree())return;
        if(e is not InputEventMouseButton b || b.ButtonIndex!=MouseButton.Left)return;
        string? target=null;
        foreach(var hit in hits.AsEnumerable().Reverse()) {
            var local=hit.transform.AffineInverse()*(GetGlobalTransformWithCanvas().AffineInverse()*b.Position);
            if(hit.triangles==null?hit.rect.HasPoint(local):hit.triangles.Any(p=>Geometry2D.IsPointInPolygon(local,p))) { target=hit.path;break; }
        }
        if(OS.GetCmdlineUserArgs().Contains("--smoke"))GD.Print("INPUT "+Resource+" "+b.Position+" -> "+target);
        if(b.Pressed)pressed=target;
        else { if(target!=null && target==pressed) {Clicked?.Invoke(target);GetViewport().SetInputAsHandled();}pressed=null; }
    }
    public Vector2 ButtonCenter(string suffix)
    {
        var hit=hits.First(x=>x.path.EndsWith(suffix));
        return hit.transform*(hit.triangles==null?hit.rect.GetCenter():(hit.triangles[0][0]+hit.triangles[0][1]+hit.triangles[0][2])/3);
    }
}
