# 第三方參考儲存庫

本目錄記錄第三方資料集的分類與取得來源。大型 checkout、媒體備份及生成的預覽頁由 Git 忽略，**不會因 clone 主倉庫而自動存在**。文中統計是 2026-10-20 的驗證紀錄，不代表每位讀者已具備相同檔案。

## 離線圖片預覽

備妥資料集並生成 `image-preview.html` 後，可直接開啟它瀏覽 Wiki 與 DGDB 圖片，無須啟動伺服器或連網。支援檔名／路徑搜尋、來源與資料夾／格式篩選；往下捲動會自動追加下一批 80 張，圖片接近畫面時才載入。也可按「載入更多」。支援放大預覽、鍵盤左右切圖，以及透明棋盤格／白色／黑色背景。資料集原檔與網路來源可從放大視窗開啟。

請將預覽頁留在此目錄，維持與 `dgdb/`、`divine-gate-wiki/` 的相對位置。新增或重新取得素材後，以 `offline/build_image_preview.py` 更新索引；產生器依專案位置尋找資料，不能代替資料集下載。

頁面範本與產生器位於 `offline/image_preview.html`、`offline/build_image_preview.py`。產生的 HTML 內嵌索引但不內嵌圖片，也不修改素材。產物由 Git 忽略。

## 來源與配置

`dgdb/` 用來放置 [Divine Gate DB](https://github.com/kennyfong19931/dgdb) 的獨立 Git checkout，供查閱資料與原始圖片。此目錄不是主專案的 submodule，需另行取得第三方儲存庫。

調查與配圖所用 commit 為 `1a15adf733bb02467d51303a110f361535a7b4bc`；重現時應固定此版本，不以目前分支 HEAD 代替。

當次取得的歷史包含 7 個 commits 與 6,439 個追蹤檔案；歷史檔案內容核對 Git blob ID，`git fsck --full` 通過。當次驗證紀錄分類於 `artifacts/dgdb-clone-verification-2026-10-20.json`，不隨原始碼附送。重新取得的資料仍需自行驗證。

- `offline/`：本專案維護的改造程式、素材配接與測試。
- `external/dgdb/`：第三方儲存庫及其自身的 Git 歷史。
- `external/divine-gate-wiki/`：Fandom 官方 API 取得的頁面 XML、媒體檔案、來源與雜湊清單；[下載方式](../offline/FANDOM_ARCHIVE.md)。
- `artifacts/`：下載調查紀錄、已核對的素材與 APK 建置產物。

常用素材位置：

- `dgdb/public/img/area/`：地圖上下貼圖，例如 `map_0001_tex_lower.png`。
- `dgdb/public/img/icon/`：角色頭像。
- `dgdb/public/img/large/`：角色立繪。
- `dgdb/public/img/panel/`、`dgdb/public/img/trap/`：格子、面板與陷阱圖。

Wiki 素材入口為 `divine-gate-wiki/catalog.html`，支援依原檔名搜尋。已保存 7,354 筆媒體項目及 13,672 頁最新 XML；6,493 筆媒體與官方原檔雜湊相符，861 筆是明確標示的 CDN 版本。媒體總數內含三張 YouTube 縮圖，不含影片串流。詳細驗證見素材庫內的 `archive-report.json` 與 `validation-report.json`。

取得資料時，以來源倉庫 URL 建立獨立 checkout，置於 `external/dgdb/`，再切換至上述固定 commit 並驗證內容。執行配置需依工具設定核對，這些相對目錄不要求特定的容器名稱或開發電腦設定。

`offline/build.py --recovered-assets` 目前仍使用 `artifacts/web-asset-search-2026-10-20` 的素材清單與已核對檔案。此 checkout 不會自動取代該來源；若要改用其中的素材，應先核對版本、檔名、尺寸與雜湊。
