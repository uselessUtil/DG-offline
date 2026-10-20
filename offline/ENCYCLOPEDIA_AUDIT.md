# Divinegate Encyclopedia 1.0.0 素材與來源檢查

檢查日期：2026-10-20。來源：使用者放入專案的 `Divinegate+Encyclopedia_1.0.0_APKPure.xapk`；檔名所述下載站不是來源已驗證的證據。

## 結論

**可以補回大量角色立繪與圖示，不能補齊全部遊戲素材。** 對照原始 1.0.6 的 523 筆角色資料，排除 ID 0 的資料整合列後，522 筆中有 **399 筆（76.4%）同時找到立繪與圖示**，123 筆未找到。這是素材編號的覆蓋率，並非已在 Android 上驗證 399 名角色的所有顯示情境。

本次只在既有 Docker 容器靜態讀取、解包與解析，沒有安裝或執行圖鑑 APK，也沒有把檔案送到外部掃描服務。現有離線 APK 未修改。

## 素材結果

| 素材包 | 實際內容 | 數量 |
| --- | --- | ---: |
| `units_0000.bin` | 角色立繪 | 664 |
| `units_1000.bin` | 角色立繪 | 503 |
| `units_2000.bin` | 角色立繪 | 376 |
| `units_9999.bin` | 角色立繪 | 517 |
| `unit_icons.bin` | 128×128 角色圖示 | 2,060 |
| `art.bin` | 額外插畫 | 170 |
| `art_icons.bin` | 插畫縮圖 | 170 |
| `bgm.bin` | AudioClip 音樂 | 51 |

角色立繪合計 2,060 張。每張圖另有一個 Sprite 物件；表中沒有將 Texture2D 與 Sprite 重複計數。立繪包含 512×512 與 1024×1024 的 RGBA32 圖片，角色圖示為 RGB24。音樂清單已讀取取樣率、聲道與時長，尚未逐首解碼或試聽。

抽樣匯出並目視檢查了素材 ID 1、4、7、28、267、610 的立繪與圖示，能正常解碼，立繪透明背景保留。預覽位於 `artifacts/encyclopedia-audit/preview-sheet.jpg`，個別 PNG 位於其 `previews/` 子目錄。

1.0.6 的缺圖名單包含《STEINS;GATE》、《FAIRY TAIL》、《進擊的巨人》、《魔法禁書目錄》及其他合作角色。這是從舊資料表名稱與缺失編號得到的結果；未據此斷言圖鑑省略素材的授權原因。完整 123 筆名單在 `coverage-summary.json`，逐筆結果在 `coverage-106.csv`。

八個大型素材包只有圖片、Sprite、AudioClip 與包索引，沒有 GameObject、Mesh 或 AnimationClip。另查 APK 的啟動畫面資料、分段 sharedassets0，以及 OBB 的 level1/sharedassets1：level1 的物件為 UnitPanel、UnitZoomPanel、WallpaperPanel、SoundPanel 等圖鑑 UI。**本次未找到可用來復原 1.0.6 關卡地板、門模型或門動畫的資源。** 圖鑑有少量 UI 動畫，不代表包含舊遊戲的關卡動畫。原遊戲資料表、關卡邏輯及伺服器行為也不會因取得這些圖片而自動復原。

## 安全與來源證據

