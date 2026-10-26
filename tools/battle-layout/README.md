# DG Battle Lab：單檔離線顯示調整工具

交付入口是 artifacts/battle-layout-editor/DG-Battle-Lab.html。複製這一個檔案即可，直接以 Chrome／Edge 雙擊開啟；不需要 HTTP server、Docker、Node、APK或素材資料夾。dist/index.html 是建置模板，不是交付入口。

全部1748角色圖像與資料內嵌。包含595個原有參數供參考、1153個待補裁切角色、304個待補尺寸分類角色；638角色被敵人資料引用，其中371使用已恢復原生幾何，267保留r17缺模型時的四邊形立繪。圖片以逐張RGBA相等驗證的無損WebP打包；成品較大，僅解碼目前顯示的立繪並限制GPU快取。

## 使用

1. 搜尋名稱、角色ID或圖鑑No.，或篩選待補裁切／尺寸／有敵人資料／已調整角色。
2. 隊伍五個位置可各別調整取景範圍與偏移，支援拖曳、滾輪及完整立繪定位圖。右側隊長是img_4，左下好友是img_0；原生party索引0..4對應img4..0。
3. 普通技橫條使用原生CutinChara網格；UV水平比例為img_cutin_tiling/1000，垂直比例固定是水平的0.15倍。
4. 有敵人資料才放入怪物區。可調尺寸、pivot、side_offset、具體敵人的位置及血條；雙敵人模式沿用原side_offset排列公式。尺寸預設來自原595角色的五種組合，不宣稱是新增角色的官方原值。
5. 原有595角色預設唯讀，可自行解除參考鎖。修改和完成狀態存於瀏覽器localStorage；務必定期匯出JSON，清除網站資料或換瀏覽器不會自動保留草稿。
6. 匯入採完整驗證後才合併；支援復原／重做。檔案不是玩家存檔，不會自動改寫APK或遊戲資料。

匯出schema1 / dg-battle-presentation，包含來源APK雜湊、data_version、角色native顯示欄位與party_slot_rects、各敵人的visual overrides。後續套用到r17或其延伸版時，必須同步更新新增角色的HUD runtime override，僅改MasterDataParamChara不會蓋過r17窄掛鉤。不同資料版本需先核對ID和引用，不能直接混用。

## 顯示契約與範圍

隊伍遮罩、背板、框線、SP、Header、Battle框、普通技橫條直接讀原生2.9.1網格、材質圖集與UV。框線與背板會依原版程式的五組clone位置放置；不把Prefab父物件當作有MeshFilter的表面，也不以一般矩形代替五格遮罩。素材來源為原2.9.1 APK及既有r17圖片存放區，未使用抽卡宣傳圖。

本工具是靜止的顯示參數編輯器，不執行Unity戰鬥、粒子、傷害或動畫。地圖及數字用來提供構圖參考；字型依瀏覽器可用字型回退。敵人的前後深度可記錄，實際遮擋須在Unity驗證。因此不能將頁面當作完整戰鬥模擬器或宣稱所有動態畫面逐像素相同。顯示文字的HP／ATK取主資料供預覽，不寫入調整檔。

## 建置與驗證（僅開發者）

所有專案命令僅在專案Docker開發容器內、從專案根目錄以已配置相依套件的Python執行。開啟交付HTML不需要這些工具。開發容器需有Pillow、UnityPy、Mono/Cecil及Node20以上；Node只用於語法和邏輯測試，不能安裝到Windows／WSL主機。

    python tools/battle-layout/prepare.py
    node tools/battle-layout/test.mjs
    python tools/battle-layout/make_standalone.py
    python tools/battle-layout/check_standalone.py artifacts/battle-layout-editor/DG-Battle-Lab.html

prepare.py預設讀取固定r17建置報告及其保留的原APK／表格工作目錄。SerializedTypes.cs根據實際DLL的可序列化欄位讀取Unity4已剝除型別樹的MonoBehaviour，逐物件檢查讀取長度。make_standalone.py讀取APK內ZIP_STORED圖片切片、校驗來源雜湊，輸出無外部依賴的HTML。runtime與dist/game是生成資料，不隨Git提交。

既有報告記錄36項Node檢查，涵蓋所有1748角色匯出／匯入、錯誤版本、非顯示欄位、外來敵人ID、取景UV、普通技橫條比例、原生雙敵人排列，以及遮罩命中測試。HTML逐一核對3509內嵌圖片的雜湊，並確認沒有外部script/link/img依賴或網路呼叫。

既有驗證未涵蓋真實瀏覽器操作，因此瀏覽器畫面、拖曳手感、不同瀏覽器的file://草稿保存與觸控仍需人工開檔確認。render_reference.py的圖是原生幾何／UV離線光柵檢查，不是瀏覽器截圖。

## 敵人 size 修正（visual_revision 2）

前版只顯示 DGDB size，預覽仍沿用640大型模板。現依 offline/v291/restored/size_layout.py 的595名原生參考映射，在產生角色目錄時套用size_width、size_height、pivot、side_offset；304名中小型角色改用原生中小型配置，原595名與新增size1維持原尺寸。リンぼん等size2角色為216×216、pivot2、side_offset78，自動血條走中型分支。細分尺寸由size/kind/rarity或kind的一致參考推定；仍有歧義時使用分類眾數，來源逐筆記錄於size-adaptation.json。非逐角色官方構圖。

舊schema1草稿沒有visual_revision=2時，只有四個尺寸欄位全等於r17原模板的新增角色，才重設成分類起點；裁切、備註、位置與血條覆寫保留。任何不同於模板的手動尺寸均保留。新匯出加visual_revision=2，明確選回640也不再被重設。畫面仍可按「還原 DGDB 尺寸起點」或手動修改。

## 待補敵人篩選修正

missingSize表示304名新增中小型角色的尺寸來源需要推定，並不代表有敵人引用。待補敵人尺寸列表必須同時符合missingSize與enemies非空，目前100名；其餘204名仍可在待補裁切或全部角色找到。ダークスパス（圖鑑No788／角色ID439）沒有敵人引用，保留隊伍與橫條預覽，不放入敵人列表或怪物區。勿將圖鑑No788與角色ID788（ミクぼん）混淆。


## 來源與本次整理

Git維護dist中的HTML、CSS與JavaScript介面來源；目錄名不代表已包含成品。dist/game與runtime為生成資料，獨立HTML及原素材都不隨Git提供。從乾淨checkout重現前，須另備精確r17 APK、其建置報告／工作目錄、原快取與相依工具；腳本仍有固定容器預設，先核對設定。單檔輸出含遊戲素材，原創工具依 [MIT授權](../../LICENSE)，第三方內容依 [權利聲明](../../THIRD_PARTY_NOTICES.md) 處理。

本次只做靜態語法、格式、連結及來源指紋檢查，未生成HTML、跑Node邏輯測試或操作瀏覽器。以上成品數量與驗證狀態引自既有報告，瀏覽器畫面、拖曳與本機儲存仍待人工確認。
