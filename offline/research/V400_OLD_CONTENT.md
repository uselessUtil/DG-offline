# jp.gungho.dg 舊內容移植到 4.0.0：DLL 還原與相容性驗證

日期：2026-10-23（Asia/Taipei）。本頁保存配接前研究；後續已有 [4.0.0 r6 候選工程](../v400/playable/README.md)，改用原始 1.0.6 的 522 角色／371 列任務。本文的 595 角色快取解析結果、缺表數量與「尚未」描述均指此歷史調查，不能混為 r6 的資料或驗證。既有分析、解碼與測試均在專案 Docker 工具環境內完成。

**結論：兩份 4.0.0 遊戲 DLL 已成功還原；舊主資料的解析及必殺技初始化已經過目標原版程式驗證，可進入 4.0.0 專用適配實作。這不代表直接搬資料夾即可遊玩：技能語意、缺表、本機後端初始化、Unity 素材及完整遊戲流程仍需處理。**

本報告取代先前「DLL 尚未解開、只能依腳本索引推測」的判斷。沒有修改、重封裝、簽署或安裝原 APK；本次也沒有完成 4.0.0 可玩版本。

## 1. 對象與完整性

- APK：`ディバゲ_4.0.0(紀念)-v20220207073837.apk`，34,724,267 bytes。
- SHA-256：`b3f2a366d553d0ab67f2c6fee76bde79dceb3a372f7ed64c3de4849fc1d9600e`。
- Manifest：`jp.gungho.dg`，versionName `4.0.0`，versionCode `59`；原生庫只有 `armeabi-v7a`。
- 初次盤點的 ZIP CRC 與 apksigner verify 通過；本次重現解碼也核對輸入 APK 雜湊且確認執行後未變。有效簽章不等於已確認發布者；「紀念」是本機檔名標示。
- 舊來源是 `jp.gungho.dg`，不是 `jp.gungho.dg(DG0)`。232 個檔案與既有大小／雜湊清單一致；清單只有 `files/UnityCache/` 與 `files/shader/`，沒有 shared_prefs 或其他玩家存檔。
- 主 CAB：`files/UnityCache/Shared/79d6fa7e088d723946c81293ea54c9a47fb93ebc/CAB-72c279a6913804ff087db8ac6979662c`，SHA-256 `b3c342f849beddf4a871d6bf06ca9502e44ede4a78d0409efb4aa45a5d16b586`。

## 2. DLL 已解開，且可重現

原 APK 中兩份遊戲 DLL 沒有正常 PE／CLI 標頭。追查 `lib__6dba__.so` 後，還原多層包裝、區段解碼器與 DLL 專用解碼參數，再以 Unicorn 執行原生庫中的純解碼函式。沒有啟動 Android 程序、JNI 或遊戲。

| 還原檔案 | 大小 | SHA-256 |
| --- | ---: | --- |
| Assembly-CSharp.dll | 2,633,728 bytes | `b0fd8872dc515aedf6d676c36c9e6099409c5f5a48a972f225741cb99fd4e99f` |
| Assembly-CSharp-firstpass.dll | 110,592 bytes | `f4e5135e97bc17da040536d408f23591843a07d2a425b748da22b46241364cd4` |

兩份檔案具有正常 MZ、PE 與 CLI 結構；monodis 可讀，遊戲程序集的 ILSpy 專案反編譯成功。Cecil 逐方法讀取結果：

| 程序集 | 型別（含巢狀） | 方法 | 有 IL 的方法本體 | 指令 |
| --- | ---: | ---: | ---: | ---: |
| Assembly-CSharp | 1,707 | 9,591 | 8,518 | 433,192 |
| Assembly-CSharp-firstpass | 68 | 692 | 637 | 14,861 |

所有分支／switch／例外區段的已讀取指令引用均位於其方法本體中。這是還原完整性與 IL 可讀性檢查，不是所有方法的執行或完整 CLR 型別驗證。

新腳本從原 APK 再次還原，得到相同兩個雜湊；只接受上述已核對的 APK。還原 DLL、原生解碼區段、主表 JSON 與反編譯內容均留在隔離的容器暫存區，不放入版本庫。

