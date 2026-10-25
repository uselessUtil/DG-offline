# Divine Gate 1.0.6 離線版

本專案有權授權的原創程式及文件採用 [MIT License](../LICENSE)，著作權署名為 uselessUtil。原遊戲、引擎與素材的範圍見 [第三方權利聲明](../THIRD_PARTY_NOTICES.md)。

維護者與其他 agent 請先讀 [離線化實作與其他版本 APK 移植指南](../OFFLINE_PORTING_GUIDE.md)：包含架構、逐項修改位置、資料／素材來源、缺失、建置測試與版本移植流程。

三版差異與最初舊資料移植實驗見 [比較報告](comparison/README.md)：保存原後端對 1.0.6 舊表的有限探測。後續各版本候選版另有專用配接與驗證，均不代表新版完整內容復原。

1.0.9 的獨立玩法候選版 1 r4 見 [候選版說明](v109/playable/README.md)：接入舊備份主資料／原生素材及 1.0.6 舊卡池，提供本地免費代幣商店。既有容器受管程式檢查已記錄，r4 的 Android 畫面、操作及實際遊玩仍待驗證；不是官方 1.0.9 全內容復原。

2.1.0 已新增 [舊內容玩法候選版](v210/playable/README.md)：接入舊備份、舊卡池及版本專用技能／商店配接；目前 r4 包含鑰匙樓層與更新標記修正，既有報告記錄 389 任務／760 層鑰匙及特定存檔更新循環檢查；全面戰鬥／技能／商店等回歸仍未完成，Android 未驗證。原 [診斷報告](v210/README.md) 留作歷史；不能視為官方 2.1.0 完整內容復原。

2.4.0 已有 [舊內容候選版](v240/playable/README.md)：接入舊資料、卡池與免費商店，另適配原分頁、Boost／入場條件及商店初始化；既有受管檢查有紀錄，Android 尚未驗證。

2.9.0 已有 [舊內容離線工程](v290/playable/README.md)：包含 Unity 4.6 素材轉換與 22r6 帳號／抽卡／商店／助戰規則。既有受管測試及有限 Android 選單、原生載入冒煙檢查有紀錄，r10 修正一般樓層配置、排序方向與成就過渡頁接續；r11 再加入倉庫助戰最愛優先，既有報告記錄受管檢查與更新安裝，但最愛列表的 Android 視覺／觸控未驗證；完整實戰及玩家特定按鍵停留情境仍待驗證；[診斷報告](v290/README.md) 留作歷史。r13-final 已有47筆原生增益狀態、21組受管檢查與跨程序重載紀錄；r14 另修正進化確認頁殘留對話等待；現行 r17 包含存檔備份、固定1.0.6單向轉入與接續取消退休，既有24組受管檢查有紀錄，Android選檔、接續及完整實戰仍待驗證。4.0.0 已有 [原始 1.0.6 內容候選版 r16](v400/playable/README.md)，以 522 角色／371 列任務為基準，包含地圖幾何與動畫重建；r8 修正敵群查詢、區域分類與文字格式，r9／r10 修正提示版面與卡池日期，r11 接通抽卡清單，r12～r15 再處理缺 Link 規則的支援、空月登入下的 New!／啟動路由及兩處線上政策等待，r16 再停用廣告ID背景查詢並保留JNI完成狀態；既有針對性檢查有紀錄，Android 及完整流程待使用者驗證。[先前研究](research/V400_OLD_CONTENT.md) 的 595 角色快取探測保留為歷史。以下內容仍專指 1.0.6。

目前實作基準為 **prototype 22r7 存檔匯出／匯入版**。Others 的 Save Backup 可透過 Android 系統選檔介面匯出存檔、確認後匯入，並保留匯入前回復備份。倉庫來源按鈕沿用原版紫色背景且靠右；新角色記錄獲得時間，舊角色時間不補寫；第五位倉庫助戰僅顯示目前五星以上且未在當前隊伍中的角色。新舊帳號至少 Rank184／倉庫2000格，180秒回體；標準42種五星與四個聯動池的五星率維持原始本機基準三倍。強化頁維持原好友流程；沿用備份關卡與原生素材範圍。**存檔選檔視窗及其他Android渲染／操作仍待驗證**。

