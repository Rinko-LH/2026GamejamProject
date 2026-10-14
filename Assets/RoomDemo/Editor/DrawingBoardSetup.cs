using UnityEditor;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 把画板玩法所需的对象装进 RoomDemo 场景。
    ///
    /// 单独做成菜单项而不是塞进 RoomSceneBuilder，是为了不动原有房间构建逻辑——
    /// 这样美术改房间时不会和玩法装配互相覆盖。
    /// </summary>
    public static class DrawingBoardSetup
    {
        const string MenuPath = "GameJam/SetupDrawingBoard";

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

            // ---- 2. 画板（挂在书桌上方） ----
            var boardGo = GameObject.Find("DrawingBoard");
            if (boardGo == null)
            {
                boardGo = new GameObject("DrawingBoard");
                // 书桌位置：RoomSceneBuilder 里 AddDesk(1.8, Depth*0.74)
                boardGo.transform.position = new Vector3(1.8f, RoomView.FloorY(RoomView.RoomDepth * 0.74f) + 0.9f, 0f);
                boardGo.transform.SetParent(systems.transform, false);
            }

            var canvas = Ensure<DrawingCanvas>(boardGo);
            var interact = Ensure<DrawingBoardInteract>(boardGo);

            // ---- 3. 画布精灵 ----
            // 注意 Transform.Find 返回的是 Transform，不是 GameObject，
            // 所以这里显式转成 GameObject 再操作组件。
            var canvasGo = boardGo.transform.Find("Canvas")?.gameObject;
            if (canvasGo == null)
            {
                canvasGo = new GameObject("Canvas");
                canvasGo.transform.SetParent(boardGo.transform, false);
                // 画布保持激活，只靠 renderer.enabled 控制房间内的可见性。
                // 用 SetActive(false) 关它的话，运行时会留下 activeInHierarchy=false
                // 的脏状态，之后再怎么设 enabled 也渲染不出来。
            }
            var canvasSr = canvasGo.GetComponent<SpriteRenderer>();
            if (canvasSr == null) canvasSr = canvasGo.AddComponent<SpriteRenderer>();
            canvasSr.sprite = RoomShapes.Rect;
            canvasSr.sortingOrder = RoomView.LayerPropsMax + 100;

            // 装配引用
            var so = new SerializedObject(interact);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("canvasRenderer").objectReferenceValue = canvasSr;
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

            var soCanvas = new SerializedObject(canvas);
            soCanvas.FindProperty("canvasWidth").intValue = 512;
            soCanvas.FindProperty("canvasHeight").intValue = 384;
            // 注意：达标线和笔刷半径用脚本默认值，不在这里覆盖——
            // 否则每次重跑装配都会把调好的手感参数冲掉。
            soCanvas.ApplyModifiedPropertiesWithoutUndo();

            // ---- 4. 交互提示组件 ----
            // 画板节点原本只是个空壳，玩家看不见也摸不着，
            // 必须给它一个可见的色块标记，靠近了还要高亮闪烁。
            var boardSr = boardGo.GetComponent<SpriteRenderer>();
            if (boardSr == null)
            {
                boardSr = boardGo.AddComponent<SpriteRenderer>();
                boardSr.sprite = RoomShapes.Rect;
                boardSr.color = RoomPalette.WoodDark;
                boardSr.sortingOrder = RoomView.Order(RoomView.RoomDepth * 0.74f, 4);
                boardGo.transform.localScale = new Vector3(1.6f, 1.1f, 1f);
            }

            // 交互提示：靠近时高亮 + 浮动标记
            var hint = boardGo.GetComponent<InteractionHint>();
            if (hint == null) hint = boardGo.AddComponent<InteractionHint>();
            hint.player = player != null ? player.transform : null;
            hint.triggerRange = 1.6f;

            var soHint = new SerializedObject(interact);
            soHint.FindProperty("hint").objectReferenceValue = hint;
            soHint.ApplyModifiedPropertiesWithoutUndo();

            // 4. 画笔（放在书架附近）
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
            soBrush.ApplyModifiedPropertiesWithoutUndo();

            // 视觉体收集交给 sprites 数组
            var visuals = soBrush.FindProperty("visualsToHide");
            visuals.arraySize = 1;
            visuals.GetArrayElementAtIndex(0).objectReferenceValue = brushSr;

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

            // ---- 5. 总控 ----
            var directorGo = FindOrCreate("GameDirector");
            var director = Ensure<GameDirector>(directorGo);
            var soDir = new SerializedObject(director);
            soDir.FindProperty("stats").objectReferenceValue = stats;
            soDir.FindProperty("timer").objectReferenceValue = timer;
            soDir.FindProperty("board").objectReferenceValue = interact;
            soDir.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(boardGo);
            EditorUtility.SetDirty(systems);
            EditorUtility.SetDirty(directorGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            // 立刻建好画布纹理并挂到精灵上，这样不进入播放模式也能看到白纸
            canvas.EnsureBuilt();
            canvas.Bind(canvasSr);

            // 画布必须处于激活状态。它历史上被 SetActive(false) 过的话，
            // activeInHierarchy 会一直是 false，运行时再怎么设 enabled 都不显示。
            canvasGo.SetActive(true);
            canvasSr.enabled = false; // 房间内不显示，只有进特写才开

            Debug.Log("[画板] 搭建完成：GameSystems / DrawingBoard / BrushPickup / GameDirector");
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