## 3. 舊主資料的實際測試

重新從原 CAB 擷取表格，核對每張 TextAsset 的雜湊。使用 **4.0.0 自己的 LitJson 與列型別**，未使用 2.9.0 的解析器，也未修改遊戲 DLL。

| 測試 | 結果 | 限制 |
| --- | --- | --- |
| 28 張有效舊表，完全原始 JSON | 27 通過，1 失敗 | Area 的 `bgm_normal` 不存在於新版型別 |
| 僅從 Area 副本移除 `bgm_normal`、`bgm_boss` | 28／28 通過 | 110 列各移除兩欄；還需映射音樂資源，解析成功不等於音樂已移植 |
| 舊 MasterDataEvent 以後端 MasterDataEventBase[] 解析 | 1,000 列通過 | 可作資源名稱映射；活動時間／週期仍須核對 |
| 舊必殺技經原 ConvertMasterSkillLimitBreak 與 ActivityLimitBreakSkill | 459／459 個非零 ID 通過 | 驗證技能 ID 與參數逐項保留，未驗證完整戰鬥效果 |

舊來源包含 595 個非零角色 ID；關卡共 399 列，其中非零 ID 且 `active=2` 有 392 筆。4.0.0 `MasterDataDefineLabel.BOOL_ENABLE=2`，其關卡查詢也以 `active==2` 判斷。這些是來源數量，不是已完成移植／可玩的數量。

技能測試在容器 Mono 執行，反射填入 BattleManager 的技能快取，以未呼叫 Unity 建構子的測試物件避開引擎依賴，再呼叫原版方法。沒有修改原遊戲程式；此測試不涵蓋 Unity 場景生命週期。

### 關鍵差異：解析成功不等於傷害正確

4.0.0 轉換器建立 16 格 `value_list`，BattleSkillActivity 的 `m_Param` 也是 16 格，459 個舊必殺技都能完整複製參數。

但技能 100000 的實測結果是：

- `value0 = 1500`，舊參數保留。
- `skill_damage_enable = 0`。
- `skill_power = skill_power_fix = skill_power_hp_rate = 0`。

原 `GetDamageValue` 的基礎傷害使用新版專用 power 欄位；上述欄位不會因舊 `value0` 存在就自動填入。因此不能僅補缺欄位為零，也不能把初始化通過說成傷害／回復正確。必須依技能類別對照舊版語意，轉換傷害、回復、吸收、反動、狀態、目標及各種倍率旗標，並建立結果對照。本次未執行完整傷害計算或戰鬥。

## 4. 本機後端存在，但不能直接啟用就當完成

已從實際方法本體確認：

- ServerDataManager 保留呼叫 `ServerForgery.CreateRecvForgery` 的收包分支；仍需在適當啟動流程建立／啟用本機後端。
- ServerForgery 保留創角、認證、選擇初始隊伍、主資料、進關、關卡生成與結算處理。
- `UserParamLoad/Save` 透過 LocalSaveUtil／PlayerPrefs 的 `SERVER_SAVE` 讀寫 `ServerForgerySaveUser` JSON；保存時呼叫 PlayerPrefs.Save。
- `Start` 依序載入 49 個 `CardDM/MasterData/...` 資源。舊 28 表將 Event 對應為 EventBase 後，仍缺 21 表。

缺少的 21 表為：Achievement、AreaCategory、AssetBundlePath、AudioData、BeginnerBoost、EnemyAbility、Gacha、GachaAssign、GachaTicket、GlobalParams、Information、LimitOver、LinkSystem、LoginMonthly、Present、PresentGroup、QuestRequirement、SkillBoost、TextDefinition、TopPage、UseItem（均帶 MasterData 前綴）。

不能一律放空陣列：`ForgeryGetMasterDataAll` 直接索引 Achievement 的第 1、4、6、7、9 列；Start 的載入都在同一 try 區段，任一缺資源就會中止後續載入。創角也有固定贈禮／測試資料。需要依舊內容模式補最小設定，或移除無關功能的硬依賴，而不是偽造完整 4.0.0 主資料。

