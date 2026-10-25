"""Write reproducible differences, source gaps and handoff notes for restored APK."""
import argparse,collections,json,hashlib
from pathlib import Path
ROOT=Path('/workspace')
def main():
 p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args()
 out=a.report.parent;r=json.loads(a.report.read_text())
 baseline=ROOT/'artifacts/2.9.1-offline-dgdb1-r10-drop-level'
 old=json.loads((baseline/'build-report.json').read_text())
 def load(m):return {f.stem:json.loads(f.read_text()) for f in (Path(m['work_directory'])/'tables').glob('*.json')}
 x,y=load(old),load(r);meta=json.loads((out/'restoration.json').read_text())
 diffs={}
 for table,rs in y.items():
  if not rs or not isinstance(rs[0],dict) or 'fix_id' not in rs[0]:continue
  before={v['fix_id']:v for v in x.get(table,[]) if v};after={v['fix_id']:v for v in rs if v}
  diffs[table]={'before':len(before),'after':len(after),'added_ids':sorted(set(after)-set(before)),
     'removed_ids':sorted(set(before)-set(after)),'modified':[
       {'id':i,'fields':{k:{'before':before[i].get(k),'after':after[i].get(k)} for k in set(before[i])|set(after[i]) if before[i].get(k)!=after[i].get(k)}}
       for i in sorted(set(before)&set(after)) if before[i]!=after[i]]}
 for n in ['MasterDataParamChara','MasterDataQuest','MasterDataQuestFloor','MasterDataEnemyChara',
           'MasterDataParamCharaEvol','MasterDataSkillActive','MasterDataSkillLimitBreak','MasterDataSkillLeader',
           'MasterDataSkillPassive','MasterDataSkillBoost','MasterDataStatusAilment']:
  assert not diffs[n]['removed_ids'] and not diffs[n]['modified'],n
 (out/'r10-data-differences.json').write_text(json.dumps(diffs,ensure_ascii=False,indent=2))
 chars={c['fix_id']:c for c in y['MasterDataParamChara'] if c['fix_id']}
 skills={}
 types={'MasterDataSkillActive':['skill_active0','skill_active1'],'MasterDataSkillLimitBreak':['skill_limitbreak'],
        'MasterDataSkillLeader':['skill_leader'],'MasterDataSkillPassive':['skill_passive'],
        'MasterDataSkillBoost':[],'MasterDataStatusAilment':[]}
 for table,fields in types.items():
  ids=set(diffs[table]['added_ids'])
  skills[table]={'existing_modified':[],'added':[dict(v,owners=[{'id':c['fix_id'],'name':c['name']} for c in chars.values() if any(c.get(k)==v['fix_id'] for k in fields)]) for v in y[table] if v['fix_id'] in ids]}
 (out/'skill-additions.json').write_text(json.dumps(skills,ensure_ascii=False,indent=2))
 old_enemy_ids={v['fix_id'] for v in x['MasterDataEnemyChara']}
 enemies=[v for v in y['MasterDataEnemyChara'] if v['fix_id'] not in old_enemy_ids]
 assert len(enemies)==2062 and all(e['drop_unit_rate']==0 for e in enemies)
 drop={v['drop_unit_id'] for v in y['MasterDataEnemyChara'] if v['drop_unit_rate']>0}
 clear={v['clear_unit'] for v in y['MasterDataQuest'] if v['fix_id'] and v['active']==2 and v['clear_unit']}
 gacha={v['assign_chara_id'] for v in y['MasterDataGachaAssign'] if v['active']==2 and v['assign_rate']>0}
 evos={v['unit_id_after'] for v in y['MasterDataParamCharaEvol'] if v['active']==2}
 availability=[]
 for i in meta['new_character_ids']:
  sources=[]
  if i in drop:sources.append('enemy-positive-rate')
  if i in clear:sources.append('quest-clear')
  if i in gacha:sources.append('existing-gacha')
  if i in evos:sources.append('evolution-target')
  availability.append({'id':i,'name':chars[i]['name'],'source_definitions':sources,
    'playable_acquisition_verified':False})
 (out/'new-character-acquisition.json').write_text(json.dumps({
   'warning':'Definitions only; an evolution target is not proof its base/materials are currently obtainable. Existing seven pools retained; no speculative new pool weights or enemy drop chances.',
   'new_enemies_with_zero_source_drop_rate':enemies,'characters':availability},ensure_ascii=False,indent=2))
 areas=[v for v in meta['fallbacks'] if v['table']=='MasterDataArea' and 'mesh_template' in v]
 (out/'map-fallbacks.json').write_text(json.dumps(areas,ensure_ascii=False,indent=2))
 lines=['未收錄角色（原生2.9.1沒有對應NS ability判定）']+[f"{v['id']} / {v['draw_id'] if 'draw_id' in v else ''} {v['name']}" for v in meta['excluded_characters']]
 lines+=['','未收錄關卡候選（保留原有392關，沒有刪除舊關卡）']
 for v in meta['excluded_quest_candidates']:lines.append(f"{v['id']} {v.get('name','')} : {v.get('restoration_reason','引用鏈缺失或名單外依賴')} {v.get('unsupported',v['dependencies']['issues'])}")
 (out/'unsupported-content.txt').write_text('\n'.join(lines)+'\n')
 extra=json.loads((out/'expanded-extra-tests.json').read_text())
 for fn in ['key-floor-regression.txt','layout-regression.txt','update-loop-regression.txt','test-report.json']:
  assert (out/fn).exists(),fn
 assert extra['apk_sha256']==r['sha256']
 assert hashlib.sha256(Path(r['output']).read_bytes()).hexdigest()==r['sha256']
 summary={
 'apk':r['output'],'sha256':r['sha256'],'size_bytes':r['size'],'package':r['package'],
 'counts':{'characters':1748,'active_quests':921,'evolutions':717,'new_characters':1153,'new_quests':529,
           'new_enemies':2062,'new_enemy_drop_rates_missing_or_zero':2062},
 'native_models_preserved':595,'new_area_exact_family':sum(v['native_source_exists'] for v in areas),
 'new_area_substitute_geometry':sum(not v['native_source_exists'] for v in areas),
 'new_characters_without_any_current_source_definition':sum(not v['source_definitions'] for v in availability),
 'new_characters_with_evolution_target':sum('evolution-target' in v['source_definitions'] for v in availability),
 'new_characters_with_quest_clear_reward':sum('quest-clear' in v['source_definitions'] for v in availability),
 'existing_combat_skill_rows_modified':0,'android_tested':False}
 (out/'restored-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2))
 report=f"""2.9.1 DGDB 還原候選 {r['version']}（獨立套件；實機由使用者測試）

APK：{Path(r['output']).name}
套件：{r['package']}
原生版本：2.9.1 / versionCode36
大小：{r['size']:,} bytes（約{r['size']/1048576:.1f} MiB）
SHA-256：{r['sha256']}

收錄與範圍
角色1748（比r10增加1153）；有效關卡921（增加529）；進化717（增加455）。
保留r10原有595角色、392有效關卡、7預留停用關卡，以及原有ID／主線順序。
原有角色、敵人、關卡、樓層、進化及技能資料列均未改寫；新增引用閉包採DGDB。
AS新增421、NS新增1276、LS新增514、PS新增202、Boost新增49、狀態新增309。
新增減傷隊長技沿用r9原生欄位轉換；目前25筆、109直接持有者。原有23筆／76角色修復保留。
完整新增技能名稱、參數、持有者見skill-additions.json；與r10逐欄差異見r10-data-differences.json。
角色與關卡以本機Wiki稽核2015-12-09截止候選集為界；Wiki沒有完整歷史快照。
數值是2017 DGDB資料，不能宣稱與2015年官方2.9.1當時平衡完全相同。

原生模型優先與門貼圖
原595角色模型／動畫的全部資源路由與native bundle逐位元組與r10相同。
原1190立繪／剪影PNG payload及829個原圖示像素保持一致；1153新增角色缺模型才使用對應draw_id的PNG。
新區域中{summary['new_area_exact_family']}個沿用已恢復的同貼圖模型家族；
{summary['new_area_substitute_geometry']}個缺原始模型，使用相容共用地圖／門模型及其原動畫。
原生路由找不到資源或載入失敗時，才進入簡化貼圖fallback。
map_0012_tex的普通門片、Boss門片與背景可分開；簡化fallback有四個開閉狀態及0.3秒門片動畫，
仍由原DoorOpen／AnimationArray流程控制，不加碰撞器、不略過鑰匙。
Dgdata地圖PNG上下方向與native快取相反，外部PNG先垂直轉正再套原UV。
空白區為不透明黑色，分離判定同時檢查RGB；只有簡化切片會將全黑padding轉透明。
有模型的map_0012等保留原模型／動畫，不套用簡化門動畫。
素材逐項來源、雜湊與轉換見images.json、native-bundle-manifest.json、map-fallbacks.json。

明確缺口與fallback
1. 新增2062筆敵人的DGDB drop_unit_rate全為0，且多數drop_unit_level為0。
   沒有猜測補入掉落機率；原有掉落方向、加值權重、等級合法化修復仍保留。
   1153新增角色中，0個有正機率敵人掉落來源、0個加入現有卡池、1個有通關獎勵定義、
   455個為進化目標；來源可重疊。697個沒有上述任何取得來源定義。
   進化目標不代表前置角色／素材已可取得；詳細見new-character-acquisition.json。
2. 七個舊卡池及其既有機率不變；沒有為新增聯動／限定角色發明機率。
   新增關卡使用DGDB一般敵人／Boss；亂入仍是舊79關／97候選組，沒有擴入新亂入排程。
3. 新增603樓層缺少DGDB生成樣式表：改用有對應敵人／陷阱／道具／回復群組的舊樣式。
   舊五欄保留、cate5=1、合計25格；原1.0.6隨機演算法保留。不是官方歷史精確格子頻率。
4. 新角色缺模型／動畫以PNG顯示；普通技視覺使用相同屬性／類型的既有特效。
   新敵人行動若無原特效，保留正確傷害／狀態參數並停用缺少的視覺特效／震動。
5. 新區域無可核對的原音訊時沿用共用／resident fallback；既有音樂對應保留。
   無法恢復的區域前置共3項以0開放，歷史活動日期門檻對新區域移除，完整列restoration.json。
6. 排除3角色：1158 百家本流千葉の剣士：エリカ、1182 天童家の剣鬼：木更、
   1473 クロウリー：サマー，原因是原生2.9.1沒有相應普通技ability判定。
   另排除3個含後期Link固定隊伍限制的關卡1633／1635／1750；
   還有76個Wiki候選因缺失／超範圍引用未匯入，見unsupported-content.txt。
7. 額外系統Link、限界突破、營運、付款服務維持停用。

存檔
與r10並存，不共用Android私有存檔。可由遊戲內匯入r10的schema2/dgdb-1備份、
2.9.0備份及固定1.0.6 prototype22r7備份。
新匯出：2.9.1 / schema3 / dgdb-2-restored；實際r10匯入器已驗證會拒絕。
只接受沒有進行中關卡的備份；原備份保持不變。備份交易式寫入、失敗回復和放棄接續修復保留。
原生DTO、角色唯一識別、貨幣、加值、最愛、時間、隊伍和通關旗標格式不變。
實際r10組件建立595角色帳號匯入，逐欄相同；新增角色237／通關529經跨程序及schema3再匯入保持一致。
原生flag256bytes可涵蓋目前最大角色ID／關卡ID，未擴大舊旗標。

驗證與待實機
全部工具於Docker執行。31組受管／資源回歸、528176個DGDB欄位比對、674個AS、
327傷害案例、119狀態生命週期、6664屬性／種族案例、25筆減傷與實際敵人攻擊均通過。
918個非教學關卡／1363樓層鑰匙驗證通過；3個教學保留原鑰匙。
40320種樣式與原1.0.6 IL oracle相同，10種種子共9180次關卡／13630樓層與跨程序保存通過。
更新循環、教學跳過、進化出發、確認窗、排序、助戰、商店、舊版匯入與故障回復回歸通過。
r10到本版的7619個原生遊戲方法全部相同；只有配接器／資料／fallback資源擴充。
原始UnityEngine.dll、原生程式及既有native bundle未修改；曲線測試只在/tmp測試引擎觀察呼叫參數。
APK ZIP完整性及簽章v1/v2/v3通過；APK沒有INTERNET／付款權限。
沒有安裝、ADB操作或改寫玩家存檔。實機仍待驗證啟動／記憶體、原生新貼圖UV、
新增角色顯示、門動畫與圖層、音訊、選單開放／前置條件及新關卡戰鬥。

鎖定來源
原APK SHA-256：{r['source_sha256']}
DGDB SQL SHA-256：a507e0d519a70bab7617c7e37bd9f32b2a75894eb8bdbbffffa0c5b4b259d25f
保留r10 SHA-256：{old['sha256']}
Wiki選集指紋、所有fallback及排除引用見restoration.json、dgdb-projection.json。
r11為未交付中間候選，方向／黑色padding判定錯誤已由r12修正，請使用r12。
"""
 (out/'REPORT.txt').write_text(report)
 r['restored_extra_tests_passed']=True;r['android_tested']=False
 r['status']='Signed restored candidate; managed/data/asset/save/door-curve regressions passed; Android testing delegated to user'
 a.report.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(summary,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
