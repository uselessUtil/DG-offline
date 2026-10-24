# 4.0.0 單機化原型：1.0.6 資料

此目錄是自行撰寫的復原、資料轉接與封裝工具，不是原遊戲原始碼。
目前候選產物（不隨 Git 提供）為 `artifacts/4.0.0-offline106-r15/ディバゲ_4.0.0.apk`。
套件名稱為 `jp.gungho.dg.offline400`，可與原版及 offline106、offline290 並存。

大小：162,883,107 bytes；SHA-256：`0740d79e85c701f5e8cfeb141e0b0c6ee4e251b5604b871ee8dcc7e37c099385`。身分見該目錄的 build-report.json、SHA256SUMS；原始報告及產物需另備。原創工具採專案 [MIT 授權](../../../LICENSE)，遊戲／第三方素材範圍見 [權利聲明](../../../THIRD_PARTY_NOTICES.md)。

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
- r11 修正已進入 SCRATCH 但卡片與按鈕空白：先前投影將 `url_img` 清空，但原生 `MainMenuSeqGachaList.PageSwitchSetting` 會直接跳過圖片位址為空的項目，連抽卡按鈕都不建立。改用非空的 `offline400/gacha/ID` 本機識別值，將圖片請求與查詢接到同步本機材質，停用該圖片管理器的遠端下載 Update。原生卡片建立、載入完成旗標、顯示切換、碰撞／點擊判定與抽卡確認入口維持原樣。
- r11 為本機卡片加上池名稱、原始簡介與價格；這是自行建立的 NGUI 卡片，以既有字型與背景色呈現，**不是復原的官方廣告圖**。資訊按鈕顯示該池原始簡介。七池的原欄位、角色、價格與指派權重仍與原始 1.0.6 一致。
- r11 截圖上方的問號來自抽卡頁 `EventPt` 元件直接存放的 18／11 個問號佔位字串，沒有綁定文字鍵。卡片建立被略過時，原本設定／隱藏該欄位的程式也沒執行。已清空兩個預設字串並將該區塊預設隱藏；原程式仍可在具活動點數的池中設定並顯示。這些與 r9 合計是三個資源中的 9 個指定物件修改，見 `layout-repairs.json`。
- r11 擴充隔離檢查，實際呼叫修改後的 `GachaResourceMaster.ResourceLoadRequest/GetResourceTexture`，核對全部七個鍵、強制／重複請求，以及拒絕未知／網路位址；另比對原生建立與互動方法的 IL，確認保留載入完成旗標與點擊流程。材質路由測試使用可丟棄的材質替身，**沒有驗證原生材質配置、實機排版或完整刮卡流程**。靜態缺鍵盤點不會找出這類原生元件佔位字串或空清單。
- r12 修正普通關卡好友選擇後卡住：原生本機支援生成器隨機附加 Link 角色，但 1.0.6 沒有 `MasterDataLinkSystem`。好友頁的 `FriendSelectChange → SetupPlayerParty → CharaOnce.CharaSetupFromID → GetLinkUnitBonusElement` 因空表越界，隊伍 HP／技能資訊與教學後續更新被中斷。已在三個支援生成器的回傳處清除新生成封包的 Link 角色／點數／旗標，保留主角色、等級、加值與技能等欄位，並讓名稱對應主角色；沒有寫入玩家持有角色或教學存檔。
- r12 為 Link 屬性／種族加成的主函式加上缺表檢查，缺少規則時回傳中性的零加成；有資料時仍執行原函式。這不是補造新版 Link 數值，也沒有解除教學輸入限制或跳過好友選擇。重新啟動後會依原本流程重建本機支援列表。
- r12 用 r11 的實際資料與 DLL 重現同一越界，再確認 64 個原生生成支援可通過好友頁使用的角色初始化多載；全部 522 個非零角色在最低／最高等級共 1,044 次初始化的原始 HP／攻擊力與加值吻合。另檢查缺表加成為零、主角色欄位保留，以及非空規則時不清除支援 Link。結果見 `support-setup-checks.txt`；隔離副本將原生 I/O 換成替身，`Mathf.Pow` 轉為 `System.Math.Pow`，沒有封入 APK，**不能視為 Android 教學點擊、隊伍總 HP 或實戰驗證**。
- r13 修正通關後結果頁只剩背景：教學完成後 `MainMenuPartsFooterChildMenuList.UpdateNewLabel` 直接讀取月登入日期清單的最後一筆；本版沒有月登入內容，空清單造成 `IndexOutOfRangeException`。例外發生在 `LinkerFadeIn → UpdateNewBadge`，頁面請求已取出，後續每幀重試便只剩 `Switch Page Request Error / NullReferenceException`。沒有以忽略例外、跳過結果或重送結算來處理。
- r13 僅將該函式的一處 `login_list` 欄位讀取換成安全存取：有紀錄時保留原陣列，無物件／無陣列／空陣列時，以目前日期建立暫存值供 New! 判定。原本的小時更新、關卡／抽卡提示演算法保留；暫存日期不寫入月登入或玩家清單，不產生登入獎勵。
- r13 在隔離 Mono 副本執行完整原生 New! 演算法，重現 r12 的空清單越界，再通過空值、空陣列、目前／歷史日期與跨日共 10 組及重複更新檢查。保留關卡 New!，玩家獎勵／進度與月登入資料不變，寫入僅有提示偏好。IL 比對確認只替換一處讀取，原結果封包、獎勵儲存、結果頁設定與淡入方法相同；見 `footer-badge-checks.txt`。偏好 I/O 使用記憶體替身，並非 Android 結果頁或完整通關交易驗證。
- r14 補上 r13 遺漏的重新登入路由：裝置日誌已確認啟動 r13，但 `MainMenuSeqSwitchPage.Update` 的月登入判斷只檢查陣列非 null，仍對空 `login_list` 讀取最後一筆而越界。現在在該路由第一次讀取月登入物件時，以日期清單非空、日曆清單非空且首筆存在判定是否可用，沒有內容時讓原本的短路條件略過月登入頁、繼續選單流程。
- r14 同步將月登入頁的直接切換入口限制為具有可用內容，通知端也使用相同的只讀判定。沒有改寫收到的月登入物件、使用者存檔或登入紀錄；New! 提示用的暫存日期不會被拿來補造登入獎勵。盤點其餘讀取位置：本機月登入產生器已停用，月登入頁與獎勵對話框保留原碼並由有效內容入口保護，底部提示沿用 r13 的安全存取，通知具備額外的有效內容檢查。
- r14 用 r13 DLL 的實際啟動路由重現同一例外，再執行修正路由，確認 6 種缺物件／缺陣列／空日期／空日曆情況及重複更新均可前往故事關卡；完整但未查看、已查看的日曆保留原路由，待處理的通關仍優先進入原結果傳送頁。玩家與月登入資料不變。見 `startup-router-checks.txt`；測試只在隔離副本替換 UI 就緒檢查與切換佇列為記錄邊界，**並非 Android 啟動、月登入傳輸或結算流程驗證**。

