# Divine Gate Wiki 素材備份

來源：[Divine Gate Wiki](https://divine-gate.fandom.com/)。素材庫的專案相對位置為 `external/divine-gate-wiki/`，不隨 Git checkout 提供；需另行取得或建立備份。本文統計依據為 2026-10-20 的封存與驗證紀錄，不代表當前網站內容。

2026-10-20 已完成：7,354 筆媒體項目、7,295 個去重後檔案（1,534,836,337 bytes），以及 13,672 頁最新版本 XML、274 個匯出批次。6,493 筆媒體與官方原檔大小／SHA-1 相符，861 筆為已標示的 CDN 版本；總數包含三張 YouTube JPEG 縮圖，不含影片串流。所有媒體的 File 描述頁都已包含，下載與匯出沒有未完成項目。獨立離線驗證通過，結果保存在素材庫 `validation-report.json`。

## 方式與範圍

使用官方 MediaWiki API 列出媒體與所有可列舉的非負名稱空間頁面。每個媒體保存最新上傳的原始檔，每個頁面保存目前一個 revision 的 XML，包含檔案描述頁、模板與分類頁。這不是完整編輯／上傳歷史、刪除或私人資料、Fandom 網站 HTML 鏡像。

調查當日，統計頁回覆 HTTP 403；官方 `api.php` 的公開列檔與 XML 匯出則可正常使用。本工具不繞過登入或存取限制，也不向 Wiki 寫入內容。

CDN URL 帶 `format=original`。實測同一個 `000-icon.png` 預設網址會傳回 WebP 轉檔，雖然網址仍以 PNG 命名；加上原圖參數後才與 API 的原始大小及 SHA-1 相符。部分 JPEG 即使要求原格式仍與上傳紀錄不同；只有格式、尺寸與解碼都通過才另存為明確標示的 `cdn_variant`，不冒充位元組相同的原檔，也不在本機重新編碼。

## 執行與續傳

下載入口為 `offline/archive_fandom.py`。以 `--output` 明確指定資料集目錄，`--workers` 設定媒體下載並行數。預設輸出及快取搜尋仍有固定位置設定，重用前應核對原始碼；不要把原設定視為所有配置都適用。

預設媒體下載同時四個，API 匯出循序執行並限制請求頻率；遇到暫時性錯誤／限流會重試並遵循 `Retry-After`。`--workers 1` 可降低並行。以相同輸出目錄續傳時，重用已保存的清單與有效檔案，再嘗試未完成項目。

完成後由 `offline/verify_fandom_archive.py` 驗證，以位置參數明確指定同一資料集目錄。驗證可離線執行，但前提是已具備媒體、XML 及 metadata；只有程式碼不足以重現封存結果。

## 產物

- `catalog.html`：可離線依原檔名搜尋、預覽與開啟圖片。
- `images/`：依實際下載內容 SHA-1 命名，避免檔案系統命名限制、大小寫碰撞與重複儲存；原檔名保存在清單中。
- `pages/current-*.xml`：每批最多 50 頁的 XML 匯出；可以重建頁面來源，並非直接顯示的 HTML。
- `metadata/siteinfo.json`：Wiki 名稱、統計、名稱空間與站點授權資訊。
- `metadata/images.json`、`pages.json`：官方 API 的完整列檔結果。
- `metadata/image-manifest.json`：原檔名、來源／描述頁網址、上傳者、時間、API 原始大小／SHA-1、實際下載大小／SHA-1／SHA-256、尺寸、相對路徑及原檔／CDN 版本狀態。
- `metadata/page-exports.json`：XML 的 SHA-256、page ID 覆蓋與失敗紀錄。
- `archive-report.json`：列檔／下載／匯出總數與未完成項目；只有失敗清單皆空時，工具才成功結束。
- `validation-report.json`：獨立讀取實際檔案後的雜湊、圖片、XML 及清單覆蓋檢查。

官方清單另含三筆 `video/youtube`，CDN 提供的是 480×269 JPEG 縮圖，並非影片串流。縮圖與來源描述仍會保存，在目錄明確標為 `external_video_thumbnail`；不宣稱已下載 YouTube 影片本體。

會先比對既有 DGDB 及七個 Fandom 城鎮圖片的檔案 SHA-1，只有與官方 API 原檔一致的快取才會重用。所有圖片都比對 API 原始大小、SHA-1；只有完全相符的才標示 `verified`。PNG／JPEG／GIF／WebP 另檢查尺寸及檔案解碼；CDN 版本標示 `cdn_variant`，在清單與瀏覽目錄都顯示差異。所有保存的媒體記錄實際大小、SHA-1 與 SHA-256，XML 核對預期 page ID 與每頁一個目前版本。

站點文字授權以保存的 `rightsinfo` 為準，圖片的來源與授權需參考各自的 File 描述頁；不將整個遊戲圖庫一概宣稱為同一種授權。素材來源與授權描述會一併保留。

這份素材庫不會自動覆蓋 DGDB 已核對的素材、重新編碼圖片或修改 APK。後續配接仍須核對遊戲版本、編號、尺寸與透明資訊。

官方參考：[MediaWiki Allimages API](https://www.mediawiki.org/wiki/API:Allimages)、[MediaWiki Export](https://www.mediawiki.org/wiki/Help:Export)、[Fandom 資料庫備份說明](https://community.fandom.com/wiki/Help:Database_download)。