本倉庫發布配接器、工具與文件；原 APK、大型素材、`artifacts/` 報告及生成的預覽頁不隨 Git checkout 提供。下列產物路徑是專案相對資料分類，不表示已附公開下載；重現前須另備來源檔與 manifests。



另有兩個獨立入口：[原生2.9.1／DGDB-1](v291/playable/README.md) 使用鎖定SQL投影、595角色／392有效關卡，現行r10沿用掉落與常駐亂入修正，另補減傷隊長技欄位與掉落角色等級；既有31組受管檢查不等於Android實戰通過。[Godot移植](../godot290/README.md) 是獨立ARM64開發工程，保留r17規則並新增實驗關卡流程；較早port1成品尚無戰鬥，不能用其報告證明目前來源。兩者均需另備APK／資料與工具。

## 調查資料

已將本機 artifacts 的重要結論整理到 [調查資訊索引](research/README.md)：包含主資料年代與版本相容性、DGDB／DG0 可用範圍、素材保存、玩法修正歷程及 Android 驗證界線。整理文件與來源報告指紋隨 Git 提供，原始報告與大型產物仍需另備。

## 最新產物

- APK：`artifacts/1.0.6-offline-prototype22r7-save-backup/ディバゲ_1.0.6.apk`
- 使用與舊離線版相同的套件 `jp.gungho.dg.offline106` 及測試簽章，更新安裝時不需先清除資料。
- 大小：176,039,210 bytes。
- SHA-256：`6f8374a4d7c1f51d109d2aac55e05cc562d651b4dcfabfe24ff7e74a03e463e0`。
- 詳細變更、測試結果與手動驗證步驟：[交付說明](../artifacts/1.0.6-offline-prototype22r7-save-backup/README.md)。
- 2026-10-21 [僅 ARM64 的 Android 16 裝置安裝檢查](../artifacts/android16-tb323fu-prototype17-test-2026-10-21/README.md)：低 SDK 繞過後仍因 `INSTALL_FAILED_NO_MATCHING_ABIS` 無法安裝；裝置僅 ARM64，而 APK 僅 ARM32。未修改 APK，原生 ARM64 移植尚未完成。
- 全部活動原名、任務數及範圍：[活動清單](../artifacts/1.0.6-offline-prototype10-events/EVENTS.md)。七筆無樓層的 reserve 維持停用，並未加入後期版本活動。

## 本機執行方式

保留原遊戲內建的 `ServerForgery` 與 `SERVER_SAVE` / `PlayerPrefs`，改走本機 API 及 APK 內建資料表；不需要另開 HTTP 伺服器。原官方帳號不會自動復原。外部版本檢查、付款、Google 登入及網路權限已移除。`FreeChipShop.cs` 將原六種 Android 商品改為 FREE，僅增加免費代幣並保留付費歷史；原 999 枚門檻搭配整包超額拒絕。

原生載入修正將 APK 內 `assets/libs/armeabi-v7a/` 的真正引擎放到標準原生程式庫位置，取代過時代理載入器；引擎本體位元內容不變。只有 32 位 ARM 引擎，相容性仍需各 Android 版本與裝置確認。

## 存檔備份與共用來源契約

匯出格式、確認／回復流程、來源檔與測試界線見 [存檔備份說明](SAVE_BACKUP.md)。目前共用 Runtime 為22r7；其他舊版本的22r6建置契約及來源配對限制也列在該文件。

## 素材與限制

