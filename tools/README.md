# 本機顯示檢視與調整工具

| 工具 | 用途 | 固定資料來源 |
| --- | --- | --- |
| [DG Battle Lab](battle-layout/README.md) | 五位置頭像、普通技橫條、敵人尺寸／血條顯示參數 | 還原版r17，另以原生參考推定中小型尺寸起點 |
| [DG Door Motion Lab](door-lab/README.md) | 區域門網格、四態原始曲線與自訂顯示動作 | 還原版r18 |

工具匯出的是顯示調整JSON，不是玩家存檔；目前不會回寫APK，後續套用需另做有來源驗證的配接器。瀏覽器localStorage草稿須定期匯出。離線HTML只能在另備資料並重建後使用，不隨Git發布，也不代表完整戰鬥／Unity動畫模擬。

只提交維護中的程式、介面模板與說明。runtime、battle-layout/dist/game、素材、成品及預覽保留在Git以外，亦排除於Docker建置內容。門工具使用battle-layout的共用Unity4結構讀取器與生成schema，請依各自說明準備輸入。所有開發工具在專案容器執行，固定路徑尚未全面參數化。

原創工具與文件依 [MIT授權](../LICENSE)；原遊戲素材、模型、字型及輸出內容中的第三方部分依 [權利聲明](../THIRD_PARTY_NOTICES.md) 處理。既有測試報告不能替代真實瀏覽器或Android畫面驗證。
