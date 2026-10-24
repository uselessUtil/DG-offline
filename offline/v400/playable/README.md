# 4.0.0 單機化原型：1.0.6 資料

此目錄是自行撰寫的復原、資料轉接與封裝工具，不是原遊戲原始碼。
目前候選產物（不隨 Git 提供）為 `artifacts/4.0.0-offline106-r10/ディバゲ_4.0.0.apk`。
套件名稱為 `jp.gungho.dg.offline400`，可與原版及 offline106、offline290 並存。

大小：162,879,011 bytes；SHA-256：`e57482514797df256b617f0046596bce6ef9ac535ce2587ef172b4b1b6d758ed`。身分見該目錄的 build-report.json、SHA256SUMS；原始報告及產物需另備。原創工具採專案 [MIT 授權](../../../LICENSE)，遊戲／第三方素材範圍見 [權利聲明](../../../THIRD_PARTY_NOTICES.md)。

## 已完成與既有驗證範圍

- 復原 4.0.0 的兩個受保護 managed DLL，接入其本機 ServerForgery 流程。
- 將 Java 整數常數解碼器改寫為等價整數運算，使已復原 DLL 不再經原生保護器啟動。
- 資料取自原始 1.0.6 APK：522 個非零角色 ID、371 筆關卡資料；7 個來源抽卡池保留。
- `jp.gungho.dg` 舊快取僅提供圖像及地圖幾何／動畫，不用其較晚版本的角色數值或關卡資料。
- 轉接舊技能與資料欄位；缺少的新版資料表設為空表，部分對應入口停用。
- 移除 INTERNET、付費及若干線上服務元件；保留原生引擎與 UnityEngine.dll。
- r4 曾在 MuMu 12（Android 12 / API 32）成功啟動、建立本機帳號、選角並進入教學地圖。
- r5 修正選角預覽 ID 914/915/916 為 1.0.6 的 4/8/12，補上 44 個選單文字。
- r6 將舊快取的地圖與門網格、UV、節點位置、四個動畫槽與音效事件重建成 Unity 5 物件，取代錯誤的整張貼圖矩形；保留原場景碰撞、相機及開門流程。
- r6 新增 131 個補寫文字，包括技能發動、冷卻回合、技能／Link 頁籤、戰鬥選單、主畫面數值與常用介面標題。這些不是官方原文復原。
- 既有 r6 結構檢查報告記錄比對 50 個資源、4,140 個頂點、1,378 條動畫曲線與 73 個事件；另檢查必要文字鍵與格式參數。結果見輸出目錄的 repair-checks.json。
- r7 修正 `PARTY_NUM_RATE`：常駐文字 `Party {0} / {1}` 需要兩個參數，但 4.0.0 編隊／出戰好友頁只傳入一個序數。原本的 FormatException 中斷頁面切換，造成 UNITS 空白及教學停住。以 `{0} Party` 覆寫該鍵，沒有跳過教學或攔截例外來掩蓋問題。
- r7 補上 `BTN_ITEM_USE_STATE`（截圖中「イベント概要」下方的問號按鈕）、編隊 HP／COST、前後頁按鈕與 CHARM 共 6 個缺字鍵。
- r7 將 97 個非教學區域對應至 5 個自行建立的 4.0.0 分類：故事、活動、Casino 育成、進化、合作。修正 `Set AreaCateList Error!!` 與空白關卡列表；保留原區域／關卡 ID、前置關卡、事件 ID 及來源排序，教學區域不列入重複遊玩清單。
- r7 容器檢查重現原始格式例外，驗證修正文字能格式化，核對兩個頁面共 8 處實際 DLL 呼叫；另比較 371 筆原關卡、102 筆區域玩法資料並檢查首城與下一城的解鎖條件，地圖回歸檢查亦通過。
- r8 修正 `ServerFogery_GetEnemyGroupFromID` 永遠回傳首筆資料的原型邏輯，依指定 ID 查找正確群組。1.0.6 的首筆是空白列，會使進化教學建立戰鬥時發生 NullReferenceException，讓本機 API 停在「通訊中」。沒有改寫關卡、敵人數值、掉落、付款或教學旗標。
- r8 從實際序列化欄位讀取 LabelSetter 與 ButtonSetter，共 987 個元件／349 個不同文字鍵，修正前次以舊字首篩選造成的漏查；另外補寫 350 個文字鍵，包括 Link 點數、百分比、最後遊玩時間、強化結果跳過按鈕及其他盤點缺項。此補充為自行撰寫的日文，並非官方文字復原。
- r8 的文字盤點要求有效缺鍵為零才可封裝；8 個數字格式／原始位元組誤判項逐項記錄在 text-audit-exclusions.json。所有綁定元件鍵均有文字；這不代表動態生成鍵、未提供的下載內容或實際文字排版已通過 Android 驗證。
- r8 在可丟棄的 Mono 測試目錄載入封裝的資料與 DLL，重現 r7 的進化教學例外後，確認修正版本可建立 364 個啟用關卡；每個非零敵人群組 ID 查詢一致，火／水／風進化教學包含原頭目。引擎原生 I/O 僅在測試副本替換，不封入 APK。另靜態核對 223 處補充文字的直接格式呼叫（99 個鍵），未發現參數越界。
- r9 修正兩個進化教學底部 TAP 提示：原本固定位置與向下浮動動畫使箭頭壓到 Evolve Quest 文字。僅將 `TutorialStepNew5250_10`／`TutorialStepNew5260_10` 的非動畫父物件調為 y=-215、XY 縮放 0.5，保留底部／狀態列對齊與原動畫。以 640 基準座標計算完整 Hermite 動畫範圍後，提示與資訊框相隔 3、與按鈕文字框相隔 5.5 單位；這是資源幾何檢查，不是 Android 實際畫面驗證。
- r9 修正「イベント概要／アイテム効果」補成完整文字後沿用左對齊造成的右側溢出：只將兩個標籤改為右對齊、字級 28→24，右緣距資訊框邊緣 20 基準單位。按鈕的碰撞區域、點擊事件、文字內容與教學狀態不變。序列化檢查限定兩個資源共 6 個物件改變，其餘 5,886 個物件位元組一致；見 `layout-repairs.json`。
- r10 修正進化後 SCRATCH 教學無法離開 QUEST：1.0.6 的抽卡池沒有 `timing_public`／`timing_start`／`timing_end` 欄位，轉入 4.0.0 後預設為零。點 SCRATCH 的 `PageSwitchGachaMenu` 先查詢所有抽卡池，`TimeManager.CheckWithinTime` 因起始年月日為零而拋出 `ArgumentOutOfRangeException`，切換便中斷。僅為缺少的日期欄位補入新版既有的 `SYSTEM_START_DATE=1970010100` 與截止值 0（無截止）；原事件 ID、價格、抽取權重、角色及教學流程不變。
- r10 的隔離 Mono 檢查以 r9 的實際嵌入資料與 DLL 重現同一例外，再以新資料驗證全部 7 池日期、2026／2030 年的優先池／清單／次數查詢、合作池事件閘門，以及教學池 ID 10 的頁籤與有效角色資料。另逐欄比對原始 1.0.6 池資料與全部指派列；結果見 `gacha-menu-checks.txt`。這不代表刮卡動畫或完整教學已通過 Android 測試。

