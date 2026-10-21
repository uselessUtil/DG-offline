# 2.1.0 離線移植嘗試：diagnostic prototype 1

核對日期：2026-10-21。**已製作離線診斷 APK，尚未完成可遊玩的單機版。**

原始 APK 有本機後端 `ServerForgery`，但沒有其要求的 **37 份主資料**。無法只靠切換 API 位址恢復創角、登入、關卡、抽卡及進度保存。此次沒有用 1.0.6 資料冒充 2.1.0，也沒有把空陣列或一律成功回應當成復原。

## 產物與使用範圍

- APK：`artifacts/2.1.0-offline-diagnostic1-verified/ディバゲ_2.1.0.apk`。
- 套件：`jp.gungho.dg.offline210diagnostic`，與官方版及其他離線版本隔離。
- 顯示名稱：`Divine Gate 2.1.0 Offline Diagnostic`。
- `versionName=2.1.0`、`versionCode=18` 沿用原值；測試簽章與官方簽章不同。
- 設計行為為啟動後顯示可捲動的英文缺失清單；此畫面尚未在 Android 確認。
- 不提供創角、戰鬥、抽卡或遊戲存檔；不能當成已完成的單機遊戲。

原始輸入為使用者提供的封存 APK，32,114,571 bytes；SHA-256：
`af40f477e5455caba4f4bbfa24d6fde94e174fc4614db39025c9411be207bb29`。
沒有下載新的遊戲 APK。產物大小、hash、實際修改清單以同目錄 `build-report.json`、`SHA256SUMS` 為準。

## 此次實際修改

| 檔案或流程 | 修改 |
| --- | --- |
| `LocalSaveManager.LoadFuncServerAddressIP` | 固定空字串，選擇原本機路由條件 |
| `ResidentManager.Start` | 保留原初始化，在唯一 return 加入診斷啟動；沒有建立缺表的 ServerForgery |
| `TitleMenuManager.Start/Update` | 保留 Singleton Start，顯示診斷並停止進入原登入／下載流程 |
| `PatchManager.Start/PatchUpdateRequest` | 保留 Singleton Start，停止啟動更新 coroutine 與更新請求 |
| `StoreManager.Start/Update/OnDestroy` | 保留 Singleton 生命週期，停用付款 SDK 初始化與更新 |
| `PlayGameServiceUtil`、`LocalNotificationUtil` | 停用原靜態 void 操作 |
| `DG210Diagnostic.dll` | 新增缺失清單畫面；以原裁剪 BCL／UnityEngine 編譯 |
| `mainData` | 僅在 MonoManager 登錄新增 DLL；核對其他物件原始資料不變 |
| Manifest | 改包名／名稱，移除網路、付款、外部儲存權限及指定付款／廣告／通知元件 |
| `lib/armeabi-v7a/libmono.so`、`libunity.so` | 取用同一 APK 自帶 `assets/libs/armeabi-v7a/` 真正引擎，替代舊代理載入器 |

原 APK、DEX、原 UnityEngine.dll 和遊戲素材保持原位元；原 `ServerForgery` 所有方法的 IL 內容不變。重新封裝與簽署只作用在新產物；中間檔與私鑰留在容器內，不加入倉庫。

`verified` 指封裝與 managed 檢查通過，不代表 Android 實機通過。初次候選曾因裁剪 API（whiteTexture、GUIStyle 字型設定）無法編譯；r3 候選的修改清單曾包含原本已為空的方法，未通過精確差異測試。最終補丁避開不可用 API 並略過原本空方法，不使用 r3 作交付。

## 缺哪些資料

完整 37 個 Resources 路徑由該 APK 的 `ServerForgery.Start` IL 提取，見產物 `MISSING_DATA.md`。可反推出欄位型別，**不能由程式反推出歷史資料列**。`missing-data.json` 附本機欄位、37 張表的欄位 schema 與本機 handler 清單；它是需求文件，不是可匯入的遊戲資料。

| 用途 | 缺少的主要表 |
| --- | --- |
| 創角／成長 | DefaultParty、UserRank |
| 角色／技能／進化 | ParamChara、ParamCharaEvol、SkillActive／Leader／Passive／LimitBreak |
| 地區／關卡／面板 | Area、Quest、QuestFloor、CategoryPattern、ExpectPattern、ParamPanel、ParamPanelGroup |
| 敵人／戰鬥 | EnemyChara、EnemyGroup、EnemyActionTable、EnemyActionParam、StatusAilment |
| 活動／新手 | EventBase、AreaAmend、GuerrillaBoss、BeginnerBoost |
| 抽卡 | Gacha、GachaAssign；沒有相符池／權重資料 |
| 登入獎勵／禮物／成就 | LoginChain、LoginTotal、LoginEvent、LoginMonthly、Present、PresentGroup、Achievement |
| 素材定位／公告／商品 | AssetBundlePath、Information、StoreProduct、StoreProductEvent |

