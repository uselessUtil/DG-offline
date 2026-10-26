# DG Door Motion Lab

交付入口：`artifacts/door-animation-lab/DG-Door-Motion-Lab.html`。單一離線 HTML，所有貼圖、模型與曲線內嵌，Chrome／Edge 雙擊即可使用，不需要 HTTP server、Docker 或素材目錄。`index.html` 是原始建置模板，不能單獨當成品交付。

固定來源為 r18 `artifacts/2.9.1-restored-r18-enemy-size`，APK SHA-256 `d90c903f172bfca44ca8f0138b3dc465bc82611ad648db0e5c593a6fac48f4a4`。以 r18 實際主資料列出921個有效關卡、397區域、136張地圖圖集；不包含未開放的預留關卡。每個區域一份顯示設定，同區域所有關卡共用。

## 真實來源

25組門Prefab與100段AnimationArray引用的原始Clip，取自保留的舊版CAB並經既有2.9.1原生素材轉換。只抽取該Prefab的m_Clips，不把同CAB內不相干Clip也算進去。位置、四元數旋轉、縮放的每個鍵值及in/outSlope完整保留；採三次Hermite求值、正規化四元數，再逐層套用本地Transform。模型網格、UV、頂點顏色與父子階層也來自CAB。瀏覽器使用正交投影與雙面貼圖，不模擬Unity的光照、粒子、音效、完整相機或事件執行。

原生`EINGAME_DOOR_ANIM`已從原始2.9.1組件核對：OPEN=0、TATAKI=1、CLOSE=2、OPEN_BOSS=3。敲門並不是Boss待機；完整IL證據見交付目錄`native-state-contract.txt`。

七類是依實際節點與曲線整理的動作分類，不是遊戲中原有的分類欄位：雙扇鉸鏈旋開13組、單扇鉸鏈旋開1組、單片升降2組、左右雙片滑開4組、上下雙片滑開3組、八片複合展開1組、四片分層伸縮1組。完整模型名、曲線與長度見`native-animation-audit.json`。

206個區域同時具有模型家族與貼圖的快取路徑；162個區域用原模型家族搭配其他貼圖。這表示資源存在，不構成逐區域官方配對已完整還原的證明。另29區域使用缺模型家族map_4026_door、map_6001_door、map_6002_door，預覽保留r18分離門片與0.3秒水平收合。替代組slot1僅為Boss門關閉，沒有官方敲門動畫；不將其計入100段原始Clip。

## 編輯與匯出

可搜尋921關卡及其區域、資源名；播放、暫停、逐格、倒播、時間軸、預覽速度與鏡頭只影響檢視。模型樣板切換同時替換門網格、UV與四段曲線，保留目前關卡貼圖，可能需要手動對位。每個門片可選原始曲線、鉸鏈旋開、水平／垂直滑開、水平收合，調整位移、角度、支點及UV倍率／偏移。自訂動作套用開門與關門，敲門保留原始曲線。進階曲線編輯可逐鍵修改值及切線，但不允許改為其他元件綁定。

匯出格式`dg-door-animation` schema1，帶r18 APK SHA、資料版本與區域ID。只含門顯示設定，拒絕遊戲數值、貨幣、未知ID、跨版本來源、外來節點、錯誤維度和超限曲線。完整驗證後才合併；原始資料和APK皆唯讀。後續需另外建立APK配接器才能套用，本工具不會寫入遊戲或存檔。

草稿以來源SHA分隔保存在瀏覽器localStorage，支援復原／重做、完成標記、備註及同貼圖區域批量套用。請定期匯出，file://儲存行為可能因瀏覽器而異。

## 建置與驗證

所有下列專案命令僅在專案Docker開發容器內、從專案根目錄以已配置相依套件的Python執行：

    python tools/door-lab/prepare.py
    python tools/door-lab/build.py
    node tools/door-lab/test.mjs
    python tools/door-lab/render_reference.py
    python tools/door-lab/check_delivery.py

沿用battle-layout的唯讀Unity4序列化結構讀取器與schema。先核對r18 APK及CAB雜湊，保留原始圖片；無損WebP解碼RGBA逐位元組比較。既有報告記錄31項Node檢查，涵蓋全關卡引用、100原始曲線每個關鍵影格、104段動畫多時點幾何求值、七類動作、替代曲線、區域匯出／匯入、錯誤來源／遊戲欄位阻擋及DOM靜態綁定。HTML另驗證272張內嵌圖片、資料與測試目錄一致、實際內嵌JS語法及沒有外部依賴／網路呼叫。

既有驗證未涵蓋真實瀏覽器操作。`native-door-animation-reference.jpg`是來源幾何／貼圖的離線光柵檢查，不是瀏覽器截圖。實際瀏覽器畫面、WebGL、拖曳時間軸、檔案下載／匯入及本機草稿仍需人工開檔確認。未接觸手機、模擬器或玩家存檔。


## 來源與本次整理

本目錄維護介面與打包程式；runtime、交付HTML及原素材不隨Git提供。先依 [battle-layout說明](../battle-layout/README.md) 準備其共用Unity4結構schema，再核對精確r18 APK、報告與CAB工作目錄。腳本仍有固定容器輸入／工作目錄，不能只取得Git就直接建置。

原創工具依 [MIT授權](../../LICENSE)，擷取的模型、曲線、圖集及輸出包內第三方內容依 [權利聲明](../../THIRD_PARTY_NOTICES.md) 處理。本次只做靜態語法、格式、連結與來源指紋檢查，未生成成品、執行Node邏輯測試或操作瀏覽器；上述數量與結果均引自既有報告。
