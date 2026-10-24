# 1.0.6 存檔匯出／匯入（prototype 22r7）

本功能只配接 `jp.gungho.dg.offline106`，不移植原官方帳號，也不提供其他 APK 版本的存檔轉換。原創實作依專案 [MIT 授權](../LICENSE)；原遊戲範圍見 [第三方權利聲明](../THIRD_PARTY_NOTICES.md)。

## 操作與範圍

完成目前關卡或退休，回到 Others 的 Save Backup。Export Save 透過系統選檔器輸出 `.dgbackup.json`；若曾匯入，可選 Before last import (recovery) 匯出最近一次覆蓋前的備份。

Import Save 選取檔案後，先顯示名稱、Rank、持有數與備份時間，確認 Import and close game 才替換帳號；完成後關閉遊戲，重開繼續。取消不替換帳號。選檔功能要求 Android API19 以上及可用的文件提供者；實際選檔、暫停／返回、確認與關閉流程仍待裝置驗證。

備份包含 SERVER_SAVE 的角色／唯一ID／等級／加值／獲得時間、隊伍、進度、貨幣／友情點、體力、登入及禮物資料，另保留明列的角色保護、好友最愛、使用記錄、教學組別、排序、聲音與畫質偏好。它不搬移網路位址、主資料、圖像、金鑰或裝置檔案。

戰鬥／進化關卡尚未結束時，匯出與匯入均拒絕。匯入後清除明列的關卡／刮卡／登入畫面暫存，避免沿用前帳號狀態。重新載入仍會套用本版既有帳號規則與正常時間計算。

## 格式與覆蓋回復

格式為 `dg-offline-save`、schema1、game_version=1.0.6，UTF-8 檔案上限4MiB。外層保存 prototype、UTC時間、字串形式的 payload 及 payload 的 SHA-256；payload 內含序列化 SERVER_SAVE 與型別化偏好清單。SHA-256 用於檢查損壞，不是加密或身分驗證；不能直接匯入任意 PlayerPrefs XML。

Java 預覽先核對格式／版本／雜湊，再顯示摘要；C# 寫入前另驗證數字範圍、角色及隊伍引用、偏好鍵與型別。舊 LitJson 可能將溢位整數轉為零，因此原始數字字串也需核對，不能只檢查反序列化後物件。

通過驗證後，先將當前帳號寫成 app 私有目錄中的 `before-import-*.json` 及 `restore-journal.json`，再替換偏好並載入。備份檔先寫暫存，再用 libc.rename 提交。寫入途中失敗會回復；程序中斷則於下次帳號載入前依日誌還原。成功後刪除日誌，保留覆蓋前備份。此回復設計與既有測試不代表所有 Android 文件提供者或斷電情境已驗證。

## 維護位置

| 位置 | 用途 |
| --- | --- |
| `SaveBackup.cs` | 匯出格式、相容SHA-256、完整驗證、覆蓋前備份及中斷日誌回復。 |
| `SaveTransferUI.cs` | 重用 Others 的舊資料移轉頁，接系統選檔結果、確認匯入與退出；消耗返回當幀避免重複觸控。 |
| `android/SaveTransferActivity.java` | 僅存取使用者選取的文件URI；提供預覽／確認及私有結果狀態，Activity 不對外匯出。 |
| `save_transfer_android.py`、`manifest.py`、`build.py` | 編譯／合併 Activity，Manifest 以 save_transfer 選項加入非公開入口，不新增儲存／網路權限。原 DEX 的852份 smali 在合併前逐檔核對不變；DEX 容器本身會重新組譯。 |
| `PatchRules.cs` | 連接 libc.rename，於帳號載入前加回復掛鉤，新增 Others 的 UI／輸入入口。 |
| `tests/VerifySaveBackup.cs`、`tests/verify_save_backup.py`、`tests/SavePreviewTest.java` | 成品組件往返、壞檔拒絕、跨程序回復及桌面 Java 預覽協定核對。 |

Dockerfile 新增橋接編譯用 JDK 及固定版本 R8／Android／JSON 參考jar；工具jar不封裝進APK，實際輸入指紋見建置報告。依 [指南](../OFFLINE_PORTING_GUIDE.md) 在專案容器內建置，執行前核對腳本尚未參數化的工具設定。

共用 Runtime 的 Build 已升為22r7。1.0.9／2.1.0／2.4.0／2.9.0 的建置腳本目前仍明確要求22r6字串，直接搭配現行共用來源會在契約檢查停止；須使用相符來源快照或另完成配接。4.0.0 現行腳本接受22r修訂標記，但沒有移植本存檔功能。其他版本既有 APK 與驗證報告不因此失效。

## 候選與既有證據

候選紀錄：`artifacts/1.0.6-offline-prototype22r7-save-backup/ディバゲ_1.0.6.apk`；176,039,210 bytes；SHA-256 `6f8374a4d7c1f51d109d2aac55e05cc562d651b4dcfabfe24ff7e74a03e463e0`。APK、原始報告及測試產生的帳號／備份檔不隨 Git 提供。先前 save-transfer／final／release 目錄的中間候選已標示被此紀錄取代。

既有 build-report.json 與 save-transfer-tests.txt 記錄 Unicode／SHA向量、溢位數字及錯誤引用拒絕、帳號／設定往返、覆蓋前回復、獨立程序中斷回復、Java預覽協定、非公開Activity與DEX整合檢查。引擎I/O替身及桌面Java無法驗證 Android 選檔器、文件提供者、對話框生命週期或真正退出。

歷史 prototype20／22r2 APK 缺少時，關卡升級與通知負對照改用 `tests/PrepareLegacyFixture.cs` 產生並明列來源的暫存夾具；不是再次驗證原歷史APK。若提供雜湊相符的原APK，仍優先走原對照。

本次提交整理僅做靜態語法、Git差異、文件、來源指紋及公開資訊檢查，未編譯或執行完整回歸，也未操作玩家存檔。