名稱表格省略共同的 `MasterData` 前綴。`ServerForgery.Start` 雖建立長度 38 的暫存陣列，但索引 17 沒有載入；**實際要求 37 份，不能算成 38**。主資料缺失已從全部 Unity 序列化檔案盤點，15 個 TextAsset 中沒有 MasterData；不是只看 ZIP 檔名。

具體失敗點：`UserParamCreate` 直接讀取 `m_MasterDataUserRank[0].stamina` 等欄位。隔離的 managed 測試執行實際交付 APK 的原後端，確認缺表會拋 NullReferenceException；空表則拋 IndexOutOfRangeException。測試使用引擎替身，不代表 Android 實機結果。

## 缺哪些素材

| 原下載分類 | 仍需補齊及核對 |
| --- | --- |
| `Android/MasterData0001` | 2.1.0 相符主資料包與素材目錄；最高優先 |
| `Android/ResUnit0001/QUALITY_1_*` | 角色立繪、prefab、Mesh、材質及品質級別對照 |
| `Android/ResIcon0001` | 角色／區域／面板圖集、Sprite 名稱與裁切座標、活動 UI |
| `Android/ResMap0001` | 關卡模型、貼圖、UV、門與房間動畫及材質關聯 |
| `Android/ResSound0001` | 外下載 BGM／SE 與對照表；原 APK 已有 142 個 AudioClip，不能說全部音訊都缺 |
| `Android/ResScratch0001` | 刮卡演出及相關素材 |

APK 已有大量 UI、特效與動畫，但不等於下載素材齊全。缺少 `MasterDataAssetBundlePath`、角色及區域資料列，**尚不能精確計算缺少幾張角色圖、幾個地圖包或列出完整缺檔名單**；不能沿用 1.0.6 的 522 角色／84 缺圖數字。

倉庫既有素材可作後續候選：

- CDN 封存中有 2.1.0 路徑 `Unit_0973.unity3d`，SHA-256 `ea108a7227bce79cd6b61ee74f399e55cf46e27ea35a26a677e285f3c920386f`。僅一個角色 bundle，未接入本診斷 APK。
- 另一包 `Unit_000_009` 來自 1.1.0 路徑；不能稱作 2.1.0 完整原版素材。
- `external/dgdb/`、Wiki 備份、圖鑑 XAPK 有大量候選美術；尚未建立 2.1.0 `fix_id → draw_id → resource_id` 映射、覆蓋率與載入配接。
- 網頁立繪 PNG 無法補出關卡幾何、動畫、戰鬥數值或抽卡規則。

歷史 CDN 地址與封存範圍見 [CDN_FINDINGS.md](../CDN_FINDINGS.md)，其原站連線結果是 2026-10-20 的紀錄，本次未重新探測網路，不宣稱它們的即時狀態。

## 建置與驗證

所有指令均在依倉庫 Dockerfile 建立的隔離工具環境內執行。先啟用包含 UnityPy 的 Python 環境，並以倉庫根目錄作為工作目錄：

```sh
python offline/v210/audit.py
python offline/v210/build.py
python offline/v210/verify.py
```

再次建置必須以 `--output artifacts/<新目錄>` 指定不存在的目錄，防止覆寫舊產物。腳本目前仍使用容器內工作根目錄、工具、暫存與簽署儲存位置的預設常數；執行前須依自己的隔離環境調整，不能只切換工作目錄。依賴與路徑限制請參閱倉庫根目錄的 `OFFLINE_PORTING_GUIDE.md`。此診斷版不需要下載外部遊戲素材。測試私鑰不加入倉庫，需自行保存；更換私鑰即無法直接更新既有診斷安裝。

驗證從真正交付 APK 取 DLL，檢查原方法差異與掛鉤、原 BCL 相容性、診斷訊息、缺表／空表失敗、原始 hash、ZIP、實際修改檔案集合、MonoManager、原生引擎來源、權限、簽章與對齊。詳細結果在 `verification.txt`，缺表行為另在 `missing-data-probe.txt`。

**未完成 Android 安裝、啟動、觸控、飛航模式、Unity 渲染及遊玩驗證。** 原版為 Unity 4.1.0f4、armeabi-v7a、min SDK 9／target SDK 17，並未轉成 arm64。其他版本的截圖或測試不能證明此版已通過 Android 驗證。

後續最有效的輸入是保留過完整下載資料的 **2.1.0 舊手機／模擬器備份**，尤其主資料包、AssetBundle 目錄與快取。補齊後應先驗證創角→教學→結算→重啟保存，再按 2.1.0 自己的表配對素材與原 UI；官方帳號、真實好友與歷史營運狀態仍需額外資料及實作。
