using RoomDemo.Contracts;
using RoomDemo.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo
{
    /// <summary>
    /// 把画板玩法所需的对象装进 <c>Assets/Scenes/TestScene.unity</c>。
    ///
    /// 美术已从"程序化生成色块"改为"手绘透视图"，因此本脚本
    /// <b>不创建任何程序化图形</b>：原先 <c>RoomShapes.Rect</c> /
    /// <c>RoomShapes.Bar</c> / <c>RoomPalette.*</c> 这些运行时烘焙的
    /// 色块精灵全部去掉，精灵与配色改由美术资源或场景手摆物体提供。
    /// 这里只负责搭建玩法骨架（对象层级、组件、引用关系）。
    ///
    /// 结构（可重复执行，已存在的对象只补齐组件和引用，不重复创建）：
    /// GameSystems/
    ///   PlayerStats / DayTimer
    ///   DrawingBoard/            ← 房间里的画板 + 交互入口
    ///     PaintingBoards/        ← 特写里的三块画板（房间内隐藏）
    ///       PaintingBoard1~3/
    ///         Frame  Canvas  BarBg  BarFill
    ///   BrushPickup/
    ///   Dialogue/                 ← 1.5 对话系统（DialogueManager）
///   ProgressTracker/          ← S0 契约层进度总线（支线 Report → UI/结算订阅）
///   TestSnack / TestWater          ← Demo 验收用的吃喝道具（占位纯色图）
///   UIRoot/                   ← S1 UI 四件套（Canvas + 状态栏/时钟/对话/任务提示）
/// RoomWorld/
///   Cat/                      ← S2 猫（打乱只经 Progress 上报，不碰道具，R1）
///   TidyDesk / TidyShelf      ← S3 收拾（两段式 E）
/// GameManager/
///
/// 节号说明：11 = S1 UI 四件套；12 = S2 猫；13 = S3 收拾，
/// 与《跨系统整合手册》节号切分表一致。
    ///
    /// 用法：执行 <b>GameJam/SetupDrawingBoard (TestScene)</b>。
    /// 不在 TestScene 时会自动打开它，装配完自动存盘。
    /// </summary>
    public static class DrawingBoardSetup
    {
        const string MenuPath = "GameJam/SetupDrawingBoard (TestScene)";
        const string TargetSceneName = "TestScene";
        const string TargetScenePath = "Assets/Scenes/TestScene.unity";
        const int BoardCount = 3;

        // ---- 绘制层 ----
        // 原先依赖 RoomView.LayerPropsMax（该类随程序化方案一起删除），
        // 这里改成显式常量。手绘资源的层级由美术定，这几个值只需保证
        // 画板组内部 Frame < Canvas < BarBg < BarFill，且整体高于场景家具。
        const int OrderFrame = 15100;
        const int OrderCanvas = 15101;
        const int OrderBarBg = 15102;
        const int OrderBarFill = 15103;
        const int OrderBoardMarker = 15050;
        const int OrderBrush = 15060;
        const int OrderCat = 15070;
        // 主角：必须压在家具（100）与画板标记（15050）之上，否则走到柜子/画板
        // 前就被完全盖住。夹在猫（15070）与特写画布（15100）之间——
        // 特写里主角本来就被 SetActive(false)，提层不会穿帮进特写。
        const int OrderPlayer = 15080;
        // 客厅布景（墙/地板/家具）用低值，天然垫在交互物（150xx）后面
        const int OrderFurniture = 100;
        // 收拾特写里的杂物：叠在手摆透视块之上（透视块默认 0）
        const int OrderTidyItem = 60;
        // UI 画布：必须高于所有场景精灵（150xx），否则对话框/状态栏会被场景遮住
        const int OrderUiCanvas = 30000;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != TargetSceneName)
            {
                // 不再只是警告后放弃：直接打开目标场景，避免用户以为装配成功
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(TargetScenePath);
                if (scene.name != TargetSceneName)
                {
                    Debug.LogError($"[画板] 找不到场景 {TargetScenePath}");
                    return;
                }
            }

            var player = FindPlayerTransform();

            // ---- 1. 系统节点 ----
            var systems = FindOrCreate("GameSystems");
            var stats = Ensure<PlayerStats>(systems);
            var timer = Ensure<DayTimer>(systems);

            // ---- 1.5 数值对齐（案值，docs/阶段一-变更留档.md §数值重规划） ----
            // 公开字段的序列化值不会随代码默认值更新，场景里可能还是旧值，
            // 所以在装配菜单里显式写死案值：饿 0.35 / 水 0.30 / 精神 0.30；
            // 画画 ×0.5 / 支线 ×1.5；阈值统一 20。改数值只改这里 + PlayerStats 默认值。
            var soStats = new SerializedObject(stats);
            soStats.FindProperty("waterDecayPerSec").floatValue = 0.35f;
            soStats.FindProperty("satietyDecayPerSec").floatValue = 0.30f;
            soStats.FindProperty("spiritDecayPerSec").floatValue = 0.30f;
            soStats.FindProperty("drawingDecayScale").floatValue = 0.5f;
            soStats.FindProperty("sideQuestDecayScale").floatValue = 1.5f;
            soStats.FindProperty("waterLow").floatValue = 20f;
            soStats.FindProperty("satietyLow").floatValue = 20f;
            soStats.FindProperty("spiritLow").floatValue = 20f;
            soStats.ApplyModifiedPropertiesWithoutUndo();

            // ---- 2. 画板（房间里的交互入口） ----
            var boardGo = GameObject.Find("DrawingBoard");
            if (boardGo == null)
            {
                boardGo = new GameObject("DrawingBoard");
                // 手绘场景里没有"书桌纵深"概念，位置由美术摆放；
                // 这里给一个房间中央偏右的合理默认值。
                boardGo.transform.position = new Vector3(1.8f, 1.6f, 0f);
                boardGo.transform.SetParent(systems.transform, false);
            }

            var interact = Ensure<DrawingBoardInteract>(boardGo);

            // ---- 2.5 旧版结构迁移 ----
            // 老版本画布是 DrawingBoard 的直接子物体且叫 "Canvas"，
            // 且 DrawingBoard 自己还挂着一个 DrawingCanvas。
            // 新结构里画布挂在 PaintingBoardN 下，这两处都要清掉，
            // 否则会残留一个没人用的画布组件（还可能被特写逻辑误当成第 4 块）。
            var oldCanvas = boardGo.transform.Find("Canvas");
            if (oldCanvas != null)
            {
                Object.DestroyImmediate(oldCanvas.gameObject);
                Debug.Log("[画板] 已移除旧版单画布对象，切换为三画板结构");
            }

            var strayCanvas = boardGo.GetComponent<DrawingCanvas>();
            if (strayCanvas != null)
            {
                Object.DestroyImmediate(strayCanvas);
                Debug.Log("[画板] 已移除 DrawingBoard 上的遗留 DrawingCanvas 组件");
            }

            // ---- 3. 画板本体标记（房间里的可见物体） ----
            // 精灵留给美术：这里只保证 SpriteRenderer 存在，sprite 为空时
            // 玩家看不见它，需要美术拖入手绘图。
            var boardSr = boardGo.GetComponent<SpriteRenderer>();
            if (boardSr == null)
            {
                boardSr = boardGo.AddComponent<SpriteRenderer>();
                boardSr.sortingOrder = OrderBoardMarker;
            }
            // X 位置是<b>玩法参数</b>不是美术参数：交互只看水平距离，
            // 站到两个交互物 X 区间的重叠处按一次 E 会同时触发两件东西
            // （比如同时进接水特写和收拾特写）。所以这里无条件写死，
            // 与早餐 / 桌面 / 水源 / 书架统一按 2.0 间距排开。
            // 美术要微调画架外观请动 Y / Z 或子物体，别改这个 X。
            boardGo.transform.position = new Vector3(0f, 1.6f, 0f);
            boardGo.transform.localScale = new Vector3(1.6f, 1.1f, 1f);
            // 纸白占位，美术出手绘前至少能看见桌上有块画板（P1）
            if (boardSr.sprite == null)
                boardSr.sprite = MakePlaceholderSprite("board", new Color(0.93f, 0.91f, 0.86f));

            // ---- 4. 交互提示组件 ----
            // 画板节点原本只是个空壳，玩家看不见也摸不着，
            // 必须给它一个可见的标记，靠近了还要高亮闪烁。
            //
            // 注意：highlightTargets 必须显式指定为画板本体那一个渲染器。
            // 不指定的话 Awake 会自动收集子物体下所有渲染器——三块画板的
            // 外框/画布会被一起卷进呼吸高亮，和 PaintingBoard 自己的
            // 悬停/达标配色打架。
            var hint = boardGo.GetComponent<InteractionHint>();
            if (hint == null) hint = boardGo.AddComponent<InteractionHint>();
            hint.player = player;
            hint.triggerRange = 1.2f;   // 与 DrawingBoardInteract.interactRange 一致

            var soHint = new SerializedObject(hint);
            var hintTargets = soHint.FindProperty("highlightTargets");
            hintTargets.arraySize = 1;
            hintTargets.GetArrayElementAtIndex(0).objectReferenceValue = boardSr;
            soHint.ApplyModifiedPropertiesWithoutUndo();

            // ---- 5. 三块画板（只在特写里可见） ----
            var containerGo = boardGo.transform.Find("PaintingBoards")?.gameObject;
            if (containerGo == null)
            {
                containerGo = new GameObject("PaintingBoards");
                containerGo.transform.SetParent(boardGo.transform, false);
            }
            containerGo.transform.localPosition = Vector3.zero;
            containerGo.transform.localScale = Vector3.one;

            var boards = new PaintingBoard[BoardCount];
            for (int i = 0; i < BoardCount; i++)
            {
                string name = "PaintingBoard" + (i + 1);
                var bGo = containerGo.transform.Find(name)?.gameObject;
                if (bGo == null)
                {
                    bGo = new GameObject(name);
                    bGo.transform.SetParent(containerGo.transform, false);
                }
                bGo.transform.localPosition = Vector3.zero;
                bGo.transform.localScale = Vector3.one;

                var canvas = Ensure<DrawingCanvas>(bGo);
                var board = Ensure<PaintingBoard>(bGo);

                // 外框：垫在画布底下当边框。sprite 留给美术。
                var frameGo = EnsureChild(bGo.transform, "Frame");
                var frameSr = EnsureRenderer(frameGo, OrderFrame);

                // 画布。注意 sprite 不能手填——DrawingCanvas.Bind()
                // 会用画布纹理重建 Sprite，这里留空即可。
                var canvasGo = EnsureChild(bGo.transform, "Canvas");
                var canvasSr = EnsureRenderer(canvasGo, OrderCanvas);

                // 计时条（底 + 左锚点填充）。填充图需要"左中锚点"的图，
                // 美术出图时注意锚点，否则进度条会从中间往两边长。
                var barBgGo = EnsureChild(bGo.transform, "BarBg");
                var barBgSr = EnsureRenderer(barBgGo, OrderBarBg);

                var barFillGo = EnsureChild(bGo.transform, "BarFill");
                var barFillSr = EnsureRenderer(barFillGo, OrderBarFill);

                // 接线
                var soBoard = new SerializedObject(board);
                soBoard.FindProperty("boardIndex").intValue = i + 1;
                soBoard.FindProperty("canvas").objectReferenceValue = canvas;
                soBoard.FindProperty("canvasRenderer").objectReferenceValue = canvasSr;
                soBoard.FindProperty("frame").objectReferenceValue = frameSr;
                soBoard.FindProperty("barBackground").objectReferenceValue = barBgSr;
                soBoard.FindProperty("barFill").objectReferenceValue = barFillSr;
                soBoard.ApplyModifiedPropertiesWithoutUndo();

                var soCanvas = new SerializedObject(canvas);
                soCanvas.FindProperty("canvasWidth").intValue = 512;
                soCanvas.FindProperty("canvasHeight").intValue = 384;
                // 达标判定参数同理显式写死案值（数值重规划：单幅 60 秒，
                // 低落时 90 秒由 S8 在 Tick 内部加倍率，不改这个字段）。
                soCanvas.FindProperty("paintingSeconds").floatValue = 60f;
                soCanvas.ApplyModifiedPropertiesWithoutUndo();

                // 立刻建好画布纹理并挂到精灵上，进特写前也能看到白纸
                canvas.EnsureBuilt();
                canvas.Bind(canvasSr);

                // 占位尺寸，进特写会按相机视野重排
                board.Layout(2.7f, 2.0f);
                // 房间内隐藏（只关渲染器，不 SetActive——会留脏状态）
                board.SetVisible(false);

                boards[i] = board;
            }

            // ---- 6. 交互组件引用 ----
            var so = new SerializedObject(interact);
            so.FindProperty("player").objectReferenceValue = player;

            var boardsProp = so.FindProperty("boards");
            boardsProp.arraySize = boards.Length;
            for (int i = 0; i < boards.Length; i++)
                boardsProp.GetArrayElementAtIndex(i).objectReferenceValue = boards[i];

            so.FindProperty("boardMarker").objectReferenceValue = boardSr;
            so.FindProperty("hint").objectReferenceValue = hint;
            so.FindProperty("mainCamera").objectReferenceValue = FindOrthoCamera();
            // 交互区间 1.2：与两侧（桌面 -2.2 / 水源 2.2）刚好错开，不重叠
            so.FindProperty("interactRange").floatValue = 1.2f;

            // 房间手绘图根节点（T0.3：进特写时整体隐藏房间）。找不到只跳过不阻断。
            var roomWorld = GameObject.Find("RoomWorld");
            if (roomWorld != null)
                so.FindProperty("roomRoot").objectReferenceValue = roomWorld.transform;

            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- 7. 画笔（S6：藏在书架里，整理书架后才出现） ----
            // 挂 RoomWorld 下：进收拾/接水特写时随房间一起隐藏，
            // 否则画笔会穿帮出现在特写画面里（客厅道具一律挂房间）。
            var roomWorldForItems = GameObject.Find("RoomWorld");
            var itemParent = roomWorldForItems != null ? roomWorldForItems.transform : systems.transform;
            var brushGo = GameObject.Find("BrushPickup");
            if (brushGo == null)
            {
                brushGo = new GameObject("BrushPickup");
                brushGo.transform.SetParent(itemParent, false);
            }
            else if (brushGo.transform.parent != itemParent)
            {
                // 旧装配挂在 GameSystems 下，这里搬过去（保留世界坐标）
                brushGo.transform.SetParent(itemParent, true);
            }
            // 摆在书架上（与 TidyShelf 同一个 X）：X 区间重叠本来会导致
            // 一次 E 同时触发两件东西，但书架收拾完成后 canInteract 会置 false，
            // 二者不会同时可交互——正好就是"整理完书架，笔露出来了"。
            brushGo.transform.position = new Vector3(4.2f, 1.75f, 0f);
            brushGo.transform.localScale = new Vector3(0.14f, 0.5f, 1f);

            var brush = Ensure<BrushPickup>(brushGo);
            var brushSr = brushGo.GetComponent<SpriteRenderer>();
            if (brushSr == null)
            {
                brushSr = brushGo.AddComponent<SpriteRenderer>();
                brushSr.sortingOrder = OrderBrush;
            }
            // 木杆色占位，细长条远看就是一支笔（P1：原先画笔画板都是 sprite=NULL）
            if (brushSr.sprite == null)
                brushSr.sprite = MakePlaceholderSprite("brush", new Color(0.88f, 0.52f, 0.30f));

            var soBrush = new SerializedObject(brush);
            soBrush.FindProperty("player").objectReferenceValue = player;
            soBrush.FindProperty("board").objectReferenceValue = interact;
            soBrush.FindProperty("interactRange").floatValue = 1.0f;
            soBrush.FindProperty("startHidden").boolValue = true;
            soBrush.FindProperty("revealOnQuestId").enumValueIndex = (int)QuestId.TidyShelf;

            // 视觉体数组必须和上面的引用放在同一个 SerializedObject 里一次性 apply——
            // 旧版先 apply 再设这个数组且没有二次 apply，结果数组永远是空的，
            // 拿到画笔后那个色块不会消失。
            // 改名 visualsToHide → visuals，因为它现在两头都要用：
            // 被发现时点亮、被拾取时熄灭。
            var visuals = soBrush.FindProperty("visuals");
            visuals.arraySize = 1;
            visuals.GetArrayElementAtIndex(0).objectReferenceValue = brushSr;
            soBrush.ApplyModifiedPropertiesWithoutUndo();

            // 画笔也加交互提示，靠近时高亮
            var brushHint = brushGo.GetComponent<InteractionHint>();
            if (brushHint == null) brushHint = brushGo.AddComponent<InteractionHint>();
            brushHint.player = player;
            brushHint.triggerRange = brush.interactRange;
            var brushHintTargets = new SerializedObject(brushHint);
            var targets = brushHintTargets.FindProperty("highlightTargets");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = brushSr;
            brushHintTargets.ApplyModifiedPropertiesWithoutUndo();

            // ---- 7.5 吃早餐（S5，策划案 3.4） ----
            // 正式支线替代 Demo 道具：饱食 +50 / 15 游戏分钟 / 上报 Breakfast。
            // 原 TestSnack / TestWater 两个临时道具直接删除——
            // 喝水已由 S4 的接水特写（WaterSource → WaterCloseup）正式接管，
            // 留着会和它重复给 +20 水分，且占掉两个 X 位置导致 E 键误触发。
            DeleteLegacy("TestSnack");
            DeleteLegacy("TestWater");
            // 早餐挂 RoomWorld 下（客厅道具随房间隐藏，不穿帮进特写）
            var roomWorldForBreakfast = GameObject.Find("RoomWorld");
            EnsureBreakfast(roomWorldForBreakfast != null ? roomWorldForBreakfast.transform : systems.transform,
                player, stats, timer, new Vector3(-4.2f, 1.0f, 0f));

            // ---- 8. 总控 ----
            var managerGo = FindOrCreate("GameManager");
            var manager = Ensure<GameManager>(managerGo);
            var soMgr = new SerializedObject(manager);
            soMgr.FindProperty("stats").objectReferenceValue = stats;
            soMgr.FindProperty("timer").objectReferenceValue = timer;
            soMgr.FindProperty("board").objectReferenceValue = interact;
            soMgr.ApplyModifiedPropertiesWithoutUndo();

            // ---- 9. 对话系统（策划案 1.5） ----
            // ---- 9. 对话系统（策划案 1.5） ----
            // 注意：DialogueBoxUI（对话框 UI）改由第 11 节挂到 UI Canvas 下，这里不再挂。
            var dialogueGo = EnsureChild(systems.transform, "Dialogue");
            var dialogue = Ensure<DialogueManager>(dialogueGo);
            // 清掉旧版可能残留的 DialogueBoxUI（避免重复对话框 + 脱离 Canvas 渲染错位）
            var staleDlg = dialogueGo.GetComponent<DialogueBoxUI>();
            if (staleDlg != null) Object.DestroyImmediate(staleDlg);

            // 开场对白占位台词（E2）：Inspector 里随时可改，策划案正文到了直接替换数组。
            // 叙事引导顺序修正（用户提出"按逻辑不是要先吃早餐吗"）：
            // 饿 → 妈妈留的早餐 → 吃饱想起画画 → 找画笔，
            // 和支线表「吃早餐·开场引导」对齐。
            var openingLines = new DialogueLine[]
            {
                new DialogueLine { text = "唔……早上了，肚子好饿。", autoAdvance = 3f },
                new DialogueLine { text = "桌上应该有妈妈留的早餐，先垫垫肚子。", autoAdvance = 3f },
                new DialogueLine { text = "吃饱了。今天要画画来着……", autoAdvance = 3f },
                new DialogueLine { text = "先把画笔找出来吧——我记得落在书房那边了。", autoAdvance = 3f },
            };
            // 直接字段赋值：DialogueLine[] 是可序列化类数组，用 SerializedProperty
            // 逐个建实例太绕；编辑器代码里拿组件实例直接赋值一样能存盘。
            manager.dialogue = dialogue;
            manager.openingLines = openingLines;

            // ---- 10. 进度总线（契约层 S0） ----
            // 各支线完成后 Report 到这里，UI 任务提示 / 结算统计统一订阅。
            // GameManager.Awake 会把它注册进 Services.Progress。
            var progressGo = EnsureChild(systems.transform, "ProgressTracker");
            Ensure<ProgressTracker>(progressGo);

            // ---- 11. UI 四件套（S1，策划案 1.6） ----
            // 正式 uGUI：状态栏 / 时钟 / 对话 / 任务提示，全部订阅 Services，互不持引用。
            // 各自 Start 里自建房梁 Text（占位期系统字体），进 play 即显示；美术到位后
            // 可整段换成带图版 prefab 而不动玩法代码。
            var uiRoot = EnsureChild(systems.transform, "UIRoot");
            var uiCanvas = uiRoot.GetComponent<Canvas>();
            if (uiCanvas == null)
            {
                uiCanvas = uiRoot.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 10;
                var scaler = uiRoot.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                uiRoot.AddComponent<GraphicRaycaster>();
            }
            // 无条件写死：确保 UI 永远压在场景精灵（150xx）之上，不被遮住
            uiCanvas.sortingOrder = OrderUiCanvas;
            var statusGo = EnsureChild(uiRoot.transform, "StatusBar");
            Ensure<RoomDemo.UI.StatusBar>(statusGo);
            var clockGo = EnsureChild(uiRoot.transform, "ClockDisplay");
            var clock = Ensure<RoomDemo.UI.ClockDisplay>(clockGo);
            var clockSO = new SerializedObject(clock);
            clockSO.FindProperty("timer").objectReferenceValue = timer;
            clockSO.ApplyModifiedPropertiesWithoutUndo();
            var dlgGo = EnsureChild(uiRoot.transform, "DialogueBox");
            Ensure<DialogueBoxUI>(dlgGo);
            var taskGo = EnsureChild(uiRoot.transform, "TaskHints");
            Ensure<RoomDemo.UI.TaskHints>(taskGo);

            // 关键：面板宿主必须有 RectTransform 并锚到画布四角/底中，
            // 否则是挂在 Canvas 下的普通 Transform（位于世界原点≈屏幕中心），
            // 子 UI 全部堆在屏幕中央（已实测踩坑）。宿主零尺寸，子 Label 的
            // 锚点偏移即等效于相对画布对应角，与各组件 Build 里的设计值一致。
            EnsureUiHost(statusGo, new Vector2(0f, 1f));    // 左上
            EnsureUiHost(clockGo, new Vector2(1f, 1f));     // 右上
            EnsureUiHost(dlgGo, new Vector2(0.5f, 0f));     // 底中
            EnsureUiHost(taskGo, new Vector2(0f, 0f));      // 左下

            // ---- 12. 猫（S2，策划案 2.4） ----
            // 挂 RoomWorld 下：进画板特写时随房间整体 SetActive(false)
            // 一起隐藏，猫的 Update 一并冻结，退出自动恢复。
            // 占位纯色图，美术到位后换 sprite；游走/打乱参数 Inspector 可调。
            // 打乱只经 Services.Progress 上报（R1），不直接碰任何道具。
            var roomWorldGo = GameObject.Find("RoomWorld");
            var catParent = roomWorldGo != null ? roomWorldGo.transform : systems.transform;
            var catGo = catParent.Find("Cat")?.gameObject;
            if (catGo == null)
            {
                catGo = new GameObject("Cat");
                catGo.transform.SetParent(catParent, true);
                catGo.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            }
            // 位置无条件写死：猫的游走是以<b>这个位置为锚点</b>的（wanderRadius），
            // 播放态下猫走到哪儿，退出播放后位置若被留下来，下次开场猫就会
            // 在房间角落/家具里游走。所以每次装配都把它放回房间中间。
            catGo.transform.position = new Vector3(1.4f, -0.9f, 0f);
            var cat = Ensure<CatController>(catGo);
            var catSr = catGo.GetComponent<SpriteRenderer>();
            if (catSr == null)
            {
                catSr = catGo.AddComponent<SpriteRenderer>();
                catSr.sprite = MakePlaceholderSprite("cat", new Color(0.95f, 0.62f, 0.30f));
                catSr.sortingOrder = OrderCat;
            }
            // 数值案显式写死（公开字段序列化值不随代码默认值更新，K2）
            // moveSpeed 2.0：1.1 走到早餐点要 7.6s，太慢看着像卡住。
            var soCat = new SerializedObject(cat);
            soCat.FindProperty("moveSpeed").floatValue = 2.0f;
            soCat.FindProperty("wanderRadius").floatValue = 1.2f;
            soCat.FindProperty("standYOffset").floatValue = -0.7f;
            soCat.FindProperty("arriveDistance").floatValue = 0.15f;
            soCat.FindProperty("minInterval").floatValue = 10f;
            soCat.FindProperty("maxInterval").floatValue = 20f;
            soCat.FindProperty("maxMischiefPerRun").intValue = 3;
            soCat.ApplyModifiedPropertiesWithoutUndo();

            // ---- 13. 收拾（S3，策划案 2.3） ----
            // 桌面 + 书架各一个 TidySpot，两段式 E（第一次高亮、第二次执行）。
            // 收拾物 = Items 下的 itemCount 个占位色块；高亮 = Highlight 子物体。
            // 收拾特写：进入收拾即切到独立视图（隐藏客厅、相机聚焦深蓝底+棕块背景），
            // 仿画板特写（方向 A）。全部可重复执行：已存在只补齐，不覆盖美术调好的参数。
            var tidyParent = roomWorldGo != null ? roomWorldGo.transform : systems.transform;
            var cam = FindOrthoCamera();
            var tidyCloseup = EnsureTidyCloseup(systems.transform, cam,
                roomWorldGo != null ? roomWorldGo.transform : null, player);
            // 注意：X 位置一律按 2.0 间距排开（早餐 -4.2 / 桌面 -2.2 / 画板 0 /
            // 水源 2.2 / 书架 4.2），彼此的交互区间互不重叠——
            // 否则站中间按一次 E 会同时触发两件东西。
            EnsureTidySpot(tidyParent, player, stats, tidyCloseup, "TidyDesk", new Vector3(-2.2f, 0.9f, 0f), QuestId.TidyDesk);
            EnsureTidySpot(tidyParent, player, stats, tidyCloseup, "TidyShelf", new Vector3(4.2f, 1.1f, 0f), QuestId.TidyShelf);

            // ---- 13b. 接水特写（S4，策划案 2.2） ----
            // 与收拾特写共用 CloseupBase（隐藏客厅 / 相机 / 总闸门 / Esc / IResettable）。
            // 走到水源按 E 进入，按住鼠标接水，满则 +20 水分 / 扣 15 游戏分钟 / 上报 Water。
            var waterCloseup = EnsureWaterCloseup(systems.transform, cam,
                roomWorldGo != null ? roomWorldGo.transform : null, stats, timer, player);
            EnsureWaterSource(tidyParent, player, waterCloseup, new Vector3(2.2f, 0.8f, 0f));

            // ---- 14. 客厅布景 ----
            // 让客厅不再是"深蓝底 + 浮空棕块"：加墙 / 地板 / 窗 / 地毯 / 装饰。
            // 全部挂 RoomWorld 下，进特写随房间一起隐藏；sortingOrder 低值垫底。
            EnsureRoomScenery(roomWorldGo != null ? roomWorldGo.transform : null);

            // ---- 15. 主角层级（P8） ----
            EnsurePlayerLayer(player);

            // ---- 16. 猫路点接线 ----
            // 此时五个交互物都已就位（猫那节在前面建不了）：
            // 猫巡逻时随机挑一个走到跟前站着，不再困在窝的 1.2 半径里。
            var waypointNames = new[] { "Breakfast", "TidyDesk", "DrawingBoard", "WaterSource", "TidyShelf" };
            var wpList = new Transform[waypointNames.Length];
            var wpFound = 0;
            for (var i = 0; i < waypointNames.Length; i++)
            {
                var wpGo = GameObject.Find(waypointNames[i]);
                if (wpGo != null) { wpList[i] = wpGo.transform; wpFound++; }
            }
            var soCatWp = new SerializedObject(cat);
            var wpProp = soCatWp.FindProperty("waypoints");
            wpProp.arraySize = wpList.Length;
            for (var i = 0; i < wpList.Length; i++)
                wpProp.GetArrayElementAtIndex(i).objectReferenceValue = wpList[i];
            soCatWp.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[画板] 猫路点接线完成：{wpFound}/5 个交互点");

            // ---- 17. 边界墙矫正 ----
            // 四面隐形墙是根物体"GameObject(n)"（只有 BoxCollider2D）。
            // 上墙的 offset (0.25,-1.03) 是从下墙抄来的，导致碰撞体下坠 1.5 格
            // 悬在可走区域正上方（y∈[1.22,2.12]），玩家走到 y≈1.6 就撞看不见的墙。
            // 这里按位置认墙、无条件写死 offset/size，重跑菜单即可修复。
            var wallFixed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var box = root.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                if (root.GetComponent<Rigidbody2D>() != null) continue;      // Player
                if (root.GetComponentsInChildren<SpriteRenderer>(true).Length > 0) continue;
                var p = root.transform.position;
                if (p.y > 1.5f)
                {
                    // 上墙：横贯全宽（x ∈ [-5.86, 5.74]），底缘 2.45——
                    // 原来只到 x=3.98，右上角留了个逃逸洞（玩家曾溜到 x=6）
                    box.offset = new Vector2(0.21f, 0.2f);
                    box.size = new Vector2(11.6f, 0.9f);
                    wallFixed++;
                }
                else if (p.y < -1.5f)
                {
                    box.offset = new Vector2(0.25f, -1.03f);
                    box.size = new Vector2(9.63f, 0.9f);
                    wallFixed++;
                }
                else if (p.x < -4f || p.x > 4f)
                {
                    // 侧墙加高到与上墙底缘 (y=2.45) 相接，不留角缝
                    box.offset = new Vector2(0f, 0.055f);
                    box.size = new Vector2(1f, 5.69f);
                    wallFixed++;
                }
                EditorUtility.SetDirty(root);
            }
            Debug.Log($"[画板] 边界墙矫正完成：{wallFixed}/4 面墙");

            EditorUtility.SetDirty(boardGo);
            EditorUtility.SetDirty(containerGo);
            EditorUtility.SetDirty(systems);
            EditorUtility.SetDirty(managerGo);
            EditorSceneManager.MarkSceneDirty(scene);

            // 存盘，这样画板功能就真正落在 TestScene 里了
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[画板] {TargetSceneName} 搭建完成并已存盘：" +
                      $"三块画板就位（每块落笔计时 {boards[0].canvas.paintingSeconds:F0} 秒达标）");
            if (player == null)
                Debug.LogWarning("[画板] 场景里没找到 Player，请手动把画板和画笔的 player 引用指向主角");
            if (boardSr.sprite == null)
                Debug.LogWarning("[画板] DrawingBoard 还没有手绘精灵，房间内看不见它——请把美术图拖到 Sprite Renderer 的 Sprite 槽");
        }

        // ---------------------------------------------------------------

        /// <summary>找主角。优先认带移动组件的，其次认名为 Player 的物体。</summary>
        static Transform FindPlayerTransform()
        {
            var withMovement = Object.FindObjectOfType<Simple2DMovement>();
            if (withMovement != null) return withMovement.transform;

            var named = GameObject.Find("Player");
            if (named != null) return named.transform;

            var srs = Object.FindObjectsOfType<SpriteRenderer>();
            return srs.Length == 1 ? srs[0].transform : null;
        }

        static Camera FindOrthoCamera()
        {
            var main = Camera.main;
            if (main != null) return main;

            foreach (var c in Object.FindObjectsOfType<Camera>(true))
                if (c.orthographic) return c;

            return null;
        }

        static GameObject EnsureChild(Transform parent, string name)
        {
            var go = parent.Find(name)?.gameObject;
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        /// <summary>
        /// 给 Canvas 下的面板宿主补 RectTransform 并锚定到 anchor（零尺寸、零偏移）。
        /// 普通空物体挂在 Canvas 下时停留在世界原点（≈屏幕中心），其下所有 UI
        /// 会堆在屏幕中央——宿主必须有 RectTransform。
        /// </summary>
        static void EnsureUiHost(GameObject go, Vector2 anchor)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// 主角提层（P8）：把 Player 下所有 SpriteRenderer 拉到交互物之上。
        /// 场景里主角是手摆的（Body=10 / Shadow=0），公开字段改默认值不会更新
        /// 已序列化值，所以这里无条件写死。阴影低一格，避免同层级时绘制顺序
        /// 不稳定把身体盖住。
        /// </summary>
        static void EnsurePlayerLayer(Transform player)
        {
            if (player == null) return;
            var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
                sr.sortingOrder = sr.gameObject.name == "Shadow" ? OrderPlayer - 1 : OrderPlayer;
        }

        static SpriteRenderer EnsureRenderer(GameObject go, int sortingOrder)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        static GameObject FindOrCreate(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();

            // 播放模式下装配过的场景，退出 play 时组件引用会丢，
            // 但物体还在——这时 AddComponent 可能补不回来，需要显式校验。
            if (c == null)
            {
                Debug.LogError($"[画板] 无法给 {go.name} 挂上 {typeof(T).Name}，请检查脚本是否编译通过");
            }
            return c;
        }

        /// <summary>删掉被正式支线取代的 Demo 遗留物体（可重复执行，找不到就跳过）。</summary>
        static void DeleteLegacy(string objName)
        {
            var go = GameObject.Find(objName);
            if (go == null) return;
            Object.DestroyImmediate(go);
            Debug.Log($"[画板] 已删除 Demo 遗留物体 {objName}（正式支线已接管）");
        }

        /// <summary>
        /// 摆放早餐（S5，策划案 3.4：客厅 · 饥饿 +50% · 开场引导）。
        /// 复用 ConsumablePickup，只是把数值和上报 id 换成正式支线的。
        /// </summary>
        static void EnsureBreakfast(Transform parent, Transform player, PlayerStats stats, DayTimer timer,
            Vector3 worldPos)
        {
            var go = GameObject.Find("Breakfast");
            if (go == null)
            {
                go = new GameObject("Breakfast");
                go.transform.SetParent(parent, true); // 保留世界坐标
            }
            else if (go.transform.parent != parent)
            {
                // 旧装配挂在 GameSystems 下 → 搬到 RoomWorld，随房间一起隐藏
                go.transform.SetParent(parent, true);
            }
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(0.7f, 0.55f, 1f);

            var cp = Ensure<ConsumablePickup>(go);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = OrderBrush;
            }
            if (sr.sprite == null)
                sr.sprite = MakePlaceholderSprite("breakfast", new Color(0.96f, 0.80f, 0.48f));

            var so = new SerializedObject(cp);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("stats").objectReferenceValue = stats;
            so.FindProperty("timer").objectReferenceValue = timer;
            so.FindProperty("effect").enumValueIndex = (int)ConsumablePickup.Effect.Satiety;
            so.FindProperty("amount").floatValue = 50f;
            so.FindProperty("costTime").boolValue = true;
            so.FindProperty("costMinutes").floatValue = 15f;
            so.FindProperty("interactRange").floatValue = 1.0f;
            so.FindProperty("reportId").enumValueIndex = (int)QuestId.Breakfast;
            so.ApplyModifiedPropertiesWithoutUndo();

            cp.lines = new DialogueLine[]
            {
                new DialogueLine { text = "吃了妈妈留的早餐，肚子终于不叫了。", autoAdvance = 3f }
            };
            EditorUtility.SetDirty(cp);
        }

        /// <summary>摆放一个收拾点（可重复执行：已存在只补齐组件和引用、不覆盖美术参数）。</summary>
        static void EnsureTidySpot(Transform parent, Transform player, PlayerStats stats, RoomDemo.Quest.TidyCloseup closeup,
            string objName, Vector3 worldPos, QuestId reportId)
        {
            var go = GameObject.Find(objName);
            if (go == null)
            {
                go = new GameObject(objName);
                go.transform.SetParent(parent, true); // 保留世界坐标
            }
            // X 无条件写死：交互只看水平距离，X 挤在一起会导致一次 E 触发多件东西。
            go.transform.position = worldPos;

            var spot = Ensure<RoomDemo.Quest.TidySpot>(go);

            // 清掉旧的房间侧杂物 / 高亮——杂物与高亮已移到收拾特写里（"移过去"），
            // 客厅里只留家具本体，这样"走进特写才看见乱"才成立。
            var staleItems = go.transform.Find("Items");
            if (staleItems != null) Object.DestroyImmediate(staleItems.gameObject);
            var staleHl = go.transform.Find("Highlight");
            if (staleHl != null) Object.DestroyImmediate(staleHl.gameObject);

            // 家具本体（桌 / 书架）：客厅里唯一的视觉标识
            var furniture = EnsureChild(go.transform, "Furniture");
            var fSr = furniture.GetComponent<SpriteRenderer>();
            if (fSr == null)
            {
                fSr = furniture.AddComponent<SpriteRenderer>();
                fSr.sprite = MakePlaceholderSprite("tidy_furniture_" + objName.ToLowerInvariant(),
                    new Color(0.44f, 0.31f, 0.22f));
                fSr.sortingOrder = OrderFurniture;
            }
            furniture.transform.localPosition = new Vector3(0f, 0f, 0f);
            furniture.transform.localScale = new Vector3(2.4f, 1.6f, 1f);

            // 接线（公开字段直接赋值，序列化随场景存盘；重跑不覆盖美术调好的参数）
            spot.player = player;
            spot.stats = stats;
            spot.reportId = reportId;
            spot.effect = StatKind.Spirit;
            spot.amount = 20f;
            spot.itemCount = 3;      // 杂物数量（实际视觉在收拾特写里）
            spot.interactRange = 1.0f;
            spot.itemsRoot = null;   // 房间侧不再有杂物（在特写里）
            spot.highlight = null;   // 房间侧不再有高亮（在特写里）
            spot.closeup = closeup;
            // 书架的台词第二行就是「发现画笔」的提示（S6）：
            // 收拾完成 → TidyShelf 上报 → 画笔收到进度事件自己显形，
            // 这条台词紧接着告诉玩家笔出现了，两条支线靠进度总线解耦。
            spot.lines = objName == "TidyDesk"
                ? new[]
                {
                    new DialogueLine { text = "桌面收拾干净了，清爽！", autoAdvance = 3f }
                }
                : new[]
                {
                    new DialogueLine { text = "书架理好了，舒服多了。", autoAdvance = 3f },
                    new DialogueLine { text = "嗯？书架上还插着一支笔——正好，就是它。", autoAdvance = 3f }
                };
            EditorUtility.SetDirty(spot);
        }

        /// <summary>
        /// 搭建收拾特写容器（可重复执行：已存在只补齐组件和视觉，不覆盖美术调好的参数）。
        /// 默认 inactive，进入收拾（TidySpot 第一段）时由 TidyCloseup.Enter 激活。
        /// 视觉：深蓝 = 相机清屏色（隐藏客厅后天然呈现）；大棕块 = Backdrop 占位精灵；
        /// 收拾物 = Items 下 3 个占位；高亮 = Highlight 子物体（进入时点亮）。
        /// </summary>
        static RoomDemo.Quest.TidyCloseup EnsureTidyCloseup(Transform parent, Camera cam, Transform roomRoot,
            Transform player)
        {
            // 注意：不能用 GameObject.Find——它找不到 inactive 的物体，会导致每次
            // 重跑菜单都新建一个同名副本（实测踩坑）。必须用父级作用域的 EnsureChild。
            var go = EnsureChild(parent, "TidyCloseupRoot");
            go.SetActive(false);

            var tc = Ensure<RoomDemo.Quest.TidyCloseup>(go);

            // 收拾特写不再用合成背景/高亮：画面主体改成玩家手摆的透视块
            // （GameObject (1)~(6)，带各自旋转），只在容器外保留它们，这里一律清掉旧的。
            var staleBg = go.transform.Find("Backdrop");
            if (staleBg != null) Object.DestroyImmediate(staleBg.gameObject);
            var staleHl = go.transform.Find("Highlight");
            if (staleHl != null) Object.DestroyImmediate(staleHl.gameObject);

            // 收拾物（3 个占位）
            var itemsRoot = EnsureChild(go.transform, "Items");
            for (int i = 0; i < 3; i++)
            {
                var itemGo = itemsRoot.transform.Find("Item" + i)?.gameObject;
                if (itemGo == null)
                {
                    itemGo = new GameObject("Item" + i);
                    itemGo.transform.SetParent(itemsRoot.transform, false);
                }
                var sr = itemGo.GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    sr = itemGo.AddComponent<SpriteRenderer>();
                    sr.sprite = MakePlaceholderSprite("tidycloseup_item", new Color(0.82f, 0.78f, 0.30f));
                }
                // 摆在画面中央、叠在手摆透视块之上（透视块 sortingOrder 较低）
                itemGo.transform.localPosition = new Vector3(i * 1.5f - 1.5f, -0.2f, 0f);
                itemGo.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
                sr.sortingOrder = OrderTidyItem;
            }

            // 接线（公开字段直接赋值，序列化随场景存盘；重跑不覆盖美术调好的参数）
            tc.mainCamera = cam;
            tc.roomRoot = roomRoot;
            tc.closeupRoot = go.transform;
            tc.player = player;   // 进特写把主角一起藏起来，否则角色穿帮进特写
            tc.itemsRoot = itemsRoot.transform;
            tc.highlight = null;   // 特写不再有全屏黄色高亮（会糊住手摆透视块）
            EditorUtility.SetDirty(tc);
            return tc;
        }

        /// <summary>
        /// 搭建接水特写容器（S4，可重复执行）。默认 inactive，走到水源按 E 时进入。
        /// 观感与收拾特写一致：深蓝 = 相机清屏色（隐藏客厅后天然呈现），大棕块 = Backdrop。
        /// 视觉：水龙头 + 杯子（容器）+ 水柱（WaterFill，按住鼠标上涨）。
        /// </summary>
        static RoomDemo.Quest.WaterCloseup EnsureWaterCloseup(Transform parent, Camera cam, Transform roomRoot,
            PlayerStats stats, DayTimer timer, Transform player)
        {
            // 同 EnsureTidyCloseup：用父级作用域 EnsureChild，不能用 GameObject.Find
            var go = EnsureChild(parent, "WaterCloseupRoot");
            go.SetActive(false);

            var wc = Ensure<RoomDemo.Quest.WaterCloseup>(go);

            // 背景大棕块
            var bg = EnsureChild(go.transform, "Backdrop");
            var bgSr = bg.GetComponent<SpriteRenderer>();
            if (bgSr == null)
            {
                bgSr = bg.AddComponent<SpriteRenderer>();
                bgSr.sprite = MakePlaceholderSprite("watercloseup_bg", new Color(0.46f, 0.42f, 0.36f));
                bgSr.sortingOrder = 0;
            }
            bg.transform.localPosition = new Vector3(0f, 0f, -1f);
            bg.transform.localScale = new Vector3(12f, 7f, 1f);

            // 杯子（容器）
            var cup = EnsureChild(go.transform, "Cup");
            var cupSr = cup.GetComponent<SpriteRenderer>();
            if (cupSr == null)
            {
                cupSr = cup.AddComponent<SpriteRenderer>();
                cupSr.sprite = MakePlaceholderSprite("watercloseup_cup", new Color(0.90f, 0.92f, 0.93f));
                cupSr.sortingOrder = 1;
            }
            cup.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            cup.transform.localScale = new Vector3(2.6f, 4.2f, 1f);

            // 水柱（蓝色，底边对齐，localScale.y 由 WaterCloseup 驱动）
            var fill = EnsureChild(go.transform, "WaterFill");
            var fillSr = fill.GetComponent<SpriteRenderer>();
            if (fillSr == null)
            {
                fillSr = fill.AddComponent<SpriteRenderer>();
                fillSr.sprite = MakePlaceholderSprite("watercloseup_fill", new Color(0.35f, 0.62f, 0.85f));
                fillSr.sortingOrder = 2;
            }
            fill.transform.localPosition = new Vector3(0f, -2.2f, 0f);
            fill.transform.localScale = new Vector3(2.2f, 0.001f, 1f);

            // 水龙头（上方小色块）
            var tap = EnsureChild(go.transform, "Tap");
            var tapSr = tap.GetComponent<SpriteRenderer>();
            if (tapSr == null)
            {
                tapSr = tap.AddComponent<SpriteRenderer>();
                tapSr.sprite = MakePlaceholderSprite("watercloseup_tap", new Color(0.66f, 0.70f, 0.74f));
                tapSr.sortingOrder = 1;
            }
            tap.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            tap.transform.localScale = new Vector3(1.2f, 0.9f, 1f);

            // 接线（数值案显式写死：+20 水分 / 15 游戏分钟 / 2.5 秒接满）
            wc.waterFill = fill.transform;
            wc.fillBottomY = -2.2f;
            wc.fillMaxHeight = 3.6f;
            wc.fillSeconds = 2.5f;
            wc.mainCamera = cam;
            wc.roomRoot = roomRoot;
            wc.closeupRoot = go.transform;
            wc.player = player;   // 进特写把主角一起藏起来
            wc.stats = stats;
            wc.timer = timer;
            wc.amount = 20f;
            wc.costMinutes = 15f;
            wc.lines = new DialogueLine[]
            {
                new DialogueLine { text = "接了杯水，喝下去舒服多了。", autoAdvance = 3f }
            };
            EditorUtility.SetDirty(wc);
            return wc;
        }

        /// <summary>摆放客厅里的水源（S4 入口）：走近按 E 进接水特写。</summary>
        static void EnsureWaterSource(Transform parent, Transform player, RoomDemo.Quest.WaterCloseup closeup, Vector3 worldPos)
        {
            var go = GameObject.Find("WaterSource");
            if (go == null)
            {
                go = new GameObject("WaterSource");
                go.transform.SetParent(parent, true); // 保留世界坐标
            }
            go.transform.position = worldPos;

            var ws = Ensure<RoomDemo.Quest.WaterSource>(go);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = MakePlaceholderSprite("water_source", new Color(0.55f, 0.72f, 0.85f));
                sr.sortingOrder = OrderFurniture;
            }
            go.transform.localScale = new Vector3(0.9f, 1.4f, 1f);

            ws.player = player;
            ws.closeup = closeup;
            ws.interactRange = 1.0f;
            ws.canInteract = true;
            EditorUtility.SetDirty(ws);
        }

        /// <summary>
        /// 客厅布景（让房间不再是"深蓝底 + 几块浮空棕块"）：
        /// 墙 / 地板 / 窗 / 地毯，全部挂 RoomWorld 下（进特写随房间一起隐藏），
        /// sortingOrder 用低值垫在交互物（150xx）后面。占位色块，美术到位可整段替换。
        /// </summary>
        static void EnsureRoomScenery(Transform roomParent)
        {
            if (roomParent == null) return;

            // 墙：铺满上半部
            var wall = EnsureChild(roomParent, "Wall");
            var wallSr = wall.GetComponent<SpriteRenderer>();
            if (wallSr == null)
            {
                wallSr = wall.AddComponent<SpriteRenderer>();
                wallSr.sprite = MakePlaceholderSprite("room_wall", new Color(0.72f, 0.70f, 0.66f));
                wallSr.sortingOrder = -100;
            }
            wall.transform.localPosition = new Vector3(0f, 1.0f, 2f);
            wall.transform.localScale = new Vector3(26f, 12f, 1f);

            // 地板：底部横向木色带，房间立刻有纵深
            var floor = EnsureChild(roomParent, "Floor");
            var floorSr = floor.GetComponent<SpriteRenderer>();
            if (floorSr == null)
            {
                floorSr = floor.AddComponent<SpriteRenderer>();
                floorSr.sprite = MakePlaceholderSprite("room_floor", new Color(0.52f, 0.36f, 0.24f));
                floorSr.sortingOrder = -90;
            }
            floor.transform.localPosition = new Vector3(0f, -2.6f, 1f);
            floor.transform.localScale = new Vector3(26f, 4f, 1f);

            // 窗：墙上浅色矩形（光源感）
            var win = EnsureChild(roomParent, "Window");
            var winSr = win.GetComponent<SpriteRenderer>();
            if (winSr == null)
            {
                winSr = win.AddComponent<SpriteRenderer>();
                winSr.sprite = MakePlaceholderSprite("room_window", new Color(0.85f, 0.92f, 0.95f));
                winSr.sortingOrder = -80;
            }
            win.transform.localPosition = new Vector3(-3.2f, 2.4f, 1.5f);
            win.transform.localScale = new Vector3(3.2f, 2.6f, 1f);

            // 地毯：地面暖色矩形
            var rug = EnsureChild(roomParent, "Rug");
            var rugSr = rug.GetComponent<SpriteRenderer>();
            if (rugSr == null)
            {
                rugSr = rug.AddComponent<SpriteRenderer>();
                rugSr.sprite = MakePlaceholderSprite("room_rug", new Color(0.62f, 0.42f, 0.45f));
                rugSr.sortingOrder = -70;
            }
            rug.transform.localPosition = new Vector3(0f, -1.9f, 0.5f);
            rug.transform.localScale = new Vector3(6f, 1.4f, 1f);

            // 挂画：右侧墙面小装饰
            var frame = EnsureChild(roomParent, "WallFrame");
            var frameSr = frame.GetComponent<SpriteRenderer>();
            if (frameSr == null)
            {
                frameSr = frame.AddComponent<SpriteRenderer>();
                frameSr.sprite = MakePlaceholderSprite("room_frame", new Color(0.80f, 0.68f, 0.45f));
                frameSr.sortingOrder = -60;
            }
            frame.transform.localPosition = new Vector3(3.0f, 2.2f, 1.5f);
            frame.transform.localScale = new Vector3(1.6f, 2.0f, 1f);

            // 盆栽：左侧地面小装饰
            var plant = EnsureChild(roomParent, "Plant");
            var plantSr = plant.GetComponent<SpriteRenderer>();
            if (plantSr == null)
            {
                plantSr = plant.AddComponent<SpriteRenderer>();
                plantSr.sprite = MakePlaceholderSprite("room_plant", new Color(0.38f, 0.55f, 0.35f));
                plantSr.sortingOrder = -50;
            }
            plant.transform.localPosition = new Vector3(-4.2f, -1.2f, 0.5f);
            plant.transform.localScale = new Vector3(1.0f, 1.8f, 1f);
        }

        /// <summary>
        /// 生成/加载一张纯色占位精灵。注意必须存成 PNG 资产再用
        /// TextureImporter 转成 Sprite——运行时 Texture2D 直接 Sprite.Create
        /// 出来的引用存不进场景，重开场景会丢。
        /// </summary>
        static Sprite MakePlaceholderSprite(string name, Color color)
        {
            const string dir = "Assets/Scenes/Placeholders";
            string path = dir + "/" + name + ".png";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Scenes", "Placeholders");

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(16, 16);
            var px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = color;
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
