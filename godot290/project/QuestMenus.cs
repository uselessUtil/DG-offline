using Godot;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using ServerDataDefine;

namespace DivineGateGodot;

public partial class Main
{
    QuestView? questView;
    static string QuestName(string name)=>Regex.Replace(name,@"\[(?:[0-9a-fA-F]{6}|-)\]","");
    void QuestList(uint area)
    {
        ClearDynamic();Label("Quest Select",new Rect2(35,155,550,60),36);
        Button("返回",new Rect2(30,220,160,55),()=>Show("quest"));
        var scroll=new ScrollContainer{Position=new(30,295),Size=new(580,620)};dynamicUi.AddChild(scroll);
        var list=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(list);
        foreach(var q in core.Server.m_MasterDataQuest.Where(q=>q.area_id==area && q.active==2)){
            var b=new Button{Text=QuestName(q.quest_name)+"\nSTAMINA "+q.quest_stamina,CustomMinimumSize=new(560,90)};
            b.AddThemeFontOverride("font",font);b.AddThemeFontSizeOverride("font_size",23);list.AddChild(b);
            b.Pressed+=()=>PrepareQuest(q.fix_id);
        }
    }
    void PrepareQuest(uint id)
    {
        var q=core.Server.m_MasterDataQuest.First(x=>x.fix_id==id);
        var assign=core.Player.unit_party_assign[core.Player.unit_party_current];
        long[] ids={assign.unit0_unique_id,assign.unit1_unique_id,assign.unit2_unique_id,assign.unit3_unique_id};
        PacketStructUnit[] team=new PacketStructUnit[5];for(int i=0;i<4;i++)team[i]=core.Player.unit_list.FirstOrDefault(u=>u.unique_id==ids[i])!;
        PacketStructFriend[] helpers=core.Api<RecvQuestHelperGet>(SERVER_API.SERVER_API_QUEST_HELPER_GET,new SendQuestHelperGet()).result.friend;
        team[4]=helpers.FirstOrDefault()?.unit??team[0];
        void Draw(){
            ClearDynamic();Label(QuestName(q.quest_name),new Rect2(30,160,580,70),25);
            Label("Party / Helper",new Rect2(30,280,580,40),28);
            for(int i=0;i<5;i++){
                int slot=i;var u=team[i];
                if(u!=null)Portrait(u.id,new Rect2(20+i*123,320,112,170));
                Button(i==4?"助戰":i==0?"隊長":"隊員",new Rect2(20+i*123,500,112,55),()=>ChooseUnit(slot));
                if(u!=null)Label("Lv."+u.level,new Rect2(25+i*123,565,112,30),22);
            }
            Label("點選隊員可更換持有角色。\n助戰使用 2.9.0 離線版的助戰列表。\n\n將消耗體力："+q.quest_stamina,new Rect2(35,630,570,150),24);
            Button("Quest Start",new Rect2(150,820,340,70),()=>Confirm("開始關卡",$"消耗 {q.quest_stamina} 體力？",()=>{var run=new QuestRun(core);run.Start(id,team);PlayQuest(run);}));
            Button("返回",new Rect2(30,220,145,50),()=>QuestList(q.area_id));
        }
        void ChooseUnit(int slot){
            ClearDynamic();Label("選擇角色",new Rect2(30,180,550,55),32);
            var scroll=new ScrollContainer{Position=new(30,260),Size=new(580,580)};dynamicUi.AddChild(scroll);
            var list=new VBoxContainer{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(list);
            foreach(var u in slot==4?helpers.Select(h=>h.unit):core.Player.unit_list){
                var ch=MasterDataUtil.GetCharaParamFromID(u.id);var b=new Button{Text=ch.name+"  Lv."+u.level,CustomMinimumSize=new(560,65)};
                b.AddThemeFontOverride("font",font);b.AddThemeFontSizeOverride("font_size",23);list.AddChild(b);
                b.Pressed+=()=>{if(slot<4 && team.Where((_,n)=>n<4 && n!=slot).Any(x=>x?.unique_id==u.unique_id)){Notice("同一角色不能重複編入隊伍。");return;}team[slot]=u;Draw();};
            }
            Button("返回",new Rect2(180,870,280,60),Draw);
        }
        Draw();
    }
    void PlayQuest(QuestRun run)
    {
        ClearDynamic();page.Visible=false;header.Visible=false;footer.Visible=false;
        questView=new QuestView{Run=run,Font=font};AddChild(questView);
        questView.Leave=()=>{var closing=questView;questView=null;closing!.Hide();closing.ProcessMode=ProcessModeEnum.Disabled;closing.QueueFree();header.Visible=true;footer.Visible=true;Show("quest");};
    }
}
