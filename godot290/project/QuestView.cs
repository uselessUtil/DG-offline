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
    int drag=-1,selected=-1,skillOwner=-1;
    readonly List<BattleEvent> playback=new();
    int frame;float elapsed,duration;BattleParticles? particles;
    bool Animating=>frame<playback.Count;
    BattleEvent? Current=>Animating?playback[frame]:null;
    bool BattleVisible=>Run.InBattle || Animating && Current!.EnemyIds.Length>0;
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
        if(!menu && skillOwner<0 && error=="")try{if(Animating)Advance((float)delta);else {Run.Tick(delta);Capture();}}catch(Exception ex){error=ex.Message;GD.PushError(ex.ToString());}
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
                else if(Run.CanMove(i) && !Run.Dark){string risk=p.risk==1?"b":p.risk==3?"r":"y";string name=p.star==8?"panel_Question":p.star==10?"panel_Attention":$"panel_{risk}_{Math.Clamp(p.star,1,6)}";Image(Recovered("AtlasPanelIcon",name),rect);}
            } else if(p.type==3){var g=s.Build.list_battle.FirstOrDefault(x=>x.unique_id==p.type_unique);uint id=g?.enemy_list.FirstOrDefault(x=>x>0)??0;var e=id>0?MasterDataUtil.GetEnemyParamFromID(id):null;if(e!=null)Image(Icon(e.chara_id),rect,new Color(1,1,1,.6f));}
            else if(p.type==2)Image(Recovered("AtlasPanelIcon",Run.Area.res_icon_box),rect,new Color(1,1,1,.55f));
        }
        Rect2 here=Cell(s.Position);Image(Portrait(s.Units[0].id),new Rect2(here.Position-new Vector2(0,25),new Vector2(118,142)),null,true);
        DrawRect(here,Colors.Yellow,false,4);
        if(BattleVisible)DrawBattle();
        DrawParty();
        if(!BattleVisible && !Animating)Lines(s.Message,new(20,120),20);
        if(!Animating && (s.Phase=="cleared" || s.Phase=="defeat"))DrawResult();
        DrawPlayback();
        if(skillOwner>=0)DrawSkill();
        if(menu || error!="")DrawMenu();
        if(drag>=0 && !menu)Image(Sprite(Element(s.Hand[drag])),new Rect2(pointer-new Vector2(42,42),new Vector2(84,84)),new Color(1,1,1,.85f));
    }
    void DrawBattle()
    {
        var s=Run.State;
        DrawRect(new Rect2(0,95,640,850),new Color(.025f,.01f,.035f,.96f));
        Text(s.Boss?"BOSS BATTLE":"BATTLE",new(20,142),40,Colors.Gold);
        Lines(Animating?Current!.Name:s.Message,new(20,180),19);
        int count=Current?.EnemyIds.Length??Run.Enemies.Length;
        float width=590f/Math.Max(1,count);
        for(int i=0;i<count;i++){
            int hp=Current?.EnemyHp[i]??Run.Enemies[i].m_EnemyHP;
            int max=Current?.EnemyMax[i]??Run.Enemies[i].m_EnemyHPMax;
            uint id=Current?.EnemyIds[i]??Run.Enemies[i].m_EnemyMasterParamChara.fix_id;
            bool dying=Animating && Current!.Target==i && Current.Kind=="damage" && hp<=0;
            if(hp<=0 && !dying)continue;
            var rect=new Rect2(25+i*width,240,width,280);var portrait=rect;Color tint=Colors.White;
            if(Animating && Current!.Target==i && Current.Kind=="enemy"){
                Vector2 offset=BattleMotion.Sample("EnemyAttack00","m_PositionCurves",elapsed),zoom=BattleMotion.Sample("EnemyAttack00","m_ScaleCurves",elapsed,scale:true);
                portrait=new(rect.GetCenter()+offset-rect.Size*zoom/2,rect.Size*zoom);
            }
            if(dying)tint.A=Math.Clamp(1-elapsed/duration,0,1);
            if(Animating && Current!.Target==i && Current.Kind=="damage" && elapsed<.12f)tint=new Color(1,.5f,.5f,tint.A);
            Image(Portrait(id),portrait,tint,true);
            if(s.Target==i)DrawRect(rect,new Color(1,.85f,.1f),false,3);
            float x=rect.Position.X+6,y=530;DrawRect(new Rect2(x,y,width-12,15),new Color(.2f,.02f,.05f));
            DrawRect(new Rect2(x,y,(width-12)*Math.Max(0,hp)/Math.Max(1,max),15),new Color(.95f,.04f,.22f));
            if(i<Run.Enemies.Length)Text($"NEXT {Run.Enemies[i].m_EnemyTurn}",new(x,526),23,Colors.White,width-8);
            Text($"{Math.Max(0,hp)}/{max}",new(x,569),17,Colors.White,width-8);
            if(!Animating && i<Run.Enemies.Length)Text(Run.StatusText(Run.Enemies[i].m_EnemyAilmentID),new(x,590),14,Colors.Gold,width-8);
        }
        Text(Animating?"ACTION PHASE":s.RoundActive?$"{s.Seconds:0.0} SEC":"PLAYER PHASE",new(20,620),31,Colors.Cyan);
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
        int y=BattleVisible?990:970;
        DrawRect(new Rect2(0,y-5,640,146),new Color(.055f,.02f,.08f));
        int shownHp=Current?.PartyHp??p.m_PartyTotalHP;
        Text($"HP {shownHp}/{p.m_PartyTotalHPMax}",new(16,y+23),27);
        DrawRect(new Rect2(16,y+32,608,11),new Color(.3f,.01f,.05f));DrawRect(new Rect2(16,y+32,608f*shownHp/p.m_PartyTotalHPMax,11),Colors.LimeGreen);
        Text($"SP {p.m_PartyTotalSP}/{p.m_PartyTotalSPMax}",new(420,y+23),24,Colors.Yellow);
        for(int i=0;i<5;i++)if(Run.Team[i]?.m_CharaMasterDataParam!=null){
            Image(Icon(Run.Team[i].m_CharaMasterDataParam.fix_id),new Rect2(18+i*123,y+46,88,88));
            Text(Run.ActiveSkill(i)==null?"Lv."+Run.Team[i].m_CharaLevel:Run.SkillTurns(i)>0?"AS "+Run.SkillTurns(i):"AS READY",new(20+i*123,y+132),18,Run.SkillTurns(i)==0?Colors.Gold:Colors.White);
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
    void DrawSkill()
    {
        var sk=Run.ActiveSkill(skillOwner);DrawRect(new Rect2(0,0,640,1136),new Color(0,0,0,.8f));
        DrawRect(new Rect2(35,240,570,610),new Color(.12f,.06f,.17f));
        Image(Icon(Run.Team[skillOwner].m_CharaMasterDataParam.fix_id),new Rect2(65,270,100,100));
        Text("ACTIVE SKILL",new(185,315),32,Colors.Gold);
        Text(sk?.name??"沒有主動技能",new(65,410),28,Colors.White,510);
        string detail=sk?.detail??"";detail=System.Text.RegularExpressions.Regex.Replace(detail,@"\[(?:[0-9A-Fa-f]{6}|-)\]","");
        float y=460;string line="";
        foreach(char c in detail){if(c=='\n' || Font.GetStringSize(line+c,fontSize:23).X>505){Text(line,new(65,y),23);line="";y+=30;if(c=='\n')continue;}line+=c;}
        Text(line,new(65,y),23);
        Text($"SP {sk?.use_sp??0}   /   CD {Run.SkillTurns(skillOwner)}",new(65,660),26,Colors.Yellow);
        string why=Run.SkillUnavailable(skillOwner);Text(why,new(65,710),22,Colors.Orange,510);
        Button("返回",new Rect2(65,750,235,65));Button("發動",new Rect2(330,750,245,65),why==""?new Color(.55f,.12f,.25f):new Color(.16f,.16f,.18f));
    }
    void Capture()
    {
        if(Run.Events.Count==0)return;
        playback.Clear();playback.AddRange(Run.Events);Run.Events.Clear();frame=0;BeginEvent();
    }
    void BeginEvent()
    {
        if(particles!=null){particles.Hide();particles.QueueFree();particles=null;}
        elapsed=0;if(!Animating)return;var e=Current!;
        duration=e.Kind switch{"active"=>BattleMotion.Duration("CutinSlide0"),"skill"=>.25f,"enemy"=>BattleMotion.Duration("EnemyAttack00"),"damage"=>BattleMotion.Duration("DamageValueHit"),"hands"=>BattleMotion.Duration("SkillHandAllIn"),"battle"=>.5f,_=>.5f};
        if(e.Effect>0 && e.Kind=="damage"){
            particles=new BattleParticles();int anchor=BattleParticles.Anchor(e.Effect);
            float width=590f/Math.Max(1,e.EnemyIds.Length);
            particles.Position=anchor==1?new Vector2(320,960):anchor==2 && e.Target>=0?new Vector2(25+(e.Target+.5f)*width,400):new Vector2(320,400);
            particles.ZIndex=1;AddChild(particles);particles.Setup(e.Effect);duration=Math.Max(duration,Math.Min(2,particles.Duration));
        }
    }
    void Advance(float delta)
    {
        elapsed+=delta;particles?.Seek(elapsed);
        if(elapsed>=duration){frame++;BeginEvent();}
    }
    void DrawPlayback()
    {
        if(particles!=null)particles.Visible=!menu && error=="";
        if(!Animating)return;var e=Current!;
        if(e.Kind=="skill" || e.Kind=="active"){
            Vector2 off=e.Kind=="active"?BattleMotion.Sample("CutinSlide0","m_PositionCurves",elapsed):Vector2.Zero;
            DrawRect(new Rect2(off+new Vector2(0,280),new Vector2(640,160)),new Color(.25f,0,.35f,.93f));
            if(e.Owner>=0)Image(Icon(Run.Team[e.Owner].m_CharaMasterDataParam.fix_id),new Rect2(off+new Vector2(20,295),new Vector2(115,115)));
            Text(e.Name,off+new Vector2(150,355),28,Colors.Gold,465);
            if(e.Kind=="active")Text("ACTIVE SKILL",off+new Vector2(150,400),26,Colors.White);
        }
        if(e.Kind=="damage" || e.Kind=="heal" || e.Kind=="enemy" || e.Kind=="player-hit"){
            float width=590f/Math.Max(1,e.EnemyIds.Length);
            Vector2 pos=e.Kind=="damage"?new Vector2(25+(e.Target+.5f)*width,410):new Vector2(310,1005);
            Vector2 off=BattleMotion.Sample("DamageValueHit","m_PositionCurves",elapsed);
            Text((e.Kind=="heal"?"+":"")+e.Amount,pos+off*.3f-new Vector2(30,0),40,e.Kind=="heal"?Colors.LimeGreen:Colors.Gold);
        }
        if(e.Kind=="hands" || e.Kind=="battle" || e.Kind=="win" || e.Kind=="defeat"){
            float zoom=Math.Clamp(elapsed/.12f,0,1);Text(e.Name,new(140,350),Math.Max(1,(int)(46*zoom)),Colors.Gold);
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(!IsVisibleInTree())return;
        if(e is InputEventKey k && k.Pressed && k.Keycode==Key.Escape){if(skillOwner>=0)skillOwner=-1;else{Run.Save();menu=!menu;}return;}
        if(e is InputEventMouseMotion motion){pointer=motion.Position;return;}
        if(e is not InputEventMouseButton b || b.ButtonIndex!=MouseButton.Left)return;
        pointer=b.Position;
        try {
            if(b.Pressed){if(!menu && skillOwner<0 && !Animating && Run.InBattle)for(int h=0;h<5;h++)if(Hand(h).HasPoint(pointer)){drag=h;selected=h;}return;}
            if(menu || error!=""){
                if(new Rect2(80,520,480,60).HasPoint(pointer)){menu=false;error="";}
                else if(new Rect2(80,600,480,60).HasPoint(pointer)){Run.Save();Leave?.Invoke();}
                else if(new Rect2(80,680,480,60).HasPoint(pointer)){Run.Retire();Run.Dismiss();Leave?.Invoke();}
                drag=-1;return;
            }
            if(skillOwner>=0){
                if(new Rect2(65,750,235,65).HasPoint(pointer))skillOwner=-1;
                else if(new Rect2(330,750,245,65).HasPoint(pointer) && Run.SkillUnavailable(skillOwner)==""){Run.UseActive(skillOwner);skillOwner=-1;Capture();}return;
            }
            if(Animating){if(menuButton.HasPoint(pointer)){Run.Save();menu=true;}return;}
            if(Run.State.Phase=="cleared" || Run.State.Phase=="defeat"){
                if(new Rect2(140,710,360,70).HasPoint(pointer)){if(!Run.Finished)Run.Retire();Run.Dismiss();Leave?.Invoke();}return;
            }
            if(menuButton.HasPoint(pointer)){Run.Save();menu=true;drag=-1;return;}
            int partyY=Run.InBattle?1036:1016;
            for(int c=0;c<5;c++)if(new Rect2(18+c*123,partyY,106,100).HasPoint(pointer) && Run.Team[c]!=null){skillOwner=c;drag=-1;selected=-1;return;}
            if(Run.InBattle){
                int hand=drag>=0?drag:selected;drag=-1;
                if(hand>=0)for(int f=0;f<5;f++)if(Field(f).HasPoint(pointer)){Run.Place(hand,f);selected=-1;return;}
                if(new Rect2(396,930,226,48).HasPoint(pointer)){Run.Resolve();Capture();return;}
                float width=590f/Run.Enemies.Length;
                for(int i=0;i<Run.Enemies.Length;i++)if(new Rect2(25+i*width,240,width,330).HasPoint(pointer)){Run.State.Target=i;Run.Save();return;}
            }else{
                if(new Rect2(185,145,270,188).HasPoint(pointer)){Run.Door();Capture();return;}
                for(int i=0;i<25;i++)if(Cell(i).HasPoint(pointer)){Run.Move(i);Capture();return;}
            }
        }catch(Exception ex){error=ex.Message;GD.PushError(ex.ToString());}
        finally{if(IsInsideTree()){GetViewport().SetInputAsHandled();QueueRedraw();}}
    }
}
