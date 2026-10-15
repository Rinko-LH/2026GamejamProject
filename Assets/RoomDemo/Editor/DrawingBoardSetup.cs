using UnityEditor;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 把画板玩法所需的对象装进 RoomDemo 场景。
    ///
    /// 单独做成菜单项而不是塞进 RoomSceneBuilder，是为了不动原有房间构建逻辑——
    /// 这样美术改房间时不会和玩法装配互相覆盖。
    ///
    /// 结构（可重复执行，已存在的对象只补齐组件和引用，不重复创建）：
    /// GameSystems/
    ///   PlayerStats / DayTimer
    ///   DrawingBoard/            ← 房间里的可见画板色块 + 交互入口
    ///     PaintingBoards/        ← 特写里的三块画板（房间内隐藏）
    ///       PaintingBoard1~3/
    ///         Frame  Canvas  BarBg  BarFill
    ///   BrushPickup/
    /// GameDirector/
    /// </summary>
    public static class DrawingBoardSetup
    {
        const string MenuPath = "GameJam/SetupDrawingBoard";
        const int BoardCount = 3;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.name != "RoomDemo")
            {
                Debug.LogWarning($"[画板] 当前场景是 {scene.name}，请先切到 RoomDemo");
                return;
            }

            var player = Object.FindObjectOfType<RoomPlayer>();

            // ---- 1. 系统节点 ----
            var systems = FindOrCreate("GameSystems");
            var stats = Ensure<PlayerStats>(systems);
            var timer = Ensure<DayTimer>(systems);

            // ---- 2. 画板（挂在书桌上方，房间里的交互入口） ----
            var boardGo = GameObject.Find("DrawingBoard");
            if (boardGo == null)
            {
                boardGo = new GameObject("DrawingBoard");
                // 书桌位置：RoomSceneBuilder 里 AddDesk(1.8, Depth*0.74)
                boardGo.transform.position = new Vector3(1.8f, RoomView.FloorY(RoomView.RoomDepth * 0.74f) + 0.9f, 0f);
                boardGo.transform.SetParent(systems.transform, false);
            }

            var interact = Ensure<DrawingBoardInteract>(boardGo);

            // ---- 2.5 旧版结构迁移 ----
            // 老版本画布是 DrawingBoard 的直接子物体且叫 "Canvas"。
            // 新结构里画布挂在 PaintingBoardN 下，所以这里可以安全删掉旧的。
            var oldCanvas = boardGo.transform.Find("Canvas");
            if (oldCanvas != null)
            {
                Object.DestroyImmediate(oldCanvas.gameObject);
                Debug.Log("[画板] 已移除旧版单画布对象，切换为三画板结构");
            }

            // ---- 3. 画板本体标记（房间里的可见色块） ----
            var boardSr = boardGo.GetComponent<SpriteRenderer>();
            if (boardSr == null)
            {
                boardSr = boardGo.AddComponent<SpriteRenderer>();
                boardSr.sprite = RoomShapes.Rect;
                boardSr.color = RoomPalette.WoodDark;
                boardSr.sortingOrder = RoomView.Order(RoomView.RoomDepth * 0.74f, 4);
                boardGo.transform.localScale = new Vector3(1.6f, 1.1f, 1f);
            }

            // ---- 4. 交互提示组件 ----
            // 画板节点原本只是个空壳，玩家看不见也摸不着，
            // 必须给它一个可见的色块标记，靠近了还要高亮闪烁。
            //
            // 注意：highlightTargets 必须显式指定为画板本体那一个渲染器。
            // 不指定的话 Awake 会自动收集子物体下所有渲染器——三块画板的
            // 外框/画布会被一起卷进呼吸高亮，和 PaintingBoard 自己的
            // 悬停/达标配色打架。
            var hint = boardGo.GetComponent<InteractionHint>();
            if (hint == null) hint = boardGo.AddComponent<InteractionHint>();
            hint.player = player != null ? player.transform : null;
            hint.triggerRange = 1.6f;

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

                // 外框：垫在画布底下当边框
                var frameGo = bGo.transform.Find("Frame")?.gameObject;
                if (frameGo == null)
                {
                    frameGo = new GameObject("Frame");
                    frameGo.transform.SetParent(bGo.transform, false);
                }
                var frameSr = frameGo.GetComponent<SpriteRenderer>();
                if (frameSr == null) frameSr = frameGo.AddComponent<SpriteRenderer>();
                frameSr.sprite = RoomShapes.Rect;
                frameSr.sortingOrder = RoomView.LayerPropsMax + 100;

                // 画布
                var canvasGo = bGo.transform.Find("Canvas")?.gameObject;
                if (canvasGo == null)
                {
                    canvasGo = new GameObject("Canvas");
                    canvasGo.transform.SetParent(bGo.transform, false);
                }
                var canvasSr = canvasGo.GetComponent<SpriteRenderer>();
                if (canvasSr == null) canvasSr = canvasGo.AddComponent<SpriteRenderer>();
                canvasSr.sortingOrder = RoomView.LayerPropsMax + 101;

                // 计时条（底 + 左锚点填充）
                var barBgGo = bGo.transform.Find("BarBg")?.gameObject;
                if (barBgGo == null)
                {
                    barBgGo = new GameObject("BarBg");
                    barBgGo.transform.SetParent(bGo.transform, false);
                }
                var barBgSr = barBgGo.GetComponent<SpriteRenderer>();
                if (barBgSr == null) barBgSr = barBgGo.AddComponent<SpriteRenderer>();
                barBgSr.sprite = RoomShapes.Rect;
                barBgSr.sortingOrder = RoomView.LayerPropsMax + 102;

                var barFillGo = bGo.transform.Find("BarFill")?.gameObject;
                if (barFillGo == null)
                {
                    barFillGo = new GameObject("BarFill");
                    barFillGo.transform.SetParent(bGo.transform, false);
                }
                var barFillSr = barFillGo.GetComponent<SpriteRenderer>();
                if (barFillSr == null) barFillSr = barFillGo.AddComponent<SpriteRenderer>();
                barFillSr.sprite = RoomShapes.Bar;
                barFillSr.sortingOrder = RoomView.LayerPropsMax + 103;

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

            // Camera.main 依赖 MainCamera 标签，而场景里的相机是 RoomSceneBuilder 运行时
            // 生成的，标签要等 play 模式才生效。所以这里直接全场景找一台正交相机。
            var sceneCam = Camera.main;
            if (sceneCam == null)
            {
                foreach (var c in Object.FindObjectsOfType<Camera>(true))
                {
                    if (c.orthographic) { sceneCam = c; break; }
                }
            }
            so.FindProperty("mainCamera").objectReferenceValue = sceneCam;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- 7. 画笔（放在书架附近） ----
            var brushGo = GameObject.Find("BrushPickup");
            if (brushGo == null)
            {
                brushGo = new GameObject("BrushPickup");
                // 书架位置：RoomSceneBuilder 里 AddBookshelf(-3.4, Depth*0.86)
                brushGo.transform.position = new Vector3(-3.4f, RoomView.FloorY(RoomView.RoomDepth * 0.86f) + 0.5f, 0f);
                brushGo.transform.SetParent(systems.transform, false);
            }
            var brush = Ensure<BrushPickup>(brushGo);
            var brushSr = brushGo.GetComponent<SpriteRenderer>();
            if (brushSr == null)
            {
                brushSr = brushGo.AddComponent<SpriteRenderer>();
                brushSr.sprite = RoomShapes.Rect;
                brushSr.color = RoomPalette.Book;
                brushSr.sortingOrder = RoomView.Order(RoomView.RoomDepth * 0.86f, 5);
            }
            brushGo.transform.localScale = new Vector3(0.12f, 0.45f, 1f);

            var soBrush = new SerializedObject(brush);
            soBrush.FindProperty("player").objectReferenceValue = player;
            soBrush.FindProperty("board").objectReferenceValue = interact;

            // 视觉体收集交给 sprites 数组。
            // 注意：必须和上面的引用放在同一个 SerializedObject 里一次性 apply——
            // 旧版先 apply 再设这个数组且没有二次 apply，结果数组永远是空的，
            // 拿到画笔后书架上那个色块不会消失。
            var visuals = soBrush.FindProperty("visualsToHide");
            visuals.arraySize = 1;
            visuals.GetArrayElementAtIndex(0).objectReferenceValue = brushSr;
            soBrush.ApplyModifiedPropertiesWithoutUndo();

            // 画笔也加交互提示，靠近时高亮
            var brushHint = brushGo.GetComponent<InteractionHint>();
            if (brushHint == null) brushHint = brushGo.AddComponent<InteractionHint>();
            brushHint.player = player != null ? player.transform : null;
            brushHint.triggerRange = brush.interactRange;
            var brushHintTargets = new SerializedObject(brushHint);
            var targets = brushHintTargets.FindProperty("highlightTargets");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = brushSr;
            brushHintTargets.ApplyModifiedPropertiesWithoutUndo();

            // ---- 8. 总控 ----
            var directorGo = FindOrCreate("GameDirector");
            var director = Ensure<GameDirector>(directorGo);
            var soDir = new SerializedObject(director);
            soDir.FindProperty("stats").objectReferenceValue = stats;
            soDir.FindProperty("timer").objectReferenceValue = timer;
            soDir.FindProperty("board").objectReferenceValue = interact;
            soDir.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(boardGo);
            EditorUtility.SetDirty(containerGo);
            EditorUtility.SetDirty(systems);
            EditorUtility.SetDirty(directorGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[画板] 搭建完成：三块画板就位（每块落笔计时 {boards[0].canvas.paintingSeconds:F0} 秒达标）");
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
    }
}