- r15 修正主選單反覆出現「プライバシー・ポリシー更新」並返回標題：原本只在 `TitleMenuSeqSwitchPage.Update` 略過已停用服務的線上政策閘門，主選單 `MainMenuSeq.PageSwitchUpdate` 仍讀取政策狀態；狀態非 2 時開啟更新彈窗，回答後返回標題，重新登入又重現。完整盤點確認這個政策讀取函式只有上述兩個呼叫端，現在兩處均以相同的局部 IL 替換處理。
- r15 沒有將同意狀態寫成已接受，也沒有修改 `agree_ok_policy`、政策版本或政策讀寫函式；僅讓離線路由不再進入這個線上政策閘門。主選單資料版本檢查、原有條款分支、一般彈窗回標題處理與恢復事件保持原碼。
- r15 的隔離 Mono 測試先在 r14 實際主選單程式重現三次「政策更新彈窗 → 返回標題」循環，再於修正版執行 12 種已存政策狀態／版本組合，各重複三次標題與主選單檢查；均未觸發政策讀取、政策彈窗、場景重設或偏好寫入。另確認資料版本不同時仍開啟更新彈窗並返回標題，原條款路由保留；IL 正規化比對確認兩處閘門各只替換一個政策呼叫。見 `policy-gate-checks.txt`。測試替換的僅為隔離副本中的 UI 就緒、彈窗／場景記錄與偏好 I/O，**並非 Android 重登或遊玩驗證**。

