# 1.1.0 修改前靜態安全檢查

核對日期：2026-10-21。來源為使用者放在倉庫的 `sczm/184044_sczm.apk`；外部下載出處與可信官方 hash 未提供。原檔 31,159,626 bytes，SHA-256：

`0d3e13a9e44f02c79f984e7f83bb187f6c1362a8b9002c1709b76278869f35fd`

**本次檢查未發現已知惡意程式或惡意重簽證據，可以在容器中繼續靜態修改實驗；不能認證 APK 絕對安全。** 未執行原 APK、未安裝到手機、未上傳第三方掃毒服務。

- `apksigner verify --verbose --print-certs` 通過 v1 簽章，v2/v3 不存在。憑證 SHA-256 `9623d6804d17d3abe7741709ff7ce2bcb98b1fb0d0782c10dfc7d68569d00bbc` 與倉庫 1.0.6、2.1.0 相同。這只能支持檔案間簽署者一致；沒有獨立官方 hash，且 v1 不涵蓋所有 ZIP metadata，不能據此證明完全未被修改或無惡意程式。[Android 簽章說明](https://source.android.com/docs/security/features/apksigning)
- ZIP CRC、重複檔名、絕對路徑與目錄穿越檢查通過。解包位於隔離環境暫存區，未覆寫原檔。
- `freshclam` 確認病毒碼 daily 28159、main 63、bytecode 339 已是當次可取得版本。ClamAV **1.4.3** 掃描原 APK 與全部解包檔案，共 **2,193** 個檔案、感染 **0**。更新器另提示引擎建議版為 1.4.6；本次未更換倉庫工具映像，不能稱為使用最新版掃描引擎。原始輸出見 `artifacts/1.1.0-security-audit/{freshclam,clamav}.txt`。[ClamAV 掃描說明](https://docs.clamav.net/manual/Usage/Scanning.html)
- Manifest 有網路、付款、外部儲存、`READ_PHONE_STATE`；沒有簡訊／通訊錄／相機／錄音／無障礙／裝置管理員宣告。存在 Google Play、prime31、AppAdForce／dimage 等舊 SDK；這些 SDK 的資料收集與舊漏洞仍是風險。
- 完整 DEX 反組譯後搜尋：沒有本次指標 `Runtime.exec`、`ProcessBuilder`、`DexClassLoader`、`PathClassLoader`、簡訊發送與通訊錄 API 命中。這是有限指標搜尋，無法排除反射、JNI 或混淆後的等效行為。
- `jp/co/dimage/android/b.smali` 的 `C()`、`f.smali` 的 `/system/bin/su` 命中為 `java.io.File.exists()`，後者將結果作為追蹤參數 `_jb=1`；檢查的位置沒有執行 su 或安裝程式碼。
- `TelephonyManager.getDeviceId()` 位於 UnityPlayer 與 acquire DeviceInfoUtility 的 IMEI 方法；另外幾個 `getDeviceId` 命中是輸入裝置 ID。離線診斷版移除電話權限並停用 `AndroidDeviceInfo` 的外部儲存初始化。
- 對兩個遊戲 managed DLL 另列出 WWW、Android Java bridge、原生 P/Invoke、OpenURL 與反射／Process 指標。命中集中於下載、登入、商店、WebView、廣告／裝置資訊及儲存橋接；沒有本次列舉的 Process／Assembly 動態載入 API 命中。下載字串包含原 GungHo、S3、CloudFront 與 SDK 位址，也保留 NGUI 等範例字串；字串存在不表示實際執行。報告見 `managed-sensitive.tsv`。

限制：單一掃毒引擎、沒有 Android 行為沙箱、未逐一完整逆向所有 C#／DEX／原生 SO、未獨立驗證官方來源、舊引擎與 SDK 可能存在已知或未知漏洞。因此不能替使用者確認「安全無惡意程式或惡意修改」這個絕對命題；以上是本次實際取得的證據與邊界。
