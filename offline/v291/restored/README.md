# 2.9.1 年代內容還原設定（r12）

本設定沿用 [原生2.9.1工程](../playable/README.md)，以額外開關建立獨立套件，不取代原dgdb-1／r10。年代選集來自指紋鎖定的Wiki稽核，數值與引用來自鎖定DGDB；不是2015官方完整平衡快照。

原創工具與文件依 [MIT授權](../../../LICENSE)，原遊戲程式、資料與素材依 [第三方權利聲明](../../../THIRD_PARTY_NOTICES.md) 處理。原APK、SQL、稽核輸入、素材、報告及私鑰不隨Git提供。

## 候選身分

- 專案相對目錄：artifacts/2.9.1-restored-r12/ディバゲ_2.9.1.apk
- 套件：jp.gungho.dg.offline291.restored
- 大小：875,035,215 bytes
- SHA-256：55900aa5bb51befdc95d4119ed44f2c08ff9f1f5444e2db2985bad17b179265a
- 資料／備份：dgdb-2-restored、schema3
- 1748角色、921有效關卡／928任務列、1366樓層列、717進化

r11是地圖方向與不透明黑色空白判定修正前的中間候選，已有停用標記，應使用r12。原r10套件、成品與舊存檔格式維持原範圍。

## 來源、實作與缺口

content.py讀取固定units.json／quests.json稽核指紋；playable/dgdb.py再以原SQL完成依賴閉包。原595角色、392有效關卡及既有技能／敵人等資料列保留，新增1153角色及529關。三個NS ability不支援角色、三個後期Link固定隊伍關卡及76個依賴不完整候選未開放。

新增關卡若缺原生成表，選用相容的舊24格加一格鑰匙配置；沒有相容敵群時明列替代，不宣稱歷史精確分布。三項無法恢復的區域前置以零開放；新區域歷史日期限制移除。原七卡池與79關常駐亂入不擴成推測機率。

新增2062筆敵人來源的掉落率均為零，保留來源值。697個新增角色沒有目前掉落、卡池、通關或進化目標來源定義；455個有進化目標、1個有通關來源，也不代表前置角色／素材已可取得。數量描述的是定義，不是全部角色可正常獲得。

assets.py保留595原生模型路由、原立繪及圖示像素；只為缺模型角色補正確draw_id的PNG。新增288區域中126項有相符原模型家族，162項使用相容共用幾何／門動畫。map_layout.py修正外部圖集上下方向，RGB判斷分離門片；黑色padding只在簡化切片透明化。Presentation291.cs只在原生請求不可用的fallback分支建立門框與普通／Boss獨立門片，四態曲線為0.3秒且不增加碰撞器。

## 備份相容性

輸出schema3／dgdb-2-restored；接受自身格式、原2.9.1 schema2／dgdb-1、2.9.0 schema1及固定1.0.6 prototype22r7單向轉入。各格式仍走原雜湊、引用驗證與交易回復。原r10拒絕schema3，返回舊版須使用原舊備份；套件資料空間分離，不自動讀取其他套件私人資料。

## 重現入口與限制

所有工具僅在專案Docker開發容器內，由專案根目錄使用已配置UnityPy／Pillow等相依套件的Python執行。須另備原2.9.1 APK、鎖定DGDB SQL、指紋相符的Wiki稽核、dgdata與r10素材快取；工具／工作根目錄仍有固定預設，執行前核對設定，不能只取Git來源直接建置。

```sh
python offline/v291/playable/build.py --revision 13 --restore-content --output artifacts/NEW-291-RESTORED --assets-from artifacts/2.9.1-offline-dgdb1-r10-drop-level/build-report.json
python offline/v291/restored/test.py artifacts/NEW-291-RESTORED/build-report.json
python offline/v291/restored/verify_more.py artifacts/NEW-291-RESTORED/build-report.json
python offline/v291/restored/report.py artifacts/NEW-291-RESTORED/build-report.json
```

命令是後續重現入口，本次提交整理沒有執行。--revision只改標記，不重建歷史來源；不加--restore-content仍為原dgdb-1設定。不得覆蓋既有APK。

## 既有證據與待測

r12既有31組受管／資源報告涵蓋擴充數量、原生技能、鑰匙、配置與存檔。test.py在容器暫存測試副本中調整有來源的數量、格式及套件期望，保留測試主體。複合狀態探針逐一檢查四槽乘算與最長有效期。

verify_more.py另核對原生包／立繪／圖示像素保留、新角色原圖、地圖方向與門片、r10備份轉入、schema3往返、跨程序保存及舊版拒收。門曲線由暫存引擎替身記錄呼叫參數，不是Android動畫呈現證據。report.py彙整逐表差異、技能新增、來源缺口與不可取得定義。

本次只做靜態語法、格式、連結、公開資訊及來源指紋檢查，未建置或執行回歸。Android啟動、記憶體、UV、碰撞、觸控、門動畫與實際遊玩仍待使用者驗證；沒有操作玩家存檔。
