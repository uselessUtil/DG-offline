# 1.1.0 離線化嘗試：診斷原型 1

日期：2026-10-21。輸入 `sczm/184044_sczm.apk` 實際為 **1.1.0 / versionCode 14**。

**目前交付的是離線診斷 APK，不能遊玩。** 原 APK 的 `ServerForgery` 需要 36 份 Resources 主資料，但 APK 的 16 個 TextAsset 中沒有任何 MasterData。僅切換空 API 位址會在創角時因缺表失敗，無法單靠這個 APK 復原完整單機版。

修改前已完成 [靜態安全檢查](SECURITY_REVIEW.md)，未檢出已知感染或惡意重簽證據；不能保證絕對無毒。未執行原 APK、未上傳外部服務。安全證據在 `artifacts/1.1.0-security-audit/`，原檔保留不變。

## 產物與修改

輸出 `artifacts/1.1.0-offline-diagnostic1-verified/ディバゲ_1.1.0.apk`。使用獨立套件 `jp.gungho.dg.offline110diagnostic` 與容器內的本機測試簽章，不覆蓋官方遊戲與其他離線原型。

- 移除 INTERNET、ACCESS_NETWORK_STATE、BILLING、READ_PHONE_STATE 與外部儲存讀寫權限，移除指定付款、廣告追蹤接收器、Google metadata 與 URL bridge 的 Manifest 登錄。
- API 位址固定空字串；保留本機後端原方法，但缺資料時不啟動它，不製造虛假的成功回應。
- 標題初始化改為診斷畫面，列出全部 36 份缺失表，阻止原登入／下載／創角流程。保留 Resident 初始化並掛接診斷元件。
- 停用 patch 更新協程、商店 SDK Start／Update／OnDestroy、外部登入的靜態 void 入口；停用裝置資訊的外部儲存初始化及 keychain 可用判定。本版不存在新版的 `LocalNotificationUtil`，沒有對不存在的型別套補丁。
- MonoManager 只新增診斷 DLL 登錄，其餘 Unity 物件原始內容逐一核對。
- 用**本 APK 自帶的真實 libmono.so／libunity.so**取代安裝路徑的舊代理載入器；保留原引擎位元內容。原 DEX、UnityEngine.dll、其他 SO、音訊與美術不改。

這些處理沒有刪除所有 SDK 程式碼，也沒有恢復任何官方線上帳號。畫面使用英文以避開 CJK 字型依賴；完整中文缺失與限制記在本文。

## 缺失的資料與素材

| 類別 | 已確認缺口與影響 |
| --- | --- |
| 核心主資料 | 36 份全部缺失：角色數值／進化、四類技能、成長、初始隊伍、敵人／敵群／行動／狀態異常、地區／任務／樓層、面板、活動、抽卡與分配、登入獎勵、成就、禮物／商店、公告及下載目錄。精確 Resources 名稱與欄位見產物 `missing-data.json` 與 `MISSING_DATA.md`。 |
| 創角／帳號 | 原 `UserParamCreate` 直接讀 `m_MasterDataUserRank[0]`；缺 rank 與 starter 表便不能建立正常帳號。官方帳號、好友、進度與營運回應也未復原。 |
| 角色／圖示 | 缺與 1.1.0 對應的下載 bundle、原 prefab、mesh、材質、圖集與資源 ID 映射；DGDB／Wiki／圖鑑只能當候選來源，未在本產物接入。 |
| 地圖／演出 | APK 有部分共用模型／動畫，但缺該版本下載的地圖／場景包及表列關聯，不能視為完整關卡素材。1.0.6 的簡化地圖不是本版原模型。 |
| 音效／音樂 | APK 內盤點有 142 個 AudioClip；無版本相符的下載目錄與快取，不能確定下載 BGM／SE 缺少的曲目與精確數量。 |
| 抽卡／活動 | 缺本版正式卡池、權重、時間／獎勵、scratch 下載演出與素材；不以猜測資料宣稱復原。 |
| 素材總清單 | `MasterDataAssetBundlePath` 同樣缺失，所以目前無法列出完整應有檔名或可信覆蓋率。原碼中的 CloudFront 1.1.0 路徑只證明下載定位邏輯，未聯絡舊 CDN，也不代表該處仍有檔案。 |
| 平台／測試 | ARMv7、Unity 4.1.0f4／4.1.0b3 標記、minSdk 9／targetSdk 17。Android 安裝、原生引擎啟動、畫面、觸控與相容性尚未驗證。 |