- 套件為 `jp.gungho.divingate.encyclopedia`，名稱 `ディバゲ図鑑`，versionName `1.0.0`，versionCode `10018`，minSdk 17，targetSdk 27。XAPK 宣告與 APK manifest 相符。
- `apksigner verify --verbose --print-certs` 驗證 v1、v2 簽章通過。憑證 SHA-256：`142e66efb58161464c14e9869dfb79821b664d70ac43021282f32e19fae2788a`。憑證主體自述 GungHo；自述名稱不能獨立證明發行者身分。
- 原始 1.0.6 APK 的憑證 SHA-256 為 `9623d6804d17d3abe7741709ff7ce2bcb98b1fb0d0782c10dfc7d68569d00bbc`，與圖鑑不同。不同應用可能使用不同憑證，單憑差異不能判定被重簽，也不能用舊遊戲的憑證替圖鑑背書。本次未取得可信的官方圖鑑憑證指紋供對照。
- v1 驗證有 `META-INF/services/com.fasterxml.jackson.core.JsonFactory` 未受 JAR 簽章保護的警告；整體 v2 驗證仍通過。這項警告本身不是惡意程式的證據。
- APK 簽章驗證的範圍是 APK 本身，**不會替外層 XAPK 與外掛 OBB 驗證官方來源**。ZIP CRC 與本地 SHA-256 可記錄完整性，沒有可信對照值時不能認證來源。
- 權限有網路、讀寫外部儲存、網路／Wi-Fi 狀態、Google Play 授權檢查與喚醒鎖。manifest 沒有宣告簡訊、通訊錄、麥克風、相機、無障礙服務或裝置管理員等權限／元件。
- Java 入口 `jp.gungho.PermissionManager.MainActivity` 繼承 UnityPlayerActivity，檢視的自訂 Java 元件主要處理權限與廣告 ID。另包含 Google Ads、Keen、Treasure Data 等廣告／分析相關程式線索。動態類別載入關鍵字的命中位於 Google Ads 程式內，不能僅憑關鍵字推論惡意行為。
- 本次不是完整惡意程式鑑識：未完整逆向 IL2CPP 原生遊戲程式、未做行為沙箱或病毒掃描。因此結論為 **簽章有效、素材可讀，來源與執行安全尚未獲得保證**，不能標示為「安全無毒」。

後續接入宜只取出並重新編碼成 PNG 等資料格式的圖片，沿用現有離線版的程式；不需要安裝圖鑑，也不需要把圖鑑的 DEX、SO、IL2CPP 程式或 SDK 放入離線 APK。

## 接入 1.0.6 的方式與限制

圖鑑使用 Unity 2018.2.1f1 的 UnityFS 包，原遊戲是 Unity 4.1.0f4。應採取匯出圖片、重新編碼、再由現有配接器讀取的方式；不能假設把新版本 bundle 放進舊版引擎就能使用。

舊版實際載入規則經 `AssetBundleUtil.RequestCharaTexture` / `RequestCharaMesh` 檢查，是依角色 ID 產生 `tex_chara_l_###` / `chara_prefab_###`。覆蓋率依舊表的 `res_chara_id_str` 對照圖鑑的 `unit_####` 與 `chara_icon_####`，不是用 `draw_id`；後者不是同一組素材編號。接入仍需處理舊版顯示尺寸、透明邊界、圖示框與裁切座標，並在 Android 檢查角色詳情、隊伍圖示及戰鬥顯示。缺少的 123 筆應保留原有替代圖。

## 檔案指紋

| 檔案 | bytes | SHA-256 |
| --- | ---: | --- |
| 使用者提供的 XAPK | 1,441,348,970 | `c9e4c7cac1305168f0b1fac1dc1843842054e46f32dee0a8358d54b69f562db2` |
| 內含 APK | 32,716,506 | `975403b61a80234a279c7b8e89ca9ba2f990020010ac7c7afd25e4dcb71f4aa9` |
| 內含 OBB | 1,408,560,579 | `591ca9530652bb0c11d04306103380b46c6605f2137321beff84851e58d65a0e` |

內含 APK 原檔名：`jp.gungho.divingate.encyclopedia.apk`。OBB 原檔名：`Android/obb/jp.gungho.divingate.encyclopedia/main.10018.jp.gungho.divingate.encyclopedia.obb`。

## 重現與證據

`offline/audit_encyclopedia.py` 讀取容器暫存目錄中已抽出的八個 `.bin`，輸出清單、覆蓋率及抽樣預覽。輸入的舊表是從原始 1.0.6 的 TextAsset 取得的 JSON；暫存解包內容沒有作為維護中的遊戲原始碼。

本次使用：

特定開發配置的指令已移除；重現前應核對建置程式、來源檔及輸入參數。


