using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace DivineGateGodot;

public partial class Main : Node2D
{
    readonly GameCore core=new();
    LegacyView header=null!,footer=null!,page=null!;
    Control dynamicUi=null!;
    string current="shop";
    bool collab;
    int poolTab=1;
    uint[] PoolIds=>collab?new uint[]{300100,300200,300300,300400}:new uint[]{100100,200100,0,0};
    Font font=null!;
    public override void _Ready()
    {
        try {
            LegacyView.Index=JsonNode.Parse(FileAccess.GetFileAsString("res://Reference/index.json"))!.AsObject();
            var key=LegacyView.Index["fonts"]!.AsObject().OrderByDescending(x=>x.Value!["font"]!["mSaved"]!.AsArray().Count).First().Key;
            // Original bitmap fonts contain placeholder glyphs for several
            // Traditional Chinese characters. New controls use OS fallbacks;
            // LegacyView still draws the original Japanese bitmap typography.
            font=ThemeDB.FallbackFont;
            bool smoke=OS.GetCmdlineUserArgs().Contains("--smoke") || OS.GetCmdlineUserArgs().Contains("--quest-smoke") || OS.GetCmdlineUserArgs().Contains("--battle-smoke");
            core.Boot(ProjectSettings.GlobalizePath(smoke?"user://smoke-"+Guid.NewGuid().ToString("N"):"user://r17-port"),ProjectSettings.GlobalizePath("res://Tables"));
            if(smoke) {
                core.SelectStarter(1);uint initial=core.Player.have_stone;
                core.Claim(5);if(core.Player.have_stone!=initial+85)throw new Exception("Godot chip claim mismatch");
                var pool=core.Server.m_MasterDataGacha.First(x=>x.fix_id!=10 && x.price==5);
                int before=core.Player.unit_list.Length;core.Draw(pool.fix_id,1);
                if(core.Player.have_stone!=initial+80 || core.Player.unit_list.Length!=before+1)throw new Exception("Godot draw/save mismatch");
                GD.Print("PASS Godot starter, free chips, scratch and persisted account");
            }
            View("prefab/mainmenu/mainmenufixlayer/fixlayerbg");
            page=View("prefab/mainmenu/mainmenupage/pageshoproot");
            header=View("prefab/mainmenu/mainmenufixlayer/fixlayerplayerparam");
            header.Active["LayerUserDetail"]=false;
            foreach(string s in new[]{"LabelMoneyLimitOverSymbol","LabelStoneLimitOverSymbol","LabelTicketLimitOverSymbol"})header.Active[s]=false;
            footer=View("prefab/mainmenu/mainmenufixlayer/fixlayerlinker");
            footer.Labels["Label_message"]="Godot 移植開發版 / r17 規則";
            footer.Active["CountMark"]=false;
            footer.Clicked=OnFooter;page.Clicked=OnPage;
            dynamicUi=new Control{MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(dynamicUi);
            Show("shop");
            foreach(string arg in OS.GetCmdlineUserArgs())if(arg.StartsWith("--page="))Show(arg.Substring(7));
            if(core.Player.first_select_num<1)Starter();
            if(OS.GetCmdlineUserArgs().Contains("--quest-smoke") || OS.GetCmdlineUserArgs().Contains("--battle-smoke")){
                var team=new ServerDataDefine.PacketStructUnit[5];for(int i=0;i<4;i++)team[i]=core.Player.unit_list[i];team[4]=core.Player.unit_list[0];
                var run=new QuestRun(core);run.Start(1,team);PlayQuest(run);
                if(OS.GetCmdlineUserArgs().Contains("--battle-smoke")){run.Move(17);run.Move(12);run.Events.Clear();run.Team[0].AddCharaLimitBreak(999);run.UseActive(0);}
            }
            if(OS.GetCmdlineUserArgs().Contains("--capture"))Capture();
        } catch(Exception ex) { GD.PushError(ex.ToString());Label(ex.ToString(),new Rect2(20,180,600,700)); }
    }
    LegacyView View(string name) { var view=new LegacyView();AddChild(view);view.LoadScene(name);return view; }
    void Header()
    {
        var p=core.Player;
        header.Labels["LabelRank"]=p.player_rank.ToString();header.Labels["LabelUserName"]=p.user_name;
        header.Labels["LabelStone"]=p.have_stone.ToString();header.Labels["LabelTicket"]=p.have_ticket.ToString();header.Labels["LabelMoney"]=p.have_money.ToString("N0");
        header.Labels["LabelStamina"]=$"{p.stamina_now}/{p.stamina_max}";
        header.Labels["LabelStaminaCount"]="";header.Labels["FixLabelStaminaAto"]="";
        header.QueueRedraw();footer.QueueRedraw();
    }
    void ClearDynamic() { foreach(Node child in dynamicUi.GetChildren()) {if(child is CanvasItem item)item.Hide();child.ProcessMode=ProcessModeEnum.Disabled;child.QueueFree();} }
    void Show(string target)
    {
        current=target;ClearDynamic();page.Visible=true;page.Labels.Clear();page.Active.Clear();Header();
        switch(target) {
            case "shop":
                page.LoadScene("prefab/mainmenu/mainmenupage/pageshoproot");
                page.Active["ForReplace"]=false;
                page.Active["StoneDiscountMsg"]=false;
                page.Labels["StonePrice"]="FREE";
                for(int i=1;i<=6;i++) {
                    string root="/PageShopRoot/PageRoot/LinkerList/ProductButtons/Button"+i;
                    page.Labels[root+"/StoneNum"]=DivineGateOffline.FreeChipProductAt(i-1).product_num.ToString();
                }
                break;
            case "scratch":Pools();break;
            case "units":Units();break;
            case "quest":Areas();break;
            case "others":
                page.LoadScene("prefab/mainmenu/mainmenupage/pagewideuseempty");
                Label("Godot 移植進度\n\n已接上 r17 本機後端與關卡流程。\n技能與原版演出仍在移植中。\n存檔與原 APK 分開保存。",new Rect2(40,230,560,300));
                break;
        }
    }
    void OnFooter(string path)
    {
        if(core.Player.first_select_num<1)return;
        if(path.Contains("gacha"))Show("scratch");else if(path.Contains("shop"))Show("shop");else if(path.Contains("unit"))Show("units");else if(path.Contains("quest"))Show("quest");else Show("others");
    }
    void OnPage(string path)
    {
        if(current=="shop")for(int i=1;i<=6;i++)if(path.EndsWith("/Button"+i)) {
            int pack=i-1;Confirm("無料チップ",$"免費領取 {DivineGateOffline.FreeChipProductAt(pack).product_num} 枚代幣？",()=>{core.Claim(pack);Show("shop");});return;
        }
        if(current=="scratch") {
            if(path.EndsWith("/ButtonDetail")){collab=!collab;poolTab=collab?0:1;Show("scratch");return;}
            for(int i=0;i<4;i++)if(path.EndsWith("/Switch"+i) && PoolIds[i]!=0){poolTab=i;Show("scratch");return;}
            if(path.EndsWith("/ButtonOK")) {
                uint id=PoolIds[poolTab];var pool=core.Server.m_MasterDataGacha.First(x=>x.fix_id==id);
                Confirm(pool.name,"1 回めくりますか？",()=>DrawScratch(id));
            }
        }
    }
    void Starter()
    {
        ClearDynamic();page.Visible=false;
        Label("選擇初始隊伍",new Rect2(50,250,540,60),38);
        Label("沿用 r17 的跳過教學規則與初始獎勵。",new Rect2(50,330,540,80),23);
        string[] names={"アカネ / 火","アオト / 水","ミドリ / 風"};
        for(int i=0;i<3;i++) { int party=i+1;Button(names[i],new Rect2(80,460+i*95,480,75),()=>{core.SelectStarter(party);page.Visible=true;Show("shop");}); }
    }
    void Pools()
    {
        page.LoadScene("prefab/mainmenu/mainmenupage/pagegacharootnew");
        var ids=PoolIds;
        for(int i=0;i<4;i++) {
            page.Active["Window"+i]=i==poolTab;
            page.Active["Switch"+i]=ids[i]!=0;
            page.Labels["TabLabel"+i]=ids[i]==0?"":core.Server.m_MasterDataGacha.First(p=>p.fix_id==ids[i]).tab_name;
        }
        var pool=core.Server.m_MasterDataGacha.First(p=>p.fix_id==ids[poolTab]);
        page.Labels["WindowTabLabel"]=pool.tab_name;
        page.Labels["MarkNum"]=((pool.type==1?core.Player.have_friend_pt:core.Player.have_stone)/pool.price).ToString();
        page.Labels["/PageGachaRootNew/PageRootTop/PageRoot/WindowRootBottom/WindowRoot/GachaButtons/ButtonDetail/ButtonMsg"]=collab?"通常に戻る":"コラボを選択";
        uint art=core.Server.m_MasterDataGachaAssign.First(x=>x.gacha_id==pool.fix_id && x.active==2 && x.assign_rate>0).assign_chara_id;
        page.Active["URLTexture"]=true;page.MeshTextures["URLTexture"]="recovered/portrait/"+art+".png";
        page.Positions["URLTexture"]=new(0,500);page.Scales["URLTexture"]=new(400,300);
        page.AddLabel("Window"+poolTab,pool.name+"\n"+(pool.type==1?"友情ポイント ":"チップ ")+pool.price+" / 1回",new(0,290),21);
        page.QueueRedraw();
    }
    void DrawScratch(uint pool)
    {
        try {
            var units=core.Draw(pool,1);ClearDynamic();page.Visible=false;Header();
            Label("GET UNIT",new Rect2(60,220,520,70),40);
            var u=units[0];var c=core.Server.m_MasterDataParamChara.First(x=>x.fix_id==u.id);
            Portrait(u.id,new Rect2(70,320,500,440));
            Label(c.name+"  Lv."+u.level,new Rect2(60,770,520,50),30);
            Button("戻る",new Rect2(190,840,260,70),()=>Show("scratch"));
        } catch(Exception ex) {Notice(ex.Message);}
    }
    void Units()
    {
        page.LoadScene("prefab/mainmenu/mainmenupage/pagewideuseempty");
        Label("Units",new Rect2(60,160,520,60),40);
        var scroll=new ScrollContainer{Position=new(25,250),Size=new(590,670)};dynamicUi.AddChild(scroll);
        var grid=new GridContainer{Columns=4,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(grid);
        foreach(var unit in core.Player.unit_list??Array.Empty<ServerDataDefine.PacketStructUnit>()) {
            var c=core.Server.m_MasterDataParamChara.First(x=>x.fix_id==unit.id);
            var button=new Button{Text=c.name+"\nLv."+unit.level,CustomMinimumSize=new(140,120),TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis};
            button.AddThemeFontOverride("font",font);button.AddThemeFontSizeOverride("font_size",19);grid.AddChild(button);
            button.Pressed+=()=>{ClearDynamic();Portrait(unit.id,new Rect2(20,240,600,550));Label(c.name+"\nLv."+unit.level,new Rect2(50,810,540,70),30);Button("戻る",new Rect2(200,900,240,60),()=>Show("units"));};
        }
    }
    void Areas()
    {
        page.LoadScene("prefab/mainmenu/mainmenupage/pagewideuseempty");Label("Quest",new Rect2(60,160,520,60),40);
        var resume=new QuestRun(core);
        if(resume.Restore())Button(resume.Finished?"查看通關結果":"繼續上次關卡",new Rect2(30,225,580,65),()=>PlayQuest(resume));
        var scroll=new ScrollContainer{Position=new(30,310),Size=new(580,620)};dynamicUi.AddChild(scroll);
        var list=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(list);
        foreach(var area in core.Server.m_MasterDataArea.Where(a=>core.Server.m_MasterDataQuest.Any(q=>q.area_id==a.fix_id && q.active==2))) {
            var b=new Button{Text=area.area_name,CustomMinimumSize=new(560,75)};b.AddThemeFontOverride("font",font);b.AddThemeFontSizeOverride("font_size",25);list.AddChild(b);
            b.Pressed+=()=>QuestList(area.fix_id);
        }
    }
    void Portrait(uint id,Rect2 rect)
    {
        string path="res://Reference/recovered/portrait/"+id+".png";
        if(!ResourceLoader.Exists(path))return;
        dynamicUi.AddChild(new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Texture=GD.Load<Texture2D>(path),Position=rect.Position,Size=rect.Size,MouseFilter=Control.MouseFilterEnum.Ignore});
    }
    void Label(string text,Rect2 rect,int size=26)
    {
        var label=new Label{Text=text,Position=rect.Position,Size=rect.Size,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};
        label.AddThemeFontOverride("font",font);label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",Colors.White);label.AddThemeColorOverride("font_outline_color",Colors.Black);label.AddThemeConstantOverride("outline_size",3);
        (dynamicUi??(Node)this).AddChild(label);
    }
    void Button(string text,Rect2 rect,Action action)
    {
        var b=new Button{Text=text,Position=rect.Position,Size=rect.Size};b.AddThemeFontOverride("font",font);b.AddThemeFontSizeOverride("font_size",26);dynamicUi.AddChild(b);
        b.Pressed+=()=>{try{action();}catch(Exception ex){Notice(ex.Message);}};
    }
    void Notice(string text)=>Confirm("通知",text,null);
    void Confirm(string title,string text,Action? accept)
    {
        var d=new ConfirmationDialog{Title=title,DialogText=text,OkButtonText=accept==null?"OK":"確認",CancelButtonText="返回",Size=new(560,350),Exclusive=true};
        bool committed=false;
        AddChild(d);d.AddThemeFontOverride("font",font);d.Confirmed+=()=>{if(committed)return;committed=true;d.QueueFree();try{accept?.Invoke();}catch(Exception ex){Notice(ex.Message);}};d.Canceled+=d.QueueFree;d.PopupCentered();
    }
    async void Capture()
    {
        try {
            await ToSignal(GetTree().CreateTimer(1),SceneTreeTimer.SignalName.Timeout);
            if(OS.GetCmdlineUserArgs().Contains("--smoke") && current=="scratch") {
                async System.Threading.Tasks.Task Click(string suffix) {
                    Vector2 p=GetViewport().GetFinalTransform()*page.ButtonCenter(suffix);
                    Input.ParseInputEvent(new InputEventMouseMotion{Position=p,GlobalPosition=p});
                    Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true});
                    Input.ParseInputEvent(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false});
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                }
                await Click("/ButtonDetail");if(!collab)throw new Exception("Collab button input failed");
                await Click("/Switch3");if(poolTab!=3)throw new Exception("Index tab input failed");
                await Click("/ButtonDetail");if(collab || poolTab!=1)throw new Exception("Return to normal scratch failed");
                int before=core.Player.unit_list.Length;
                DrawScratch(PoolIds[poolTab]);
                if(page.Visible || core.Player.unit_list.Length!=before+1)throw new Exception("Scratch result did not hide the previous page");
                Show("scratch");
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                await Click("/ButtonDetail");if(!collab)throw new Exception("Collab button failed after draw result return");
                await Click("/ButtonDetail");if(collab)throw new Exception("Normal return failed after draw result");
                GD.Print("PASS real Godot mouse events: normal -> collab -> Index -> normal; button bounds restored");
            }
            if(OS.GetCmdlineUserArgs().Contains("--battle-smoke")){
                await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("/tmp/dg-godot290/battle-preview.png");
                await ToSignal(GetTree().CreateTimer(5),SceneTreeTimer.SignalName.Timeout);
                GD.Print("PASS Godot active cutin / damage / source particle presentation");
            }
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng("/tmp/dg-godot290/preview.png");GetTree().Quit();
        } catch(Exception ex) {GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
}
