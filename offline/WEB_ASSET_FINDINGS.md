# Divine Gate 網路素材搜尋結果

檢查日期：2026-10-20。以下為該次調查紀錄。

後續補查：依使用者指出的 DGDB 命名差異，另補抓 118 張地圖變體及 4 張效果圖示候選，逐檔 Git blob、大小、PNG 驗證通過；累計保存 1,501 張圖片。原 MasterDataArea 需要的 83 組 lower/upper 全部取得，prototype 5 封裝 86 組（含模型包名稱額外 3 組），但上層幾何尚未重建。3 個新增面板／寶箱別名及未確認項目見 [命名比對報告](../artifacts/dgdb-name-mapping-2026-10-20/README.md)。以下 1,379 張、54 張地圖與「未修改 APK」描述保留為初次搜尋的歷史結果。

## 結論

**找到比原 CDN 封存完整得多的玩家備份，並已取回與 1.0.6 對應的 1,379 張圖片，共 76,178,082 bytes（約 72.65 MiB）。**

主要來源是 [kennyfong19931/dgdb](https://github.com/kennyfong19931/dgdb)，專案自述為 Divine Gate 角色資料庫網站。固定來源 commit 為 `1a15adf733bb02467d51303a110f361535a7b4bc`。沒有執行該網站程式、安裝其套件或匯入 SQL 到資料庫；僅讀取目錄、圖片與資料表。

本次沒有修改、重建或安裝離線 APK。這份備份可大幅補回圖片，但仍未取得完整舊版 Unity 關卡模型與動畫，不能宣稱已恢復完整遊戲。

## 已下載與驗證

| 類別 | 已取回數量 | 驗證與用途 |
| --- | ---: | --- |
| 角色立繪 | 522 | 對應 1.0.6 排除整合列後全部 522 筆角色；全數有透明資訊 |
| 角色圖示 | 522 | 全部 128×128，對應相同角色 |
| 地圖上下背景貼圖 | 54 | 1.0.6 的 27 個 `map_XXXX` 名稱，每個各有 upper/lower 兩張 |
| 面板、鑰匙、寶箱等圖片 | 245 | 包含多個年代與合作活動，後續需挑選適合 1.0.6 的部分 |
| 陷阱圖片 | 36 | 亦包含後續版本內容，不能直接視為全屬於 1.0.6 |
| **合計** | **1,379** | **逐檔大小、Git blob SHA-1 與 PNG 解碼皆通過，0 個失敗** |

角色大圖中，270 張為 1024×1024、238 張為 512×512、14 張為 256×256。522 張立繪的 SHA-256 各自不同；522 張圖示也各自不同。這代表檔案能讀取且沒有整批重複成同一張替代圖，**不是對每張圖都已人工鑑定或在 Android 驗證**。

來源為玩家整理的 PNG，部分使用調色盤格式、不同解析度；Git 雜湊比對只證明下載檔與該公開備份相同，不等於原 CDN AssetBundle 的位元組完全相同。522/522 是圖片檔案與資料表的覆蓋率，不能當成遊戲執行相容率。

抽樣目視確認了主角、クリスティーナ（紅莉栖外觀）、エレン（艾連）及地圖背景。先前圖鑑檢查列出的 123 筆缺失角色，這份備份都能找到對應立繪與圖示檔，包含 STEINS;GATE、FAIRY TAIL、進擊的巨人、魔法禁書目錄等合作角色。

## 編號對照：不能直接以舊素材編號找圖

備份檔名使用 `draw_id`（圖鑑編號），舊 APK 載入角色時使用 `res_chara_id_str`，兩者不同。例如：

| 角色 | 舊 fix_id / 素材編號 | 圖鑑 draw_id | 備份立繪檔名 |
| --- | --- | --- | --- |
| クリスティーナ | 347 | 227 | `public/img/large/227.png` |
| ナツ | 379 | 303 | `public/img/large/303.png` |
| エレン | 468 | 404 | `public/img/large/404.png` |
| 上条当麻 | 577 | 515 | `public/img/large/515.png` |

這是本次網站備份的命名規則，不應據此推論圖鑑 XAPK 使用相同命名規則。

從備份的 `database/dump_20170921.sql` 唯讀解析出 2,695 筆 unit 資料，解析錯誤為 0。1.0.6 的 522 筆 `fix_id` 與素材編號均能與新資料表對上；名稱 521 筆相同，另 1 筆是 `リヴァイ兵長` / `リヴァイ兵士長` 的文字差異（draw_id 413）。備份網站的 PHP 顯示程式也以 `draw_id` 選圖，與實際圖片相符。

完整對照存於 `artifacts/web-asset-search-2026-10-20/character-map-106.csv`。後續接入時，應用這個對照把下載圖片連到舊素材編號，並另處理尺寸、裁切和顯示位置。

## 地圖資源的實際範圍

已取回的 54 張圖是 `map_XXXX_tex_lower.png` / `map_XXXX_tex_upper.png`：lower 為 552×699，upper 為 1024×315。它們可補背景，但不包含門、地板或房間的完整 3D 網格、Collider、材質配接與動畫。

1.0.6 內的 27 個 map bundle 名稱在此備份均有上下貼圖；這不是宣稱 27 個完整關卡 bundle 都已取得。完整清單在 `dgdb-coverage-106.json`。

## 其他搜尋到的位置

| 位置 | 實際檢查結果 | 使用價值 |
| --- | --- | --- |
| [統合世界圖鑑](https://dg-pedia.koga.me/) | 網站仍可讀，角色、故事、合作與關卡頁面可用；合作角色圖庫的 JPG 抽樣下載成功（227.jpg 為 1000×781 RGB） | 補資料、確認名稱與外觀；JPG 無透明背景，本次優先保留 GitHub PNG |
| [Divine Gate DB 的資料與圖片目錄](https://github.com/kennyfong19931/dgdb/tree/master/public/img) | 目錄列有 large 2,697、icon 2,809、area 452、panel 245、trap 36 個檔案 | 本次主要素材來源；大目錄數是列檔結果，未全量下載所有年代角色 |
| [資料庫 SQL](https://github.com/kennyfong19931/dgdb/blob/master/database/dump_20170921.sql) | 16,118,204 bytes，已取回；含 unit、quest、quest_floor、enemy、panel 等表 | 版本對照與後續資料研究；不能直接覆蓋舊版本資料 |
| [公開音訊目錄](https://github.com/kennyfong19931/dgdb/tree/master/public/asset) | 列出 12 個 anime_se ZIP 與 10 個獨立 WAV/MP3；抽樣下載 20160107 ZIP 並確認內容、CRC（125 個音訊檔，CRC 通過） | 有後期角色語音與音效線索；不保證屬於 1.0.6，尚未完整下載或逐首試聽 |
| [Anson5951/godot-game-1](https://github.com/Anson5951/godot-game-1) | 小型 Godot 重製專案，只有少量角色和面板圖、戰鬥腳本 | 可參考玩法，未找到完整原版資源備份 |
| [KiyamaMizuki/Divine-Gate](https://github.com/KiyamaMizuki/Divine-Gate) | Swift / SpriteKit 練習專案，含圖片、截圖與一般場景資源 | 未找到原版 Unity bundle；不將其場景素材當作原遊戲模型 |
| [wonliao/DragMe](https://github.com/wonliao/DragMe) | 2014 年 Corona / Lua 模仿操作練習 | 並非完整原遊戲素材 |
| [pigmon/divine_img](https://github.com/pigmon/divine_img) | 從中文 Wikia 抓圖的腳本，專案本身沒有保存圖片 | 提供另一個圖片來源線索 |

Spriters / Models / Sounds Resource 的站內搜尋本次回覆 403，因此無法確認其目錄；沒有將禁止存取誤寫成「沒有資源」。一般日、英、中關鍵字搜尋亦出現許多動畫、同名作品、周邊與 AI 圖片，未把這些結果算作原遊戲素材。

本次未找到可直接下載的完整舊遊戲快取、私服伺服器備份或完整關卡 AssetBundle。這是搜尋結果的範圍限制，不代表私人備份或未索引檔案不存在。

## 本機保存與後續使用

- 已驗證圖片：`artifacts/web-asset-search-2026-10-20/recovered-dgdb/public/img/`。
- 下載結果與數量：`dgdb-download-summary.json`。
- 每張圖的固定來源 URL、Git blob SHA-1、SHA-256、大小與尺寸：`dgdb-downloaded-assets.json`。
- 舊版角色對照：`character-map-106.csv`、`dgdb-coverage-106.json`。
- 前次缺失角色的補圖結果：`dgdb-missing-coverage.json`。
- SQL 與靜態顯示程式樣本：`samples/database/`、`samples/app/`、`samples/resources/`。
- 其他站點與 GitHub 搜尋原始回應：本產物目錄中的 JSON / TXT。

資料與圖片目前僅保存於產物目錄，尚未合入離線 APK。接入優先順序可先恢復角色立繪與圖示，再加背景和面板；門與地板模型仍需從其他快取取得，或依既有遊戲程式要求重建。
