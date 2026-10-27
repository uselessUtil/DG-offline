# Divine Gate 2.9.0 → Godot 移植

**狀態：主動技能與戰鬥演出開發版（port3）；尚非完整 1:1 復刻。**

目前提交為來源 checkpoint，尚未完成 port3 發布封裝。已驗證範圍、暫停位置及恢復步驟見 [CHECKPOINT-port3.md](CHECKPOINT-port3.md)。

參照2.9.0 r17 APK，SHA-256為 b8caed0f1f5c6b34e1285db7dffcf3584436a2980393aa88324e18faa72b6e8f；來源位於專案相對目錄 artifacts/2.9.0-offline-playable1-r17-restore-retire。原APK保持不變，新套件為 org.divinegate.godot290，具有獨立存檔。

原創工具、配接器與文件依 [MIT授權](../LICENSE)；原遊戲程式、改寫片段、美術、字型與其他素材依 [第三方權利聲明](../THIRD_PARTY_NOTICES.md) 處理。Godot／.NET／Android工具依各自條款；匯出專案或APK不會使其內第三方內容成為MIT。

## 目前來源

- Godot 4.6.3 .NET／net9.0工程，分別匯出 Android ARM64 手機包及 x86_64 MuMu 包。
- 保留r17本機規則組件，bridge配接檔案、時間、數學與PlayerPrefs；未配接的Unity原生API明確報錯。原版libunity.so不隨Godot執行，舊managed型別不等於Unity引擎。
- 從來源匯出原圖集、字型、prefab層級與UI網格，供LegacyView及選單使用；商店與抽卡接r17規則，免費代幣為1／6／12／25／52／85枚。
- 帳號、倉庫、角色及關卡選單為混合原資源與暫時Godot介面。存檔採獨立原子寫入並保留前一版，不自動讀寫原APK帳號。
- 新增QuestRun／QuestMenus／QuestView：隊伍選擇、格子移動、鑰匙及門、戰鬥面板計時、敵方回合、結算／退休與重開還原。LegacyEnemyAttack含原程式配接片段；這些來源不能概括為全技能或原版演出已還原。
- QuestSkills／LegacySkillStatus／QuestEnemyActions 接上 AS/LB 的原版發動條件、SP、冷卻、傷害／治療、面板轉換、狀態付加與解除，BOOST、追擊／反擊、根性、敵方先制與技能反應。隨機數的完整狀態隨關卡保存。原程式 SkillBoost 類別 6–9 的空方法維持空行為；沒有自行添加規則。
- export_battle_presentation.py 匯出 90 份動畫曲線、93 組技能特效定義、41 張原貼圖；Godot 播放敵人攻擊、技能切入、數字、簡化粒子和消失演出。邏輯結算與播放分開，動畫不能再次扣款或發獎；MENU 可暫停演出，顯示層不穿過選單。
- tests/SkillTests.cs 掃描目前 595 角色所引用的 248 個不同 AS，覆蓋發動、成本、冷卻、重複拒絕、指定敵屬性、治療公式與上限、全體目標數、存讀，以及相同輸入在讀檔前後的相同結果。這是規則與測試場景覆蓋，不等於每個技能都已在裝置或原遊戲逐項對照。
- tests/QuestTests.cs 已執行教學關 1、普通關 83 的實際路徑與戰鬥結算，並驗證中途存讀、手牌與倒數、體力只扣一次、不重複領獎、自然敗北及撤退。Main 的 quest-smoke 另檢查 Godot 地圖繪製。
- export_battle_assets.py 從原 APK 擷取 88 張地圖貼圖、50 組地圖／門模型，MapArt 使用原頂點與 UV 重建平面投影；格子星數、鑰匙與敵人頭像取自原圖集。
- Android Mono 無法載入舊 DLL 超過 1 MiB 的內嵌圖片值型別；RewriteEmbeddedData 將 1,828 個 RVA 資料陣列改為等值 manifest resources，保留原位元組及遊戲規則。

## 已有里程碑與目前來源的差別

既有 artifacts/godot290-port1/manifest.json 記錄較早的選單／抽卡里程碑：
APK為346,844,839 bytes，SHA-256為318652805d903d94690fe6a6037574791383d695f1eca808538a92748c271647。
當時記錄ZIP、簽章、ARM64／16KiB對齊與容器Godot選單輸入檢查，Android未驗證；狀態明列沒有可玩戰鬥。它不是目前關卡新增來源的建置證明。

舊轉換報告記錄412個prefab、21個圖集、2個點陣字型及若干缺腳本／序列化問題。另有1377張內嵌圖片及595角色成長值、392關卡建立的既有後端測試；這些數量不表示所有元件可用或392關實際通關。

2026-10-27 已重新編譯、匯出及測試目前關卡流程。新成品在 `artifacts/godot290-port2-quests-r2/`，原 port1 APK 及參照 r17 APK 均保留；manifest.json 記錄新 APK、工程與來源檔案雜湊。先前 `godot290-port2-quests/` 是未包含返回選單節點生命週期修正的候選包，請使用 r2。

