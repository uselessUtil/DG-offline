# 1.0.9 修改前靜態安全檢查

核對日期：2026-10-21。來源是使用者提供的 `sczm/179978_sczm.apk`，實際為 `jp.gungho.dg` **1.0.9 / versionCode 13**，30,883,893 bytes。未取得獨立官方下載來源或可信官方 hash。來源 SHA-256：

`15ab6c8bf7f016dc9c0450bc0b8ba29da637ac4d57515d7d033a0f6502791e34`

**本次有限靜態檢查未發現已知感染或可確認的惡意修改，可以繼續容器內的修改實驗；不能認證絕對安全、完全無惡意程式或從未被修改。** 原 APK 未執行、未安裝、未上傳外部服務。所有專案分析工具都在隔離的專案工具環境內執行，解包位於暫存區，原檔不變。

## 實際證據

- APK v1 簽章驗證通過；v2/v3 不存在。簽署憑證 SHA-256 為 `9623d6804d17d3abe7741709ff7ce2bcb98b1fb0d0782c10dfc7d68569d00bbc`，與本次重新驗證的倉庫 1.0.6、2.1.0 相同。這支持簽署者一致，不能獨立證實官方來源。v1 也不保護所有 ZIP metadata，詳見 [Android 簽章說明](https://source.android.com/docs/security/features/apksigning)。
- ZIP CRC、重複檔名、絕對路徑、反斜線與目錄穿越檢查通過；每個 entry 的大小及 hash 已記錄。
- `freshclam` 當次更新成功，daily 28159、main 63、bytecode 339 顯示已是當次可取得版本。ClamAV **1.4.3** 掃描原 APK 及解包檔案，共 **2,155** 個檔案，感染 **0**，未出現超限／掃描錯誤。更新器提示建議引擎 1.4.6，因此不能稱為使用最新版引擎。單一引擎的未檢出不是無毒證明；掃描機制見 [ClamAV 文件](https://docs.clamav.net/manual/Usage/Scanning.html)。
- Manifest 有 INTERNET、ACCESS_NETWORK_STATE、BILLING、READ_PHONE_STATE、外部儲存寫入及隱含讀取權限；沒有簡訊、通訊錄、相機、錄音、無障礙服務或裝置管理員宣告。包含 Unity、Google Play、prime31、AppAdForce／dimage 等舊 SDK。
- 完整 DEX 反組譯後，這次的 `Runtime.exec`、`ProcessBuilder`、`DexClassLoader`、`PathClassLoader`、簡訊發送、通訊錄／Telephony provider、無障礙／裝置管理員指標均無命中。有限字串／呼叫指標無法排除反射、JNI 或混淆後的等效行為。
- `/system/bin/su` 的兩個命中已讀取周邊程式碼：`jp/co/dimage/android/b.smali` 的 `C()` 及 `f.smali` 使用 `File.exists()` 檢測 root，後者加入 `_jb=1` 追蹤參數；此處沒有執行 su。這也顯示原版含廣告追蹤行為，不能將「未檢出惡意程式」理解為沒有資料收集。
- 電話裝置 ID 命中位於 UnityPlayer 的 `getDeviceUniqueIdentifier` 與 acquire `DeviceInfoUtility.getIMEI()`；其他 `getDeviceId` 主要是輸入裝置 ID。離線診斷補丁移除電話權限並停用 managed 裝置資訊初始化；未刪除所有 DEX SDK。
- 兩個遊戲 managed DLL 的 **198** 筆敏感呼叫、P/Invoke 與字串已列出並審閱，涵蓋下載、商店、登入、WebView、外部連結、裝置資訊及本機儲存橋接。此次列舉的 `System.Diagnostics.Process` 與 `System.Reflection.Assembly` 呼叫無命中。下載字串包括 GungHo、S3、CloudFront 與 SDK 位址；存在字串不等於已觀察到執行。

## 證據檔案與邊界

原始輸出在 `artifacts/1.0.9-security-audit/`：`certificates.json`、`manifest.txt`、`freshclam.txt`、`clamav.txt`、`file-inventory.json`、`dex-indicators.json`、`dex-urls.json`、`managed-sensitive.tsv`、`inventory.json`、`preflight.json`、`manual-review.json`。

沒有 Android 行為沙箱；沒有完整逐一逆向所有 SO、DEX 與 C#；沒有獨立官方 hash；舊引擎、明文 HTTP 及 SDK 仍可能存在已知或未知漏洞。憑證一致、掃毒與有限靜態分析都無法證明「絕無惡意程式或惡意修改」。未聯絡原遊戲 API／CDN，也沒有取得任何帳號資料。
