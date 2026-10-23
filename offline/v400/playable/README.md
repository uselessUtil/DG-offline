# 4.0.0 單機化原型：1.0.6 資料

此目錄是自行撰寫的復原、資料轉接與封裝工具，不是原遊戲原始碼。
既有候選產物（不隨 Git 提供）為 `artifacts/4.0.0-offline106-r6/ディバゲ_4.0.0.apk`。
套件名稱為 `jp.gungho.dg.offline400`，可與原版及 offline106、offline290 並存。

大小：162,870,819 bytes；SHA-256：`d7c72c441e2c6e202c188c5acd2235efbc5385b480cb9aa1d591dd4353fc97ea`。身分來自該目錄 build-report.json；原始報告及產物需另備。原創工具採專案 [MIT 授權](../../../LICENSE)，遊戲／第三方素材範圍見 [權利聲明](../../../THIRD_PARTY_NOTICES.md)。

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

使用者於 2026-10-24 接手遊玩測試。**r6 未安裝至模擬器，未宣稱通過遊玩測試。**
建置時的編譯、APK ZIP 完整性、簽章、對齊、修改項目及來源雜湊檢查，不等同於遊戲流程驗證。

## 已知限制

- 角色幾何仍是簡化版本。場景改採原始網格與動畫重建，不載入 Unity 4 原生 AssetBundle。
- `map_6001`、`map_6002` 的原始幾何不在提供的快取中，明確使用 `map_0001` 的結構替代；其餘 46 個門／地板資源從對應原件重建。教學使用的 `map_0001` 有原件。
- 尚未確認完整教學、戰鬥、結算、抽卡、合成、進化與關閉重開後存檔的正確性。
- 曾觀察到部分 Layout Add Error；尚未確認所有介面的影響。
- 新版成就、Link、Limit Over、部分道具與付費功能沒有 1.0.6 對應資料。補上名稱不代表功能已實作。
- text-fallback.json 是自行補寫的日文提示，並非復原的官方原文。
- text-repairs.json 是 r6 的補充；修復本次畫面與常用流程，並未宣稱所有頁面文字已齊全。其餘缺字候選保留在 text-audit.json，沒有改成通用文字來掩蓋缺失。
- 此工作是舊內容移植，**未實作玩家舊帳號／持有角色／進度存檔遷移**。

## 建置

所有專案工具在依 [Dockerfile](../../../Dockerfile) 建立的容器內執行；工作目錄設為倉庫根目錄，先啟用含 UnityPy 的 Python 環境。需要原始 4.0.0／1.0.6 APK、舊快取與 `artifacts/cache-backup-audit-2026-10-22/source-files.json` 等基準清單，不可覆寫既有輸出 APK。

```sh
python offline/v400/playable/build.py --output artifacts/NEW-400-CANDIDATE
python offline/v400/playable/audit_text.py --report artifacts/NEW-400-CANDIDATE/build-report.json --output artifacts/NEW-400-CANDIDATE/text-audit.json
python offline/v400/playable/verify_repairs.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/repair-checks.json
```

`BUILD_WORK_DIR` 代表該次 build-report.json 的 work_directory，不是固定路徑。後兩個入口會讀取建置中間檔；結構檢查不會重新執行 Android 遊戲。建置本身的檢查也不代表這兩項或完整回歸已跑過。

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
| `text-fallback.json`、`text-repairs.json`、`audit_text.py` | 95＋131 筆自行補寫文字及缺鍵候選盤點。盤點共用 2.9.0 的 TextKeys.cs；靜態候選包含未必可達的 UI，動態文字另需實測。 |
| `verify_repairs.py` | 核對匯出地圖資料與文字格式；不是完整技能、封包、存檔或 Android 回歸。 |

本版 22 張缺表以空集合處理，精確名稱見 build-report.json 的 missing_tables_disabled。涵蓋新版成就、登入禮物、敵人能力／行動、Link／LimitOver、音訊／首頁等定義；空表不代表所有相關路徑都可正常運行。

既有 r6 文字盤點保留 1798 筆常駐文字，加 226 筆補充，仍有 341 個靜態缺鍵候選。這不是可達頁面缺字的精確數量，也未以通用文字掩蓋缺失。

## 手動測試交接

在相容的 ARMv7 Android 測試環境安裝 r6 候選版；更新既有 offline400 時須維持同一套件與簽署憑證。若要保留進度，避免解除安裝或清除資料。
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

本次提交整理只執行靜態語法、差異、文件連結、來源指紋與公開資訊檢查，未編譯或執行完整回歸；前述測試數字與 r4 裝置紀錄均來自既有報告，不能延伸為 r6 已通過 Android 遊玩。
