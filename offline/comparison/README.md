# 三版本比較與 1.0.6 資料移植評估

核對日期：2026-10-21。對象是倉庫的三個原始 APK，以及現有 1.0.6 prototype 16 實作。

**歷史探測結論：1.0.6 的資料可作為新版客戶端承載舊內容的移植起點。當時已在兩版未修改的原後端驗證 28 張舊表解析、創角、初始隊伍、登入包與記憶體存檔往返，尚未製作可玩 APK。** 後續實作分別見 [2.1.0](../v210/playable/README.md)、[2.4.0](../v240/playable/README.md)、[2.9.0 r9](../v290/playable/README.md)；本文原始試驗不代表新版完整內容復原。

## 證據與範圍

本次重新從原 APK 讀取 DLL、所有 Unity 序列化資料及 Manifest，檢查 SHA-256 與 ZIP CRC。未更動、重簽或安裝任何 APK，未查詢線上服務。解包、轉換後 JSON、引擎替身與測試 DLL 都放在隔離環境的暫存區，不當作維護中的遊戲原始碼。

完整原始碼在本目錄；產生的 metadata、逐欄差異、轉換清單與測試輸出在 `artifacts/version-comparison-2026-10-21-final/`。大型 `artifacts/` 不隨 Git checkout 發布，核心結果因此也記錄在本文。

| 版本 | 原 APK bytes | SHA-256 |
| --- | ---: | --- |
| 1.0.6 | 31,047,374 | `ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2` |
| 2.1.0 | 32,114,571 | `af40f477e5455caba4f4bbfa24d6fde94e174fc4614db39025c9411be207bb29` |
| 2.9.0 | 35,409,743 | `3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4` |

## 原 APK 的主要差異

| 項目 | 1.0.6 | 2.1.0 | 2.9.0 |
| --- | --- | --- | --- |
| 原本機後端 `ServerForgery` | 有 | 有 | 有 |
| 該後端 Start 要求的 Resources 主資料 | 28 份，全部內建 | 37 份，全部未內建 | 42 份，全部未內建 |
| Unity 序列化版本標記 | 4.1.0f4／4.1.0b3 | 4.1.0f4／4.1.0b3 | 4.6.5p1／4.6.1p5 |
| Android versionCode | 8 | 18 | 35 |
| min SDK／target SDK | 9／17 | 9／17 | 9／22 |
| 原生架構 | ARMv7 | ARMv7 | ARMv7 |
| 全部 TextAsset 物件 | 46 | 15 | 18 |
| 內建 AudioClip 物件 | 141 | 142 | 142 |
| 目前專案產物 | 有離線原型及資料／程式測試 | 僅診斷 APK | 僅診斷 APK |

Unity 標記來自各序列化檔案，混合標記可能含較早匯入資源，不能直接當成兩個引擎同時執行。音訊數是 APK 物件盤點，不代表全部下載音樂已齊全。

1.0.6 共有 31 個名稱以 `MasterData` 開頭的 TextAsset：29 個 JSON 資料列陣列、2 個其他 JSON 物件；其中本機後端要求 28 份。這三種數字的計算對象不同。核心資料包含角色 523 列（含 fix_id=0 整合列）、任務 371 列、地區 102 列、成長 500 列及抽卡分配 11,000 列。資料存在不表示每列都是有效開放內容或已核實的官方營運設定。

### 1.0.6 到新版的欄位差異

以下比較以「新版 `ServerForgery` 主資料欄位的元素型別」為基準，只判斷非靜態欄位名稱及型別，不代表資料值或執行行為相同。

| 相對於 1.0.6 | 2.1.0 的 37 種 | 2.9.0 的 42 種 |
| --- | ---: | ---: |
| 同名型別、欄位完全相同 | 12 | 11 |
| 同名型別、欄位有變更 | 15 | 16 |
| 1.0.6 沒有同名型別 | 10 | 15 |

最後一類包含活動表從 `MasterDataEvent` 到 `MasterDataEventBase` 的調整，不能把全部都解讀為完全沒有可參考的舊資料。套用此活動表別名後，兩版都有 **28 張舊表候選**，仍各有 **9／14 張**無舊表可填。

具體差異及其影響：

