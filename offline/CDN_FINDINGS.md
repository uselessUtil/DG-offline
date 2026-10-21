# Divine Gate 原 CDN 與素材封存調查

檢查日期：2026-10-20。本文記錄當日 APK 靜態分析、下載及封存驗證結果，不代表網址目前仍維持相同狀態。現行 APK 配接情況見 [移植指南](../OFFLINE_PORTING_GUIDE.md)。

`artifacts/` 為另行保存的調查資料，不隨原始碼發布。文中的相對路徑說明資料分類；未附資料集時，對應檔案不會因 clone 倉庫而自動存在。

## 結論

**調查當日，原 CDN 與 APK 指向的兩個 S3 儲存桶無法提供素材；Wayback Machine 則保留了兩個真正的 Unity AssetBundle，並已取回驗證。**

兩包共解析出 10 張 1024×1024 角色圖片，以及角色 GameObject、Mesh、Material 與 Shader。來源路徑分別標示 1.1.0 與 2.1.0；沒有找到完整 1.0.6 素材集。本次調查本身未修改 APK，不能據此推論後續版本的素材接入狀態。

## APK 中的實際下載位置

從使用者提供的原始 APK 的 `Assembly-CSharp.dll` 靜態分析 `AssetBundleManager.CreatePackagePath` 與 `PatchManager`，不是依檔名猜測伺服器。

1.0.6 的預設前綴：

```text
http://d3btkj3txnv5zz.cloudfront.net/download/900_ReleaseAndroid/v1_0_6_a12thasb6narjpau8afsfn6asfa86fa9/
```

備用來源：

```text
http://gungho-pqdm.s3.amazonaws.com/download/900_ReleaseAndroid/v1_0_6_a12thasb6narjpau8afsfn6asfa86fa9/
```

檔名移除 `[1.0.6]` 標籤。`Unit_` 類素材另依設定插入 `QUALITY_1_1/`、`QUALITY_1_2/` 或 `QUALITY_1_4/`；例如 `QUALITY_1_2/Unit_000_009.unity3d`。`MasterData.unity3d` 與 `IconPackUnit.unity3d` 不插入品質目錄。HTTP 與 HTTPS 均已測試。

2.1.0 使用相同 CloudFront 網域，但前綴改為：

```text
http://d3btkj3txnv5zz.cloudfront.net/900_Release/v2_1_0_9aujw0ejg0aj0w3j0jq30jg0jq09dskcq/
```

角色目錄為 `Android/ResUnit0001/QUALITY_1_*/`；其他分類有 MasterData、Map、Icon、Sound、Scratch 目錄。下載函式亦可附加 `?version=<版本號>`。兩個 APK 中未找到另一個素材 CDN 網域。

## 原站檢查

| 位置 | 實測結果 | 可下的結論 |
| --- | --- | --- |
| `d3btkj3txnv5zz.cloudfront.net` | curl 錯誤 6，系統解析不到位址；Google Public DNS 的 A 查詢亦無 Answer | 目前無法透過這個網域連線，不單憑此斷言後端所有副本已刪除 |
| `gungho-pqdm.s3.amazonaws.com` | HTTP/HTTPS 均為 404，XML 明確回覆 `NoSuchBucket`，BucketName 為 `gungho-pqdm`；區域型 URL 與桶根路徑也相同 | 原備用桶目前不存在，並非單一素材名稱拼錯或只有目錄禁止列出 |
| `s3-ap-northeast-1.amazonaws.com/divinegate/` | 1.0.6/2.1.0 PatchDataList 與 BannerDataList 均 404 / `NoSuchBucket` | 原更新清單桶目前不存在 |

逐筆 URL、HTTP 狀態、錯誤與回應內容見 `artifacts/cdn-investigation-2026-10-20/probes.json`，Google DNS 原始回應見同目錄 `google-dns.json`。

## 取回的原版素材

兩筆封存日期均為 2016-04-08 UTC。下載使用 Wayback 的 `id_` 原始內容模式。兩個檔案均為 `UnityWeb` 格式，Unity 引擎標記為 `4.1.0f4`；下載內容的 SHA-1（Base32）與 CDX payload digest 相符。

