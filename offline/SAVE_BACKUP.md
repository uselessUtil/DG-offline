# 1.0.6 存檔匯出／匯入（prototype 22r7）

本頁主體記錄 1.0.6。後續已移植至 [2.9.0](v290/playable/README.md)：r15 提供同版本往返，r16 另支援固定 1.0.6 prototype 22r7 備份單向轉入 2.9.0；1.0.6 本身未變更，仍只接受同版本備份。不提供原官方帳號還原。原創實作依專案 [MIT 授權](../LICENSE)；原遊戲範圍見 [第三方權利聲明](../THIRD_PARTY_NOTICES.md)。

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

共用 Runtime 的 Build 已升為22r7。1.0.9／2.1.0／2.4.0 的建置腳本目前仍明確要求22r6字串，直接搭配現行共用來源會在契約檢查停止；須使用相符來源快照或另完成配接。2.9.0 r15 已配接22r7來源與本功能。4.0.0 現行腳本接受22r修訂標記，但沒有移植本存檔功能。其他版本既有 APK 與驗證報告不因此失效。

## 候選與既有證據

候選紀錄：`artifacts/1.0.6-offline-prototype22r7-save-backup/ディバゲ_1.0.6.apk`；176,039,210 bytes；SHA-256 `6f8374a4d7c1f51d109d2aac55e05cc562d651b4dcfabfe24ff7e74a03e463e0`。APK、原始報告及測試產生的帳號／備份檔不隨 Git 提供。先前 save-transfer／final／release 目錄的中間候選已標示被此紀錄取代。

既有 build-report.json 與 save-transfer-tests.txt 記錄 Unicode／SHA向量、溢位數字及錯誤引用拒絕、帳號／設定往返、覆蓋前回復、獨立程序中斷回復、Java預覽協定、非公開Activity與DEX整合檢查。引擎I/O替身及桌面Java無法驗證 Android 選檔器、文件提供者、對話框生命週期或真正退出。

歷史 prototype20／22r2 APK 缺少時，關卡升級與通知負對照改用 `tests/PrepareLegacyFixture.cs` 產生並明列來源的暫存夾具；不是再次驗證原歷史APK。若提供雜湊相符的原APK，仍優先走原對照。

本次提交整理僅做靜態檢查，未編譯、執行完整回歸或操作玩家存檔。下節 2.9.0 結果均引自既有報告；r17 另有使用獨立備份副本的隔離退休檢查，未寫回裝置。


## 2.9.0 r16：匯入固定 1.0.6 備份

在 1.0.6 prototype 22r7 的 Others → Save Backup → Export Save 輸出檔案，再於 2.9.0 r16 同頁選 Import Save。系統確認摘要會標示 1.0.6 轉換；確認後才替換帳號、保留覆蓋前備份並關閉遊戲。重新開啟即可載入。輸出仍為 2.9.0 格式，不能反向匯回 1.0.6。

只接受 dg-offline-save/schema1、game_version=1.0.6、prototype 精確等於 1.0.6 offline prototype 22r7 的備份；不接受裸 SERVER_SAVE、PlayerPrefs XML、未知修訂或戰鬥中資料。來源 APK 指紋仍為上列 6f8374a4…e463e0，未修改或重建 1.0.6。

- 固定來源的 522 種角色逐一核對 ID、名稱與等級上限；角色實例、EXP、加值、技能等級、原取得時間及倉庫順序保留。五組隊伍及唯一 ID 不重建。
- 371 筆關卡逐筆依區域與名稱核對；魔禁 420～424 的發現／通關位元轉為 436～440，其餘沿用。未知角色、關卡位元或禮物引用會停止匯入，不靜默捨棄。
- 原帳號數值、貨幣、體力、禮物、圖鑑、教學組別、最愛與排序保留；仍受 2.9.0 既有帳號下限與時間規則處理。
- 舊版沒有的票券與商品活動 ID 設零，關卡建置／月登入設空，活動購買／成就／登入清單為空集合；不承接被覆蓋帳號的資料。
- 四個舊聲音／引導選項保留，新增提示／通知選項採原生零值；新增篩選與教學跳過偏好不設值。還原安全標記轉為 240after，移除舊 SAVE_VERSION 與流程暫存。
- 轉換後再次走完整 2.9.0 驗證與原有備份／日誌交易，不改動 2.9.0 同版本匯入格式。

版本專用來源為 v290/playable/LegacySave106.cs、Legacy106Account.cs 與 legacy106-catalog.json；save_backup.py 在建置時對目前主資料核對對照。新增 verify_legacy106.py、VerifyLegacySchema106.cs、ExportLegacy106Fixture.cs 與 ImportLegacy106Tests.cs，從固定來源 APK 匯出獨立測試帳號，再由簽署 r16 APK 的組件匯入、逐欄比對與跨程序重登；另驗證損壞／未知資料拒絕、故障回復與 Java 預覽。完整受管回歸、鑰匙及更新檢查通過，詳見 r16 成品旁的報告。引擎 I/O 使用測試替身；Android 選檔器、確認／退出及實際遊玩仍交由使用者測試。

目前 2.9.0 來源為 r17：另修正取消接續時必須透過原退休 API 保存關卡結束狀態，以及無接續／結算資料的孤立關卡。退休前日誌可在載入帳號前回復中斷交易；失敗時保留接續資料並阻擋換頁。r17 的 24 組受管檢查結果屬既有報告，Android 選檔、退出、接續及實際遊玩仍待驗證。

legacy106-catalog.json 是固定來源的最小角色／關卡對照契約，只保留核對所需的 ID、名稱、等級上限與區域；不包含完整主資料或玩家存檔，亦不是通用轉換表。名稱等原遊戲資料依第三方權利聲明處理。建置會逐筆核對目標資料，驗證工具另從指紋鎖定的來源 APK 核對此契約；變更來源修訂時須重新審查，不能只改版本字串。


## 2.9.1／DGDB-1 備份

[2.9.1工程](v291/playable/README.md) 使用game_version=2.9.1、schema2、data_version=dgdb-1識別，接受2.9.0 schema1及固定1.0.6 prototype22r7備份單向轉入。保留帳號欄位與交易回復；新技能上限較低時才限制原技能等級，將來源checksum及調整項目記入migration_report。新格式不可匯回2.9.0或1.0.6，回舊版須使用原舊備份。

既有跨程序測試從固定舊APK生成帳號，驗證轉入及降限；不代表任意官方帳號可遷移，Android選檔與實際遊玩仍待驗證。Godot使用另一套獨立存檔，目前未接本格式遷移。