| 資料／流程 | 已確認的變更 | 移植影響 |
| --- | --- | --- |
| 成長、初始隊伍 | `MasterDataUserRank`、`MasterDataDefaultParty` 兩版欄位都與舊版相同 | 可沿用舊數值作第一階段創角與隊伍驗證 |
| 角色 | 2.1.0 增 `skill_plus`、`sub_kind`、`timing_public`；2.9.0 再有 `skill_plus_element` | 舊角色可解析，但新增機制的預設值與開放條件需查執行邏輯 |
| 隊長／被動技能 | 兩版都有新增技能類型與通用參數欄位 | 舊專用欄位仍存在，但 JSON 解析成功不能證明戰鬥效果等價 |
| 必殺技能 | 增傷害、吸收、反動、狀態異常等欄位 | 兩版原 `ConvertMasterSkillLimitBreak` 可處理舊列；它只初始化部分運算欄位，不會自動補齊全部新規則 |
| 敵人 | 2.1.0 增開場／死亡／行動表；2.9.0 再增能力、血條與座標欄位 | 需驗證舊式普攻及新 AI 未指定時的行為，不能一律補零便宣稱正確 |
| 地區／音樂 | 刪除 `bgm_normal`、`bgm_boss`，增 `packname_bgm`、`packname_se` | 原 JSON 直接交給新版 LitJson 會拋錯；刪欄只能解決解析，仍須重建音訊映射 |
| 素材目錄 | 刪除 `path`，增 `category`、`path_type`、`title_dl` | 不能直接沿用舊下載目錄；需本機載入器與新版分類對照 |
| 關卡／樓層 | 新增票券、回復面板；2.9.0 有入場條件、接關／重試等欄位 | 舊關卡需明確的相容規則，並實測進場到結算 |
| 抽卡 | Assign 表兩版仍相同；Gacha 表增顯示／URL，2.9.0 再增分頁、保底相關欄位 | 舊池可作候選，新版正式池與歷史機率無法由此補出 |
| API 編號 | 2.1.0 舊 API 編號大致維持；2.9.0 插入 MASTER_HASH_GET，許多編號位移 | 必須對新版 enum 名稱編譯，不能複製舊整數編號 |
| 存檔 | 新版頂層均較舊版多 9 欄，包含票券、成就、月獎勵等 | 需遷移與空值初始化；頂層舊欄位同型別仍不保證巢狀封包相容 |

例如 `SERVER_API_MASTER_DATA_GET_ALL` 從 1.0.6 的 6 變為 2.9.0 的 7；QUEST_START 從 21 變 22；GACHA_PLAY 從 34 變 35。新版 UI 效果常數也有位移，不能整套搬舊數字。

兩版共同缺乏可直接取自舊表的資料為：LoginMonthly、Present、PresentGroup、Achievement、StoreProductEvent、EnemyActionTable、EnemyActionParam、StatusAilmentParam、BeginnerBoost。2.9.0 還缺 SkillBoost、EnemyAbility、QuestRequirement、AreaCategory、TextDefinition。這是相對於舊表來源的缺口，並非宣稱每條舊內容路徑都會讀取這些表。

### 2.1.0 到 2.9.0 的增量

按相同欄位判準，42 種中有 22 種保持相同、15 種改變、5 種新增。新增的五份 Resources 為 EnemyAbility、SkillBoost、QuestRequirement、AreaCategory、TextDefinition。2.9.0 還增加 SNS 搬移等本機分派入口；入口存在不代表相關官方服務可以離線重現。

## 為何 1.0.6 能單機化

關鍵是「原本機後端＋完整舊主資料＋額外配接」，不只是跳過登入。

1. `ServerDataManager` 已有空 API 位址且存在 `ServerForgery` 時走本機回應的路徑。
2. `ServerForgery` 內已有創角、登入、隊伍、關卡、抽卡與保存等處理；1.0.6 同時附了其 28 份輸入資料。
3. 現有補丁固定空位址、建立本機後端、將表配接到 `MasterDataManager`，並略過更新、協議、下載與付款初始化等外部門檻。
4. APK 外下載素材另以核對過的圖片、圖集與簡化物件替代；也修正原生載入、碰撞、HUD 與 UI 生命週期問題。

所以主資料與資源都重要。1.0.6 的圖像／模型仍有替代與缺失，prototype 16 尚無文件記錄的完整 Android 遊玩驗證；不能把這裡的「成功」擴張為完整原版服務復原。實作位置見 [Runtime.cs](../Runtime.cs)、[Patch.cs](../Patch.cs) 與 [移植指南](../../OFFLINE_PORTING_GUIDE.md)。

## 本次實際移植實驗

兩版分別使用**從原 APK 重新取出的原 Assembly-CSharp.dll 與 firstpass.dll**，執行前後核對位元完全相同。Unity 原生呼叫在一次性測試 DLL 中替換為預設值，PlayerPrefs 以記憶體字典模擬。這些替身沒有加入 APK，也沒有修改 API 處理器、強制成功回應或建立缺失表的空陣列。

資料轉換僅有：

- 檔名 `MasterDataEnemyChara` 對應類別 `MasterDataParamEnemy`。
- 舊 `MasterDataEvent` 的列作為新版 `MasterDataEventBase` 的候選輸入；新增欄位留為反序列化預設值。
- 依新版欄位過濾舊 JSON：Area 移除兩個舊 BGM 欄位，AssetBundlePath 移除 `path`。沒有推測新路徑、音訊包或新技能數值。
- 將有對應同型別欄位的表接到 `MasterDataManager`；這不涵蓋新版所有初始化與活動表轉換。

