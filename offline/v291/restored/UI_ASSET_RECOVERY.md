# 關卡 UI 與隊伍立繪修正（r17）

僅調整關卡門縮圖、地塊圖集查找與關卡底部隊伍立繪。原595角色的裁切與原生模型不變；不改編隊列表圖示、數值、技能、存檔格式或資料版本。沿用r13以APK儲存圖片切片的32位元記憶體修正。

native_ui.py 指紋鎖定 DG0 的兩個 UnityFS 圖集包；只取具名像素，不載入其後期程式／主資料／模型。核對原生 UIAtlas 與 TexturePacker 兩份矩形，一致才接受。目前補34種門縮圖及105地塊（68鑰匙／寶箱、16 SP／增傷、21降攻／易傷）。門圖248×152正規化為既有207×131，地塊保留128×128。原93門圖與111地塊不改。mugen_box 缺精確原圖，明列為沿用原 town_box。

EnsureSprite 對未知名字不建立隨意圖塊，使2.9.1既有多圖集查找可繼續找到後續圖集。關卡內敵人頭像是原生翻牌內容，不改敵人／關卡邏輯。

DGDB SQL、已收錄Wiki頁面、dgdata及DG0沒有新增角色的原生 img_0..4 裁切主資料。portrait_layout.py 對照每個角色自己的完整立繪與頭像；原595使用原生快取像素，新增角色使用現有PNG來源。hud_calibration.py 用595個原版參數，分別擬合五個位置的中心與邊長。依原生程式，party0..4對應img4..0。頭像匹配門檻0.055；進化家族與共用draw_id不跨交叉驗證分組。只將擬合結果烘焙為1153個新增角色的UV規則，手機不執行匹配或回歸。

r17既有校準報告中495名通過門檻；五折每折99名，五格重疊度中位數約78%～80%，中心誤差約4.4%～5.7%原版邊長。1007新增角色使用校正規則，146低匹配／素材／巨型例外使用整體可見立繪並列人工檢查。好的匹配不代表官方構圖；原595仍使用原資料，不套模型。這是初稿適配，非官方逐角色裁切資料還原。

HudPortraits291.cs 僅在原生隊伍建立完成時更換材質UV。VerifyUiHook.cs 移除窄掛鉤後，逐指令比對原方法，驗證原失敗／返回／狀態轉換保留。verify_ui.py 檢查實際簽章DLL的GetSprite／GetPanelAtlas、所有使用到的陷阱名稱、5765個新增裁切範圍，以及DG0來源像素與成品像素一致。

重現需要原APK、固定DGDB／Wiki／dgdata、原快取、兩包DG0及r10素材工作目錄，均不隨Git發布。所有指令僅在專案Docker開發容器內、從專案根目錄以已配置相依套件的Python執行；工具與工作目錄預設仍須核對：

    python offline/v291/playable/build.py --revision 18 --restore-content --output artifacts/NEW-UI-BUILD --assets-from artifacts/2.9.1-offline-dgdb1-r10-drop-level/build-report.json
    python offline/v291/restored/verify_image_streaming.py artifacts/NEW-UI-BUILD/build-report.json
    python offline/v291/restored/test.py artifacts/NEW-UI-BUILD/build-report.json
    python offline/v291/restored/verify_ui.py artifacts/NEW-UI-BUILD/build-report.json
    python offline/v291/restored/verify_more.py artifacts/NEW-UI-BUILD/build-report.json

輸出含來源雜湊／矩形、hud-calibration.json（逐角色驗證與五組係數）、hud-image-inputs.json、hud-portrait-crops.json及例外清單。預覽呈現來源裁切矩形，不模擬原生多邊形遮罩。Android呈現、動畫與實際遊玩由使用者測試，不操作玩家存檔。


目前候選身分見 [還原設定說明](README.md)。以上數量與檢查結果引自既有r17報告；本次提交整理沒有建置、擬合、執行回歸或操作Android。r14～r16為較早裁切／素材適配階段，不能以其報告取代r17校準結果；推估UV仍需逐角色人工檢視。
