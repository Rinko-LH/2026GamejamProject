using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
    ///   TestSnack / TestWater          ← Demo 验收用的吃喝道具（占位纯色图）
    /// GameManager/
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
                boardGo.transform.localScale = new Vector3(1.6f, 1.1f, 1f);
            }

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
                // 注意：达标判定相关参数（paintingSeconds / brushRadius）用脚本默认值，
                // 不在这里覆盖——否则每次重跑装配都会把调好的手感参数冲掉。
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
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- 7. 画笔 ----
            var brushGo = GameObject.Find("BrushPickup");
            if (brushGo == null)
            {
                brushGo = new GameObject("BrushPickup");
                brushGo.transform.position = new Vector3(-3.4f, 1.2f, 0f);
                brushGo.transform.SetParent(systems.transform, false);
            }
            var brush = Ensure<BrushPickup>(brushGo);
            var brushSr = brushGo.GetComponent<SpriteRenderer>();
            if (brushSr == null)
            {
                brushSr = brushGo.AddComponent<SpriteRenderer>();
                brushSr.sortingOrder = OrderBrush;
            }
            brushGo.transform.localScale = new Vector3(0.12f, 0.45f, 1f);

            var soBrush = new SerializedObject(brush);
            soBrush.FindProperty("player").objectReferenceValue = player;
            soBrush.FindProperty("board").objectReferenceValue = interact;

            // 视觉体收集交给 sprites 数组。
            // 注意：必须和上面的引用放在同一个 SerializedObject 里一次性 apply——
            // 旧版先 apply 再设这个数组且没有二次 apply，结果数组永远是空的，
            // 拿到画笔后那个色块不会消失。
            var visuals = soBrush.FindProperty("visualsToHide");
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

            // ---- 7.5 测试用吃喝道具 ----
            // Demo 验收专用：手绘场景没有占位图，这里用编辑器生成的纯色 PNG
            // 当可见道具。靠近按 E 拾取 → 状态值回补 + 倒计时按倍率扣除
            // （20 游戏分钟 = 10 真实秒，正好能在 HUD 上看到剩余秒数跳减）。
            // 正式支线玩法做好后，本节和 Placeholders 目录可整个删掉。
            EnsureTestConsumable(systems.transform, player, stats, timer,
                "TestSnack", new Vector3(3.2f, 1.2f, 0f),
                ConsumablePickup.Effect.Satiety, 20f, 20f,
                new Color(0.95f, 0.62f, 0.28f), "test_snack");
            EnsureTestConsumable(systems.transform, player, stats, timer,
                "TestWater", new Vector3(-1.6f, 1.2f, 0f),
                ConsumablePickup.Effect.Water, 20f, 20f,
                new Color(0.38f, 0.68f, 0.95f), "test_water");

            // ---- 8. 总控 ----
            var managerGo = FindOrCreate("GameManager");
            var manager = Ensure<GameManager>(managerGo);
            var soMgr = new SerializedObject(manager);
            soMgr.FindProperty("stats").objectReferenceValue = stats;
            soMgr.FindProperty("timer").objectReferenceValue = timer;
            soMgr.FindProperty("board").objectReferenceValue = interact;
            soMgr.ApplyModifiedPropertiesWithoutUndo();

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

        /// <summary>摆放一个测试用吃喝道具（可重复执行：已存在只补齐组件和引用）。</summary>
        static void EnsureTestConsumable(Transform parent, Transform player, PlayerStats stats, DayTimer timer,
            string objName, Vector3 worldPos, ConsumablePickup.Effect effect, float amount, float costMinutes,
            Color tint, string spriteName)
        {
            var go = GameObject.Find(objName);
            if (go == null)
            {
                go = new GameObject(objName);
                go.transform.position = worldPos;
                go.transform.SetParent(parent, true); // 保留世界坐标
            }

            var cp = Ensure<ConsumablePickup>(go);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = MakePlaceholderSprite(spriteName, tint);
                sr.sortingOrder = OrderBrush;
            }
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var so = new SerializedObject(cp);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("stats").objectReferenceValue = stats;
            so.FindProperty("timer").objectReferenceValue = timer;
            so.FindProperty("effect").enumValueIndex = (int)effect;
            so.FindProperty("amount").floatValue = amount;
            so.FindProperty("costTime").boolValue = true;
            so.FindProperty("costMinutes").floatValue = costMinutes;
            so.FindProperty("interactRange").floatValue = 1.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
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
