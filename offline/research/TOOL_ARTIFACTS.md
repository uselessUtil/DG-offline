# Battle Lab 與 Door Motion Lab 交付資料

整理日期：2026-10-26。兩個工具的維護程式已在 [tools/](../../tools/README.md)；本頁保存本機交付包的身分、資料範圍及使用限制。來源S190–S197、S204–S209見 [來源清單](sources.json)。HTML、runtime、圖片與ZIP不隨Git提供。

## 維護來源與實際入口

| 項目 | battle-layout-editor | door-animation-lab |
| --- | --- | --- |
| 倉庫程式與操作說明 | [tools/battle-layout](../../tools/battle-layout/README.md) | [tools/door-lab](../../tools/door-lab/README.md) |
| 本機交付HTML | `artifacts/battle-layout-editor/DG-Battle-Lab.html` | `artifacts/door-animation-lab/DG-Door-Motion-Lab.html` |
| 固定遊戲來源 | restored r17 | restored r18 |
| 用途 | 五位置裁切、普通技橫條、敵人尺寸／位置／血條 | 區域門網格、原始曲線及自訂門片動作 |
| 完整HTML大小 | 282,777,457 bytes（約269.7 MiB） | 80,634,537 bytes（約76.9 MiB） |
| 內嵌圖片 | 3,509 | 272 |
| 調整檔格式 | `dg-battle-presentation` schema1、visual_revision2 | `dg-door-animation` schema1 |
| 設定單位 | 角色及其實際敵人引用 | 區域，同區域所有關卡共用 |

交付HTML已內嵌資料；依使用說明可直接在Chrome／Edge開檔，不需要另啟HTTP server或提供素材目錄。Git中的 `tools/battle-layout/dist/index.html` 與 `tools/door-lab/index.html` 是建置模板，不能取代交付檔。重建需另備指定APK、快取、表格及容器工具；詳見各工具README。

兩者都以來源APK SHA-256與資料版本核對匯入。雖然同屬 `dgdb-2-restored`，r17與r18雜湊、格式與資料對象不同，不能把兩種JSON互相匯入，也不能當成玩家存檔。目前尚無自動寫回APK的配接器。

## Battle Lab：完整與QA版不同

完整工具收錄1,748角色、595個原始參考設定及1,153個新增裁切對象。638角色有敵人引用，其中371有原生幾何、267沿用r17四邊形立繪替代；不是1,748個角色都應在敵人預覽出現。

304個新增中小型尺寸是參考原595角色推定的起點，並非官方逐角色顯示資料。其中只有100個角色有敵人引用，另204個應從全部角色／待補裁切檢視。圖鑑No.788的ダークスパス是角色ID439，沒有敵人引用；角色ID788則是另一角色，搜尋與回寫不可混用兩種ID。

ZIP另附 `DG-Battle-Lab-QA.html`，僅11角色、35圖片、1個原生敵人網格，7,917,385 bytes。它是小範圍QA產物，不能用來聲稱完整工具缺少其餘角色；正式入口是無QA字樣的HTML。S206保存此版本身分。

需保留的顯示契約：

- 原生party0..4對應img4..0；右側隊長為img4，左下好友為img0。
- 普通技橫條垂直UV比例為水平的0.15倍。
- 尺寸包含width、height、pivot及side_offset；只縮圖片而不改間距，仍會產生錯誤排列。
- visual_revision2匯入舊草稿時，只將新增角色四個尺寸欄位全等於舊模板者換成分類起點；手動尺寸、既有裁切及其他覆寫保留。新草稿明確選回大型尺寸也不再被自動重設。
- 若後續套回r17或延伸版，須同步處理HUD runtime override；只改角色主資料不會蓋過r17的窄掛鉤。

S205記錄3,509筆來源PNG、RGBA與無損WebP指紋；不能因壓縮檔位元組不同就認定像素損失。既有報告以RGBA相等核對，本次沒有重新轉碼或視覺比對。

## Door Motion Lab：原始曲線與替代曲線分開

工具列出921個有效關卡、397個區域、136張地圖圖集。25個原始門家族各有4段AnimationArray引用，共100段原始Clip；同CAB中不相關的Clip不算進來。來源CAB及表格雜湊另見S209。

| 動作分類 | 家族數 |
| --- | ---: |
| 雙扇鉸鏈旋開 | 13 |
| 單扇鉸鏈旋開 | 1 |
| 單片升降 | 2 |
| 左右雙片滑開 | 4 |
| 上下雙片滑開 | 3 |
| 八片複合展開 | 1 |
| 四片分層伸縮 | 1 |

這七類是工具依節點與曲線整理的分類。原生狀態索引是OPEN=0、TATAKI=1、CLOSE=2、OPEN_BOSS=3；slot1是敲門，不是Boss待機。原曲線以Hermite切線求值、四元數正規化及父子Transform組合，線性插值不能視為相同動作。

206區域同時有模型家族與貼圖路徑，162區域共用原家族搭配其他貼圖，另29區域缺map_4026_door、map_6001_door或map_6002_door原模型。後者沿用r18的0.3秒分離門片替代；替代slot1只表示Boss門關閉，沒有官方敲門動畫。測試提到104段原生／替代動畫求值，不能將104都稱為恢復的原始Clip。

模型樣板切換會一起換網格、UV與曲線，但保留目前區域貼圖，需人工對位。自訂動作用於開／關門，敲門保留原曲線；時長欄位作用於該區域四個狀態，0保留各段原始時長。預覽速度、鏡頭與倒播不匯出，也不應當成遊戲參數。

## 交付包核對

本次以串流讀取ZIP成員，逐檔核對與目前同名artifacts檔案大小及SHA-256一致；Battle包12個檔案、Door包10個檔案，均不含目錄項目。沒有解包到倉庫或執行HTML。

| ZIP | 位元組 | SHA-256 |
| --- | ---: | --- |
| `artifacts/battle-layout-editor.zip` | 209,180,981 | `03f41c145f06b6226475548eabddaf421c1d80321aaaae18ade6424d8921943d` |
| `artifacts/door-animation-lab.zip` | 60,361,687 | `72130537760b901916282c8467a61b04efb62cba39a8918f2b92d5ff6586539f` |

完整HTML身分分別為 `386492bbbd492214519d519de2a53a70a2ef900f65cc49b061112e3eb59db966` 與 `397e6e87f94ad09c123c0b47d8688b16be92f471c278f0325d5ba8bcae3642e0`，與包內及本機報告一致。ZIP完整相符只證明封裝內容，不能替代執行驗證。

## 哪些已驗證、哪些仍待確認

既有Battle報告有36項Node邏輯檢查，Door有31項，包含來源拒絕、引用合法性及匯入／匯出；Door另核對100段原曲線的鍵值與104段動畫的多時點幾何。兩者已有單檔內嵌資料／圖片與無外部網路依賴的靜態紀錄。

兩份 `standalone-validation.json` 均明列 `browser_visual_verified=false`。預覽JPG是離線光柵參考，不是瀏覽器截圖；此次ZIP核對也沒有補做瀏覽器或Android驗證。

後續人工檢查重點為Battle的五格遮罩、拖曳、橫條及敵人遮擋；Door的WebGL、狀態切換、時間軸、門片UV與動畫。兩者都需確認下載／重新匯入和本機草稿保存。localStorage不是可攜備份，應保留匯出JSON；HTML顯示正確也不等於Unity的相機、光照、粒子或實際遊戲流程相同。
