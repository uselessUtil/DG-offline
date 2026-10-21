# 1.0.9 單機化嘗試：離線診斷原型 1

日期：2026-10-21。輸入 `sczm/179978_sczm.apk` 實際為 **1.0.9 / versionCode 13**。

**本次交付離線診斷 APK，仍不能遊玩。** 已完成修改前的 [靜態安全檢查](SECURITY_REVIEW.md)，未檢出已知感染或可確認的惡意修改，不能保證完全無毒。原本機後端 `ServerForgery` 要求的 **34 份 Resources 主資料全部缺失**；APK 的 16 個 TextAsset 沒有 MasterData。僅將 API 位址設空無法建立可用遊戲帳號。

## 產物與修改

輸出：`artifacts/1.0.9-offline-diagnostic1-verified/ディバゲ_1.0.9.apk`。獨立套件 `jp.gungho.dg.offline109diagnostic`，使用容器內本機測試簽章；原檔與官方套件不覆寫。

- API 位址固定空字串；標題流程改成可捲動的缺失資料診斷畫面，阻止原登入、下載與創角流程。
- 停用更新協程、商店 SDK Start／Update／OnDestroy、外部登入的靜態 void 入口，以及 AndroidDeviceInfo 外部儲存初始化／keychain 可用判斷。
- 移除網路、付款、電話與外部儲存讀寫權限，以及指定付款、廣告追蹤接收器、Google metadata 與 URL bridge 的 Manifest 登錄。沒有刪除所有 SDK 程式碼。
- 保留原 Resident 初始化與 `ServerForgery` 方法；沒有注入假主資料或強制成功回應。
- MonoManager 僅登錄新增 `DG109Diagnostic.dll`，其餘 Unity 物件原始內容保持一致。安裝位置的舊代理引擎改用**同一 APK 自帶的真正 ARMv7 libmono.so／libunity.so**，不修改引擎本體。
- 原 DEX、UnityEngine.dll、其他 SO、美術及音訊內容保留。反編譯原碼、解包、引擎替身及私鑰留在容器暫存／專用儲存區，不能提交 Git。

## 缺失的東西與素材

| 類別 | 缺失與影響 |
| --- | --- |
| 核心主資料 | 34 份全部未內建：初始隊伍、地區／任務／樓層、角色／進化／成長、四類技能、敵人／敵群／行動／狀態異常、面板、活動／亂入、抽卡與分配、登入獎勵、商店／活動商品、公告及下載目錄。精確 Resources 路徑及型別欄位由原 DLL 產生，見產物 `MISSING_DATA.md`、`missing-data.json`。 |
| 創角與進度 | `UserParamCreate` 直接讀取 `m_MasterDataUserRank[0]`；無表為 NullReferenceException，空表仍為 IndexOutOfRangeException。官方帳號、好友、營運回應及歷史進度未復原。 |
| 角色美術 | 缺版本相符的下載 bundle、角色 prefab、mesh、材質、圖集及角色／資源 ID 映射。現有 DGDB、Wiki、圖鑑是候選來源，本診斷版尚未核對接入，不能把 PNG 當成完整原 prefab。 |
| 地圖／演出 | APK 有 196 個 Mesh、646 個 AnimationClip 與共用場景物件，但沒有可用主資料／下載清單證明完整。缺下載地圖、場景、材質及任務關聯；物件數不等於內容覆蓋率。 |
| 音樂／音效 | 內建 142 個 AudioClip，缺版本相符的 BGM／SE 下載清單及快取，無法確認應有曲目、缺少檔名或精確數量。 |
| 抽卡／活動 | 缺本版正式卡池、權重、活動時程／獎勵與 scratch 下載素材及演出；不能由舊版表或猜測還原本版正式設定。 |
| 完整素材清單 | `MasterDataAssetBundlePath` 也缺失，因此目前無法列出完整應有檔名或可信百分比。程式中的 1.0.9 CDN 路徑只證明定位邏輯，未查詢該 CDN，也不代表檔案仍存在。 |
| 執行與相容性 | ARMv7、Unity 4.1.0f4／4.1.0b3 序列化標記、minSdk 9／targetSdk 17。Android 安裝、原生啟動、畫面、觸控、戰鬥及持久存檔尚未驗證。 |

最有幫助的補充是 **1.0.9 已下載完成的舊裝置／模擬器快取、MasterData bundle、主資料回應與下載目錄備份**。不需要帳密或私人憑證。倉庫 1.0.6 表可做明確標示的舊內容相容實驗，不能冒充 1.0.9 原始完整資料。

## 1.0.6 舊內容相容實驗