| 檢查 | 2.1.0 | 2.9.0 |
| --- | --- | --- |
| 無表直接 `UserParamCreate` | 重現 NullReferenceException | 重現 NullReferenceException |
| 原舊 JSON 不轉換 | Area／AssetBundlePath 兩表被 LitJson 拒絕 | 同左 |
| 經上述有限轉換的表 | 28 張全部解析 | 28 張全部解析 |
| 使用舊 rank 創角 | 通過，體力／基礎持有數均 20 | 同左 |
| 原必殺技表轉換函式 | 通過 | 通過 |
| USER_CREATE API | 4096（原 SUCCESS）且 result 存在 | 同左 |
| SELECT_DEF_PARTY API | 成功，產生 5 個單位，ID 都在舊角色表 | 同左 |
| GET_LOGIN_PACK API | 4096 且 result 存在 | 同左 |
| 同程序記憶體存檔往返 | 姓名與單位數保留 | 同左 |

**登入包成功只代表這次請求走到的原後端路徑通過。** 沒有測完登入回應的每個內容關聯、其他 starter 選項、完整客戶端登入、主資料更新、戰鬥技能、缺失表功能或任意不同日期。也沒有證實 Unity 場景能正常載入與顯示。

記憶體存檔往返不等於 Android PlayerPrefs 落盤、跨程序保存或舊 1.0.6 玩家存檔遷移。尚未測試 Android 安裝、原生引擎、觸控、飛航模式、戰鬥、抽卡演出與完整關卡循環。

## 可行性與建議路線

| 目標 | 本次判斷 |
| --- | --- |
| 沿用 1.0.6 維持可測試的舊內容離線版 | 現有成果最多，風險最低；仍需完成實機驗證 |
| 用 2.1.0 客戶端運行轉換後的 1.0.6 內容 | 有實驗支持，適合下一個有限範圍原型 |
| 用 2.9.0 客戶端運行轉換後的 1.0.6 內容 | 後端同樣有正面證據，但引擎、UI、AI、技能與入場條件的配接範圍較大 |
| 只靠 1.0.6 還原完整 2.1.0／2.9.0 | 資料不足；舊版沒有後來新增角色、敵人配置、技能、活動與正式營運設定 |
| 把現有 1.0.6 DLL／補丁直接複製到新版 | 不可直接套用；需按新版方法簽章、欄位、enum、資源與原生引擎重新配接 |

優先建議為 **2.1.0 客戶端＋明確標示的 1.0.6 內容集**。先限定創角、同一 starter、教學關卡及一組固定卡池，逐項補接 `MasterDataManager`、資源載入與必要的新欄位。新增功能只在路徑與資料確定後啟用；缺表功能可限制入口或做有文件的舊規則配接。

原型的下一道驗收應是：Android 上創角 → 進教學 → 實際移動及戰鬥 → 結算 → 關閉程序後重新開啟 → 還原進度，並在無網路下重複。技能要比較實際效果，不能只看表讀取成功；地圖及立繪要驗證載入路徑、UV、材質與操作。

1.0.6 已核對的 PNG、角色 ID／資源 ID 對照及簡化資源工廠可以重用其資料與設計，但新版需要重新編譯和驗證。2.9.0 的引擎已不同，不能把舊 UnityEngine.dll、libunity.so 或舊格式 bundle 當成通用替換件。

若目標是後期版本完整內容，仍應優先找相符版本已下載完成的舊裝置／模擬器快取、MasterData bundle、主資料回應與下載清單。現有 DGDB、Wiki、圖鑑可補候選資料或美術，必須逐表核對年代、欄位、ID 與關聯。

## 重現

在依倉庫 Dockerfile 建立的隔離工具環境中執行，先啟用包含 UnityPy 的 Python 環境，並以倉庫根目錄作為工作目錄。確認 Mono.Cecil 工具位置符合腳本的設定，且已備妥三個相符 hash 的原始 APK。輸出必須是尚不存在的新目錄：

```sh
python offline/comparison/compare.py --output artifacts/version-comparison-new
python offline/comparison/probe.py artifacts/version-comparison-new/comparison.json
```

測試會使用倉庫內的 2.1.0 引擎替身準備器，所有修改只作用在一次性測試副本；報告與暫存不加入 Git。

`comparison.json` 包含檔案 hash、表需求、schema、常數、API 分派與頂層存檔差異。`*-metadata.tsv` 是直接從原 DLL 輸出的 metadata；`*-conversion.json` 記錄來源表、列數及移除欄位；`*-transplant-probe.txt` 是原後端實驗結果。成功的探測器會檢查原遊戲 DLL 位元不變、預期的缺表錯誤與所有列出的通過階段。
