# Divine Gate 1.0.6 缺圖名稱核對

本文保留 2026-10-20 初次缺圖 audit 的需求清單，該次工作未修改 APK。這是歷史需求，不能視為目前仍全部缺失：prototype 19 優先使用匯入快取的原圖，112 項中已有 109 項顯示配接，剩餘 `mugen_box.png`、`panel_panic.png` 與 `panel_weekness.png`。備份版本尚未確定，配接不代表已核實為 1.0.6 原版美術。現況及限制見 [移植指南第 10 節](../OFFLINE_PORTING_GUIDE.md#10-仍缺失或尚未驗證的部分)。

以下 `artifacts/` 清單、預覽與報告不隨原始碼發布；未另附資料集時，對應連結不表示已提供下載。

- [可直接轉交的需求文字](../artifacts/missing-image-audit-2026-10-20/給備份者的補圖需求.txt)：含用途、優先順序、精確名稱及缺模型說明。
- [優先圖片名稱 112 項](../artifacts/missing-image-audit-2026-10-20/priority-image-filenames.txt)：28 個未配對面板／效果／寶箱名稱，加 84 個關卡圖示。
- [原始整張地圖貼圖 83 項](../artifacts/missing-image-audit-2026-10-20/original-map-texture-filenames.txt)：程式實際使用的 res_map，不能只用 27 個模型包名稱推算。
- [候選清單 CSV](../artifacts/missing-image-audit-2026-10-20/missing-image-candidates.csv)：195 個名稱、分類、優先順序及判定限制。
- [較完整的原始下載包清單](../artifacts/missing-image-audit-2026-10-20/preferred-original-bundles.txt)：2 個圖示包及 27 個地圖包。

## 最值得補的圖集

`AtlasPanelIcon`、`AtlasAreaIcon_000_020` 是 1.0.6 程式確實請求的圖集 GameObject 資源名，**未取得原包前不能保證 Texture2D 或 PNG 的實際檔名與之完全相同**。請備份者用這些關鍵字找圖集，並優先保留配套的 prefab、材質或圖集座標資料；若有 IconPackPanel.unity3d / IconPackArea.unity3d 原包更好。

面板候選包括 `panel_b_1`～`panel_b_6`、`panel_r_1`～`panel_r_6`、`panel_y_1`～`panel_y_6`、4 個 heal 圖、`panel_alarm`、`panel_banana`、`panel_panic`、原拼字 `panel_weekness`，以及 `mugen_box`、`toaru_box_01`。清單保留原程式的大小寫與拼字。

這 28 個名稱是現有配接圖集中尚無同名項，**不代表已證明等效圖片像素完全遺失**。某些公開圖片可能改名，或可在其他內建 UI 圖集中找到相似區域。UnityPy 無法解碼本 APK 自訂 MonoBehaviour 的完整圖集座標表，因此沒有把「掃不到 Sprite 名」當成不存在的證據。

## 地圖編號與現有備份

1.0.6 的 MasterDataArea 有 83 個不同的 `res_map` 名稱、84 個 `res_map_icon` 名稱；模型下載包只有 27 種。多個區域可共用一套網格但使用不同貼圖，兩組編號不可混為一談。

83 組對應的 `_lower.png` / `_upper.png` 在當次核對的 DGDB 版本中全部能找到。初期依 27 個模型包只接入 54 張貼圖；後續已補齊 83 種所需變體，prototype 16 封裝 86 組上下圖（含額外 3 組）。原始完整 `map_XXXX_tex` 貼圖仍未取得，不能把分拆圖覆蓋率當作原場景完整復原。

關卡圖示 `area_icon_XXXX` 包括資料表中的活動；圖片需求與區域解鎖是不同問題。活動開放情況應查現行版本說明，不能由此歷史缺圖表推斷。

## 截圖核對

原 APK 共找到 151 個 Texture2D 物件、148 個不同名稱。以下截圖名稱已存在：

- `AtlasMainMenu.png`
- `pq_c_01_tx_6.png`
- `ef_pq_screw_01.png`
- `ef_glowline_01.png`
- `ef_magic.png`

截圖下方被遮住的檔名若為 `pq_c_01_tx_2.png`，原 APK 也已有同名貼圖。`inGameUI_1.png`、`ef_star.png`、`ef_pq_note_01.png` 未找到同名 Texture2D，但版本或匯出命名尚未確認，不直接列為 1.0.6 必缺圖。原 APK 另有 `AtlasInGameUI`、`inGame_UI_tex`、`ef_pq_star_01`，不能只憑相似名稱認定完全相同。

不同版本即使圖集同名，也可能使用不同切圖位置；請保留備份的版本資訊與原始尺寸。原門／地板網格、動畫與碰撞設定依然需要模型或原始 AssetBundle，PNG 本身不包含這些資料。

## 證據

原 audit 產物包括 `original-textures.json`、`screenshot-name-check.json`、`summary.json`。名稱來源是原始 1.0.6 內建 MasterDataArea / MasterDataAssetBundlePath、反編譯 InGameUtil.SetupMaterial 的實際請求，以及當時素材版的 `recovered-images.json`。`original-ui-sprites.json` 為未成功解碼的嘗試，空陣列不代表原 APK 沒有 UI 圖集。