已對**未修改的原 1.0.9 後端 DLL**做一次性 managed 實驗，結果在 `artifacts/1.0.9-old-content-probe1-verified/`。Unity 原生 I/O 用一次性引擎替身、PlayerPrefs 用記憶體字典替代，這些測試程式沒有放入 APK。遊戲兩個 DLL 在實驗前後的 hash 相同。

- 舊版 28 張表全部可由 1.0.9 原 LitJson 直接解析，不需要刪除欄位；僅將舊檔名 `MasterDataEnemyChara` 對應目標型別 `MasterDataParamEnemy`。
- 通過舊 rank 創角、必殺技表轉換、USER_CREATE、SELECT_DEF_PARTY（5 個單位，ID 均存在於舊表）及同程序記憶體存檔往返。兩個測試 API 均回傳原 SUCCESS 4096。
- 1.0.9 原創角流程的 `present_list` 此時為 null；測試只核對已初始化的禮物，不捏造空清單，也不宣稱禮物系統已正常。
- **GET_LOGIN_PACK 仍失敗**：原 `CreateLoginMonthlyList` 因缺 `MasterDataLoginMonthly` 拋出 NullReferenceException。這與 1.1.0 的測試結果不同，不能由其他版本通過推定本版通過。

相對於 1.0.6 來源，仍無以下 **6 種** 1.0.9 資料；實驗前後保持 null，沒有以空陣列或猜測值補上：

1. `MasterDataLoginMonthly`
2. `MasterDataLoginPresent`
3. `MasterDataStoreProductEvent`
4. `MasterDataEnemyActionTable`
5. `MasterDataEnemyActionParam`
6. `MasterDataStatusAilmentParam`（Resources 名稱為 `MasterDataStatusAilment`）

因此，1.0.6 資料可作舊內容移植起點，**仍不足以直接登入 1.0.9**。本次診斷 APK 未嵌入舊表或既有圖片；沒有驗證完整客戶端登入、戰鬥、技能效果、活動、禮物、Android 磁碟／跨程序存檔或素材載入。28／34 也不是完整內容覆蓋率。

## 重現與驗證

所有命令均在依倉庫 Dockerfile 建立的隔離工具環境內執行。先啟用包含 UnityPy 的 Python 環境，並以倉庫根目錄作為工作目錄：

```sh
python offline/v109/audit.py
python offline/v109/build.py
python offline/v109/verify.py
python offline/v109/probe.py
```

腳本仍使用容器內工作根目錄、工具、暫存與簽署儲存位置的預設常數；執行前須依自己的隔離環境調整，不能只切換工作目錄。依賴與路徑限制請參閱倉庫根目錄的 `OFFLINE_PORTING_GUIDE.md`。

建置前必須實際閱讀敏感程式碼與掃描輸出，再留下綁定來源、掃描輸出與本安全報告 hash 的 `manual-review.json`。自動 preflight 或文字檔存在本身不能視為安全認證。建置要求精確來源 hash、成功掃描、34 份缺失合約與 16 個 TextAsset。輸出目錄不可覆寫，重跑請用 `--output` 指定不存在的新目錄。

驗證從交付 APK 重新抽出 DLL，核對修改清單外的方法、原後端、診斷引用、全部 34 項缺失訊息、ZIP entry 差異、MonoManager、原生引擎來源、權限／元件、簽章與對齊。一次性引擎替身用來重現缺表失敗，不放入 APK，也不等於 Android 行為驗證。

最終 hash、大小與實際驗證結果以產物的 `build-report.json`、`SHA256SUMS`、`verification.txt` 及 `postbuild-clamav.txt` 為準。這些檢查不能替代 Android 安裝與遊玩測試。

本次交付驗證通過：**4,641 個非目標方法保持不變、18 個方法符合修改清單、32 個診斷引用可對原 Unity／BCL 解析，全部 34 項缺失列入診斷訊息**。原後端缺表與空表失敗均已重現；ZIP、原檔保存、原生引擎來源、MonoManager、權限／元件移除、簽章及對齊通過。

修改後 APK 及其解包內容另經 ClamAV 掃描：**2,156 個檔案，感染 0**，無掃描錯誤或超限警報。原始輸出在 `postbuild-clamav.txt`；引擎版本與靜態檢查限制仍同安全報告，不能視為絕對無毒證明。

交付 APK：**36,701,021 bytes**，SHA-256：

`31fd3ecada83f7e4db9eaa1f02e7f65be6f791e7fca55050bdccfaa43a9dc86d`

本機測試簽署憑證 SHA-256：`d5aa82d4fe49be8bda22a2dfbb677205b1cbdb7588bf6ddfc15cf49c081cdae5`，與來源簽章不同。Android 安裝與啟動尚未驗證。
