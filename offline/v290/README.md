# 2.9.0 離線化嘗試：診斷原型 1（歷史）

現行實作見 [2.9.0 r9 舊內容候選版](playable/README.md)。以下保留 2026-10-21 診斷版的安全檢查、缺失與驗證範圍；「目前」「尚未」均指當時的診斷產物，不是後續候選版狀態。

日期：2026-10-21。**這不是可玩的單機版。** 目前產物只提供不連網的離線診斷畫面，阻止缺資料時進入登入／創角流程。Android 啟動、畫面、觸控與遊戲流程尚未實機驗證；不能稱為移植完成。

另見 [三版比較與舊資料移植實驗](../comparison/README.md)：1.0.6 舊資料經有限轉換後，已在未修改的 2.9.0 原後端通過創角等 managed 探測；尚未接入本文的診斷 APK，也不是 2.9.0 完整內容復原。

## 修改前的安全檢查

輸入 `jp.gungho.dg-v2.9.0.apk`，35,409,743 bytes，SHA-256：

`3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4`

- `apksigner verify` 通過 v1 簽章；沒有 v2／v3。憑證 SHA-256 為 `9623d6804d17d3abe7741709ff7ce2bcb98b1fb0d0782c10dfc7d68569d00bbc`，與倉庫原始 1.0.6、2.1.0 完全一致。這是檔案間簽署者一致的證據；未取得可信官方 2.9.0 檔案 hash 或下載出處，不能把相同憑證當作獨立官方認證。
- 原 APK ZIP CRC、重複檔名與路徑檢查通過。
- ClamAV 1.4.3，病毒碼 daily 28159、main 63、bytecode 339，掃描原 APK 與全部解包檔案，1,579 個檔案未檢出感染。精確執行結果及病毒碼版本見 `artifacts/2.9.0-security-audit/`。病毒碼版本與掃描結果是當次紀錄；重新稽核前應更新病毒碼。
- Manifest 沒有簡訊、通訊錄、相機、麥克風、無障礙服務或裝置管理員宣告；有網路、付款、震動與外部儲存權限，並包含 PartyTrack、Google 等廣告／分析／登入 SDK。
- DEX 敏感 API 命中已追查：`DexClassLoader` 在 Google GMS `zzal`；`Runtime.exec` 在 PartyTrack SDK，其檢視的命令為讀取 PATH 的 `sh -c echo $PATH`。命中本身不構成惡意證據，也不代表這些 SDK 已經完整審計。
- 沒有上傳 APK 到任何外部服務，沒有在 Android 上執行原 APK。

**結論：本次檢查未發現已知惡意程式或惡意重簽跡象，但無法確認「絕對安全無毒」。** 單一防毒引擎可能漏報；沒有行為沙箱，也未逐一完整逆向所有 C#、DEX 及原生 SO。舊引擎／SDK 的漏洞風險仍存在。APK 簽章保障與防毒掃描的範圍各有不同，參見 [Android 簽章說明](https://source.android.com/docs/security/features/apksigning) 與 [ClamAV 格式及掃描限制](https://docs.clamav.net/)。

## 實際修改

輸出目錄：`artifacts/2.9.0-offline-diagnostic1/`，APK：`ディバゲ_2.9.0.apk`。來源原檔保留原位，內容不改。

- 使用獨立套件 `jp.gungho.dg.offline290diagnostic` 與容器內產生的測試憑證，不覆蓋官方套件或 1.0.6 原型。
- 移除網路、付款、外部儲存讀寫權限，以及指定付款／追蹤／通知／Google bridge 的 Manifest 元件。
- `LocalSaveManager.LoadFuncServerAddressIP` 固定空位址，保留原本機路由的前提；本次沒有啟動缺表的 `ServerForgery`。
- 保留 Resident 初始化，在其唯一 return 掛接診斷元件；標題選單停在明確缺資料畫面，不創建原登入／下載頁面。
- 停用商店 Start／Update 的 SDK 處理、外部 patch 更新，以及 `PlayGameServiceUtil`、`LocalNotificationUtil` 中的靜態 void 入口。並非已移除全部 SDK 程式碼。
- `mainData` 只修改 MonoManager 的新增程序集登錄，其餘 Unity 物件原始資料核對不變。
- 原 DEX、所有 SO、UnityEngine.dll、原圖像／音效／場景位元內容不變。沒有套用 1.0.6 的角色、關卡或抽卡數據。

確切修改方法見輸出的 `modified-methods.txt`，檔案差異、權限與簽章見 `build-report.json`、`manifest-summary.txt`、`signature.txt`。畫面為英文，以避免把缺少 CJK 字型另混為啟動失敗；中文缺失說明在本文件。

## 缺少的資料與素材

