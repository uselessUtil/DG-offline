# 2.9.0 舊內容離線候選版 r10

本版使用原始 2.9.0 程式、引擎及常駐資源，接入舊 `jp.gungho.dg` 快取內容，並移植 1.0.6 prototype 22r6 的帳號、倉庫助戰、獲得排序、免費商店及卡池規則。不是官方 2.9.0 全內容復原，也不會恢復原線上帳號。

原創工具沿用專案 [MIT 授權](../../../LICENSE)；原遊戲及素材範圍見 [第三方權利聲明](../../../THIRD_PARTY_NOTICES.md)。APK、快取、報告與簽署材料不隨 Git 提供。

## 候選產物與來源

- 最新產物：`artifacts/2.9.0-offline-playable1-r10-final/ディバゲ_2.9.0.apk`。
- 套件：`jp.gungho.dg.offline290`，獨立於原版與其他版本；更新既有候選版須使用相同簽署憑證。
- 大小：221,854,287 bytes。
- SHA-256：`f01e85dddcc784a121cfb9072f2b38eca04443a509965946e2772206867a19bf`。
- 原始 2.9.0 APK：35,409,743 bytes；SHA-256 `3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4`。
- 卡池 donor：原始 1.0.6 APK；SHA-256 `ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2`。
- 資料／素材：舊快取的逐檔盤點清單及來源檔，包含 `artifacts/cache-backup-audit-2026-10-22/source-files.json`、`objects.json`。DG0 備份不是本版主資料來源。

r10 身分及結果見 artifacts/2.9.0-offline-playable1-r10-final 下的 REPORT.txt、build-report.json、test-report.json、layout-regression.json 與 android-smoke.json；報告需另備。r9-final 保留為前版基準。

## 實作位置與行為

| 位置 | 功能及界線 |
| --- | --- |
| `assets.py`、`donor.py`、`data.py` | 核對來源與鎖定的兩張卡池表，保留舊 ID。595 個有效角色、399 列任務；任務含停用／預留列，不全是可玩關卡。舊 Area 音樂欄位移除，音樂包名留空；Event 對應後端 EventBase，再投影客戶端 Event。60 列 CategoryPattern 保留 cate0..4，補 cate5=1，總數由 24 變 25。 |
| `Runtime290.cs`、`Patch.cs`、`CompileApi.cs` | 等待主資料、時間及存檔管理器就緒才建立本機後端，保留 SERVER_SAVE 與原關卡開始／結算。本機資料就緒才存 Android 更新標記 offline290-1；原更新判斷保留。停止新帳號示範禮物、自動營運摘要及缺資料頁面；r10 將成就結算過渡頁接往原訂下一頁，避免成功賣出後停留。 |
| `LegacyFloor290.cs`、`PatchQuest290.cs` | 以原版 1.0.6 的兩組各 100 次交換、事件調整及起點排除，生成一般關卡的星數與事件配置。沿用 2.9.0 敵人／道具／陷阱生成及封包；問號遮罩沿用舊規則，但不是完整 1.0.6 RNG 消耗序列的逐位元重播。已存地圖不重抽，三個固定教學配置不變。 |
| `Skills290.cs` | 只對已匯入的舊必殺技接初始化、傷害、狀態邊界；普通技能走原 2.9.0。初始化成功不代表所有戰鬥效果等價。 |
| `native.py`、`nativeconvert.py` | 將 Unity 4.1 原生物件逐項轉成 4.6.5p1 序列化格式，補新欄位並核對投影後內容讀回。保留貼圖、網格、動畫內容與參照；不能只改 hash。90 包覆蓋 595 個角色與 106 個地圖資源組合，1377 PNG 作備援。 |
| `text-fallback.json`、`text_coverage.py`、`TextKeys.cs` | 保留 1561 筆常駐文字，另撰寫 51 筆日文缺鍵補充，核對程式字面鍵與序列化 UI。補充不是找回的官方文字；動態鍵與未到達流程仍需驗證。 |
| `AccountRules290.cs`、`PatchRules290.cs` | 新舊帳號至少 Rank 184／倉庫 2000，保留更高進度；同步 Rank 經驗、成本與體力欄位，僅首次不足 Rank 的遷移提高目前體力。180 秒回體並處理等於時間邊界。 |
| `AcquisitionOrder290.cs` | 新角色記錄獲得秒數，同秒按插入順序；r10 修正時間鍵方向以符合原生列表降冪比較器，新角色排在舊角色前。舊角色不補寫時間，保留隊伍優先規則。 |
| `WarehouseSupport290.cs` | 一般關卡第五位可用自己倉庫中目前五星以上角色，排除當前隊伍實例；同角色另一張可用。進關再次驗證所有權及資格，不消耗角色／不給助人 FP。教學與強化頁使用原流程。按鈕重用原紫色 buttonn_small 背景與 184×38 碰撞框。 |
| `Gacha290.cs`、`PatchGacha290.cs` | 教學、友情、標準及四個聯動共七池。沿用原四分頁並切換一般／聯動模式，四聯動各佔一頁。確認、連抽、刮卡使用原流程。 |
| `Commerce290.cs`、`FreeChipShop290.cs`、`PatchCommerce290.cs` | 免費領取 1／6／12／25／52／85 枚；總額 999，超額整包拒絕，只增加免費餘額，一次確認只發一次，存檔失敗回復餘額。抽卡扣款前拒絕滿倉、0 次或超過 9 次。 |
| `build.py` | 組合共用 1.0.6 Runtime／NativeAssets 的有限轉換及本版配接器，RVA 封裝資料與圖片，掛接明確方法並產生獨立 APK；不替換原 Unity 引擎。 |

