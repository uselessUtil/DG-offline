# Divine Gate 1.0.6 ARM64 移植工程（尚無 APK）

本目錄保存移植工具；還原的遊戲工程放在 隔離工具環境的暫存區，並另存封存檔於
`artifacts/1.0.6-arm64-port1/`。它不是原始 Unity 開發工程，也尚未確認能執行。
prototype 17 的 APK 保持原位、位元組不變。

## 已完成

- Docker 安裝 Unity 2019.4.40f1 Linux Editor 與 Android Player 模組。
- 從 prototype 17 還原 6 個場景、791 個 Prefab、643 份動畫。
  使用 Unity 4.1 序列化格式匯出，未透過刪除動畫繞過匯出錯誤。
- 保留 1,223 份離線圖片，逐份比對 prototype 17 的 SHA-256。
- 合併還原腳本的編譯順序，保留腳本 GUID，重建離線介面與圖片讀取方式。
- 遷移舊 Component 捷徑、字串 AddComponent、AssetBundle、WWW 標頭、音訊初始化、
  已移除的 WebPlayer 平台比較與 UnityScript 的 Boo 轉型呼叫。
- 855 份執行期 C# 程式已通過 Unity 2019 API 參照下的獨立編譯；
  Editor 建置入口亦通過獨立編譯。這兩項不是 Editor 匯入或 APK 建置成功。

## 尚未完成與阻擋

1. 此次 Editor 匯入與建置未通過授權啟用階段。操作者須依官方流程取得並啟用有效授權，
   再驗證 Editor 匯入；帳號、密碼及授權檔均不應提交 Git。
2. 場景、Prefab、動畫與 26 份還原 Shader 尚未經新版 Editor 匯入與渲染驗證。
3. 舊 `MySharedPreferences`、`LocalStorage` 原生外掛，以及裝置資訊、WebView、
   分析與 Google 服務的自訂 Java 元件尚未移植。原本 ARM32 `.so` 不可用於 ARM64。
   `AndroidDeviceInfo.Awake()` 會呼叫舊裝置外掛，因此即使未來成功打包，
   仍須先處理此處，不能直接推定可進入遊戲。
4. Android SDK、NDK r19、JDK 8 的對應模組仍待補齊；未執行 IL2CPP/AOT 建置。
   反射、泛型 AOT、舊網頁內容與 Android 16 行為仍需檢查。
5. 尚無 ARM64 APK，未安裝到平板，未做啟動、存檔或遊戲驗證。

## 隔離環境中的重現方式

在依倉庫工具政策建立的隔離環境內執行，工作目錄設為倉庫根目錄。另備指定版本的 Unity Editor、Android 建置模組、AssetRipper 匯出與 firstpass 來源。先核對腳本內的工作根目錄、Unity API 參照及 Mono.Cecil 工具位置；目前它們仍有預設常數，並非任意環境直接可用。下列大寫名稱須替換成自己的隔離環境路徑，生成工程須位於倉庫之外：

```sh
python3 offline/arm64/prepare_project.py \
  --recovered EXPORTED_PROJECT \
  --firstpass FIRSTPASS_SOURCE \
  --project NEW_PROJECT_OUTSIDE_REPOSITORY \
  --report NEW_PROJECT_REPORT.json
python3 offline/arm64/check_compile.py NEW_PROJECT_OUTSIDE_REPOSITORY
```

`prepare_project.py` 需前一階段的 AssetRipper 匯出與 prototype 17 建置暫存內容；
封存的完整工程可避免未來重建時依賴這些暫存資料。
`api-edits.json` 保存逐行轉換；只修改編譯器確認為過時 API 的位置，
保留合法的 `RaycastHit.collider`。`migrate_api.py --diagnostics <log>`
可依新的編譯診斷追加規則，不應套用到原版 APK 或 32 位元流程。

啟用完成、補齊工具與修正剩餘外掛後，Editor 建置入口為 `BuildArm64.Build`。
必須以環境變數設定新的輸出路徑及私有簽章資訊：
`DG_ARM64_APK`、`DG_ARM64_KEYSTORE`、`DG_ARM64_KEYSTORE_PASS`、`DG_ARM64_KEY_ALIAS`。
建置器拒絕覆寫任何既有輸出檔，目標設定為 ARM64/IL2CPP、min SDK 21、target SDK 29，
獨立套件名稱 `jp.gungho.dg.offline106.arm64`。這些是預定設定，並非已產出的 APK 資訊。

此舊版 Editor 的 Linux 相依程式庫與相容性設定須依自己的工具環境核對；倉庫 Dockerfile 尚未完整定義 ARM64 移植工具鏈。Editor 安裝、生成工程與編譯結果不隨 Git checkout 提供。重建隔離環境前應保存必要的工程封存檔與 `toolchain-provenance.json`，並排除授權、簽章及裝置資料。

## 來源

- [Unity Editor 官方版本資料](https://services.api.unity.com/unity/editor/release/v1/releases?version=2019.4.40f1&limit=1)
- [AssetRipper 官方專案](https://github.com/AssetRipper/AssetRipper)，本次有效匯出使用 1.3.14。
- [Unity Personal 資格與註冊入口](https://unity.com/products/unity-personal)

版本來源與雜湊詳見產物目錄的報告；授權、簽章金鑰、憑證與裝置序號不包含於封存檔。
