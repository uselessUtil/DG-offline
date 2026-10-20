# Divine Gate 1.0.6 離線版

目前交付 **prototype 16 選池返回動畫修正版**，進入選池前先清除懸停／按壓縮放，避免保存瞬間放大的尺寸；返回時停止舊 Tween 並還原正常尺寸。四池、每抽 5 枚、免費商店、常駐活動與原確認／刮卡流程保留。**此版尚未在 Android 安裝或執行，交由使用者驗證**。

## 最新產物

- APK：`artifacts/1.0.6-offline-prototype16-collab-animation-fixed/ディバゲ_1.0.6.apk`
- 使用與舊離線版相同的套件 `jp.gungho.dg.offline106` 及測試簽章，更新安裝時不需先清除資料。
- 大小：184,636,132 bytes。
- SHA-256：`9b54bdbe1301dc5637dc41e8600444e116a551f1696fd69e1fe9529c24308c88`。
- 詳細變更、測試結果與手動驗證步驟：[交付說明](../artifacts/1.0.6-offline-prototype16-collab-animation-fixed/README.md)。
- 全部活動原名、任務數及範圍：[活動清單](../artifacts/1.0.6-offline-prototype10-events/EVENTS.md)。七筆無樓層的 reserve 維持停用，並未加入後期版本活動。

## 本機執行方式

保留原遊戲內建的 `ServerForgery` 與 `SERVER_SAVE` / `PlayerPrefs`，改走本機 API 及 APK 內建資料表；不需要另開 HTTP 伺服器。原官方帳號不會自動復原。外部版本檢查、付款、Google 登入及網路權限已移除。`FreeChipShop.cs` 將原六種 Android 商品改為 FREE，僅增加免費代幣並保留付費歷史；原 999 枚門檻搭配整包超額拒絕。

原生載入修正使用 APK 自帶的真正引擎取代舊代理載入器；實際裝置相容性仍需測試。

## 素材與限制

- prototype 13 的 `CollabGacha.cs` 將聯動入口切成四個原生卡片選項，返回可取消；只常駐四個內建聯動池，原權重／等級／加值與其他池不變。每抽 5 枚，最多九連抽，原確認／刮卡程式未改動。四格共用原聯動圖片，以名稱區分。
- prototype 14 改為重用原三卡並只複製第四張，沿用原顯示父物件、圖層與深度，明確啟用／重設透明度／註冊 NGUI；返回與重新進入時還原原入口外觀。容器檢查不代表已確認實機顯示正常。
- prototype 15 在一般首頁刷新後及處理輸入前隱藏第四張卡片並停用選池返回碰撞區域，避免原頁動畫遞迴啟用造成殘留；重複關閉也會清理，原三卡的新餘額顯示保留。新增 100 次模擬啟用狀態循環檢查，原生動畫與觸控仍需實機驗證。
- prototype 16 在原外觀快照前、選池卡片啟用後及還原時，透過原 `UIButtonScale.OnHover(false)` 清除懸停，停止縮放 Tween 並將兩端設為快取正常尺寸；後續原按壓／懸停功能保留。100 次動畫狀態循環使用原版按鈕程式與引擎狀態替身，原生渲染／觸控時序仍需驗證。
- prototype 10 只取消活動區域時段／玩家分組、前置區域與正式難度順序限制；六種進化神殿常駐。教學、主線順序、體力、獎勵及既有通關紀錄維持原規則，不會修改全域活動日期表。
- 522 張立繪、522 個 128px 圖示、281 個面板／陷阱圖片已配接；保留沒有素材的名稱退回替代圖。
- prototype 7 把備份中的 `chara_icon_empty.png` 補進五組角色圖集，修正未選好友／空隊伍格因透明空圖而露出白色底層的問題。
- prototype 8 保留跨圖集搜尋的「找不到」結果，讓 `InGameCard` 繼續搜尋真正包含敵人頭像的圖集；先前在第一組建立替代 Sprite 會使搜尋錯誤地成功。地板按 lower 圖片比例縮到棋盤遠端 z=2.5，避免與 upper 門景重疊。
- prototype 9 從 Fandom 公開圖片 CDN 取得七個城鎮門圖，對應 `area_icon_0001..0007`，保留原圖 207×131 的比例／像素。它們是 Wiki 圖片，未證明與原 1.0.6 圖集像素完全一致。
- 封裝 86 組地圖上下貼圖，涵蓋原始 MasterDataArea 使用的全部 83 組及先前依模型包保存的 3 組。按區域的 `res_map` 選圖；下層用於簡化地面，上層以無碰撞平面顯示於棋盤遠端，包含 `map_0006` 與魔法禁書目錄的 `map_5005`。完整門／地板模型和動畫仍缺少。
- 保留 `panel_alarm → trap_siren`、`panel_banana → trap_banana`、`toaru_box_01 → toaru_box` 對應；新增 `panel_b/r/y_1..6 → panel_1..6`，三組共用黃色星星圖片，沒有猜測原本顏色。這些是相容素材配接，非原始圖集像素完全一致的宣稱。
- 優先清單剩餘 84 項尚未配接，含 77 個選關圖示、4 個效果圖示候選，以及 3 個無可靠配對的名稱。無限迷宮與魔法禁書目錄的選關圖示仍未找到。先前 [檔名對照](../artifacts/dgdb-name-mapping-2026-10-20/README.md) 為 prototype 5 的歷史結果。
- 保留 prototype 2 的地板碰撞盒修正；移除中央白色替代門方塊，原 Boss 入口判定不變。
- prototype 4 以立繪 Alpha 遮罩修復屬性陰影的有色矩形，使用 APK 內的透明 Shader 路徑。
- prototype 6 為敵人血條、回合數及狀態圖示複製專用 UI 材質，使用原 APK 內的 Overlay Shader（queue 3001），在角色 quad（3000）之後繪製；不更改原 Boss 尺寸、血量與選取碰撞。
- prototype 9 修正上述 Overlay 在選單上方顯示的副作用：選單開啟第一幀、選項／退休頁面、關閉過渡及對話框期間恢復原 NGUI 材質；回到一般戰鬥再恢復角色前方的 HUD。
- `res_chara_id_str` 與網站 `draw_id` 的對照由原始 1.0.6 表產生，沒有把新年代資料表覆蓋進舊版。
- 配接器圖片快取會在原場景資源清理時放開非駐留持有關係；實機顯示、點擊、長時間記憶體與全部關卡仍需驗證。

圖片來源與下載調查見 [WEB_ASSET_FINDINGS.md](WEB_ASSET_FINDINGS.md)、[CDN_FINDINGS.md](CDN_FINDINGS.md)；原圖鑑檢查見 [ENCYCLOPEDIA_AUDIT.md](ENCYCLOPEDIA_AUDIT.md)。原始 APK 和先前測試產物均保留。

Android 16 安裝調查見 [相容性報告](../artifacts/android16-compatibility-audit-2026-10-20/README.md)：目前 target SDK 17、僅 32 位元 ARM，舊引擎確有 text relocation，不能只提高 SDK 後便宣稱已支援新版。尚待裝置型號、ABI 與安裝錯誤；未變更目前交付 APK。

## 重現



特定開發配置的指令已移除；重現前應核對建置程式、來源檔及輸入參數。




