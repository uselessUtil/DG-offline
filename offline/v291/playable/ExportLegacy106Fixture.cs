// Runs only with the delivered 1.0.6 DLLs in a separate test process.
using System;using System.IO;using System.Reflection;using LitJson;using ServerDataDefine;
class ExportLegacy106Fixture {
 static void Main(string[] args) {
  Singleton<MasterDataManager>.Instance=new MasterDataManager();Singleton<LocalSaveManager>.Instance=new LocalSaveManager();
  Singleton<UserDataAdmin>.Instance=new UserDataAdmin();var server=new ServerForgery();Singleton<ServerForgery>.Instance=server;
  typeof(ServerForgery).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(server,null);
  server.UserParamCreate(new SendCreateUser{uuid="fixed-106-migration",terminal=new PacketStructTerminal{platform=1,name="Docker fixture"}});
  var save=(ServerForgerySaveUser)typeof(ServerForgery).GetField("m_ForgeryPlayerSave",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(server);
  var add=typeof(ServerForgery).GetMethod("AddUnit",BindingFlags.NonPublic|BindingFlags.Instance);
  foreach(var row in server.m_MasterDataParamChara)if(row!=null && row.fix_id!=0)
   add.Invoke(server,new object[]{row.fix_id,(uint)row.level_max,3u,4u});
  for(int i=0;i<save.unit_list.Length;i++){
   var u=save.unit_list[i];uint max=0;
   foreach(var c in server.m_MasterDataParamChara)if(c.fix_id==u.id)
    foreach(var sk in server.m_MasterDataSkillLimitBreak)if(sk.fix_id==c.skill_limitbreak && c.skill_limitbreak!=0)max=(uint)sk.level_max;
   u.exp=(uint)(i*13);u.add_def=(uint)(i%5);u.limitbreak_lv=Math.Min((uint)(i%3),max);
   if(u.id==148)u.limitbreak_lv=max;
   u.get_time=i==0?0u:1700000000u+(uint)(i/3);
  }
  save.user_name="跨版備份測試⚡";save.player_rank=250;save.player_exp=server.m_MasterDataUserRank[249].exp_next_total+17u;
  save.have_money=12345678;save.have_stone_free=81;save.have_stone_pay=17;save.have_stone=98;save.have_friend_pt=6245;
  save.stamina_now=17;save.stamina_recover=2000000000;save.unit_paid=90;save.unit_base=2000;save.unit_total=2090;
  save.friend_paid=77;save.friend_total=300;save.party_paid=0;save.first_select_num=3;
  save.unit_party_assign=new PacketStructPartyAssign[5];
  for(int i=0;i<5;i++)save.unit_party_assign[i]=new PacketStructPartyAssign{unit0_unique_id=save.unit_list[i*4].unique_id,
   unit1_unique_id=save.unit_list[i*4+1].unique_id,unit2_unique_id=save.unit_list[i*4+2].unique_id,unit3_unique_id=save.unit_list[i*4+3].unique_id};
  save.unit_party_current=3;
  foreach(var q in server.m_MasterDataQuest)if(q!=null && q.fix_id!=0) {
   ServerDataUtil.AddBitFlag(ref save.flag_quest_check,q.fix_id);ServerDataUtil.AddBitFlag(ref save.flag_quest_clear,q.fix_id);
  }
  save.flag_tutorial[0]=0x25;
  save.present_list=new[]{
   new PacketStructPresent{serial_id=101,send_timing=1700000000,message="コイン",present_type=1,present_value0=123,delete_timing=-1,message_fix=0},
   new PacketStructPresent{serial_id=102,send_timing=1700000001,message="友情",present_type=2,present_value0=456,delete_timing=2000000000,message_fix=2},
   new PacketStructPresent{serial_id=103,send_timing=1700000002,message="無料",present_type=3,present_value0=4,delete_timing=-1},
   new PacketStructPresent{serial_id=104,send_timing=1700000003,message="有料",present_type=5,present_value0=5,delete_timing=-1},
   new PacketStructPresent{serial_id=105,send_timing=1700000004,message="通知備份",present_type=6,delete_timing=-1},
   new PacketStructPresent{serial_id=106,send_timing=1700000005,message="ユニット",present_type=7,present_value0=592,present_value1=1,delete_timing=-1}};
  var favorites=new TemplateList<long>();favorites.Add(save.unit_list[0].unique_id);favorites.Add(save.unit_list[521].unique_id);
  LocalSaveUtil.SetValueString("divinegate_FAVOTIRE_UNIT",JsonMapper.ToJson(favorites));
  var friends=new TemplateList<uint>();friends.Add(60001);LocalSaveUtil.SetValueString("divinegate_FAVOTIRE_FRIEND",JsonMapper.ToJson(friends));
  var used=new TemplateList<LocalSaveFriendUse>();used.Add(new LocalSaveFriendUse{m_FriendID=60001,m_FriendUseTime=1700000000});
  LocalSaveUtil.SetValueString("divinegate_FRIEND_USE",JsonMapper.ToJson(used));
  LocalSaveUtil.SetValueString("divinegate_OPTION",JsonMapper.ToJson(new LocalSaveOption{m_OptionBGM=0,m_OptionSE=1,m_OptionVoice=1,m_OptionGuide=1}));
  LocalSaveUtil.SetValueInt("divinegate_TUTORIAL_GROUP",511);LocalSaveUtil.SetValueInt("divinegate_SORT_UNIT_LIST",6);
  LocalSaveUtil.SetValueString("divinegate_RESTORE_VER","old106-token");
  LocalSaveUtil.SetValueString("divinegate_USER_UUID",save.uuid);
  LocalSaveUtil.SetValueString("divinegate_SCRATCH","stale106-data");
  Directory.CreateDirectory(args[0]);
  File.WriteAllText(Path.Combine(args[0],"legacy106.dgbackup.json"),DivineGateOffline.ExportSaveBackup(server));
  File.WriteAllText(Path.Combine(args[0],"source-chars.json"),JsonMapper.ToJson(server.m_MasterDataParamChara));
  File.WriteAllText(Path.Combine(args[0],"source-quests.json"),JsonMapper.ToJson(server.m_MasterDataQuest));
  Console.WriteLine("PASS fixed 1.0.6 APK exported fixture: "+save.unit_list.Length+" units, all quest flags, 5 parties, 6 gift types, favorite/sort/options.");
 }
}