MuMu 12（Android 12，1080×1920）使用使用者指定的 Windows ADB 直接連線。ARM64 包在 MuMu 的 ARM 轉譯系統函式庫 libimg_utils.so 啟動崩潰；x86_64 包可原生執行。以觸控拖曳與點擊完成教學關「始まりの赤」及普通關「２匹の妖精」：拿鑰匙、進 Boss、戰鬥、結算；並在 Boss 戰已放兩張面板時強制關閉，重開還原並通關。沒有透過修改 HP、敵人、掉落或直接授予勝利來完成實機測試。ARM64 真機及 Android 16 執行仍未驗證。

操作：Quest → 區域 → 關卡 → 選隊員／助戰 → Quest Start。地圖只可移動到相鄰格，拿到鑰匙後到最上排中格並點門；拖曳下方手牌到上方五個欄位，第一次放牌開始倒數，也可按「攻擊／PASS」。MENU 可暫停、儲存回選單或撤退。擊敗 Boss 後再點門換層／結算。

## 檔案與重現入口

project/為維護中的C#、場景及匯出設定；bridge/是舊managed服務配接工具。原始APK、DLL、Textures、Tables、生成截圖、Godot快取、報告及簽署材料均不隨Git提供。

所有命令只在依 [專案Docker設定](../Dockerfile) 建立的開發容器內、從專案根目錄執行；不要在主機安裝SDK。使用已配置相依套件的Python環境：

```sh
python godot290/setup.py --android
python godot290/prepare.py
python godot290/export_assets.py
python godot290/export_battle_assets.py
python godot290/export_battle_presentation.py
python godot290/probe.py
python godot290/probe.py reload
python godot290/test_quests.py
python godot290/build.py --import --capture --smoke --page=scratch
python godot290/build.py --capture --quest-smoke
python godot290/build.py --import --capture --battle-smoke
python godot290/export_android.py
python godot290/export_android.py --x86-64
python godot290/package_battle.py
```

上述命令是重現入口，須先完成以下來源配對，不能直接當作完整自動建置流程：

1. 核對來源APK與r17 build-report.json。prepare.py驗證APK後擷取組件、原圖並複製維護來源，export_assets.py再轉換UI。
2. prepare.py尚未自動建立遊戲Tables；需從相同r17報告的已驗證主資料準備到生成專案Tables。不得借用DGDB或其他修訂表替代。
3. probe.py與test_quests.py要求DG_TEST_TABLES環境變數指向相同表目錄；不再依賴某次建置的隨機暫存路徑。測試帳號與遊戲帳號分離。
4. setup／build／export工具的容器工作根目錄、工具位置等仍有固定預設，執行前核對來源設定。Android模板透過Godot標準安裝位置尋找，不固定使用者家目錄。
5. package.py／package_quest.py 保留 port1／port2 打包流程；目前使用 package_battle.py，拒絕覆蓋不同的既有成品。匯出專案包含來源 DLL 及遊戲素材，其權利不因打包而改變。

smoke使用一次性帳號；一般啟動使用Godot獨立使用者資料目錄。quest-smoke只進入關卡畫面；battle-smoke 在獨立帳號設定滿冷卻測試技能繪圖，不等於自然遊玩或原遊戲對照。工具版本雖有部分釘選，Android command-line tools仍依latest來源解析，不能稱為完全鎖定的可重現工具鏈。

## 待驗證與缺口

關卡仍使用暫時 Godot 排版，棋子以隊長立繪表示。90 份動畫曲線雖已匯出，尚未全部綁到原來的 UI、骨架與時序；粒子以 Godot 2D 投影播放，85 個非 billboard emitter 的網格／拉伸、完整 3D 形狀和子發射器尚未等價還原。不得把 93 組特效資料匯出解讀為 93 組演出已 1:1 完成。來源 APK 未找到 AudioClip 或外掛音訊檔，尚無可還原的原版音效。

一般攻擊、AS/LB、BOOST、異常狀態、追擊／反擊與敵方行動已接入，但沒有每一敵人／狀態／隊伍組合的原遊戲差分證據。位移型 AS、落穴／隨機移動／回退等特殊地圖流程、全部多層關卡仍需補完；目前毒、黑暗及狀態陷阱、移動回復與地板數量條件已接上。兩個關卡的通關與 248 個 AS 測試均不能代表全遊戲一致。

原版全部頁面、完整編隊／好友、強化／進化／出售、連抽／刮卡演出、多層關卡、原版存檔遷移、ARM64 真機、Android 16 及長時間遊玩仍需逐項驗證。編隊頁更換的隊員目前只用於本次出擊，尚未保存為常駐隊伍。

舊報告與暫存測試不能替代目前成品檢查；應以新輸出APK的雜湊重新綁定來源、測試與裝置結果。