使用者於 2026-10-24 接手遊玩測試，提供卡住與排版異常的畫面；先前修正僅唯讀取得 Unity 日誌來定位例外。**r10 未由代理安裝或操作模擬器，尚待使用者遊玩確認。**
建置時的編譯、APK ZIP 完整性、簽章、對齊、修改項目及來源雜湊檢查，不等同於遊戲流程驗證。

## 已知限制

- 角色幾何仍是簡化版本。場景改採原始網格與動畫重建，不載入 Unity 4 原生 AssetBundle。
- `map_6001`、`map_6002` 的原始幾何不在提供的快取中，明確使用 `map_0001` 的結構替代；其餘 46 個門／地板資源從對應原件重建。教學使用的 `map_0001` 有原件。
- 尚未確認完整教學、戰鬥、結算、抽卡、合成、進化與關閉重開後存檔的正確性。
- 曾觀察到部分 Layout Add Error；尚未確認所有介面的影響。
- 新版成就、Link、Limit Over、部分道具與付費功能沒有 1.0.6 對應資料。補上名稱不代表功能已實作。
- text-fallback.json 是自行補寫的日文提示，並非復原的官方原文。
- text-repairs.json 包含 r6 的 131 筆與 r7 的 7 筆補充／格式覆寫；text-complete.json 補上 r8 的 350 筆。靜態盤點範圍內有效缺鍵已處理，但仍需遊玩確認動態文字與實際排版。
- 此工作是舊內容移植，**未實作玩家舊帳號／持有角色／進度存檔遷移**。