本次確認的是方法與路由的存在及其需求，**尚未執行創角 → 進關 → 結算 → 重啟讀檔的完整後端流程**。

## 5. 素材與帳號的範圍

初次盤點中，4.0.0 的 2,324 個序列化資產檔均可被 UnityPy 解析，沒有解析錯誤；有 11 個場景，包含 SceneGameMain。17 個 TextAsset 是 15 個圖集 JSON、TextDatabase、restore001，未找到 MasterData 表。restore001 是關卡／戰鬥狀態樣本。

4.0.0 BuildSettings 為 Unity 5.3.6p4，舊快取為 Unity 4.1.0f4。舊圖片、模型、動畫及地圖可作轉換來源，但需要真正的 5.3 目標序列化、Shader、粒子、動畫及腳本欄位適配。既有 2.9.0 的 Unity 4.6 產物不能因此當成 5.3 相容；本次沒有進行 5.3 素材載入測試。

**可評估移植的是舊角色、關卡與素材內容；這份備份不足以恢復原玩家帳號、持有角色或進度。** 目前檔案只有快取與 Shader，未見玩家存檔；相同包名不會自動產生缺少的帳號資料。

## 6. 實作路線與完成門檻

1. 建立 4.0.0 專用主資料適配：Area 音樂映射、Event 資源映射、新欄位與技能語意；保留舊 ID 和原始數值作對照。
2. 接通本機後端：依 49 表需求補最小有效設定，處理 Achievement 等固定索引、贈禮與非必要功能，再驗證封包與存檔往返。
3. 先轉換一支隊伍、一個舊關卡及必要素材至 Unity 5.3，完成創角 → 進關 → 戰鬥 → 結算 → 重開讀檔；通過後擴大到其餘內容。
4. 重封裝時確認原生載入保護與修改後程序集的相容性，再在核准的 Android 測試環境驗證實際 APK；只有 armeabi-v7a 仍是裝置相容限制。

既有 `offline/v290/playable/` 的 r8／r9 工程已提供資料、素材與測試基準，可以重用設計及案例；4.0.0 的 IL 掛鉤、資料輸出、Unity 素材與存檔必須重新適配。本次未重跑 2.9.0 測試，也不把其成果當作 4.0.0 驗證。

若只為保留舊內容，延續既有 2.9.0 工程的未知項較少；若指定 4.0.0，引擎／程式碼的研究門檻已跨過，可以進入上述適配階段，但仍需完成實際運行驗證。

## 7. 重現與證據

- 新工具與結果：`artifacts/v400-dll-recovery-2026-10-23/`，含 recover.py、analyze.py、C# 探針、recovery.json、report.json 及逐表結果。
- 首次靜態盤點：`artifacts/v400-old-content-audit-2026-10-23/`，保留當時原加密 DLL 的結果；該報告的「尚未還原」描述是歷史範圍，不是目前狀態。
- 還原輸出與反編譯研究使用暫存目錄，不保證永久保存。當時的 analyze.py 與 C# 探針仍需另備；後續已維護的解碼入口為 [recover.py](../v400/playable/recover.py)，另有候選版專用資料與素材轉換工具。歷史研究的完整探測仍需其對應腳本及來源。

取得原 APK、對應工具與報告後，在依專案 Dockerfile 建立的工具環境內、從倉庫根目錄執行：

```sh
python artifacts/v400-dll-recovery-2026-10-23/recover.py
python artifacts/v400-dll-recovery-2026-10-23/analyze.py <上一個命令回傳的還原目錄>
```

既有調查使用 Mono／Cecil、UnityPy 1.24.2 與 Unicorn 2.1.4；需在容器內另備相符依賴，不能假定 Dockerfile 已包含此次研究的全部套件。依工具原始碼核對來源與輸出設定；完整工具說明仍需另備該目錄 README。

本次提交整理只核對既有報告及文件，未重新解碼、執行相容性測試、編譯或跑完整回歸。

相關基準：[主資料整理](MASTERDATA.md)、[2.9.0 現有實作](../v290/playable/README.md)。

還原與相容性結果對應來源清單 S33／S34；本頁保存摘要及指紋，原始工具與產物需另備。