使用者於 2026-10-24 接手遊玩測試，提供卡住與排版異常的畫面；先前修正僅唯讀取得 Unity 日誌來定位例外。**r15 未由代理安裝或操作模擬器，尚待使用者遊玩確認。**
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
python offline/v400/playable/verify_support.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/support-setup-checks.txt
python offline/v400/playable/verify_badges.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/footer-badge-checks.txt
python offline/v400/playable/verify_router.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/startup-router-checks.txt
python offline/v400/playable/verify_policy.py BUILD_WORK_DIR --output artifacts/NEW-400-CANDIDATE/policy-gate-checks.txt
```

`BUILD_WORK_DIR` 代表該次 build-report.json 的 work_directory，不是固定路徑。驗證入口會讀取建置中間檔；不會執行 Android 遊戲。r15 建置會執行結構檢查、關卡建立測試、抽卡入口／本機圖片路由與互動 IL 檢查、支援／角色初始化檢查、New! 更新與啟動路由檢查、政策閘門檢查、文字盤點（--require-complete）與指定排版資源檢查，通過後才封裝 APK；這些不等同於完整遊玩回歸。

腳本仍有固定工作根目錄、Python／Mono／Cecil、暫存及簽署儲存設定，執行前須依自己的容器配置核對。DLL 解碼另依賴 Unicorn 2.1.4；目前 Dockerfile 沒有列入它，須在容器內另備，不能假定只建置映像便具備全部依賴。

若前次建置中間檔仍保留，可指定 `--assets-from` 與前次 build-report.json，重用相同來源及 522 角色基準的圖像輸出；此參數不會重用原生 CAB，地圖仍重新匯出。`--resume-work` 只用於同一輸出尚無 APK 的未完成建置。清除暫存後需重建，單靠 APK 不足以執行全部盤點。

中間解包、DLL、生成資料、快取與簽署私鑰不加入 Git。每次建置輸出候選身分、SHA256SUMS、資料轉換清單及簽章檢查結果。

## 維護入口

| 位置 | 用途 |
| --- | --- |
| `recover.py`、`loader.py` | 只接受已核對雜湊的 4.0.0 APK；以原生純解碼函式還原 DLL，再替換 Java 整數解碼及保護器初始化。原 APK 不改寫，封裝時修改副本。 |
| `Inspect.cs`、`data.py`、`assets.py` | 核對目標型別、投影原 1.0.6 資料及舊快取圖像；不以較晚快取數值覆蓋舊玩法。 |
| `Runtime400.cs`、`Skills400.cs`、`Patch.cs`、`CompileApi.cs` | 本機後端、更新標記、舊技能與版本限定 IL 掛鉤。不能把 2.9.0 的額外規則或受管測試結果當成本版已驗證。 |
| `Scratch400.cs` | 抽卡卡片的本機識別值、同步背景材質、以既有字型顯示原池名稱／簡介／價格，以及本機簡介視窗。原抽卡按鈕與本機後端抽樣流程保留。 |
| `Png400.cs` | 配合目標精簡 UnityEngine 的 PNG 讀取，使用本版解碼路徑。 |
| `map_assets.py`、`Maps400.cs` | 匯出指定原地圖的網格、UV、階層、曲線及事件，在 Unity 5 重建；不直接載入 Unity 4 原生包。 |
| `layout_assets.py` | 限定三個資源的9個物件：教學提示父物件、關卡標籤、EventPt 預設隱藏及問號佔位字串；核對物件原值、其他物件位元組與動畫幾何範圍。不是實機排版檢查。 |
| `text-fallback.json`、`text-repairs.json`、`text-complete.json`、`audit_text.py` | 95＋138＋350 筆補寫／覆寫文字，含序列化 LabelSetter／ButtonSetter 欄位盤點。IL 盤點共用 2.9.0 的 TextKeys.cs；靜態候選誤判明列於 text-audit-exclusions.json，動態文字另需實測。 |
| `verify_repairs.py`、`PartyFormatProbe.cs` | 核對匯出地圖、文字格式與區域分類；以 Mono String.Format 重現例外並核對 DLL 呼叫。不是完整技能、封包、存檔或 Android 回歸。 |
| `verify_quest_build.py`、`PrepareQuestProbe400.cs`、`QuestBuildProbe400.cs` | 在隔離暫存目錄建立獨立 Mono 測試副本，載入實際嵌入資料，驗證群組查詢與啟用關卡建立；只在副本轉接 .NET 組件參照與引擎原生 I/O。 |
| `verify_gacha.py`、`GachaDateProbe400.cs`、`GachaUiContract400.cs` | 用同樣的隔離 I/O 準備工具呼叫實際 DLL 的抽卡日期／優先池／頁籤／次數及本機圖片路由；比對原生卡片建立／互動 IL，重現舊錯誤並核對原始池欄位與指派資料。不是原生材質、刮卡或完整教學測試。 |
| `verify_support.py`、`SupportSetupProbe400.cs`、`SupportProbePrep400.cs` | 在隔離副本重現好友 Link 越界，呼叫實際支援生成器與好友頁使用的 `CharaOnce` 多載，核對原始角色數值端點及無 Link 狀態；原生數學只在副本轉接，未執行 Android UI 或戰鬥。 |
| `verify_badges.py`、`BadgeUpdateProbe400.cs`、`BadgeProbePrep400.cs`、`BadgeProbeIO400.cs` | 實際執行底部 New! 演算法，檢查空登入紀錄與跨日、玩家資料不變、僅寫入提示偏好，以及原結算／淡入方法保持一致。所有偏好替身與測試修改僅放在可丟棄副本。 |
| `verify_router.py`、`RouterMonthlyProbe400.cs`、`RouterProbePrep400.cs`、`RouterProbeIO400.cs` | 執行實際啟動路由，檢查空月登入內容、有效內容與待結算入口；只在隔離副本替換 UI 就緒／切換佇列邊界，不操作 Android 或玩家存檔。 |
| `verify_policy.py`、`PolicyGateProbe400.cs`、`PolicyProbePrep400.cs`、`PolicyProbeIO400.cs` | 重現主選單政策彈窗循環，執行兩處路由的政策狀態／版本組合與重複檢查；核對偏好不變、更新／條款分支及其他指令保留。記錄替身只用於隔離副本。 |

本版缺表以空集合處理，精確名稱見 build-report.json 的 missing_tables_disabled；r7 已補上區域分類表。其餘涵蓋新版成就、登入禮物、敵人能力／行動、Link／LimitOver、音訊／首頁等定義；空表不代表所有相關路徑都可正常運行。

r8 保留 1798 筆常駐文字，加 583 筆補充／覆寫；文字盤點的有效缺鍵為零。原始候選仍列出 8 個已說明的非文字誤判項，沒有用空字串或通用訊息掩蓋缺鍵。

## 手動測試交接

在相容的 ARMv7 Android 測試環境覆蓋安裝 r15 候選版，再完全關閉並重新啟動遊戲；更新既有 offline400 時須維持同一套件與簽署憑證。若要保留進度，避免解除安裝或清除資料。
本次優先確認重登後政策更新彈窗不再出現，返回關卡選單並切換頁面後仍可操作，再關閉重開確認進度。r15 修正主選單的另一處線上政策閘門，沒有代寫政策同意或修改獎勵／教學進度。
本次優先確認重新登入後能返回可操作的關卡選單，再進行普通關卡通關，檢查結算的經驗／金錢／角色列表、下一步按鈕、好友頁與返回關卡列表。這次補上月登入內容的路由判定；沒有修改結算封包、補發獎勵、寫入教學旗標或重播已回傳的結算。r13 的底部提示、r12 的好友選擇與 r11 的抽卡修正仍保留，完整回歸待遊玩測試。
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

既有 r15 報告記錄當時的容器編譯、上述針對性檢查與 APK 完整性／簽章檢查；r4 裝置紀錄不能延伸為 r15 已通過 Android 遊玩。實機顯示、點擊及完整教學仍待使用者測試。

本次提交整理只做靜態語法、差異、文件與來源指紋檢查，未編譯或執行完整回歸；上述結果均引自既有報告。

現行建置以明確的22r修訂標記契約配接共用 Runtime，保留唯一字串替換檢查；這未將1.0.6的Save Backup功能移植至4.0.0。
