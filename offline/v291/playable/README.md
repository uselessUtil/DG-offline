# 原生 2.9.1／DGDB-1

以原始 jp.gungho.dg-291.apk（2.9.1、versionCode36）建立獨立套件 jp.gungho.dg.offline291。內容白名單取自 jp.gungho.dg，保留595角色及392有效關卡，主資料採鎖定DGDB。來源APK SHA-256：3832bed7dee0530ff821c141622bc518d335dcab9ee664caa321425490968136。

最新交付：artifacts/2.9.1-offline-dgdb1-r8-rewards-permanent/ディバゲ_2.9.1.apk，221,874,767 bytes，SHA-256 736cf6783a269d9e164efce81dd9226c9dfbd95b8df0a6bb0684848601ede5b9。原生2.9.1傷害、狀態施放與隊長技條件判定方法未修改，不安裝舊格式技能掛鉤。

## 資料

dgdb.py鎖定SQL SHA-256 a507e0d519a70bab7617c7e37bd9f32b2a75894eb8bdbbffffa0c5b4b259d25f，遞迴匯入依賴並拒絕缺失引用。保留262進化，排除92條超出白名單路線；進化零號列是有效路線。

沿用已修復的原版隨機生成、CategoryPattern／ExpectPattern及一格鑰匙；DGDB沒有這兩張表。保留舊素材、音樂、顯示位置、活動開放與主線選單拓樸。缺少的顯示欄位和狀態clear_type採明確映射，見fallbacks.json。不新增Link或限界突破。

保留r17帳號下限、180秒回體、助戰與最愛優先、入手排序、卡池／商店、教學及放棄接續修正。data.py舊增傷轉換只建立比較基準，隨後由DGDB覆蓋；成品不保留舊倍率。

## 存檔

備份識別：2.9.1／schema2／dgdb-1。接受2.9.0 schema1與固定1.0.6 prototype22r7。技能等級保留數字，超過新上限才限制；調整及來源checksum記入migration_report。保留交易式回復、中斷恢復與原備份。2.9.0拒收新備份，回舊版使用原舊備份。

套件資料空間獨立，不自動讀其他套件私人資料。固定1.0.6目錄沿用offline/v290/playable/legacy106-catalog.json，不更動來源ID。

## 建置與驗證

全部工具在專案 Docker 開發容器執行，由專案根目錄使用具有 UnityPy 等相依套件的 Python 環境。下列命令是後續重現入口，本次提交整理不執行：

- offline/v291/playable/build.py --revision 9 --output artifacts/NEW-291-CANDIDATE
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

## 來源與重現限制

原創配接器、工具及文件依 [MIT 授權](../../../LICENSE)；遊戲、DGDB 與聯動內容的權利見 [第三方權利聲明](../../../THIRD_PARTY_NOTICES.md)。原APK、SQL、快取、素材、報告原文及私鑰均不隨 Git checkout 提供。DGDB SQL 的精確輸入身分由 dgdb.py 核對，不能改 hash 繞過失配。

本工程由 [2.9.0 r17](../../v290/playable/README.md) 配接，build.py 明確要求共用 Runtime 的22r7標記。來源中的工作目錄、工具位置與簽署設定尚未全面參數化，須先核對容器設定；只取得 Git 來源不能直接重建。--revision只調整候選標記，不會恢復對應歷史實作；目前累積來源包含r8修正。

r4／r8 的通過結果均引自既有報告。本次整理僅核對語法、格式、連結、公開資訊及來源指紋，未編譯、執行回歸、安裝或操作Android；r8實機仍待驗證。