- prototype 22r7 新增 `SaveBackup.cs`、`SaveTransferUI.cs` 與 `android/SaveTransferActivity.java`。Others原資料移轉入口改為Save Backup，使用系統文件選擇器匯出／匯入4MiB內的schema1備份；包括帳號JSON及角色保護、排序、聲音等本機設定。匯入有版本／雜湊／資料引用驗證與原生確認，覆蓋前保留可再次匯出的舊存檔，啟動前可回復中斷的匯入。限主選單、無進行中關卡，成功後關閉遊戲再開啟。功能已接入1.0.6及 [2.9.0 r17](v290/playable/README.md)。2.9.0 可單向轉入固定1.0.6 prototype22r7備份；1.0.6仍只接受同版本；[2.9.1](v291/playable/README.md) 另以schema2／dgdb-1接受舊備份單向轉入，其他版本尚未套用。
- prototype 22r6 的倉庫來源按鈕改為複製原生 `PagePartyBlendBuildUp/SwitchButtonClear`，使用參考截圖的 `buttonn_small` 背景與184×38碰撞框；按排序列原左側留白鏡像對齊右緣，文字改為置中。原 `LabelSetter` 的清除文字鍵清空，避免覆蓋倉庫切換文字。來源切換及五星以上篩選規則維持原設定。
- prototype 22r5 將標準及四個聯動池的五星總機率改為原始本機抽選實際機率的三倍，透過兩階段抽選保留各星等類別內的角色與加值權重。倍率基準在補入18種標準五星前擷取，並納入原抽選器首尾票號偏差；池內說明同步顯示三倍與實際百分比。
- prototype 22r4 新增 `AcquisitionOrder.cs`：只在 `ServerForgery.AddUnit` 建立新角色物件時記錄伺服器格式秒數，載入／存檔不補寫舊角色獲得時間。兩種共用獲得順序鍵改用秒級時間，同秒按原始倉庫插入順序區分；零時間舊卡仍按原圖鑑編號排列，其他排序保留。倉庫助戰候選增加目前 `rare >= 4` 條件，即五星以上；一般好友清單與強化流程不變。
- prototype 22r3 依使用者截圖修正真正的 Present Info「助人獎勵／友情點」自動通知。先前22r2只處理一般／卡池公告，未解決這張通知。現在僅消耗待顯示的助人計數，不變更FP入帳；無真正禮物的占位清單也不再開啟空通知。既有交付報告透過原本機 login-pack 入帳及回覆處理重現 6245+20=6265，再驗證100次返回與存檔餘額保留。
- prototype 22r2 補上先前漏攔的抽卡公告入口：一般公告與抽卡公告的自動分流均跳過。以交付22r1組件重現100次重設／返回仍出現抽卡公告，再驗證修正版100次返回及結算、退休、登入獎勵、友情點禮物路由；不以單純欄位檢查當作功能生效證據。
- prototype 22r1 取消 prototype 22 的強化頁倉庫選擇，移除相關橋接、按鈕及驗證掛鉤；一般關卡倉庫助戰保留。既有交付報告記錄強化頁及強化 API 的指令與原版一致。22r1 的歷史提交採其最終狀態，未另重建 prototype 22 的強化倉庫版本。
- prototype 22 新增 `AccountRules.cs`、`WarehouseSupport.cs`、`PatchRules.cs`：新舊帳號至少 Rank184／倉庫2000格，保留更高進度；API／前景／倒數同步180秒回體，停用自動營運通知。標準池補齊稽核列出的18種既有五星（合計42種），標準與四個聯動池依 prototype21 實際機率精確加倍。prototype 22 曾提供強化及一般關卡倉庫選擇，22r1 僅保留一般關卡；助戰角色不消耗，教學仍走原流程。細節見本版交付說明。
- prototype 13 的 `CollabGacha.cs` 將聯動入口切成四個原生卡片選項，返回可取消；只常駐四個內建聯動池；當時保留原權重／等級／加值。prototype 22r1 起的五星機率改由 `AccountRules.cs` 調整，22r5 使用三倍基準。每抽 5 枚，最多九連抽，原確認／刮卡程式未改動。四格共用原聯動圖片，以名稱區分。
- prototype 14 改為重用原三卡並只複製第四張，沿用原顯示父物件、圖層與深度，明確啟用／重設透明度／註冊 NGUI；返回與重新進入時還原原入口外觀。程式檢查不代表已確認實機顯示正常。
- prototype 15 在一般首頁刷新後及處理輸入前隱藏第四張卡片並停用選池返回碰撞區域，避免原頁動畫遞迴啟用造成殘留；重複關閉也會清理，原三卡的新餘額顯示保留。新增 100 次模擬啟用狀態循環檢查，原生動畫與觸控仍需實機驗證。
- prototype 16 在原外觀快照前、選池卡片啟用後及還原時，透過原 `UIButtonScale.OnHover(false)` 清除懸停，停止縮放 Tween 並將兩端設為快取正常尺寸；後續原按壓／懸停功能保留。100 次動畫狀態循環使用原版按鈕程式與引擎狀態替身，原生渲染／觸控時序仍需驗證。
- prototype 17 新增 `area_icons.py`，優先保留來源清單內的 Wiki PNG，其餘按 `res_map` 與 `res_map_icon` 製作 A 構圖裁切；84 個圖示涵蓋 101 筆有效區域。裁切替代圖不等於找回原版選關美術。
- prototype 18 從 `fandom-ui-images.json` 載入經核對的 Wiki `Empty-icon.png`，覆蓋 `chara_icon_empty` 並排除面板圖集內舊的同名來源；`lock1` 搜尋結果記錄在本次交付說明。
- prototype 19 新增 `backup_images.py` 與 `--backup-assets`，逐檔驗證備份，將六個原圖集的 JSON／UIAtlas 座標交叉核對後以原尺寸像素重新排圖。原 805 個框全數保留，另有共用 UI 框；五組角色需求中最後一組仍提供 Empty／Lock 等共用圖。
- prototype 20 新增 `native_bundles.py`、`NativeAssets.cs` 與 `--restore-native`。原 CAB 不改位元組，重包 UnityRaw v3 後由 APK 本機非同步載入，原模型／材質／動畫關聯保留；共用 mapMaterial 依模型包選取。原生角色跳過矩形陰影補丁，恢復原 Opaque 輪廓網格與陰影深度契約；missing/error/timeout 退回舊簡化路徑。
- prototype 21 新增 `backup_stages.py`、`BackupStages.cs`、`--backup-stages`。原 TextAsset 保留；相容備份表嵌入配接器，啟動時先全部解析，再同時交給本機 API 與選單。角色／技能／抽卡表不在本次匯入範圍。原地圖快照可能仍帶舊遭遇，更新後請重新進入關卡。資料範圍及必備盤點清單見 [移植指南第 7.7 節](../OFFLINE_PORTING_GUIDE.md#77-備份關卡設定prototype-21)。
- prototype 10 只取消活動區域時段／玩家分組、前置區域與正式難度順序限制；六種進化神殿常駐。教學、主線順序、體力、獎勵及既有通關紀錄維持原規則，不會修改全域活動日期表。
- 522 張立繪、522 個 128px 角色頭像及全部 84 張選關圖已改用原快取；Panel 原圖優先，189 個未找到備份對應的相容圖仍保留。圖片匯入階段不含主資料；prototype21 的關卡表另由 `BackupStages.cs` 套用。
- prototype 7 把備份中的 `chara_icon_empty.png` 補進五組角色圖集，修正未選好友／空隊伍格因透明空圖而露出白色底層的問題。
- prototype 8 保留跨圖集搜尋的「找不到」結果，讓 `InGameCard` 繼續搜尋真正包含敵人頭像的圖集；先前在第一組建立替代 Sprite 會使搜尋錯誤地成功。地板按 lower 圖片比例縮到棋盤遠端 z=2.5，避免與 upper 門景重疊。
- prototype 9 從 Fandom 公開圖片 CDN 取得七個城鎮門圖，對應 `area_icon_0001..0007`，保留原圖 207×131 的比例／像素。它們是 Wiki 圖片，未證明與原 1.0.6 圖集像素完全一致。
- PNG fallback 仍封裝 86 組地圖上下圖。prototype 20 原生路徑改用原模型與完整貼圖；萬聖節／聖誕節兩區與載入錯誤才回到原簡化顯示。第五監獄 `ef_stage_0012` 缺失，僅其環境效果 fallback；該區模型與動畫可接回。
- `panel_b/r/y_1..6` 已使用備份各自的原圖。`toaru_box_01 → toaru_box`、`toaru_key → toaru_Key` 等拼字別名優先指向原圖；`panel_alarm`／`panel_banana` 仍用舊相容素材。
- 優先清單 112 項已配接 109 項，仍缺 `mugen_box.png`、`panel_panic.png`、`panel_weekness.png`。四個 heal 原面板已找到，`happytresure` 配接至備份的 `happytreasure`。先前 [檔名對照](../artifacts/dgdb-name-mapping-2026-10-20/README.md) 為 prototype 5 的歷史結果。
- 保留 prototype 2 的地板碰撞盒修正；移除中央白色替代門方塊，原 Boss 入口判定不變。
- prototype 4 以立繪 Alpha 遮罩修復屬性陰影的有色矩形，使用 APK 內的透明 Shader 路徑。
- prototype 6 為敵人血條、回合數及狀態圖示複製專用 UI 材質，使用原 APK 內的 Overlay Shader（queue 3001），在角色 quad（3000）之後繪製；不更改原 Boss 尺寸、血量與選取碰撞。
- prototype 9 修正上述 Overlay 在選單上方顯示的副作用：選單開啟第一幀、選項／退休頁面、關閉過渡及對話框期間恢復原 NGUI 材質；回到一般戰鬥再恢復角色前方的 HUD。
- `res_chara_id_str` 與網站 `draw_id` 的對照由原始 1.0.6 表產生，沒有把新年代資料表覆蓋進舊版。
- 配接器圖片快取會在原場景資源清理時放開非駐留持有關係；實機顯示、點擊、長時間記憶體與全部關卡仍需驗證。

圖片來源與下載調查見 [WEB_ASSET_FINDINGS.md](WEB_ASSET_FINDINGS.md)、[CDN_FINDINGS.md](CDN_FINDINGS.md)；原圖鑑檢查見 [ENCYCLOPEDIA_AUDIT.md](ENCYCLOPEDIA_AUDIT.md)。原始 APK 和先前測試產物均保留。

Android 16 相容性分析見 [相容性報告](../artifacts/android16-compatibility-audit-2026-10-20/README.md)：目前 target SDK 17、僅 32 位元 ARM，舊引擎確有 text relocation，不能只提高 SDK 後便宣稱已支援新版。該分析不包含 Android 16 裝置測試。

獨立 ARM64 工程與進度見 [移植說明](arm64/README.md)：已保留六個場景與全部匯出動畫，855 份 C# 程式通過 Unity 2019 API 獨立編譯。Editor 因缺少授權尚未匯入，舊原生／Java 外掛亦待移植，尚無可安裝 ARM64 APK；prototype 17 未變更。

## 建置與驗證

建置入口為 `offline/build.py`，驗證入口為 `offline/test.py`；完整輸入、參數及各階段見 [移植指南第 11 節](../OFFLINE_PORTING_GUIDE.md#11-建置流程與輸入要求)。工具依賴可參考 [Dockerfile](../Dockerfile)，具體執行配置需依建置設定核對。

腳本仍有固定工作目錄及工具定位設定，尚未完成跨配置參數化。執行前須核對原始碼、版本相符的 APK、素材 manifests 及來源檔；不能將既有開發配置當成通用安裝方式。`recover_dgdb_variants.py` 只補充既有資料集，不是完整素材的初次下載入口。

每次使用新的產物與中間檔目錄，保留原 APK 及舊版。需要更新安裝時應維持相同簽署憑證，私鑰不放進倉庫。Android 驗證應優先確認角色顯示、教學 Boss 入口、結算、連續 UI 返回及重啟存檔，並記錄受測 APK hash、Android 版本與 ABI。

各 prototype 的提交拆分、重建範圍與查閱方式，請參考 [Prototype 提交索引](PROTOTYPE_HISTORY.md)。

本次提交整理僅核對靜態語法、差異、公開文件及排除項目，未重跑編譯或完整回歸測試；交付說明中的測試結果來自既有報告。


獨立的 [2.9.1年代內容還原設定r12](v291/restored/README.md) 以額外開關建立另一套件及schema3，擴至1748角色／921有效關卡。原r10白名單設定保留；新增掉落率來源為零、697角色缺取得來源、模型與配置替代等限制有明列。它是保守資料還原候選，既有容器檢查不等於Android完整遊玩。
