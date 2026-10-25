"""Generate version-checked 2.9.0 variants of the maintained 1.0.6 backup feature."""
from pathlib import Path
import hashlib,json
ROOT=Path('/workspace')
FILTER_KEYS=['PARTY_FORM','BUILD_UNIT','BUILD_FRIEND','EVOLVE_UNIT','EVOLVE_FRIEND',
             'UNIT_SALE','UNIT_LIST','FRIEND_LIST','FRIEND_WAIT_ME','FRIEND_WAIT_HIM','QUEST_FRIEND']
def legacy_catalog(build):
    catalog=json.loads((ROOT/'offline/v290/playable/legacy106-catalog.json').read_text())
    tables=build.parent/'tables'
    chars={r['fix_id']:r for r in json.loads((tables/'MasterDataParamChara.json').read_text(),strict=False)}
    quests={r['fix_id']:r for r in json.loads((tables/'MasterDataQuest.json').read_text(),strict=False)}
    pairs=[]
    for old in catalog['characters']:
        assert all(chars[old['fix_id']][k]==old[k] for k in ['name','level_max']),old
    for old in catalog['quests']:
        matches=[r for r in quests.values() if r['area_id']==old['area_id'] and r['quest_name']==old['quest_name']]
        if old['fix_id'] in quests and quests[old['fix_id']] in matches:target=old['fix_id']
        else:
            assert len(matches)==1,old
            target=matches[0]['fix_id']
        pairs.append((old['fix_id'],target))
    lines=['using System.Collections.Generic;','public static partial class DivineGateOffline {']
    for name,rows in [('Characters',[(r['fix_id'],r['level_max']) for r in catalog['characters']]),('Quests',pairs)]:
        lines.append('private static readonly Dictionary<uint,uint> Legacy106'+name+'=new Dictionary<uint,uint>{'+
                     ','.join('{'+str(a)+'u,'+str(b)+'u}' for a,b in rows)+'};')
    lines.append('}')
    (build/'Legacy106Catalog.cs').write_text(chr(10).join(lines))
    return {'source_prototype':catalog['prototype'],'source_apk_sha256':catalog['apk_sha256'],
            'characters_checked':len(catalog['characters']),'quests_checked':len(pairs),
            'quest_id_remaps':{str(a):b for a,b in pairs if a!=b}}

def create(build):
    src=ROOT/'offline'
    core=(src/'SaveBackup.cs').read_text()
    assert core.count('"1.0.6"')==2
    core=core.replace('"1.0.6"','"2.9.0"')
    # The base setting order is kept so the legacy suite can still inspect it.
    core=core.replace('"divinegate_OPTION" };','"divinegate_OPTION", '+','.join('"divinegate_FILTER_'+key+'"' for key in FILTER_KEYS)+' };')
    core=core.replace('"divinegate_TUTORIAL_GROUP","divinegate_INFORMATION_2nd"', '"divinegate_TUTORIAL_SKIP","divinegate_TUTORIAL_GROUP","divinegate_INFORMATION_2nd"')
    core=core.replace('"divinegate_INGAME_CONTINUE","divinegate_INGAME_RESET","divinegate_SCRATCH" };',
        '"divinegate_INGAME_CONTINUE","divinegate_INGAME_RESET","divinegate_SCRATCH", "divinegate_RESULT_VER","divinegate_LOGIN_CALENDAR","divinegate_ACHIEVEMENT_ENSYUTSU","divinegate_RENEW_UUID" };')
    # Validate the actual new version's JSON filter object, rather than accepting
    # arbitrary strings just because their keys are on the whitelist.
    needle='            if(pref.key=="divinegate_OPTION" &&'
    assert needle in core
    core=core.replace(needle,'''            if(pref.key.StartsWith("divinegate_FILTER_")) {
                ValidateIntegralSaveNumbers(pref.text);
                if(JsonMapper.ToObject<LocalSaveFilter>(pref.text)==null)
                    throw new InvalidOperationException("Invalid unit filter settings.");
            }
'''+needle)
    needle='e.game_version!="2.9.0"'
    assert core.count(needle)==1
    core=core.replace(needle,'(e.game_version!="2.9.0" && e.game_version!="1.0.6")')
    needle='        ValidateIntegralSaveNumbers(p.server_save);'
    assert core.count(needle)==1
    core=core.replace(needle,'        if(e.game_version=="1.0.6")p=ConvertLegacy106(e,p);'+chr(10)+needle)
    migration=legacy_catalog(build)
    ui=(src/'SaveTransferUI.cs').read_text()
    assert ui.count('jp.gungho.dg.offline106.SaveTransferActivity')==1
    ui=ui.replace('jp.gungho.dg.offline106.SaveTransferActivity','jp.gungho.dg.offline290.SaveTransferActivity')
    ui=ui.replace('Choose Export or Import. Backups include units, parties, progress and protected-unit settings.',
                  'Import accepts 2.9.0 and fixed 1.0.6 prototype 22r7 backups. Export creates a 2.9.0 backup.')
    ui=ui.replace('ButtonTransferOrder','ButtonTransferBackup').replace('ButtonTransferExec','ButtonTransferInfomation')
    ui=ui.replace('UnityUtil.SetUILabelValue(UnityUtil.GetChildNode(root,"PartsTitle"),"Save Backup");',
        'foreach(UILabel label in UnityUtil.GetChildNode(root,"PartsTitle").GetComponentsInChildren<UILabel>(true))label.text="Save Backup";')
    ui=ui.replace('UnityUtil.GetChildNode(root,"PartsDetail"),','UnityUtil.GetChildNode(root,"PartsDetail","Detail"),')
    (build/'SaveBackup290.cs').write_text(core);(build/'SaveTransferUI290.cs').write_text(ui)
    return {'source_commits':['019edde','64a347d','ce08e42','c600717'],
            'game_version':'2.9.0','schema':1,'format':'dg-offline-save','byte_limit':4194304,
            'entry':'Others > Save Backup','foreign_versions_rejected':True,'legacy_106_import':migration,
            'additional_preferences':['divinegate_TUTORIAL_SKIP']+['divinegate_FILTER_'+k for k in FILTER_KEYS],
            'source_sha256':{n:hashlib.sha256((src/n).read_bytes()).hexdigest() for n in ['SaveBackup.cs','SaveTransferUI.cs']}}