標準池加入 18 種既有五星，合計 42 種。五星總機率是 prototype 21 原抽選器實際票號機率的三倍；補角前擷取基準，類別內角色／加值權重保留。

| 池 | 原票號基準 → 三倍 | 最終五星機率 |
| --- | --- | --- |
| 標準 | 1033/15814 → 3099/15814 | 19.596560% |
| FAIRY TAIL | 999/10000 → 2997/10000 | 29.970000% |
| 進擊的巨人 | 62/1244 → 186/1244 | 14.951768% |
| ケリ姫スイーツ | 62/1244 → 186/1244 | 14.951768% |
| 魔法禁書目錄 | 60/1000 → 180/1000 | 18.000000% |

一般代幣池每抽 5 枚、最多九連抽；教學使用原關卡給的 5 枚，沒有額外發幣。下載式宣傳圖缺失，以舊角色立繪、池名及機率文字展示，並非官方活動宣傳圖。

## 缺失與停用項目

原 APK 缺 42 份本機後端主資料；本版匯入／產生 31 表，另 11 表以空集合處理：LoginMonthly、Present、PresentGroup、Achievement、SkillBoost、AssetBundlePath、Information、EnemyAbility、BeginnerBoost、QuestRequirement、AreaCategory。舊內容不依賴的機制保持停用，不能視為新版完整服務已還原。

有效舊活動常駐，主線順序保留；月登入、成就、自動公告、付款、外部登入及外連停用，移除相關網路／付款權限。既有帳號禮物、貨幣及角色保留，停止新帳號注入原本機後端示範贈禮。

音訊使用 APK 常駐對應，備份中舊地區音樂欄位全空；未復原下載活動音樂，也未證明與早期版波形相同。部分素材仍走共用簡化 fallback；頂部 STAMINA／EXP 量表圖樣重疊尚未修正。僅 ARMv7，引擎及裝置相容性仍有限。

## 既有驗證範圍

既有 r9-final 報告從簽署 APK 重新抽取組件，記錄引用、方法白名單（7522 個未改／86 個明確修改）、ZIP／簽章／對齊、角色成長、392 個有樓層任務生成、459 筆技能初始化、230 筆傷害類別／開關及 112 筆狀態類別檢查。傷害檢查有容差；API 生成／結算不等於實際戰鬥。

其他報告記錄帳號遷移及回體邊界、七池授卡、獲得排序、五星助戰、五池逐票號機率、商店防重複／跨程序重載／存檔失敗回復，以及 389 個一般任務／760 層鑰匙。更新標記測試的 SERVER_SAVE 不變指已套用帳號規則之後，並非宣稱 Rank 遷移前後整份存檔相同。

