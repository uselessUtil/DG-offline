// Disposable managed probe of the same CharaOnce overload used by the friend page.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ServerDataDefine;
class SupportSetupProbe400 {
 static T Uninitialized<T>() {return (T)FormatterServices.GetUninitializedObject(typeof(T));}
 static void Require(bool value,string message) {if(!value)throw new Exception(message);}
 static CharaOnce Setup(PacketStructFriend friend) {
  var u=friend.unit;var l=friend.unit_link;var c=new CharaOnce();
  Require(c.CharaSetupFromID(u.id,(int)u.level,(int)u.limitbreak_lv,(int)u.limitover_lv,(int)u.add_pow,(int)u.add_hp,
   l.id,(int)l.level,(int)l.add_pow,(int)l.add_hp,(int)u.link_point,(int)l.limitover_lv),"Friend setup returned false");
  return c;
 }
 static void Main(string[] args) {
  var server=Uninitialized<ServerForgery>();Singleton<ServerForgery>.Instance=server;Singleton<ServerForgery>.HasInstance=true;
  var master=Uninitialized<MasterDataManager>();Singleton<MasterDataManager>.Instance=master;Singleton<MasterDataManager>.HasInstance=true;
  var adapter=Assembly.Load("DivineGateOffline");var data=adapter.GetType("DG400Data");
  data.GetMethod("Apply").Invoke(null,new object[]{server});data.GetMethod("Share").Invoke(null,new object[]{server,master});
  var original=server.CreateMobUser(12345,FRIEND_STATE.FRIEND_STATE_UNRELATED);
  Require(master.m_MasterDataLinkSystem.Length==0,"Donor unexpectedly has Link rules");
  if(args.Length>0 && args[0]=="legacy") {
   Require(original.unit_link.id!=0,"Legacy support has no random Link partner");
   try {Setup(original);}
   catch(IndexOutOfRangeException e) {
    Require(e.StackTrace.Contains("CharaLinkUtil.GetLinkUnitBonusElement"),"Wrong legacy failure");
    Console.WriteLine("PASS: r11 native generated support reproduces the logged CharaLinkUtil.GetLinkUnitBonusElement array exception in CharaOnce setup.");return;
   }
   throw new Exception("Legacy friend setup unexpectedly succeeded");
  }
  var offline=adapter.GetType("DivineGateOffline");var normalize=offline.GetMethod("NormalizeSupport400");
  // Preserve every primary-unit value except the three Link metadata fields.
  var packet=new PacketStructFriend{user_id=42,user_rank=3,friend_point=5,user_name="link-name",
   unit=new PacketStructUnit{id=4,level=3,unique_id=99,exp=20,add_pow=7,add_hp=11,limitbreak_lv=1,link_info=1,link_point=1000,link_unique_id=25},
   unit_link=new PacketStructUnit{id=8,level=5,add_hp=20}};
  var unit=packet.unit;var fields=typeof(PacketStructUnit).GetFields();
  var before=fields.ToDictionary(f=>f.Name,f=>f.GetValue(unit));
  Require(Object.ReferenceEquals(normalize.Invoke(null,new object[]{packet}),packet),"Support object replaced");
  Require(Object.ReferenceEquals(unit,packet.unit),"Primary unit replaced");
  foreach(var field in fields.Where(f=>!new[]{"link_info","link_point","link_unique_id"}.Contains(f.Name)))
   Require(Object.Equals(field.GetValue(unit),before[field.Name]),"Primary value changed: "+field.Name);
  Require(packet.user_id==42 && packet.user_rank==3 && packet.friend_point==5,"Friend metadata changed");
  Require(packet.unit_link.id==0 && unit.link_info==0 && unit.link_point==0 && unit.link_unique_id==0,"Link metadata retained");
  Require(packet.user_name==MasterDataUtil.GetCharaParamFromID(4).name,"Support name does not match primary unit");
  for(int i=0;i<64;i++) {
   var friend=server.CreateMobUser((uint)(20000+i),FRIEND_STATE.FRIEND_STATE_UNRELATED);
   Require(friend.unit_link!=null && friend.unit_link.id==0 && friend.unit.link_point==0,"Generated support retained a Link partner");
   var c=Setup(friend);var primary=MasterDataUtil.GetCharaParamFromID(friend.unit.id);
   Require(c.m_CharaHP==Math.Min(999999,CharaUtil.GetStatusValue(primary,(int)friend.unit.level,2)+(int)friend.unit.add_hp*10),"Support HP changed");
   Require(c.m_CharaPow==Math.Min(999999,CharaUtil.GetStatusValue(primary,(int)friend.unit.level,0)+(int)friend.unit.add_pow*5),"Support ATK changed");
   Require(c.m_LinkParam.m_CharaID==0,"Character setup retained Link ID");
  }
  int count=0;
  foreach(var character in server.m_MasterDataParamChara.Where(c=>c.fix_id!=0)) {
   foreach(int level in new[]{character.level_min,character.level_max}) {
    var c=new CharaOnce();
    Require(c.CharaSetupFromID(character.fix_id,level,0,0,3,5,0u,0,0,0,0,0),"Character setup failed "+character.fix_id);
    int hp=level>=character.level_max ? character.base_hp_max : character.base_hp_min;
    int atk=level>=character.level_max ? character.base_attack_max : character.base_attack_min;
    Require(c.m_CharaHP==Math.Min(999999,hp+50) && c.m_CharaPow==Math.Min(999999,atk+15),"Original base/+ stats changed "+character.fix_id);
    count++;
   }
   foreach(int bonus in new[]{0,2})Require(CharaLinkUtil.GetLinkUnitBonusElement(character,character.level_min,0,bonus)==0,"Missing element rules not neutral");
   foreach(int bonus in new[]{0,2,6,7})Require(CharaLinkUtil.GetLinkUnitBonusRace(character,character.kind,bonus)==0,"Missing race rules not neutral");
  }
  // When real Link rules exist the original generator is not normalized.
  master.m_MasterDataLinkSystem=new[]{new MasterDataLinkSystem()};
  packet.unit_link.id=8;packet.unit.link_point=100;
  normalize.Invoke(null,new object[]{packet});
  Require(packet.unit_link.id==8 && packet.unit.link_point==100,"Nonempty Link rules were ignored");
  Console.WriteLine("PASS: 64 native generated supports initialized through the friend-page CharaOnce overload; primary metadata retained; "+count+" original character endpoint setups and missing-Link bonus lookups passed; nonempty-rule normalization bypass retained.");
  Console.WriteLine("LIMIT: disposable Mono probe, native I/O stubbed and Mathf.Pow mapped to System.Math.Pow; not Android UI, tutorial clicks, or battle verification.");
 }
}