## 建置

所有專案工具在依 [Dockerfile](../../../Dockerfile) 建立的容器內執行；工作目錄設為倉庫根目錄，先啟用含 UnityPy 的 Python 環境。需要原始 4.0.0／1.0.6 APK、舊快取與 `artifacts/cache-backup-audit-2026-10-22/source-files.json` 等基準清單，不可覆寫既有輸出 APK。

```sh
python offline/v400/playable/build.py --output artifacts/NEW-400-CANDIDATE
python offline/v400/playable/audit_text.py --report artifacts/NEW-400-CANDIDATE/build-report.json --output artifacts/NEW-400-CANDIDATE/text-audit.json
python offline/v400/playable/verify_repairs.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/repair-checks.json
python offline/v400/playable/verify_quest_build.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/quest-build-checks.txt
python offline/v400/playable/verify_gacha.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/gacha-menu-checks.txt
```

`BUILD_WORK_DIR` 代表該次 build-report.json 的 work_directory，不是固定路徑。驗證入口會讀取建置中間檔；不會執行 Android 遊戲。r10 建置會執行結構檢查、關卡建立測試、抽卡入口檢查、文字盤點（--require-complete）與指定排版資源檢查，通過後才封裝 APK；這些不等同於完整遊玩回歸。

腳本仍有固定工作根目錄、Python／Mono／Cecil、暫存及簽署儲存設定，執行前須依自己的容器配置核對。DLL 解碼另依賴 Unicorn 2.1.4；目前 Dockerfile 沒有列入它，須在容器內另備，不能假定只建置映像便具備全部依賴。

若前次建置中間檔仍保留，可指定 `--assets-from` 與前次 build-report.json，重用相同來源及 522 角色基準的圖像輸出；此參數不會重用原生 CAB，地圖仍重新匯出。`--resume-work` 只用於同一輸出尚無 APK 的未完成建置。清除暫存後需重建，單靠 APK 不足以執行全部盤點。

中間解包、DLL、生成資料、快取與簽署私鑰不加入 Git。每次建置輸出候選身分、SHA256SUMS、資料轉換清單及簽章檢查結果。

## 維護入口

| 位置 | 用途 |
| --- | --- |
| `recover.py`、`loader.py` | 只接受已核對雜湊的 4.0.0 APK；以原生純解碼函式還原 DLL，再替換 Java 整數解碼及保護器初始化。原 APK 不改寫，封裝時修改副本。 |
| `Inspect.cs`、`data.py`、`assets.py` | 核對目標型別、投影原 1.0.6 資料及舊快取圖像；不以較晚快取數值覆蓋舊玩法。 |
| `Runtime400.cs`、`Skills400.cs`、`Patch.cs`、`CompileApi.cs` | 本機後端、更新標記、舊技能與版本限定 IL 掛鉤。不能把 2.9.0 的額外規則或受管測試結果當成本版已驗證。 |
| `Png400.cs` | 配合目標精簡 UnityEngine 的 PNG 讀取，使用本版解碼路徑。 |
| `map_assets.py`、`Maps400.cs` | 匯出指定原地圖的網格、UV、階層、曲線及事件，在 Unity 5 重建；不直接載入 Unity 4 原生包。 |
| `layout_assets.py` | 僅修改原始 4.0.0 中指定的兩個進化教學提示及兩個關卡詳細標籤，核對物件身分、原值、序列化修改範圍與動畫幾何間距；在封裝副本中替換資源。 |
| `text-fallback.json`、`text-repairs.json`、`text-complete.json`、`audit_text.py` | 95＋138＋350 筆補寫／覆寫文字，含序列化 LabelSetter／ButtonSetter 欄位盤點。IL 盤點共用 2.9.0 的 TextKeys.cs；靜態候選誤判明列於 text-audit-exclusions.json，動態文字另需實測。 |
| `verify_repairs.py`、`PartyFormatProbe.cs` | 核對匯出地圖、文字格式與區域分類；以 Mono String.Format 重現例外並核對 DLL 呼叫。不是完整技能、封包、存檔或 Android 回歸。 |
| `verify_quest_build.py`、`PrepareQuestProbe400.cs`、`QuestBuildProbe400.cs` | 在隔離暫存目錄建立獨立 Mono 測試副本，載入實際嵌入資料，驗證群組查詢與啟用關卡建立；只在副本轉接 .NET 組件參照與引擎原生 I/O。 |
| `layout_assets.py` | 限定兩個序列化資源的提示父物件與標籤，核對6個修改物件、其他物件位元組與完整動畫幾何範圍；不是實機排版檢查。 |
| `verify_gacha.py`、`GachaDateProbe400.cs` | 重現缺日期例外，核對7池日期、原選單查詢與來源欄位／權重；測試使用暫存 Mono 副本。 |
| `verify_gacha.py`、`GachaDateProbe400.cs` | 用同樣的隔離 I/O 準備工具呼叫實際 DLL 的抽卡日期／優先池／頁籤／次數查詢；重現舊錯誤並核對原始池欄位與指派資料。不是刮卡或完整教學測試。 |

