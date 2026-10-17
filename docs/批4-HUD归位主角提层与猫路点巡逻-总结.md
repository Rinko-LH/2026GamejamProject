# 批 4：HUD 归位 + 主角提层 + 猫路点巡逻

> 日期：2026-10-18 ｜ 提交：`6eeb1e5`（4a 修复）+ 4b（本文档随附提交）

## 用户原始诉求

1. 对话框和状态栏在游戏运行时看不到
2. 猫的移动够不到交互点（早餐/接水），不支持到刷新点
3. 人物不要被柜子或画板隐藏，交互时始终在上方

## 4a：HUD 归位 + 主角提层（`6eeb1e5`）

### 根因：不是被遮住，是被推出了屏幕

上一轮"锚四角"修复里，`EnsureUiHost` 把面板宿主做成零尺寸锚点是**对的**，
但各面板 Label 按"offset = 距屏幕边缘的边距"写数值，而 `UIBuild.Text` 的
`pivot` 是默认 (0.5, 0.5)——offset 定位的是矩形**中心**：

| 面板 | 改前文字位置 | 结果 |
|---|---|---|
| StatusBar | 左出界 210 / 上出界 49 | ❌ 整行在屏外 |
| ClockDisplay | 右出界 170 / 上出界 49 | ❌ |
| TaskHints | 左出界 260 / 下出界 90 | ❌ |
| DialogueBox | 屏底 40–250 | ✅（本来就对） |

**一行修复**：`UIBuild.Text` 加 `rt.pivot = anchor;`——pivot 跟锚点一致后，
offset 才真的是"距边缘边距"。四个面板（含对话框 Speaker/Body）全走这一个
构造，改一处全覆盖。

**为什么之前 sortingOrder=30000 提层无效**：Canvas 是 Screen Space Overlay，
本来就永远画在场景 sprite 之上；`sortingOrder` 只在多个 Overlay 画布间比较。
"被场景遮住"从来不是真因，真因一直是几何出界。

### 主角提层

| 元素 | 改前 | 改后 |
|---|---|---|
| Player Body | 10 | **15080** |
| Player Shadow | 0 | **15079** |

家具 100 < 画板 15050 < 猫 15070 < **主角 15080** < 特写画布 15100。
装配菜单新增 `EnsurePlayerLayer`：遍历 Player 下所有 SpriteRenderer 无条件写死
（K2：序列化值不随代码默认值更新）。特写期主角本来 `SetActive(false)`，不穿帮。

### 验证方式换血（重要教训）

RenderTexture + `Camera.Render()` **拍不到 Overlay UI**——之前批 2/批 3 的
"实拍取证"对 UI 全是盲的。本次改用：

1. **数值断言**：对四面板所有 RectTransform 跑 `GetWorldCorners()`，
   与屏幕矩形求交，断言出界量全 0 ✅
2. **ScreenCapture.CaptureScreenshot 实拍**（抓完整帧，含 Overlay UI）：
   `04-4a-对白显示.png` / `05-4a-客厅HUD与主角.png`，HUD 与对话框肉眼可见 ✅

## 4b：猫路点巡逻

### 改动（`CatController.cs`）

- 新增 `waypoints`（Transform[]）/ `standYOffset = -0.7` / `arriveDistance = 0.15`
- `StartWander`：有路点时随机挑一个，目标 = 路点位置 + (±0.3, −0.7)——
  **站在交互物跟前偏下**，不挡玩家交互判定；无路点退回窝+半径（兼容旧场景）
- 游走超时 2–4s → **6–9s**：早餐到书架横穿 8.4 单位 @2.0 速度要 4.2s，
  旧超时会半路掉头
- 打乱冲刺改为**就近**（当前位置旁）扒拉，不再跑回窝
- `moveSpeed` 1.1 → **2.0**（装配写死；1.1 走到早餐要 7.6s，像卡住）
- `HasArrived` 用 `arriveDistance`（旧阈值 0.05 对路点太苛刻）

### 装配菜单

- 猫节数值写死：moveSpeed 2.0 / standYOffset −0.7 / arriveDistance 0.15
- 新增第 16 节（猫节在交互物之前建不了，放到所有交互物就位后）：
  5 个交互物 Transform 接进 `waypoints`，日志报 `5/5`

### 实跑验证（play，90+ 秒巡逻采样）

猫到访记录（写进物体名逐 tick 记账）：**DrawingBoard → WaterSource → TidyDesk →
Breakfast → TidyShelf，5/5 全部到访** ✅（含用户点名的早餐和接水点）。
取证：`06-4b-猫在交互点跟前.png`——猫正站在书架跟前。

### 本次播放态调试新坑（已记 MEMORY）

- **hidden-pause 双击 pause 复原失效**时，用
  `Application.runInBackground = true` + `EditorApplication.QueuePlayerLoopUpdate()`
  免焦点驱动播放循环
- **探针别用 `GameObject.Find` 精确名匹配做持续记账**：探针给猫改名记账后，
  下一次 Find("旧名") 找不到就静默自退——改用
  `Resources.FindObjectsOfTypeAll<T>()` + `scene.IsValid()` 过滤

## 遗留

- P2 猫打乱空转：猫现在能走到交互点跟前了，接上 CatMischief→TidySpot 复原
  就闭环（下一批顺手）
- 偶然发现（3.5）/ S7–S10 低落与结局 / P7 / 场景 7 条 missing-script 遗留物件
