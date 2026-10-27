# 素材覆蓋與原站保存

本頁整理既有調查，沒有重新連線檢查網站。來源編號見 [sources.json](sources.json)。

## 舊快取可提供什麼

S09、S10：`jp.gungho.dg` 有 232 檔、311,305,205 bytes，其中 100 個 CAB、102 個索引、30 個 shader。CAB 全部可解析為 Unity 4.1.0f4／Android；含 791 個 Texture2D、738 個 Mesh、120 個 AnimationClip、31 份 MasterData TextAsset。沒有在此備份發現 APK、原生引擎、音訊物件或帳號存檔。

| 1.0.6 需求 | 覆蓋 |
| --- | --- |
| 522 個有效角色 | 全部有 prefab 名稱、立繪、頭像；原 Mesh／UV／材質關聯已解析 |
| 101 個有效區域 | 99 區有地圖與門模型／貼圖 |
| 84 個選關圖名稱 | 全部有對應 |
| 6 個圖集 | 805 個原切圖框；TexturePacker JSON 與 UIAtlas 逐框一致 |
| 原角色空格與鎖頭 | `chara_icon_empty`、`lock1` 已找到 |
| 門動畫 | 已覆蓋地圖的四個動畫槽與曲線目標有對應 |

原 1.0.6 的 `mesh_door_boss` 全為空，不能把 0 個匹配說成缺 101 個 Boss 門。備份含更多年代的資源，也不能將其全部計成 1.0.6 需求。

## 已接入與仍缺失

prototype 19 匯入原像素與圖集；prototype 20 加入本機原生載入，將 CAB 原位元組封裝進 UnityRaw v3，恢復 prefab／網格／材質／動畫關聯。missing／error／timeout 保留簡化 fallback。這是實作狀態，Android GPU／動畫／觸控仍待驗證（S11）。

| 缺口 | 影響 |
| --- | --- |
| `map_6001_mesh/door/tex`、`ef_stage_6001` | 萬聖節區原模型、門動畫與環境效果仍缺 |
| `map_6002_mesh/door/tex`、`ef_stage_6002` | 聖誕節區同上 |
| `ef_stage_0012` | 第五監獄模型與門動畫在，僅環境效果缺 |
| `mugen_box`、`panel_panic`、`panel_weekness` | 三個圖片需求未找到精確來源 |
| 外部下載音訊 | 舊快取沒有 AudioClip；原 APK 內建音訊仍可用 |
| 各聯動池專屬入口美術 | 未找到可辨識的完整原 Banner；選池使用共用圖 |

較早「原生模型尚未接入」「Empty／Lock 還缺圖」「選關圖只能裁切」等說法，必須依報告版本解讀，不能套用到 prototype 20 以後。

S10 的角色紫色陰影調查也值得保留：原角色使用輪廓 Mesh 與 Opaque 深度寫入；前期矩形替代加 Transparent／ZWrite Off 破壞了遮擋契約。修復須連同 Mesh、UV、材質和陰影流程處理，不能只把矩形改不透明。靜態原因分析不等於已重現所有 Android 排序時序。

維護入口：[快取盤點](../audit_cached_backup.py)、[圖片匯入](../backup_images.py)、[原生包](../native_bundles.py)、[執行載入器](../NativeAssets.cs)、[原生覆蓋盤點](../audit_native_restore.py)。

## 原伺服器與官方圖鑑保存

S12 記錄 2026-10-21 的查詢結果，**沒有取得可用的原伺服器 MasterData**：

| 來源 | 當次結果 | 可下結論 |
| --- | --- | --- |
| 原 API／舊 CloudFront | DNS 無可用位址 | 當時不能透過該名稱下載 |
| 兩個舊 S3 桶名 | `NoSuchBucket` | 該桶名當時不存在 |
| 舊官方頁面 | 多數轉公司首頁 | HTTP 200 不能算取得遊戲資料 |
| 官方感謝頁 | 保存 10 個有效檔案 | HTML、背景、按鈕，不含主資料 |
| Wayback 官方圖鑑 | 1,103 個 URL；保存且核對 digest 266 檔 | 236 HTML、30 JPEG，整理 222 個角色頁 |
| 未成功圖鑑項目 | 837 筆 | 保留待查清單，不能算已保存 |
| Common Crawl／部分封存查詢 | 429、連線失敗或 504 等 | 未確認，不能寫成不存在備份 |

