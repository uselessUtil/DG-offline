using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DivineGateGodot;

// All gameplay input is in canvas coordinates; modal menus block both touch
// handling and the countdown. No persistent widgets survive a page transition.
public partial class QuestView : Node2D
{
    public QuestRun Run=null!;
    public Font Font=null!;
    public Action? Leave;
    readonly Dictionary<string,Texture2D?> art=new();
    static readonly MethodInfo RecoveredSprite=typeof(DivineGateOffline).Assembly.GetType("RecoveredImages",true)!.GetMethod("Sprite",BindingFlags.Public|BindingFlags.Static)!;
    int drag=-1,selected=-1;
    Vector2 pointer;
    bool menu;
    string error="";
    readonly Rect2 menuButton=new(510,12,116,56),door=new(210,145,220,180);
    Rect2 Cell(int n)=>new(20+n%5*120,350+n/5*120,118,118);
    Rect2 Hand(int n)=>new(20+n*120,805,112,112);
    Rect2 Field(int n)=>new(20+n*120,640,112,148);
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;SetProcess(true);}
    Texture2D? Sprite(string name)
    {
        if(art.TryGetValue(name,out var tex))return tex;
        foreach(var pair in LegacyView.Index["atlases"]!.AsObject()){
            var a=pair.Value!;var s=a["sprites"]!.AsArray().FirstOrDefault(n=>LegacyView.S(n?["name"])==name);if(s==null)continue;
            var r=s["outer"]!;tex=new AtlasTexture{Atlas=LegacyView.Texture(LegacyView.S(a["texture"])),Region=new Rect2(LegacyView.F(r["x"]),LegacyView.F(r["y"]),LegacyView.F(r["width"]),LegacyView.F(r["height"]))};art[name]=tex;return tex;
        }
        art[name]=null;return null;
    }
    void Image(Texture2D? tex,Rect2 rect,Color? tint=null,bool fit=false)
    {
        if(tex==null)return;
        if(fit){float scale=Math.Min(rect.Size.X/tex.GetWidth(),rect.Size.Y/tex.GetHeight());Vector2 size=tex.GetSize()*scale;rect=new(rect.GetCenter()-size/2,size);}
        DrawTextureRect(tex,rect,false,tint??Colors.White);
    }
    void Text(string text,Vector2 pos,int size=24,Color? color=null,float width=-1)
    {
        DrawStringOutline(Font,pos,text,HorizontalAlignment.Left,width,size,4,Colors.Black);
        DrawString(Font,pos,text,HorizontalAlignment.Left,width,size,color??Colors.White);
    }
    void Lines(string text,Vector2 pos,int size=24){foreach(string line in text.Split('\n')){Text(line,pos,size);pos.Y+=size+7;}}
    void Button(string text,Rect2 rect,Color? color=null){DrawRect(rect,color??new Color(.23f,.05f,.36f));DrawRect(rect,new Color(.74f,.3f,.9f),false,2);Text(text,new(rect.Position.X+12,rect.Position.Y+rect.Size.Y/2+10),25);}
    Texture2D? Portrait(uint id){string p="recovered/portrait/"+id+".png";return ResourceLoader.Exists("res://Reference/"+p)?LegacyView.Texture(p):null;}
    Texture2D? Recovered(string atlas,string name)
    {
        string key=atlas+"/"+name;if(art.TryGetValue(key,out var cached))return cached;
        var rect=(int[]?)RecoveredSprite.Invoke(null,new object[]{atlas,name});
        if(rect==null){art[key]=null;return null;}
        var tex=new AtlasTexture{Atlas=LegacyView.Texture("recovered/atlas/"+atlas+".png"),Region=new Rect2(rect[0],rect[1],rect[2],rect[3])};art[key]=tex;return tex;
    }
    Texture2D? Icon(uint id)=>Recovered($"AtlasCharaIcon_{id/200*200:000}_{id/200*200+199:000}",$"chara_icon_{id:000}");
    static string Element(int e)=>e switch{1=>"mu",2=>"hi",3=>"mizu",4=>"hikari",5=>"yami",6=>"kaze",7=>"kaifuku",_=>"panel_empty"};
    public override void _Process(double delta)
    {
        if(!menu && error=="")try{Run.Tick(delta);}catch(Exception ex){error=ex.Message;GD.PushError(ex.ToString());}
        QueueRedraw();
    }
    public override void _Notification(int what){if(what==NotificationApplicationPaused || what==NotificationApplicationFocusOut){Run?.Save();menu=true;drag=-1;}}
    public override void _Draw()
    {
        if(Run==null)return;
        var s=Run.State;
        DrawRect(new Rect2(0,0,640,1136),new Color(.04f,.025f,.055f));
        MapArt.Draw(this,Run.Area);
        DrawRect(new Rect2(0,0,640,95),new Color(.04f,.025f,.055f));
        Text(Run.Area.area_name,new(16,35),25,Colors.White,480);
        Text($"{s.Floor}/{s.Build.panel.Length-1} F    COIN {s.Money}    UNIT {s.Drops.Count}",new(16,75),23);
        Button("MENU",menuButton);
        Button(s.BossDefeated?"NEXT / CLEAR":s.Key?"BOSS BATTLE":"KEY REQUIRED",new Rect2(185,285,270,48),s.Key?new Color(.45f,.1f,.05f):new Color(.15f,.15f,.2f));
        for(int i=0;i<25;i++){
            Rect2 rect=Cell(i);var p=Run.Panels[i];
            if(!s.Open[i])Image(Recovered("AtlasPanelIcon","panel_blank"),rect);
            if(!s.Open[i])Image(Recovered("AtlasPanelIcon","panel_blank_frame"),rect);
            if(Run.CanMove(i))DrawRect(rect,Colors.Gold,false,3);
            if(!s.Open[i]){
                if(p.star==9)Image(Recovered("AtlasPanelIcon",Run.Area.res_icon_key),rect);
                else if(Run.CanMove(i)){string risk=p.risk==1?"b":p.risk==3?"r":"y";string name=p.star==8?"panel_Question":p.star==10?"panel_Attention":$"panel_{risk}_{Math.Clamp(p.star,1,6)}";Image(Recovered("AtlasPanelIcon",name),rect);}
            } else if(p.type==3){var g=s.Build.list_battle.FirstOrDefault(x=>x.unique_id==p.type_unique);uint id=g?.enemy_list.FirstOrDefault(x=>x>0)??0;var e=id>0?MasterDataUtil.GetEnemyParamFromID(id):null;if(e!=null)Image(Icon(e.chara_id),rect,new Color(1,1,1,.6f));}
            else if(p.type==2)Image(Recovered("AtlasPanelIcon",Run.Area.res_icon_box),rect,new Color(1,1,1,.55f));
        }
        Rect2 here=Cell(s.Position);Image(Portrait(s.Units[0].id),new Rect2(here.Position-new Vector2(0,25),new Vector2(118,142)),null,true);
        DrawRect(here,Colors.Yellow,false,4);
        if(Run.InBattle)DrawBattle();
        DrawParty();
        if(!Run.InBattle)Lines(s.Message,new(20,120),20);
        if(s.Phase=="cleared" || s.Phase=="defeat")DrawResult();
        if(menu || error!="")DrawMenu();
        if(drag>=0 && !menu)Image(Sprite(Element(s.Hand[drag])),new Rect2(pointer-new Vector2(42,42),new Vector2(84,84)),new Color(1,1,1,.85f));
    }
    void DrawBattle()
    {
        var s=Run.State;
        DrawRect(new Rect2(0,95,640,850),new Color(.025f,.01f,.035f,.96f));
        Text(s.Boss?"BOSS BATTLE":"BATTLE",new(20,142),40,Colors.Gold);
        Lines(s.Message,new(20,180),19);
        float width=590f/Math.Max(1,Run.Enemies.Length);
        for(int i=0;i<Run.Enemies.Length;i++){
            var enemy=Run.Enemies[i];if(enemy.isDead)continue;
            var rect=new Rect2(25+i*width,240,width,280);Image(Portrait(enemy.m_EnemyMasterParamChara.fix_id),rect,null,true);
            if(s.Target==i)DrawRect(rect,new Color(1,.85f,.1f),false,3);
            // Bars are inside the battle layer; the menu is drawn afterwards.
            float x=rect.Position.X+6,y=530;DrawRect(new Rect2(x,y,width-12,15),new Color(.2f,.02f,.05f));
            DrawRect(new Rect2(x,y,(width-12)*enemy.m_EnemyHP/Math.Max(1,enemy.m_EnemyHPMax),15),new Color(.95f,.04f,.22f));
            Text($"NEXT {enemy.m_EnemyTurn}",new(x,526),23,Colors.White,width-8);
            Text($"{enemy.m_EnemyHP}/{enemy.m_EnemyHPMax}",new(x,569),17,Colors.White,width-8);
        }
        Text(s.RoundActive?$"{s.Seconds:0.0} SEC":"PLAYER PHASE",new(20,620),31,Colors.Cyan);
        Text($"{Run.Skills().Count} HANDS",new(380,620),28,Colors.Gold);
        for(int f=0;f<5;f++){
            var rect=Field(f);Image(Sprite(f==s.Boost?"panel_cardslot_boost":"panel_cardslot"),rect);
            DrawRect(rect,f==s.Boost?Colors.Gold:new Color(.5f,.35f,.55f),false,2);
            for(int n=0;n<s.Fields[f].Count;n++)Image(Sprite(Element(s.Fields[f][n])),new Rect2(rect.Position+new Vector2(6+n%2*50,5+n/2*44),new Vector2(47,43)));
            Image(Sprite(Element(s.Hand[f])),Hand(f));
            if(selected==f)DrawRect(Hand(f),Colors.White,false,4);
        }
        Button("攻擊 / PASS",new Rect2(396,930,226,48));
        Text("拖曳手牌至上方格子",new(20,960),22);
    }
    void DrawParty()
    {
        var p=Run.Party;
        int y=Run.InBattle?990:970;
        DrawRect(new Rect2(0,y-5,640,146),new Color(.055f,.02f,.08f));
        Text($"HP {p.m_PartyTotalHP}/{p.m_PartyTotalHPMax}",new(16,y+23),27);
        DrawRect(new Rect2(16,y+32,608,11),new Color(.3f,.01f,.05f));DrawRect(new Rect2(16,y+32,608f*p.m_PartyTotalHP/p.m_PartyTotalHPMax,11),Colors.LimeGreen);
        Text($"SP {p.m_PartyTotalSP}/{p.m_PartyTotalSPMax}",new(420,y+23),24,Colors.Yellow);
        for(int i=0;i<5;i++)if(Run.Team[i]?.m_CharaMasterDataParam!=null){
            Image(Icon(Run.Team[i].m_CharaMasterDataParam.fix_id),new Rect2(18+i*123,y+46,88,88));
            Text("Lv."+Run.Team[i].m_CharaLevel,new(20+i*123,y+132),19);
        }
    }
    void DrawResult()
    {
        bool won=Run.State.Phase=="cleared";
        DrawRect(new Rect2(20,280,600,550),new Color(.055f,.025f,.075f,.98f));DrawRect(new Rect2(20,280,600,550),Colors.Gold,false,3);
        Text(won?"QUEST CLEAR":"GAME OVER",new(75,360),48,won?Colors.Gold:Colors.OrangeRed);
        if(won){Lines($"COIN  {Run.State.Money+Run.Quest.clear_money}\nEXP  {Run.State.Exp+Run.Quest.clear_exp}\nGET UNIT  {Run.State.Drops.Count}\nMAX HANDS  {Run.State.MaxCombo}\n\n獎勵已寫入存檔",new(65,430),28);}
        else Lines("HP 已耗盡。\n撤退後可重新挑戰。",new(65,460),28);
        Button(won?"返回關卡選單":"撤退",new Rect2(140,710,360,70));
    }
    void DrawMenu()
    {
        DrawRect(new Rect2(0,0,640,1136),new Color(0,0,0,.78f));
        DrawRect(new Rect2(35,300,570,470),new Color(.12f,.08f,.16f));
        Text("MENU",new(70,365),40);
        Lines(error!=""?error:"關卡進度已儲存。\n暫停時不會消耗操作時間。",new(70,420),22);
        Button("繼續",new Rect2(80,520,480,60));
        Button("儲存並回選單",new Rect2(80,600,480,60));
        Button("撤退（放棄本次獎勵）",new Rect2(80,680,480,60));
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(!IsVisibleInTree())return;
        if(e is InputEventKey k && k.Pressed && k.Keycode==Key.Escape){Run.Save();menu=!menu;return;}
        if(e is InputEventMouseMotion motion){pointer=motion.Position;return;}
        if(e is not InputEventMouseButton b || b.ButtonIndex!=MouseButton.Left)return;
        pointer=b.Position;
        try {
            if(b.Pressed){if(!menu && Run.InBattle)for(int h=0;h<5;h++)if(Hand(h).HasPoint(pointer)){drag=h;selected=h;}return;}
            if(menu || error!=""){
                if(new Rect2(80,520,480,60).HasPoint(pointer)){menu=false;error="";}
                else if(new Rect2(80,600,480,60).HasPoint(pointer)){Run.Save();Leave?.Invoke();}
                else if(new Rect2(80,680,480,60).HasPoint(pointer)){Run.Retire();Run.Dismiss();Leave?.Invoke();}
                drag=-1;return;
            }
            if(Run.State.Phase=="cleared" || Run.State.Phase=="defeat"){
                if(new Rect2(140,710,360,70).HasPoint(pointer)){if(!Run.Finished)Run.Retire();Run.Dismiss();Leave?.Invoke();}return;
            }
            if(menuButton.HasPoint(pointer)){Run.Save();menu=true;drag=-1;return;}
            if(Run.InBattle){
                int hand=drag>=0?drag:selected;drag=-1;
                if(hand>=0)for(int f=0;f<5;f++)if(Field(f).HasPoint(pointer)){Run.Place(hand,f);selected=-1;return;}
                if(new Rect2(396,930,226,48).HasPoint(pointer)){Run.Resolve();return;}
                float width=590f/Run.Enemies.Length;
                for(int i=0;i<Run.Enemies.Length;i++)if(new Rect2(25+i*width,240,width,330).HasPoint(pointer)){Run.State.Target=i;Run.Save();return;}
            }else{
                if(new Rect2(185,145,270,188).HasPoint(pointer)){Run.Door();return;}
                for(int i=0;i<25;i++)if(Cell(i).HasPoint(pointer)){Run.Move(i);return;}
            }
        }catch(Exception ex){error=ex.Message;GD.PushError(ex.ToString());}
        finally{if(IsInsideTree()){GetViewport().SetInputAsHandled();QueueRedraw();}}
    }
}
