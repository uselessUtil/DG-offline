# 2.4.0 舊內容離線配接器

獨立於 2.1.0／2.9.0；使用原始 2.4.0 APK 的程式、引擎及常駐資源，加上本地 jp.gungho.dg 舊資料。沒有重建反編譯專案，也沒有搬移其他套件的玩家存檔。原創工具沿用專案授權與第三方權利聲明。

既有候選產物（不隨 Git 提供）：artifacts/2.4.0-offline-playable1/ディバゲ_2.4.0.apk。套件 jp.gungho.dg.offline240，使用獨立簽章，與 2.1.0 同時安裝。實際 Android 安裝、渲染、觸控及遊玩交由使用者驗證。

大小：224,137,250 bytes；SHA-256：`14dca7e04c258fb0b9625220ff4e4c66bd6b89fee402a97a47bcbca6ca2b4826`。身分與驗證範圍取自該目錄 build-report.json、test-report.json 及三份獨立檢查報告。

## 來源與版本差異

原始 APK SHA-256：8e754c6509405a5deb2aebe0389ca914d3b1723eb1e64c8c7fb9c6e018652e64；大小 32251266 bytes；versionName 2.4.0、versionCode 25。Unity 資產標記 4.1.0b3／4.1.0f4，AnimationArray／AnimationEvent 序列化欄位與快取相容，hash 分別為 3721557914／3033599902。本版無須使用 2.9.0 的 Unity 4.6 CAB 轉換。

2.4.0 新增 SkillBoost、QuestRequirement、敵人 posz_value、Area view_id、敵人群組 drop_type、卡池 tab_num／tab_name／活動顯示欄位。已追查原始欄位使用者，調查結果在 artifacts/2.4.0-port-audit/new-field-consumers.txt。舊任務 requirement_id=0 經原入場檢查通過；730 個舊主動技能的 boost_id=0，經原 Boost 判定保持停用，不額外捏造新版技能。

2.4.0 的 StoreManager 已移除 m_StoreRestoreOK，因此只保留實際存在的初始化及鎖定欄位。原標題商品載入器在 m_UserStoreLock=true 時不送付款恢復請求，已驗證能完成。

## 保留的適配

- 595 個有效角色（表共 596 列）、399 列任務，角色／敵人／關卡來自 jp.gungho.dg，資料同時分享給 MasterDataManager 與 ServerForgery。
- Area 移除舊 bgm_normal／bgm_boss，packname_bgm／packname_se 留空，使用常駐音訊。舊 Event 對應後端 EventBase，再投影客戶端 Event。
- 保留舊 cate0..cate4，60 列原總數 24，補 cate5=1 使合計 25；不修改原生成器或開門邏輯。
- Android 專用更新鍵 offline240-1，資料就緒才存標記；原更新比對與等待流程保留，SERVER_SAVE 不因標記修復改寫。
- Bootstrap 等待 MasterDataManager／TimeManager／LocalSaveManager 存在才建立本地後端。
- 舊 LimitBreak 接入初始化、傷害及狀態三個局部邊界，其他技能走原路徑。
- 1.0.6 卡池 donor 僅兩張：46 列 Gacha、11000 列 GachaAssign；依 gacha_id 穩定分組。2.4.0 原選擇器改讀 tab_num，需將來源 type 1／2／3 對到 tab 1／2／3，tab_name 使用來源 name。沒有這個轉換，一般抽卡頁會找不到池。
- 目前原 UI 可選友情池 100100、普通池 200100、最高 priority 合作池 300400，第四頁為空。其他合作池 API 通過不代表全部合作池都有可選畫面；沒有新增四池選擇 UI。
- 教學原關卡給 5 枚代幣，原首次刮卡可完成，不額外發幣。免費商店保留 1／6／12／25／52／85 枚、總額 999、整包超額拒絕及確認防重複領取。
- 付款／外部登入 SDK 停用，標題外連移除，網路及付款權限移除；原戰鬥、敵人 AI、角色成長、進關及結算方法保留。
- 90 包 UnityRaw 保留原 CAB bytes，涵蓋 595 角色、106 個地圖資源組合；1377 PNG 以 RVA 封裝。三個區域素材仍不完整，保留既有簡化 fallback。

缺失表以空集合配置：LoginMonthly、Present、PresentGroup、Achievement、SkillBoost、AssetBundlePath、Information、BeginnerBoost、QuestRequirement。沒有復原這些表所代表的完整線上服務。

## 建置與驗證

在依專案 Dockerfile 建立的工具環境內，將工作目錄設為倉庫根目錄，啟用含 UnityPy 的 Python 環境後執行：

    python offline/v240/audit.py
    python offline/v240/playable/build.py --output artifacts/NEW-240-CANDIDATE
    python offline/v240/playable/test.py artifacts/NEW-240-CANDIDATE/build-report.json
    python offline/v240/playable/test_keys.py artifacts/NEW-240-CANDIDATE/build-report.json
    python offline/v240/playable/test_update_loop.py artifacts/NEW-240-CANDIDATE/build-report.json
    python offline/v240/playable/test_compatibility.py artifacts/NEW-240-CANDIDATE/build-report.json

另需既有快取盤點 source-files.json、objects.json 及鎖定雜湊的原 1.0.6 APK。輸出目錄必須全新，不覆蓋 APK；未完成建置可配合 work.txt 使用 --resume-work。簽署金鑰位於容器持久儲存區，不提交版本庫。

既有報告記錄：對實際簽署 APK 提取的組件執行當時的 test.py 與三項獨立檢查，包含595 個角色數值、392 個關卡生成、459 個舊必殺技初始化、230 個傷害類別／開關、112 個狀態類別、教學刮卡、七池 70 次授卡、免費代幣邊界、程序重啟存檔、389 一般任務／760 層鑰匙與門、魔禁五關受管結算後更新標記。原 2.4.0 有 4989 個方法保持相同，49 個方法在明確白名單中修改。

Unity I/O、更新對話框等替身只在隔離的暫存測試副本。任務生成／API 結算不等於實戰操作，技能公式檢查有容差，不代表所有戰鬥組合、音效、骨架動畫或畫面相同。未操作 Android 裝置。

工具仍有固定工作根目錄、暫存、Mono／Cecil 及簽署儲存設定；執行前須核對原始碼並依自己的容器配置調整。獨立測試會讀取 build-report.json 指向的建置中間檔，清除暫存後需重建，僅有 APK 不足以重跑所有檢查。

本次提交整理僅做靜態語法、差異與文件檢查，未重新編譯或執行完整回歸；上述數值均來自既有報告。