Android 既有冒煙檢查為 Android 12／API 32、具 ARM 轉譯的測試環境：最終 r9 的首頁、標準池／四聯動分頁、免費商店確認取消，以及門動畫與原生角色載入有紀錄。倉庫按鈕畫面取自初始 r9 候選，最終包核對同來源及 prefab，未宣稱最終包倉庫觸控已再測。沒有在玩家存檔領幣、抽卡或開始關卡；完整實戰、連抽動畫、音訊及長時間遊玩待驗證。

## r10 既有驗證紀錄

既有報告記錄從最終簽署 APK 重抽 DLL，20 組主回歸通過，另通過鑰匙及更新循環回歸。40320 組星數／事件配置逐一符合原版 1.0.6 抽出的 697 條 IL；389 個一般任務重複 10 輪共 7600 層，鑰匙涵蓋起點以外 24 格。以 r9 實際 APK 產生進行中存檔，換 r10 跨程序載入與重建地圖後，SERVER_SAVE 及關卡封包逐位元組相同。新增排序檢查使用原生列表比較器，補足 r9 測試未核對顯示方向的缺口。

MuMu Android 12 隔離診斷套件重現賣出成功仍停留；修正後原生「是／否」均可返回列表，好友申請「是／否」均可回首頁。診斷套件遊戲 DLL 與最終 APK 完全相同，配接器另加測試入口與輸入紀錄。取消賣出及好友無反應的特定玩家情境尚未重現，不能宣稱已全部排除。上述交易均在獨立測試帳號進行。

## 建置與檢查入口

工具在依 [Dockerfile](../../../Dockerfile) 建立的容器內執行，工作目錄為倉庫根目錄，先啟用含 UnityPy 的 Python 環境：

```sh
python offline/v290/playable/build.py --output artifacts/NEW-290-CANDIDATE
python offline/v290/playable/test.py artifacts/NEW-290-CANDIDATE/build-report.json
python offline/v290/playable/test_keys.py artifacts/NEW-290-CANDIDATE/build-report.json
python offline/v290/playable/test_update_loop.py artifacts/NEW-290-CANDIDATE/build-report.json
python offline/v290/playable/test_layout.py artifacts/NEW-290-CANDIDATE/build-report.json
```

輸出須全新，不覆寫舊 APK。需要來源快取、盤點清單及兩份雜湊相符的 APK；僅 Git checkout 不足以建置。腳本仍有固定工作根目錄、Mono／Cecil、暫存及簽署設定，執行前須依自己的容器配置核對，不是通用安裝命令。

`--resume-work` 僅續做同一輸出、尚未產生 APK 的未完成建置；`--assets-from` 可重用同來源且清單相符的既有資產，仍依賴前次中間檔。原生轉換 revision 不符時另建新副本，不改寫舊包的硬連結。獨立驗證入口也需要 build-report.json 指向的原始組件等中間檔；清除暫存後需重建。測試替身與故障注入只用於暫存測試副本，不封裝進 APK。

## 歷史範圍

- 診斷原型 1：只呈現缺資料訊息；見 [歷史診斷文件](../README.md)。
- 舊內容 r4：教學池／核心資料與圖片備援，十筆補充文字；當時一般抽卡與商店停用。尚未真正完成 Unity 4.6 原生格式轉換。
- r5～r7：素材驗證中間候選。
- r8：逐物件轉換 Unity 4.6 格式，補充文字擴至 51 筆，取消血條強制置頂、示範贈禮及自動摘要。
- r9／r9-ui1／r9-final：移植 22r6 規則及本版 UI；保留 r9-final 作為前版基準。
- r10：原版 1.0.6 關卡配置適配、取得時間排序方向修正、停用成就過渡頁的正常接續。

Git 提交依目前維護來源拆分功能，未重建每個中間 APK 的精確來源快照。

本次提交整理只核對靜態語法、差異、文件及來源指紋，未重新編譯或執行完整回歸。test_layout.py 另依賴原始 1.0.6 APK、候選版中間表，以及另備的 r9-final APK／build-report.json，供跨版本存檔比較；不會讀取玩家裝置存檔。
