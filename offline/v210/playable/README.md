# 2.1.0 舊內容離線候選版

此入口使用原始 2.1.0 APK、舊快取主資料／素材及原始 1.0.6 的兩張卡池表。套件為 `jp.gungho.dg.offline210`，與原版、診斷版、1.0.6 及 1.0.9 分離，不移轉其他套件存檔。原創工具及文件的授權見 [MIT License](../../../LICENSE) 與 [第三方權利聲明](../../../THIRD_PARTY_NOTICES.md)。

## 交付與驗證狀態

目前既有交付為 `artifacts/2.1.0-offline-playable1-r2-core/`。依該次報告，APK 為 223,997,323 bytes，SHA-256：

`fa59406ba3534faf6d20f471690020f6bbcd3c5c9f6053ffc9720e765e95d423`

既有報告記錄 ZIP、簽章、對齊、組件引用、原方法修改白名單、90 份原生包及 1,377 份 PNG 檢查，以及創角、初始隊伍、認證、完整主資料請求、登入、教學進關／結算和 595 角色的等級界限數值計算。

**完整關卡、技能、商店與跨程序存檔回歸尚未完成，Android 安裝、載入、渲染、觸控及遊玩也未驗證。** 既有 `local-api.txt` 在已列 PASS 項目後，因舊反射查找仍採 private-instance 形式而失敗；2.1.0 的 `CreateQuestBuild` 已為 public static。現行測試來源已改用該簽章，本次未重跑，不能宣稱後續全部測試或完整回歸通過。結果以同目錄 `build-report.json`、`local-api.txt`、`original-methods.txt` 等為準。

## 維護來源與資料範圍

| 檔案 | 用途 |
| --- | --- |
| `Inspect.cs`、`data.py` | 讀取後端／畫面主資料欄位，轉換舊 Area／Event 表 |
| `donor.py`、`assets.py` | 從鎖定來源讀取兩張卡池表，以原快取像素製作圖集、立繪及遮罩 |
| `Runtime210.cs` | 配置主資料、穩定分組卡池、適配新版標題資料載入頁及商品欄位 |
| `Skills210.cs`、`CompileApi.cs` | 舊必殺技活動／傷害／狀態邊界配接及狀態工具可見性參照 |
| `Patch.cs`、`PatchCommerce.cs` | 方法契約、原遊戲 IL 掛鉤及本地商店入口 |
| `build.py`、`test.py`、各 `*Tests.cs` | 隔離建置及實際 APK 提取後的受管檢查；來源存在不代表已全部通過 |

備份導入 28 張表，再從精確 hash 的原 1.0.6 APK 取 `MasterDataGacha`／`MasterDataGachaAssign`，共 30 張來源表。595 個有效角色及 399 筆任務列維持舊內容。46 筆卡池設定及 11,000 筆分配列依池穩定分組，保留各列及池內順序，不將舊卡池當作官方 2.1.0 歷史設定。

`data.py` 將舊 `MasterDataEvent` 對應後端 `MasterDataEventBase`，畫面端再按目標 Event 欄位配置。Area 僅移除已確認不屬於目標 schema 的 `bgm_normal`／`bgm_boss`，清空下載音訊包請求，使用 APK 內建音訊；其他未知來源欄位拒絕轉換。記錄輸出為 `data-conversions.json`。

未復原的七表為 `MasterDataLoginMonthly`、`MasterDataPresent`、`MasterDataPresentGroup`、`MasterDataAchievement`、`MasterDataAssetBundlePath`、`MasterDataInformation`、`MasterDataBeginnerBoost`。相關路徑停用或配置空容器，不捏造月登入、禮物、成就或新手加成；缺表成就診斷不再阻擋完整主資料回應。

## 技能、商店與 UI 界線

`Skills210.cs` 僅對登錄的舊 LimitBreak ID 接入初始化、傷害及狀態三個邊界，處理傷害開關、回復目標、吸收、犧牲與原參數語意；其他技能走原方法。這是已加入來源的實作，不是全技能或實際戰鬥已驗證的結論。

`CompileApi.cs` 在暫存編譯參照副本中將 `StatusAilmentUtil` 設為 public，正式補丁也調整同一類別的可見性。原敵人 AI、角色成長、關卡進入及結算方法保留，修改白名單見 `VerifyChanges.cs`。

本地免費代幣採 1／6／12／25／52／85 枚、總額 999 上限及整包超額拒絕，付款 SDK 停用，頁面不再等待線上商品事件。原教學、友情、一般及合作池沿用目標原抽卡流程。合作頁保留 2.1.0 原生分頁，**未移植 1.0.9 四池選擇 UI**；不能因 API 測試列出四池就認為四池畫面均可選。

## 建置前提

工具在專案 Docker 環境執行，先選用含 UnityPy／Pillow 的 Python 環境，從倉庫根目錄呼叫：

```sh
python offline/v210/playable/build.py --output artifacts/2.1.0-offline-new
python offline/v210/playable/test.py artifacts/2.1.0-offline-new/build-report.json
```

需另備精確原 2.1.0 APK（由 `offline/v210/audit.py` 限定）及 `artifacts/2.1.0-port-audit/inventory.json`，以及 `jp.gungho.dg/` 快取、`artifacts/cache-backup-audit-2026-10-22/source-files.json` 與原 1.0.6 APK。建置核對來源及卡池表 hash；其他裝置備份不會自動取代來源。

`--assets-from` 需符合前次報告來源及核心表檢查；`--resume-work` 僅接受 `work.txt` 綁定且尚未產生 APK 的同次建置。候選版更新須保留同一候選版簽署金鑰；私鑰另存專用持久儲存區，不提交 Git。原 APK、報告、大型素材及生成物也不隨 Git 提供。

腳本仍含固定工作根目錄、工具及簽署位置，執行前需依自己的隔離環境核對。共享 Runtime 轉換預期 1.0.6 22r6 的特定方法形狀與版本字串，不能直接改用任意新版。

## 尚待驗證

90 個本機 UnityRaw 包涵蓋 595 角色及 106 個區域模型／貼圖組合，缺失或真正載入錯誤仍走簡化 fallback。CAB 不改寫，不代表全部官方 2.1.0 模型、下載音訊或新年代內容已復原。

後續應依序驗證首次啟動／創角、教學戰鬥／結算、第一次刮卡、主選單關卡／技能、免費代幣及重啟存檔，再擴大到全關卡及技能。僅 ARMv7，未移植 1.0.6 的 Rank／倉庫福利或五星倍率，不代表原生 ARM64 相容。

本次整理僅做靜態語法、差異、文件及排除項目檢查，**未編譯、建置 APK 或執行完整回歸**。原 [2.1.0 診斷報告](../README.md) 保留為歷史，與此玩法候選入口分離。
