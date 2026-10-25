// One-way conversion of the fixed 1.0.6 prototype 22r7 backup protocol.
// The ordinary 2.9 validator and transactional importer run after conversion.
using System;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using ServerDataDefine;

public static partial class DivineGateOffline
{
    private static void LegacyShape106(JsonData value, Type type)
    {
        if(value==null) {
            if(type.IsValueType)throw new InvalidOperationException("Missing legacy value.");
            return;
        }
        if(type==typeof(string)) {if(!value.IsString)throw new InvalidOperationException("Invalid legacy text.");return;}
        if(type==typeof(bool)) {if(!value.IsBoolean)throw new InvalidOperationException("Invalid legacy switch.");return;}
        if(type.IsPrimitive) {
            long n;if(!long.TryParse(value.ToJson(),out n))throw new InvalidOperationException("Invalid legacy number.");
            if((type==typeof(uint) && (n<0 || n>uint.MaxValue)) ||
               (type==typeof(byte) && (n<0 || n>255)) ||
               (type==typeof(int) && (n<int.MinValue || n>int.MaxValue)) ||
               (type==typeof(ulong) && n<0))throw new InvalidOperationException("Legacy numeric value is out of range.");
            return;
        }
        if(type.IsArray) {
            if(!value.IsArray || value.Count>20000)throw new InvalidOperationException("Invalid legacy array.");
            for(int i=0;i<value.Count;i++)LegacyShape106(value[i],type.GetElementType());
            return;
        }
        FieldInfo[] fields=type.GetFields(BindingFlags.Public|BindingFlags.Instance);
        if(!value.IsObject || value.Count!=fields.Length)throw new InvalidOperationException("Unsupported legacy save fields.");
        foreach(FieldInfo field in fields)LegacyShape106(value[field.Name],field.FieldType);
    }
    private static bool LegacyPreference106(string key)
    {
        return key!="divinegate_TUTORIAL_SKIP" && !key.StartsWith("divinegate_FILTER_") &&
            key!="divinegate_RESULT_VER" && key!="divinegate_LOGIN_CALENDAR" &&
            key!="divinegate_ACHIEVEMENT_ENSYUTSU" && key!="divinegate_RENEW_UUID";
    }
    private static byte[] LegacyFlags106(byte[] flags,bool quests)
    {
        if(flags==null)return null;
        if(flags.Length>20000)throw new InvalidOperationException("Legacy flags too large.");
        byte[] mapped=new byte[flags.Length];
        for(int i=0;i<flags.Length*8;i++) {
            if((flags[i/8] & (1<<(i%8)))==0)continue;
            uint target;
            if(quests) {
                if(!Legacy106Quests.TryGetValue((uint)i,out target))
                    throw new InvalidOperationException("Unsupported legacy quest record: "+i);
            } else {
                if(!Legacy106Characters.ContainsKey((uint)i))
                    throw new InvalidOperationException("Unsupported legacy character record: "+i);
                target=(uint)i;
            }
            ServerDataUtil.AddBitFlag(ref mapped,target);
        }
        return mapped;
    }
    private static DGSavePayload ConvertLegacy106(DGSaveEnvelope envelope,DGSavePayload payload)
    {
        if(envelope.prototype!="1.0.6 offline prototype 22r7")
            throw new InvalidOperationException("Only the fixed 1.0.6 prototype 22r7 backup is supported.");
        ValidateIntegralSaveNumbers(payload.server_save);
        LegacyShape106(JsonMapper.ToObject(payload.server_save),typeof(DGSave106Account));
        DGSave106Account old=JsonMapper.ToObject<DGSave106Account>(payload.server_save);
        if(old==null || old.quest_id!=0 || old.evolve_quest_active)
            throw new InvalidOperationException("Finish the 1.0.6 quest before exporting.");
        // PacketStructUnit and PartyAssign fields are identical in both APKs.
        ServerForgerySaveUser native=JsonMapper.ToObject<ServerForgerySaveUser>(payload.server_save);
        if(old.unit_list!=null)foreach(var unit in old.unit_list)
            if(unit==null || !Legacy106Characters.ContainsKey(unit.id))
                throw new InvalidOperationException("Unsupported 1.0.6 unit.");
        native.flag_quest_check=LegacyFlags106(old.flag_quest_check,true);
        native.flag_quest_clear=LegacyFlags106(old.flag_quest_clear,true);
        native.flag_unit_check=LegacyFlags106(old.flag_unit_check,false);
        native.flag_unit_get=LegacyFlags106(old.flag_unit_get,false);
        var serials=new Dictionary<long,bool>();
        if(old.present_list!=null)foreach(var gift in old.present_list) {
            if(gift==null || gift.serial_id<=0 || serials.ContainsKey(gift.serial_id) || gift.send_timing>uint.MaxValue ||
               (gift.present_type!=1 && gift.present_type!=2 && gift.present_type!=3 &&
                gift.present_type!=5 && gift.present_type!=6 && gift.present_type!=7))
                throw new InvalidOperationException("Unsupported 1.0.6 gift.");
            serials.Add(gift.serial_id,true);
            uint max;
            if(gift.present_type==7 && (!Legacy106Characters.TryGetValue(gift.present_value0,out max) ||
                gift.present_value1<1 || gift.present_value1>max))
                throw new InvalidOperationException("Unknown unit or level in 1.0.6 gift.");
        }
        // New features have no legacy counterpart. Never inherit the current
        // 2.9 account's ticket balance, achievements or pending login rewards.
        native.have_ticket=0;native.shop_product_event=0;native.quest_build=null;
        native.monthly_present=null;
        native.event_purchase_list=new TemplateList<EventProductPurchace>();
        native.achievement_list_clear=new TemplateList<uint>();
        native.achievement_list_ensyutsu=new TemplateList<uint>();
        native.achievement_list_acquired=new TemplateList<uint>();
        native.player_login_list=new TemplateList<uint>();

        var expected=new Dictionary<string,int>();
        foreach(string key in saveStringKeys)if(LegacyPreference106(key))expected.Add(key,1);
        foreach(string key in saveIntKeys)if(LegacyPreference106(key))expected.Add(key,0);
        foreach(string key in saveTransientKeys)if(LegacyPreference106(key))expected.Add(key,1);
        if(payload.preferences==null || payload.preferences.Length!=expected.Count)
            throw new InvalidOperationException("Incomplete 1.0.6 settings.");
        var converted=new List<DGSavePreference>();
        foreach(var pref in payload.preferences) {
            int kind;
            if(pref==null || pref.key==null || pref.text==null ||
               !expected.TryGetValue(pref.key,out kind) || kind!=pref.kind)
                throw new InvalidOperationException("Unsupported 1.0.6 preference.");
            expected.Remove(pref.key);
            if(pref.key=="divinegate_OPTION" && pref.present && pref.text.Length>0) {
                ValidateIntegralSaveNumbers(pref.text);
                LegacyShape106(JsonMapper.ToObject(pref.text),typeof(DGSave106Option));
                pref.text=JsonMapper.ToJson(JsonMapper.ToObject<LocalSaveOption>(pref.text));
            }
            // 2.9's restore gate requires its own token. The source token is
            // not an account progress field and must not be copied verbatim.
            if(pref.key=="divinegate_RESTORE_VER") {pref.present=true;pref.text="240after";}
            if(pref.key=="divinegate_SAVE_VERSION") {pref.present=false;pref.text="";}
            converted.Add(pref);
        }
        foreach(string key in saveStringKeys)if(!LegacyPreference106(key))
            converted.Add(new DGSavePreference{key=key,kind=1,text="",present=false});
        foreach(string key in saveIntKeys)if(!LegacyPreference106(key))
            converted.Add(new DGSavePreference{key=key,kind=0,text="",present=false});
        foreach(string key in saveTransientKeys)if(!LegacyPreference106(key))
            converted.Add(new DGSavePreference{key=key,kind=1,text="",present=false});
        return new DGSavePayload{server_save=JsonMapper.ToJson(native),preferences=converted.ToArray()};
    }
}
public sealed class DGSave106Option
{
    public int m_OptionBGM,m_OptionSE,m_OptionVoice,m_OptionGuide;
}