官方圖鑑快照分布於 2015-03-27～2017-07-19 UTC，並非同一版伺服器快照；有後期調整與 Link 內容。圖鑑號不能自動當作舊 APK 的 `fix_id`。這些頁面尚未轉為完整 MasterData 或接入遊戲。

原 CDN 保存的兩包是 2026-10-20 已取得、後來再次核對的成果，不能重複計為新發現：

| 路徑版本 | bundle | 已辨識內容 |
| --- | --- | --- |
| 1.1.0 | `Unit_000_009.unity3d` | 001–009 貼圖及 prefab／mesh／材質 |
| 2.1.0 | `Unit_0973.unity3d` | 973 貼圖及 prefab／mesh／材質 |

另外保存的 1.0.5 內建表來自 APK，而非伺服器。31 個文字物件包含 29 個 JSON 陣列與 2 個設定物件；28 份後端需求都有對應。不得混稱為「從網路找回 31 張伺服器主表」。

## 既有資料入口與優先順序

| 來源 | 維護文件 | 最適合用途 |
| --- | --- | --- |
| DGDB | [網路素材調查](../WEB_ASSET_FINDINGS.md)、[主資料整理](MASTERDATA.md) | 後期 SQL、圖片、名稱對照；先核對年代及 ID |
| 圖鑑 XAPK／OBB | [圖鑑調查](../ENCYCLOPEDIA_AUDIT.md) | 圖片與音樂候選；單獨對 1.0.6 僅 399／522 角同時有立繪與圖示 |
| Fandom 備份 | [Wiki 保存](../FANDOM_ARCHIVE.md) | 名單、文字、活動與年代交叉核對；最新 revision 不是 2014 數值 |
| CDN 封存 | [CDN 調查](../CDN_FINDINGS.md) | 少量原生 bundle 與來源證據 |
| 早期缺圖調查 | [缺圖說明](../MISSING_IMAGES.md) | 歷史需求分類；現況須再對照舊快取恢復結果 |

對已有原快取的角色／場景，優先保留原資源關聯；需要補圖時依目標版本的資源 ID 映射。DGDB 的 PNG、Wiki 縮圖和裁切預覽不會提供原門動畫。不同來源覆蓋率不可直接相加，也不代表跨引擎載入成功。

原站查詢回應、網頁、圖片、解包資料與完整來源清單仍保存在本機 `artifacts/`，未納入此文件整理。第三方內容權利範圍見 [聲明](../../THIRD_PARTY_NOTICES.md)。


## 2.9.1 restored r17 的UI來源與裁切

本機DG0稽核的86包提供具名圖集，新增139個DG0圖塊；來源記錄另含2個DGDB PNG與1個mugen_box替代，共142筆。搜尋範圍、尺寸契約、沒有找到原生img_0..4的證據界線，以及146項人工檢查的互斥分類見 [r17 UI證據](V291_UI_EVIDENCE.md)。原595角色不套推估裁切；新增角色的校準結果不是官方metadata，預覽矩形也不是Android遮罩呈現。


## DG0深度盤點與技能橫條

2026-10-27的新增研究區分頭像圖集矩形、五格HUD與技能橫條UV：DG0本機86包／833物件有頭像圖集資料，但沒有找回逐角色HUD或橫條裁切表。另以595個舊角色研究頭像定位與橫條取樣的關係，僅能提供推估起點。搜尋範圍、原生UV換算及分組驗證限制見 [裁切資料證據](CROP_DATA_FINDINGS.md)，來源S214–S216；未將推估值套入遊戲。