最有效的補充是 **1.1.0 已下載完成的舊手機／模擬器快取、MasterData bundle、原 API 主資料與下載清單備份**。不需要帳密或私人憑證。1.0.6 內建主資料可供明確標示的舊內容移植實驗，但無法補出 1.1.0 新增的正式內容；需逐表對照後才能接入。

## 舊內容移植實驗

`probe.py` 已在**未修改的原 1.1.0 遊戲 DLL**上執行受限的 1.0.6 舊內容實驗，結果在 `artifacts/1.1.0-old-content-probe1-clean/`。28 張舊表經原 LitJson 解析；只有 Area 的舊音樂欄位遭拒，依目標 schema 過濾後可讀取。僅套用 EnemyChara → ParamEnemy 名稱映射、移除目標不存在欄位，沒有捏造缺表、技能值或成功回應。

通過：使用舊 rank 創角、原必殺技能轉換、USER_CREATE、SELECT_DEF_PARTY（5 個單位，ID 均存在）、GET_LOGIN_PACK，以及同程序記憶體 PlayerPrefs 的姓名／單位存檔往返。API 回應碼均為原 SUCCESS 4096。兩個原遊戲 DLL 的 hash 前後不變；Unity 原生 I/O 僅在一次性副本換成測試替身。

1.0.6 來源仍沒有下列 **8 種** 1.1.0 需要的資料型別：

- `MasterDataLoginMonthly`
- `MasterDataPresent`、`MasterDataPresentGroup`
- `MasterDataAchievement`
- `MasterDataStoreProductEvent`
- `MasterDataEnemyActionTable`、`MasterDataEnemyActionParam`
- `MasterDataStatusAilmentParam`（Resources 名稱為 `MasterDataStatusAilment`）

這是「舊內容可作移植起點」的證據，不是 1.1.0 原表復原；舊表的列數／年代也不同。缺表在實驗中保留 null，並未驗證所有活動、禮物、敵人技能、戰鬥或整個登入回應。**本次診斷 APK 未嵌入這些舊資料與素材**。Android 上的完整客戶端流程、磁碟／跨程序保存、觸控、渲染與關卡仍未測試。

## 重現與驗證

所有命令都在依倉庫 Dockerfile 建立的隔離工具環境內執行。先啟用包含 UnityPy 的 Python 環境，並以倉庫根目錄作為工作目錄：

```sh
python offline/v110/audit.py
python offline/v110/build.py
python offline/v110/verify.py
python offline/v110/probe.py
```

腳本仍使用容器內工作根目錄、工具與暫存位置的預設常數；執行前須依自己的隔離環境調整，不能只切換工作目錄。依賴與路徑限制請參閱倉庫根目錄的 `OFFLINE_PORTING_GUIDE.md`。再次執行建置或移植實驗時，請使用 `--output` 指定不存在的新目錄，避免覆寫既有產物。

`build.py` 要求精確來源 hash、成功的掃毒紀錄與安全檢查說明檔案；安全說明必須由操作者實際閱讀，檔案存在本身不構成安全認證。存在的輸出目錄不覆寫。產物含 `SHA256SUMS`、`build-report.json`、簽章、Manifest、修改方法清單與主資料 schema；私鑰、解包與測試副本均留在容器暫存／專用儲存區，不能提交 Git。

驗證器從**交付 APK**重新抽取 DLL，比對修改清單外的方法不變、原本機後端保留、配接器引用可對原 Unity／BCL 解析、全部 36 項缺失均出現在診斷訊息，並檢查 ZIP／權限／元件／MonoManager／原生引擎來源／簽章／對齊。另用一次性引擎替身重現真實 `UserParamCreate` 的缺表錯誤；替身與測試程式不進 APK。

靜態與 managed 檢查不能替代 Android 實機驗證。完整通過結果與產物 hash 以輸出目錄的 `verification.txt`、`build-report.json` 為準。

本次交付驗證通過：**4,757** 個非目標方法保持不變、**18** 個方法符合修改清單、**32** 個診斷配接器引用可對原 Unity／BCL 解析，全部 **36** 項缺失均列入畫面訊息。ZIP、原檔保存、原生引擎來源、權限／元件移除、程序集登錄、簽章與對齊均通過。修改後 APK 另經 ClamAV 掃描，感染 0；輸出在 `postbuild-clamav.txt`。

交付 APK：**36,999,501 bytes**，SHA-256：

`b56d671b41b198e35bd107331a05239adf7af9432cdbb5740bf59bce062e4404`

本機測試簽署憑證 SHA-256：`da7549cfac81d788678f7fcae344c2ccc16f8f65f3347644774cfbf200fe5f19`，與來源簽章不同。Android 安裝與啟動尚未驗證。
