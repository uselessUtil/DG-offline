# 原生 2.9.1／DGDB-1

以原始 jp.gungho.dg-291.apk（2.9.1、versionCode36）建立獨立套件 jp.gungho.dg.offline291。內容白名單取自 jp.gungho.dg，保留595角色及392有效關卡，主資料採鎖定DGDB。來源APK SHA-256：3832bed7dee0530ff821c141622bc518d335dcab9ee664caa321425490968136。

最新交付：artifacts/2.9.1-offline-dgdb1-r10-drop-level/ディバゲ_2.9.1.apk，221,874,767 bytes，SHA-256 a4710edf6b4c39e931bc61840bbf7f7268cc15e7c73613cbda75b8e416c2f1df。原生2.9.1傷害、狀態施放與隊長技條件判定方法未修改，不安裝舊格式技能掛鉤。

## 資料

dgdb.py鎖定SQL SHA-256 a507e0d519a70bab7617c7e37bd9f32b2a75894eb8bdbbffffa0c5b4b259d25f，遞迴匯入依賴並拒絕缺失引用。保留262進化，排除92條超出白名單路線；進化零號列是有效路線。

沿用已修復的原版隨機生成、CategoryPattern／ExpectPattern及一格鑰匙；DGDB沒有這兩張表。保留舊素材、音樂、顯示位置、活動開放與主線選單拓樸。缺少的顯示欄位和狀態clear_type採明確映射，見fallbacks.json。不新增Link或限界突破。

保留r17帳號下限、180秒回體、助戰與最愛優先、入手排序、卡池／商店、教學及放棄接續修正。data.py舊增傷轉換只建立比較基準，隨後由DGDB覆蓋；成品不保留舊倍率。

## 存檔

備份識別：2.9.1／schema2／dgdb-1。接受2.9.0 schema1與固定1.0.6 prototype22r7。技能等級保留數字，超過新上限才限制；調整及來源checksum記入migration_report。保留交易式回復、中斷恢復與原備份。2.9.0拒收新備份，回舊版使用原舊備份。

套件資料空間獨立，不自動讀其他套件私人資料。固定1.0.6目錄沿用offline/v290/playable/legacy106-catalog.json，不更動來源ID。

## 建置與驗證

全部工具在專案 Docker 開發容器執行，由專案根目錄使用具有 UnityPy 等相依套件的 Python 環境。下列命令是後續重現入口，本次提交整理不執行：

- offline/v291/playable/build.py --revision 11 --output artifacts/NEW-291-CANDIDATE
- offline/v291/playable/test.py artifacts/NEW-291-CANDIDATE/build-report.json
- offline/v291/playable/test_keys.py artifacts/NEW-291-CANDIDATE/build-report.json
- offline/v291/playable/test_layout.py artifacts/NEW-291-CANDIDATE/build-report.json
- offline/v291/playable/test_update_loop.py artifacts/NEW-291-CANDIDATE/build-report.json

不得覆蓋既有APK。--assets-from只可重用同原生APK、相同素材路由及已驗證的轉換結果。沿用離線簽章，私鑰不加入專案。

r4通過25組受管檢查、212,271欄位對SQL比對、40,320組原始1.0.6 IL生成對照、鑰匙與更新循環回歸。實際1.0.6／2.9.0 APK匯出到新版本的跨程序遷移、技能降限、失敗回復與舊版拒收均已檢查。Unity I/O替身僅存於容器暫存測試副本。

歷史r4曾在核准的Android模擬器安裝並送出啟動，關閉Android舊版應用提示後，依使用者要求停止操作。未確認Android首頁或新增頁面，未匯入玩家存檔、抽卡、領幣或開始關卡。觸控、動畫、文件選擇器與完整戰鬥交由使用者測試。

交付目錄REPORT.txt含完整報告；skill-changes.txt及skill-changes-effective.json比較實際r17／r4有效定義，區分格式、文字與數值差異；excluded-evolutions.json記錄排除路線。

## r8 掉落及常駐亂入

QuestRewards291.cs／PatchRewards291.cs修正萬分比掉落方向，依DGDB三項相對權重抽取關卡掉落加值，並保存於原生關卡封包。LegacyFloor291以固定樓層種子決定七星敵人格的亂入結果；不替換Boss，重登不重抽。

亂入依使用者要求移除歷史日期限制。79關、97組去重候選，14個衝突關維持原20%總機率後等分不同敵群。active、玩家分組及前置通關條件仍受檢查；沒有亂入的進化／一般關卡保留原清單。invasions.py輸出完整衝突及依賴稽核。34張主資料與r4相同，技能定義變更0筆。

r8通過27組受管測試及鑰匙、原106地圖生成、更新循環回歸。APK安裝與實機玩法由使用者自行測試，本次沒有使用ADB。完整報告與衝突清單在r8交付目錄。

## r9 減傷隊長技

dgdb.py將現有76角色共用的23筆DGDB type5減傷資料轉為原生skill_decline_dmg三欄；承傷百分比保持原值，技能鏈、名稱與說明不變。只有MasterDataSkillLeader改變，其他33表與r8相同；原生Assembly-CSharp.dll及素材皆相同。新增LeaderGuardTests291.cs直接呼叫原生減傷及敵人攻擊流程，涵蓋屬性、隊長／助戰、雙減傷疊加、實際HP損失與複合增傷鏈。