| 原站版本路徑 | Bundle | 取回內容 | 實際檔案大小 |
| --- | --- | --- | ---: |
| 1.1.0 | `Unit_000_009.unity3d` | ID 001–009：9 張圖片、9 個角色 prefab、9 個 mesh；另有子物件、材質與 Shader | 949,441 bytes |
| 2.1.0 | `Unit_0973.unity3d` | ID 973：1 張圖片、1 個角色 prefab、1 個 mesh；另有子物件、材質與 Shader | 195,953 bytes |

001–009 的物件名稱為 `tex_chara_l_001` / `chara_prefab_001` 等；973 為 `tex_chara_l_973` / `chara_prefab_973`。兩包都未包含 AnimationClip。本次成功解碼全部 10 張 Texture2D，並目視確認 ID 001 為遊戲主角原圖。

UnityPy 初次解析第一包遇到舊格式的版本字串 `4.1.0f4\n1`；僅在解析器記憶體中取第一行處理，**沒有修改下載的 Bundle**。原始檔、HTTP headers、PNG 匯出與 SHA-256 已保存。

來源：

- [20160408104743-Unit_000_009.unity3d](https://web.archive.org/web/20160408104743id_/http://d3btkj3txnv5zz.cloudfront.net/900_Release/v1_1_0_kh9onea2habnfla4v3g14dast8ausprob/Android/QUALITY_1_1/Unit_000_009.unity3d)
  - 原始 URL：`http://d3btkj3txnv5zz.cloudfront.net/900_Release/v1_1_0_kh9onea2habnfla4v3g14dast8ausprob/Android/QUALITY_1_1/Unit_000_009.unity3d`
  - SHA-256：`24890975487554690834683f7fa79aeac52f14216e0cd467a32cd0290af31acc`
  - 封存資料相對位置：`artifacts/cdn-investigation-2026-10-20/recovered/20160408104743-Unit_000_009/20160408104743-Unit_000_009.unity3d`
- [20160408105235-Unit_0973.unity3d](https://web.archive.org/web/20160408105235id_/http://d3btkj3txnv5zz.cloudfront.net/900_Release/v2_1_0_9aujw0ejg0aj0w3j0jq30jg0jq09dskcq/Android/ResUnit0001/QUALITY_1_1/Unit_0973.unity3d)
  - 原始 URL：`http://d3btkj3txnv5zz.cloudfront.net/900_Release/v2_1_0_9aujw0ejg0aj0w3j0jq30jg0jq09dskcq/Android/ResUnit0001/QUALITY_1_1/Unit_0973.unity3d`
  - SHA-256：`ea108a7227bce79cd6b61ee74f399e55cf46e27ea35a26a677e285f3c920386f`
  - 封存資料相對位置：`artifacts/cdn-investigation-2026-10-20/recovered/20160408105235-Unit_0973/20160408105235-Unit_0973.unity3d`

完整物件清單、圖片尺寸、digest 比對及檔案路徑見 `artifacts/cdn-investigation-2026-10-20/recovered-assets.json`。

## 封存範圍與限制

[Wayback CDN 索引查詢](https://web.archive.org/cdx/search/cdx?url=d3btkj3txnv5zz.cloudfront.net%2F%2A&output=json&filter=statuscode%3A200&collapse=urlkey&limit=1000) 成功回傳兩個不同的 HTTP 200 URL（上限 1000，沒有達到上限）。S3 虛擬主機、全球 path-style 與東京區域 path-style 的查詢均成功回傳空清單。1.0.6 的 MasterData 精確網址在 Availability API 沒有快照。

較大範圍的部分 Wayback 查詢曾回覆 503，之後以編碼 URL 重查成功；不將 503 當成沒有備份。Common Crawl 的兩次查詢回覆 502，該部分無法確認，不宣稱其完全沒有素材。一般網頁搜尋與 Internet Archive 標題搜尋未找到完整 CDN 備份；搜尋無結果也不能證明私人備份不存在。

這 10 張圖片與 prefab 是部分復原，**不能補齊關卡地板、門、Boss 房模型或全部角色資源**。它們可作為後續復原的實際參考，但跨版本載入與遊戲內顯示仍需另外整合、測試，不能僅憑相同 Unity 版本保證可直接使用。

圖鑑 XAPK 分析另找到大量立繪與圖示，詳見 [圖鑑分析](ENCYCLOPEDIA_AUDIT.md)；本次 CDN 封存額外價值在於取得舊遊戲的原始角色 prefab 與 Mesh。若要繼續尋找完整素材，較有機會的來源仍是保留下載快取的舊手機、模擬器備份或玩家留下的資料備份。
