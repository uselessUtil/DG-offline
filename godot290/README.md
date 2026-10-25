# Divine Gate 2.9.0 → Godot 移植

**狀態：開發中的獨立移植，尚未驗證完整復刻或 Android 遊玩。**

參照2.9.0 r17 APK，SHA-256為 b8caed0f1f5c6b34e1285db7dffcf3584436a2980393aa88324e18faa72b6e8f；來源位於專案相對目錄 artifacts/2.9.0-offline-playable1-r17-restore-retire。原APK保持不變，新套件為 org.divinegate.godot290，具有獨立存檔。

原創工具、配接器與文件依 [MIT授權](../LICENSE)；原遊戲程式、改寫片段、美術、字型與其他素材依 [第三方權利聲明](../THIRD_PARTY_NOTICES.md) 處理。Godot／.NET／Android工具依各自條款；匯出專案或APK不會使其內第三方內容成為MIT。

## 目前來源

- Godot 4.6.3 .NET／net9.0工程，Android ARM64開發匯出設定。
- 保留r17本機規則組件，bridge配接檔案、時間、數學與PlayerPrefs；未配接的Unity原生API明確報錯。原版libunity.so不隨Godot執行，舊managed型別不等於Unity引擎。
- 從來源匯出原圖集、字型、prefab層級與UI網格，供LegacyView及選單使用；商店與抽卡接r17規則，免費代幣為1／6／12／25／52／85枚。
- 帳號、倉庫、角色及關卡選單為混合原資源與暫時Godot介面。存檔採獨立原子寫入並保留前一版，不自動讀寫原APK帳號。
- 新增QuestRun／QuestMenus／QuestView：隊伍選擇、格子移動、鑰匙及門、戰鬥面板計時、敵方回合、結算／退休與重開還原。LegacyEnemyAttack含原程式配接片段；這些來源不能概括為全技能或原版演出已還原。
- tests/QuestTests.cs提供教學與一個普通關卡路徑、再載入、不重複領獎及退休的隔離探針；Main亦提供quest-smoke入口。提交了測試程式不代表此次已執行。

## 已有里程碑與目前來源的差別

既有 artifacts/godot290-port1/manifest.json 記錄較早的選單／抽卡里程碑：
APK為346,844,839 bytes，SHA-256為318652805d903d94690fe6a6037574791383d695f1eca808538a92748c271647。
當時記錄ZIP、簽章、ARM64／16KiB對齊與容器Godot選單輸入檢查，Android未驗證；狀態明列沒有可玩戰鬥。它不是目前關卡新增來源的建置證明。

舊轉換報告記錄412個prefab、21個圖集、2個點陣字型及若干缺腳本／序列化問題。另有1377張內嵌圖片及595角色成長值、392關卡建立的既有後端測試；這些數量不表示所有元件可用或392關實際通關。

本次整理未編譯、匯入素材、匯出APK或執行測試。新關卡邏輯、可攜路徑調整與目前整份來源尚未重新綁定到成品雜湊，需後續驗證。

## 檔案與重現入口

project/為維護中的C#、場景及匯出設定；bridge/是舊managed服務配接工具。原始APK、DLL、Textures、Tables、生成截圖、Godot快取、報告及簽署材料均不隨Git提供。

所有命令只在依 [專案Docker設定](../Dockerfile) 建立的開發容器內、從專案根目錄執行；不要在主機安裝SDK。使用已配置相依套件的Python環境：

```sh
python godot290/setup.py --android
python godot290/prepare.py
python godot290/export_assets.py
python godot290/probe.py
python godot290/probe.py reload
python godot290/test_quests.py
python godot290/build.py --import --capture --smoke --page=scratch
python godot290/export_android.py
python godot290/package.py
```

上述命令是重現入口，須先完成以下來源配對，不能直接當作完整自動建置流程：

1. 核對來源APK與r17 build-report.json。prepare.py驗證APK後擷取組件、原圖並複製維護來源，export_assets.py再轉換UI。
2. prepare.py尚未自動建立遊戲Tables；需從相同r17報告的已驗證主資料準備到生成專案Tables。不得借用DGDB或其他修訂表替代。
3. probe.py與test_quests.py要求DG_TEST_TABLES環境變數指向相同表目錄；不再依賴某次建置的隨機暫存路徑。測試帳號與遊戲帳號分離。
4. setup／build／export工具的容器工作根目錄、工具位置等仍有固定預設，執行前核對來源設定。Android模板透過Godot標準安裝位置尋找，不固定使用者家目錄。
5. package.py保留固定舊里程碑輸出目錄；若既有APK不同會拒絕覆蓋，製作新候選前須另設新目錄。匯出專案包可能含來源DLL及遊戲素材，散布前依第三方權利處理。

smoke使用一次性帳號；一般啟動使用Godot獨立使用者資料目錄。quest-smoke只進入關卡畫面，不等於自動完成全流程。工具版本雖有部分釘選，Android command-line tools仍依latest來源解析，不能稱為完全鎖定的可重現工具鏈。

## 待驗證與缺口

原版全部頁面、隊伍／好友、強化／進化／出售、連抽／刮卡演出、技能與敵方行動覆蓋、異常狀態、關卡UI逐像素對照、裁切、音效／音樂、動畫曲線、粒子及過場仍需逐項核對。原版存檔遷移與Android安裝、觸控、暫停恢復、長時間遊玩尚未驗證。

舊報告與暫存測試不能替代目前成品檢查；應以新輸出APK的雜湊重新綁定來源、測試與裝置結果。
