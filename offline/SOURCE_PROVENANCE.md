# 公開來源與本機重建內容

2026-10-27 核對，基準提交為 `0ffc44a`。這是檔案來源及建置邊界紀錄，不是法律授權判定。

## 原遊戲衍生程式

`godot290/project/LegacyEnemyAttack.cs` 與 `LegacySkillStatus.cs` 已從維護來源移除。
`godot290/recover_rules.py` 從指定 2.9.0 r17 APK 的 Assembly-CSharp.dll 以 Dockerfile
固定的 ILSpy 9.1.0.7988 擷取兩個方法，套用原先 Godot 配接差異後，寫入
容器 `/tmp/dg-godot290/project/Generated/`。

來源 APK、組件、反編譯方法及生成程式各有 SHA-256 契約；不符即停止，不使用空實作或推估規則。
配接只保留既有變更：敵方／手牌欄位轉接、移除 Unity 顯示呼叫、移除舊 r17 Unity
狀態入口 hook，與 Godot 方法名稱／namespace 配接。生成後的兩份方法與移除前版本
逐行去除縮排、空行、註解後的指紋一致；不是重新創作原遊戲邏輯。

prepare、build、Android export、關卡／技能測試都會重建這兩份程式。
直接編譯未準備的公開工程會明確提示缺少本機來源；不會悄悄停用戰鬥功能。
來源位置及雜湊沿用 `godot290/prepare.py`。原 APK 不改寫。

## 角色與關卡目錄

已移除 v290、v291 的兩份 `legacy106-catalog.json`。兩份原內容相同：
522 筆角色的 ID／名稱／等級上限、371 筆關卡的 ID／區域／名稱，均與原始 1.0.6 APK
TextAsset 投影逐筆一致；不是本專案自行撰寫的角色／關卡內容。

`offline/local_sources.py` 從原始 `ディバゲ_1.0.6.apk` 的 ResourceManager 找出表格，
檢查 APK 與三張表的指紋，再於記憶體產生相同目錄。可用 `DG_SOURCE_106` 指定同位元組
來源路徑。原始 APK SHA-256 為
`ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2`。

目錄內的 `apk_sha256` 仍指向被允許匯入存檔的 22r7 修改版 APK；
這與用來恢復表格的原始 APK 是不同用途的指紋，不能互換。
v290／v291 建置和舊存檔驗證已改用此生成器。生成的 Legacy106Catalog.cs
與原 r17 建置結果逐位元組相同，保留 420–424 → 436–440 的既有關卡映射。

## 補寫文字與常駐文字

依據：提交 `3eb609f`、`24dc69c`、`6a002ee`、`aa9a161`，
對應 README、build.py 的補寫／格式修正紀錄，以及三個固定 APK 的 TextDatabase 逐鍵比對。

| 檔案 | 筆數 | 原 APK 無此鍵 | 原 APK 同鍵、值不同 | 原 APK 同鍵同值 |
| --- | ---: | ---: | ---: | ---: |
| v290/text-fallback.json | 51 | 51 | 0 | 0 |
| v291/text-fallback.json | 51 | 51 | 0 | 0 |
| v400/text-fallback.json（修改前） | 95 | 88 | 4 | 3 |
| v400/text-repairs.json | 138 | 137 | 1 | 0 |
| v400/text-complete.json | 350 | 350 | 0 | 0 |

表內路徑均位於 `offline/<版本>/playable/`。v290 與 v291 的 51 筆補寫相同，
不能當成 102 筆不同原文。

缺鍵文字有專案補寫紀錄；4.0.0 的四筆既有鍵覆寫是邀請畫面標題、離線提示與按鈕用語，
另一筆 repairs 覆寫是 PARTY_NUM_RATE 格式修正。這些補寫／修正保留。
`text-audit-exclusions.json` 是稽核誤判分類，不是遊戲顯示文字。
`dgdb-sprite-aliases.json` 是五筆名稱對照與配圖判斷，不是圖片內容。

三筆同值的鍵是 FRIEND_INVITE_DIALOG_BACK、FRIEND_INVITE_DIALOG_BTN_LINE、
FRIEND_INVITE_DIALOG_BTN_TWITTER。共同的短標籤本身不能證明曾經擷取；
為避免公開來源附帶這三筆相同值，將其值改為 `null`，
由 `offline/text_sources.py` 在 4.0.0 建置時從固定來源 APK 讀取。
null 是本機解析標記，不會進入遊戲文字資料表。解析後的 95 筆內容和順序與原版本完全一致。

「原 APK 無此鍵」不能獨自證明獨立創作，也不代表已比對所有歷史版本、伺服器或 Wiki。
本次結論是結合專案來源紀錄與指定 APK 的內容比對；未取得權利人授權證明。

## 重現與驗證

所有命令在 `divine-gate-dev` 容器的 `/workspace` 執行：

```sh
/opt/unity-tools/bin/python offline/local_sources.py
/opt/unity-tools/bin/python offline/audit_text_provenance.py --output artifacts/public-source-audit/text-provenance.json
/opt/unity-tools/bin/python offline/tests/test_local_sources.py
python3 godot290/build.py
DG_TEST_TABLES=/path/to/verified/r17/tables python3 godot290/test_quests.py
```

最後一項的表格仍須依 Godot README 準備；本次沒有把遊戲表格搬入公開倉庫。
文字稽核報告只保存鍵名、數量及雜湊，不包含原 APK 常駐文字值。

本次已通過 Godot 編譯、關卡流程、248 項主動技能及保存／亂數還原測試；
兩版 APK 的遷移程式重新生成後與既有產物逐位元組相同並可編譯，
4.0.0 補寫文字解析後的內容與順序相同。沒有重新封裝、簽署或在 Android 上測試完整 APK。

## 發布邊界

生成程式、表格、圖片、DLL 與含這些內容的完整工程 ZIP／APK 仍屬本機輸入或產物，
不因生成工具公開而取得第三方散布授權。Git 與 Docker 忽略規則排除相關生成路徑。

本機歷史清理範圍為上述兩份戰鬥程式與 v290／v291 的兩份角色目錄，涵蓋本機分支、標籤、遠端追蹤及檢查點參照，並清除舊 reflog／不可達物件。
改寫前的歷史備份與 commit 對照留在 Git 忽略的 artifacts/history-cleanup-*，備份仍含原內容，不屬於公開版本。此操作不會清除遠端或他人的 clone；本次不推送。