r9通過28組受管／資源檢查及鑰匙、原106地圖、更新循環回歸。DEX雖有二進位排序差異，實際r8／r9反組譯的6034個smali類別全部相同。成品目錄提供leader-guard-changes.txt、affected-characters.txt、r8-diff.json與REPORT.txt。未操作ADB或裝置，實機由使用者測試。

## r10 掉落角色等級

QuestRewards291.QuestDropLevel291從實際擊敗的敵人取得drop_unit_level，PatchRewards291只替換DeadEnemy原本的常數0等級初始化。重用既有取得紀錄／續關／結算欄位，不改封包或存檔型別。34張主資料與r9相同，減傷修復保留。

等級依掉落角色level_min..level_max限制；一筆Lv5キングフレアエッグ實際上限為1，另八筆0掉落率資料等級0以最低合法等級處理。drop-level-normalizations.json完整記錄，原SQL及主資料不改寫。既有入庫角色和更新前已收集的level0紀錄不回溯升級。

r10通過31組受管／資源檢查及鑰匙、地圖、更新循環回歸；新增2144筆實際敵人死亡、同角色不同來源等級、原生100格續關跨程序保存、一般／進化結算及發獎重登測試。r9實際DLL產生的進行中存檔交給r10讀取，SERVER_SAVE及關卡封包保持逐位元組相同。未使用ADB，實機由使用者測試。

## 2.9.1 年代內容還原候選 r12

獨立設定的身分、相依輸入與缺口見 [還原工程說明](../restored/README.md)；本頁頂端的最新交付仍指原dgdb-1的r10。

新增獨立套件 `jp.gungho.dg.offline291.restored`，資料版本 `dgdb-2-restored`、備份 schema3；r10保留原套件與成品。以指紋鎖定的Wiki年代稽核白名單擴至1748角色、921有效關卡、717進化，完整引用閉包使用鎖定DGDB；既有角色、敵人、關卡和技能列不改寫。排除3個NS ability不支援角色及3個後期Link固定隊伍關卡，另76個依賴不完整候選未開放。

`../restored/content.py`建立保守擴充資料；`assets.py`保留所有595角色原生模型路由和原圖，只為缺模型的角色補PNG。缺地圖模型時使用相容共用模型／原門動畫；`Presentation291.cs`只在原生資源不可用的fallback分支，以分離門片執行四態動畫。Dgdata地圖PNG的上下方向與native快取相反，`map_layout.py`轉正後套原UV；用RGB排除不透明黑色空白區，只有簡化切片將黑色padding轉透明。

新增2062筆敵人來源的角色掉落率全為0；本候選保留來源值，沒有猜測補率。七個原卡池及舊79關常駐亂入維持原集合，沒有替新增角色建立推測機率。1153新增角色中697個沒有目前的掉落／卡池／通關／進化目標來源定義；其餘也須逐項確認前置可取得性。這是資料與可執行引用的還原候選，不是2015官方完整平衡快照。

容器內重現（必須另備已驗證的r10素材快取、原APK、鎖定SQL／Wiki稽核及dgdata）：

```sh
python offline/v291/playable/build.py --revision 12 --restore-content --output artifacts/2.9.1-restored-r12 --assets-from artifacts/2.9.1-offline-dgdb1-r10-drop-level/build-report.json
python offline/v291/restored/test.py artifacts/2.9.1-restored-r12/build-report.json
python offline/v291/restored/verify_more.py artifacts/2.9.1-restored-r12/build-report.json
python offline/v291/restored/report.py artifacts/2.9.1-restored-r12/build-report.json
```

測試將固定舊白名單／schema的斷言移至容器暫存副本，僅調整有明確來源的擴充數量／套件／格式期望；舊回歸主體不省略。補強複合狀態測試，逐一計算四個狀態槽的原生乘算及最長有效期。實際r10組件匯出、r12匯入、新角色與新通關旗標跨程序及備份再匯入、舊版拒絕schema3均有獨立測試。

完整差異、不可取得角色、技能新增、門／素材fallback、來源缺口及驗證範圍見交付目錄REPORT.txt和對應JSON。r11是地圖方向／分離判定修正前的中間候選，應使用r12。沒有使用ADB或改寫玩家存檔；Android啟動、記憶體、UV與動畫仍由使用者實機測試。

## 來源與重現限制

原創配接器、工具及文件依 [MIT 授權](../../../LICENSE)；遊戲、DGDB 與聯動內容的權利見 [第三方權利聲明](../../../THIRD_PARTY_NOTICES.md)。原APK、SQL、快取、素材、報告原文及私鑰均不隨 Git checkout 提供。DGDB SQL 的精確輸入身分由 dgdb.py 核對，不能改 hash 繞過失配。

本工程由 [2.9.0 r17](../../v290/playable/README.md) 配接，build.py 明確要求共用 Runtime 的22r7標記。來源中的工作目錄、工具位置與簽署設定尚未全面參數化，須先核對容器設定；只取得 Git 來源不能直接重建。--revision只調整候選標記，不會恢復對應歷史實作；目前累積來源包含r10修正。

r4／r8／r9／r10 的通過結果均引自既有報告。先前文件整理僅核對語法、格式、連結、公開資訊及來源指紋；r12另有既有容器建置與回歸報告。本次提交整理僅做靜態核對，未編譯、執行回歸、安裝或操作Android；實機驗證仍由使用者進行。
