# DGDB 尺寸分類配接（r18）

r17 新增1153個角色時，DGDB 的 unit.size 沒有投影成原生 size_width、size_height、pivot、side_offset；新增者全部借用角色10的640×640、中心定位與320間距，造成中／小型敵人放大，以及雙敵人排列超出畫面。

size_layout.py 從原有595個角色的原生參數建立參考集，先保持595個角色不變，再處理新增角色。只修改四個顯示欄位，不新增角色／關卡，也不改戰鬥、AI、掉落、HP、攻防、技能、存檔格式或資料版本。

| DGDB size | 已有原生顯示組合（寬=高） | pivot | side_offset |
| --- | --- | --- | --- |
| 1 | 640 | 1（中心） | 320 |
| 2 | 216／284 | 2（底部） | 78／105 |
| 3 | 106／140 | 2（底部） | 48／65 |

分類內精確子尺寸不是DGDB直接提供。新角色依序採同size全體一致設定、同size／kind／rare一致設定、同size／kind一致設定；無一致細分時，採同size中最常見的原生設定。所有引用角色、原參數／新參數及方法記錄於 size-layout-adaptations.json。未知分類或參考集改變時停止建置；不猜測補零。

r18中849名新增size1維持640。304名新增者改為245名216、37名284、13名106、9名140。其中138名使用同size／kind／rare參考共識，166名使用size分類眾數fallback；兩者都屬明列的格式配接，不是找到逐角色官方構圖。原595不改。

截圖中的ミクぼん、リンぼん、レンぼん、ルカぼん、はちゅねミク和六種ホーク改為216、底部定位、間距78。ヘンドリクセン為size1大型Boss，保留640與原定位。原生 BattleManager 累加side_offset，同兩名小怪的中心從−320／＋320改為−78／＋78；三名216角色為−156／0／＋156。血條維持原生pivot／寬度分支，不加跳過或新排列演算法。

四個欄位由共用 AssetAutoSetCharaMesh 消費，因此使用該元件的角色大立繪也採相同尺寸；編隊小圖示與r17五位置HUD裁切不變。原模型、動畫、PNG、DG0圖塊以及r13 APK圖片切片讀取保留。

驗證入口（全部在專案Docker內執行）：

    python offline/v291/restored/test.py artifacts/NEW-BUILD/build-report.json
    python offline/v291/restored/verify_image_streaming.py artifacts/NEW-BUILD/build-report.json
    python offline/v291/restored/verify_ui.py artifacts/NEW-BUILD/build-report.json
    python offline/v291/restored/verify_more.py artifacts/NEW-BUILD/build-report.json
    python offline/v291/restored/verify_enemy_sizes.py artifacts/NEW-BUILD/build-report.json

新驗證比較簽章DLL實際匯出的角色資料，只允許304筆四欄差異；其餘34表及原595資料不變。核對參考來源、原生Game DLL方法、所有APK素材與HUD裁切保留。另用實際r17簽章組件產生的schema3測試備份驗證r18匯入、重匯出及獨立程序重登，不操作玩家資料。配置預覽是來源圖片搭配原生尺寸／位置公式的模擬，不是Android實測畫面。實機渲染與遊玩由使用者測試。


以上通過結果引自既有r18報告。本次提交整理只檢查語法、格式、文件與來源指紋，未建置、跑回歸或操作裝置。候選身分見 [還原設定說明](README.md)；[戰鬥版面工具](../../../tools/battle-layout/README.md) 可產生顯示調整JSON，尚不會套用回APK。