| 項目 | 已確認的缺失／影響 |
| --- | --- |
| 主資料 | `ServerForgery.Start` 請求 42 份 Resources 主資料，APK 內 18 個 TextAsset 中沒有任何 MasterData。無法從這個 APK 單獨還原角色數值、技能、敵人、關卡、成長與獎勵。 |
| 最初創角資料 | 缺 `MasterDataUserRank` 與 `MasterDataDefaultParty` 等；原 `UserParamCreate` 直接存取 rank 第一列，切空伺服器位址仍會失敗。 |
| 下載目錄 | 缺 `MasterDataAssetBundlePath`、對應更新版本與主資料 hash；無法列出該版本「全部應有」素材檔名。 |
| 角色／圖示 | 缺該版本完整原始 ResUnit／ResIcon 包、prefab、mesh、材質及映射；現有 DGDB／圖鑑圖片可作候選，但不能計算 2.9.0 精確覆蓋率。 |
| 關卡 | 缺該版本 ResMap 完整模型、門／地板動畫及對應原表。1.0.6 的簡化地圖與 86 組貼圖不是 2.9.0 完整場景。 |
| 音樂／音效 | APK 有 142 個 AudioClip，但這不代表下載音樂與音效完整；缺 ResSound 需求目錄與實檔對照。圖鑑有 51 首音樂候選，未在本次接入。 |
| 抽卡／活動 | 缺 ResScratch 下載包及 2.9.0 正式卡池、權重、活動日期／獎勵資料；沒有用猜測權重補成功回應。 |
| 帳號／存檔 | 原官方線上帳號與進度沒有復原；2.9.0 本機創角、保存／重讀及新增 API 尚待有資料後測試。 |
| 平台 | 僅 ARMv7，minSdk 9、targetSdk 22；資料中有 Unity 4.6.5p1 與 4.6.1p5 標記。Android 啟動與相容性尚未驗證，未提高 target SDK 冒充相容新版。 |

42 份 Resources 名稱、各主資料型別／欄位、保留的本機後端方法清單，在建置輸出的 `missing-data.json`。這是從 2.9.0 DLL 的 IL／欄位直接取得的清單，沒有直接使用 1.0.6 的定義。

倉庫的 `external/dgdb/database/dump_20170921.sql` 是其他日期的第三方資料，未證明與 2.9.0 相符，且缺少所需的完整登入／卡池／下載目錄組合。Wiki、圖鑑、DGDB 可提供部分圖片、文字或數據候選，但仍須逐表對照 schema、ID、枚舉與關聯。不能因素材數量多就視為已補齊。

最有用的後續輸入是**原 2.9.0 已完成下載的舊手機／模擬器快取、MasterData bundle、原 API 主資料回應與下載清單備份**。取得後才能列出缺少的每個角色／關卡素材 ID、驗證關聯，再依移植指南補本機初始化與資源載入器。不要提供帳密或私人憑證；本機帳號重建不需要官方登入憑證。

## 重現與驗證

所有指令都在依倉庫 Dockerfile 建立的隔離工具環境內執行，工作目錄設為倉庫根目錄，並先啟用包含 UnityPy 的 Python 環境。下列命令不指定使用者的容器名稱或主機路徑：

```sh
freshclam --stdout
python offline/audit290.py
python offline/v290/build.py
python offline/v290/verify.py
```

建置目錄已存在時須指定全新的 `--output`，不覆寫舊產物。腳本目前仍使用預設的容器內工作根目錄、工具、暫存及簽署儲存位置；執行前須依自己的工具環境調整相關常數，不能只改工作目錄就視為已完成設定。相關移植與路徑限制見 [公開移植指南](../../OFFLINE_PORTING_GUIDE.md)。解包／編譯暫存、私鑰與生成報告均不加入倉庫。防毒掃描通過只是建置前的一項條件，不能視為安全保證。

驗證腳本從**交付 APK**重新抽取 DLL，對照原 APK 檢查非目標方法、原後端、程序集引用、診斷內容、Manifest、檔案差異、簽章與對齊。這些是靜態／程式檢查；不等於原生 Unity 在 Android 上已成功載入，也沒有證實完整單機遊戲可玩。

本次已通過：7,580 個非目標方法內容保持一致、28 個方法與申報補丁清單相符、34 個配接器引用可對原 Unity／BCL 解析、交付 DLL 的缺表訊息包含全部 42 項，以及 ZIP、權限、元件移除、簽章與對齊檢查。原 APK SHA-256 再次核對不變。完整輸出在產物的 `verification.txt`。

交付 APK：35,873,967 bytes，SHA-256 `659c497d5cd2a4c5e44749bc39dc1d3a52e3fe19fd3e369a47c6d94042e70344`。簽署憑證 SHA-256 為 `84c4fc46f3e4165c38234b273230284333aa1814b29d97bbb6f0a74a5c83649b`，是本機診斷測試憑證，與官方簽章不同。

交付 APK 亦另經同一 ClamAV 病毒碼掃描，未檢出感染；輸出在 `postbuild-clamav.txt`。這仍是靜態掃描結果，沒有新增 Android 行為驗證。