本版缺表以空集合處理，精確名稱見 build-report.json 的 missing_tables_disabled；r7 已補上區域分類表。其餘涵蓋新版成就、登入禮物、敵人能力／行動、Link／LimitOver、音訊／首頁等定義；空表不代表所有相關路徑都可正常運行。

r8 保留 1798 筆常駐文字，加 583 筆補充／覆寫；文字盤點的有效缺鍵為零。原始候選仍列出 8 個已說明的非文字誤判項，沒有用空字串或通用訊息掩蓋缺鍵。

## 手動測試交接

在相容的 ARMv7 Android 測試環境覆蓋安裝 r10 候選版，再完全關閉並重新啟動遊戲；更新既有 offline400 時須維持同一套件與簽署憑證。若要保留進度，避免解除安裝或清除資料。
本次優先從目前 QUEST 的 SCRATCH 教學提示繼續，點擊 SCRATCH 後應進入抽卡頁並顯示後續教學。再確認選擇抽卡、刮卡、領取角色與後續命名教學是否能完成；本次沒有更動教學旗標或使用者存檔。
r9 排版修正亦保留：確認進化關卡選擇與出發確認頁的 TAP 箭頭不壓到 Evolve Quest 文字；關卡資訊右側的「イベント概要／アイテム効果」完整留在框內。已通過該教學的存檔可能不再出現提示，不必為此清除現有資料。
優先確認選角小視窗的 LINK／百分比、強化結果跳過按鈕，再接續目前的進化教學，確認「通訊中」結束後能進入戰鬥。這次例外發生在進化扣款與 UserParamSave 之前，更新沒有額外修改使用者存檔或跳過教學。
先從目前進度進入 UNITS，確認隊伍與持有角色出現、完成教學指定的編隊並儲存，檢查教學是否繼續。回 QUEST 確認故事分類與首個城鎮可選，並確認道具效果按鈕不再是問號。
先確認門是否置中完整、取得鑰匙後是否開門、能否點擊進門，以及技能視窗按鈕和兩個頁籤文字。
若測試其他版本，回報時請註明版本。
建議依序確認教學移動與戰鬥、通關結算、返回主畫面、關閉重開後進度，再檢查抽卡與育成。
回報問題時提供版本、操作步驟、停住的畫面與是否閃退，便可對應修正。

## 原始檔識別

- 4.0.0：`ディバゲ_4.0.0(紀念)-v20220207073837.apk`，34,724,267 bytes，versionCode 59。
  SHA-256：`b3f2a366d553d0ab67f2c6fee76bde79dceb3a372f7ed64c3de4849fc1d9600e`
- 1.0.6：`ディバゲ_1.0.6.apk`。
  SHA-256：`ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2`
- 來源為使用者提供的本機封存檔；此工作未另外驗證其外部發布來源。

既有 r10 報告記錄當時的容器編譯、上述針對性檢查與 APK 完整性／簽章檢查；r4 裝置紀錄不能延伸為 r10 已通過 Android 遊玩。實機顯示、點擊及完整教學仍待使用者測試。

本次提交整理只核對靜態語法、Git 差異、文件、來源指紋及公開資訊，未重新編譯或執行完整回歸。
