using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 一键搭建整个房间：地板、三面墙、家具、主角、相机。
    /// 全部程序化生成，纯色块，零美术资源。
    /// </summary>
    public static class RoomSceneBuilder
    {
        // ---------- 基础工具 ----------

        /// <summary>
        /// 放置房间外壳（地板/墙）。这类元素已经由 RoomShapes 把透视烘焙进贴图，
        /// 所以这里既不吃缩放也不吃透视位移，直接按屏幕坐标摆放即可。
        /// </summary>
        static SpriteRenderer AddShell(Transform parent, string name, Vector3 pos,
            Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// 在指定横坐标处放一件家具。
        ///
        /// 关键：Project(x, depth) 返回的 x 已经含透视缩放，
        /// 而 widthUnits/heightUnits 是"未缩放的真实占位尺寸"，
        /// 所以这里要自己乘该纵深的 Scale —— 两者相乘才是屏幕上应有的宽度。
        /// </summary>
        static SpriteRenderer AddPropAt(Transform parent, string name, float x, float depth,
            Sprite sprite, Color color, float widthUnits, float heightUnits, int extraOrder = 0)
        {
            float s = RoomView.Scale(depth);
            // x 保持房间坐标原值，只有纵向吃透视（见 RoomView.Anchor 的说明）
            var pos = RoomView.Anchor(x, depth);
            int order = RoomView.Order(depth, extraOrder);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(widthUnits * s, heightUnits * s, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- 主角 ----------

        public static RoomPlayer BuildPlayer(Transform parent, float depth)
        {
            var root = new GameObject("Player");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, RoomView.FloorY(depth), 0f);

            // 角色也要按所在纵深缩小，才能和家具处在同一透视体系里。
            // 额外乘 0.9，让身高（约 2.2 单位）在 11 单位宽的房间里比例更自然。
            float s = RoomView.Scale(depth) * 0.9f;
            root.transform.localScale = new Vector3(s, s, 1f);

            int order = RoomView.Order(depth, 0);

            // 脚下的椭圆影子。Ellipse 锚点在中心，所以 y 要压到脚底之下，
            // 且排层低于腿，才能透出腿的两侧。
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            shadow.transform.localScale = new Vector3(1.3f, 0.34f, 1f);
            var shadowSr = shadow.AddComponent<SpriteRenderer>();
            shadowSr.sprite = RoomShapes.Shadow;
            shadowSr.color = new Color(0f, 0f, 0f, 0.32f);
            shadowSr.sortingOrder = order;

            // 身体容器承载走路起伏
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);

            // 关键：Rect 精灵锚点在【底部中心】，所以每个部件的 localPosition.y
            // 就是它的【下沿】高度。腿必须从 0 起步（脚踩地面），逐段往上叠，
            // 否则整个角色会悬空。
            //
            // 比例：腿 0.85 → 躯干 0.80~1.75 → 头 1.75~2.45 → 头发 2.30~2.55

            // 腿（下沿 = 0，脚踩地面）
            var legs = new GameObject("Legs");
            legs.transform.SetParent(body.transform, false);
            legs.transform.localPosition = new Vector3(0f, 0f, 0f);
            legs.transform.localScale = new Vector3(0.5f, 0.85f, 1f);
            var legsSr = legs.AddComponent<SpriteRenderer>();
            legsSr.sprite = RoomShapes.Rect;
            legsSr.color = RoomPalette.Pants;
            legsSr.sortingOrder = order + 1;

            // 躯干
            var torso = new GameObject("Torso");
            torso.transform.SetParent(body.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.80f, 0f);
            torso.transform.localScale = new Vector3(0.78f, 0.95f, 1f);
            var torsoSr = torso.AddComponent<SpriteRenderer>();
            torsoSr.sprite = RoomShapes.Rect;
            torsoSr.color = RoomPalette.Shirt;
            torsoSr.sortingOrder = order + 2;

            // 头（Ellipse 锚点在中心，所以 y 是中心高度）
            var head = new GameObject("Head");
            head.transform.SetParent(body.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.10f, 0f);
            head.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
            var headSr = head.AddComponent<SpriteRenderer>();
            headSr.sprite = RoomShapes.Ellipse;
            headSr.color = RoomPalette.Skin;
            headSr.sortingOrder = order + 3;

            // 头发（盖住头顶）
            var hair = new GameObject("Hair");
            hair.transform.SetParent(body.transform, false);
            hair.transform.localPosition = new Vector3(0f, 2.36f, 0f);
            hair.transform.localScale = new Vector3(0.74f, 0.34f, 1f);
            var hairSr = hair.AddComponent<SpriteRenderer>();
            hairSr.sprite = RoomShapes.Rect;
            hairSr.color = RoomPalette.Hair;
            hairSr.sortingOrder = order + 4;

            // 脸（朝向指示，背对时隐藏）
            var face = new GameObject("Face");
            face.transform.SetParent(body.transform, false);
            face.transform.localPosition = new Vector3(0f, 2.04f, 0f);
            face.transform.localScale = new Vector3(0.28f, 0.24f, 1f);
            var faceSr = face.AddComponent<SpriteRenderer>();
            faceSr.sprite = RoomShapes.Rect;
            faceSr.color = RoomPalette.FaceMark;
            faceSr.sortingOrder = order + 5;

            return root.AddComponent<RoomPlayer>();
        }

        // ---------- 相机 ----------

        static Camera BuildCamera(Transform parent)
        {
            var go = new GameObject("RoomCamera");
            go.transform.SetParent(parent, false);

            // 地面在 XY 平面，相机正对、不旋转。缩小的比例已经烘焙进贴图里了。
            float top = RoomView.FloorY(RoomView.RoomDepth)
                      + RoomView.WallHeight * RoomView.Scale(RoomView.RoomDepth);
            go.transform.localPosition = new Vector3(0f, top * 0.5f, -20f);
            go.transform.localRotation = Quaternion.identity;

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            // 房间在屏幕上高约 top、宽 11；按 16:9 取能容下高度的最小值
            float halfH = top * 0.5f;
            float halfW = RoomView.HalfWidth;
            cam.orthographicSize = Mathf.Max(halfH, halfW / (16f / 9f)) + 0.35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RoomPalette.Background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
            return cam;
        }

        // ---------- 家具 ----------
        // 注意：房间宽 11 个单位，而 Rect 精灵本身就是 1x1 世界单位，
        // 所以家具的 widthUnits/heightUnits 就是它在房间里的真实占位尺寸。
        // 挂画/壁灯这类贴墙的小物件再乘 0.8 左右即可。

        static void AddBed(Transform parent, float x, float depth)
        {
            float s = RoomView.Scale(depth);
            float w = 2.9f * s, h = 1.15f * s;

            AddPropAt(parent, "BedFrame", x, depth, RoomShapes.Rect,
                RoomPalette.WoodDark, w, h, 1);
            AddPropAt(parent, "BedBedding", x, depth, RoomShapes.Rect,
                RoomPalette.Bedding, w * 0.94f, h * 0.60f, 2);
            AddPropAt(parent, "Pillow", x, depth, RoomShapes.Rect,
                RoomPalette.Pillow, w * 0.44f, h * 0.26f, 3);
        }

        static void AddWardrobe(Transform parent, float x, float depth)
        {
            float s = RoomView.Scale(depth);
            AddPropAt(parent, "WardrobeBody", x, depth, RoomShapes.Rect,
                RoomPalette.WoodMid, 1.9f * s, 2.9f * s, 1);
            AddPropAt(parent, "WardrobeSeam", x, depth, RoomShapes.Rect,
                RoomPalette.WoodDark, 0.08f * s, 2.7f * s, 2);
        }

        static void AddDesk(Transform parent, float x, float depth)
        {
            float s = RoomView.Scale(depth);
            AddPropAt(parent, "DeskTop", x, depth, RoomShapes.Rect,
                RoomPalette.WoodLight, 2.3f * s, 0.85f * s, 3);
            AddPropAt(parent, "DeskLegL", x - 0.9f, depth, RoomShapes.Rect,
                RoomPalette.WoodDark, 0.16f * s, 0.5f * s, 1);
            AddPropAt(parent, "DeskLegR", x + 0.9f, depth, RoomShapes.Rect,
                RoomPalette.WoodDark, 0.16f * s, 0.5f * s, 1);
        }

        static void AddBookshelf(Transform parent, float x, float depth)
        {
            float s = RoomView.Scale(depth);
            AddPropAt(parent, "ShelfBody", x, depth, RoomShapes.Rect,
                RoomPalette.WoodMid, 1.7f * s, 2.6f * s, 1);

            // 几本彩色的书
            float[] heights = { 0.46f, 0.58f, 0.40f, 0.54f };
            Color[] colors = {
                RoomPalette.Book, RoomPalette.BeddingAlt,
                RoomPalette.WoodLight, RoomPalette.LeafDark
            };
            for (int i = 0; i < heights.Length; i++)
            {
                AddPropAt(parent, $"Book{i}", x + (-0.52f + i * 0.35f), depth, RoomShapes.Rect,
                    colors[i], 0.24f * s, heights[i] * s, 2);
            }
        }

        static void AddPlant(Transform parent, float x, float depth)
        {
            float s = RoomView.Scale(depth);
            // 花盆
            AddPropAt(parent, "Pot", x, depth, RoomShapes.Rect,
                RoomPalette.Pottery, 0.8f * s, 0.7f * s, 1);
            // 叶子
            AddPropAt(parent, "Leaves", x, depth, RoomShapes.Ellipse,
                RoomPalette.Leaf, 2.0f * s, 1.7f * s, 2);
        }

        static void AddLamp(Transform parent, float x, float depth)
        {
            float s = RoomView.Scale(depth);
            AddPropAt(parent, "LampPost", x, depth, RoomShapes.Rect,
                RoomPalette.Metal, 0.18f * s, 2.2f * s, 1);
            AddPropAt(parent, "LampShade", x, depth, RoomShapes.Ellipse,
                RoomPalette.Pillow, 1.1f * s, 0.9f * s, 2);
        }

        static void AddRug(Transform parent)
        {
            float depth = RoomView.RoomDepth * 0.44f;
            float y = RoomView.FloorY(depth) + 0.01f;

            // 外框
            var outer = new GameObject("Rug");
            outer.transform.SetParent(parent, false);
            outer.transform.localPosition = new Vector3(0f, y, 0f);
            outer.transform.localScale = new Vector3(3.6f, 1.05f, 1f);
            var outerSr = outer.AddComponent<SpriteRenderer>();
            outerSr.sprite = RoomShapes.Rect;
            outerSr.color = RoomPalette.RugBorder;
            outerSr.sortingOrder = RoomView.LayerRug;

            // 内芯（略小、略高一层，形成镶边效果）
            var inner = new GameObject("RugInner");
            inner.transform.SetParent(parent, false);
            inner.transform.localPosition = new Vector3(0f, y + 0.01f, 0f);
            inner.transform.localScale = new Vector3(3.1f, 0.80f, 1f);
            var innerSr = inner.AddComponent<SpriteRenderer>();
            innerSr.sprite = RoomShapes.Rect;
            innerSr.color = RoomPalette.Rug;
            innerSr.sortingOrder = RoomView.LayerRug + 1;
        }

        // ---------- 总装 ----------

        public static GameObject BuildScene()
        {
            var existing = GameObject.Find("RoomWorld");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("RoomWorld");
            root.AddComponent<RoomSortingFix>();

            // ---- 房间外壳 ----
            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);

            // 地板：锚点在屏幕底部中心，放在地板最前缘
            AddShell(shell.transform, "Floor", new Vector3(0f, RoomView.FrontY, 0f),
                RoomShapes.Floor, RoomView.LayerFloor);

            // 后墙：贴在房间最深处的地面上
            AddShell(shell.transform, "BackWall",
                new Vector3(0f, RoomView.FloorY(RoomView.RoomDepth), 0f),
                RoomShapes.BackWall, RoomView.LayerWall);

            // 侧墙：锚点在自身包围盒左下角
            AddShell(shell.transform, "WallLeft", RoomView.LeftWallPivot,
                RoomShapes.WallLeft, RoomView.LayerWall);
            AddShell(shell.transform, "WallRight", RoomView.RightWallPivot,
                RoomShapes.WallRight, RoomView.LayerWall);

            // ---- 家具 ----
            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);

            float D = RoomView.RoomDepth;
            AddRug(props.transform);

            // 靠后墙一带，横向错开
            AddWardrobe(props.transform, -4.2f, D * 0.74f);
            AddBookshelf(props.transform, -1.6f, D * 0.80f);
            AddDesk(props.transform, 1.8f, D * 0.74f);
            AddBed(props.transform, 4.3f, D * 0.84f);

            // 前场：盆栽 / 立灯
            AddPlant(props.transform, -4.4f, D * 0.36f);
            AddLamp(props.transform, 4.5f, D * 0.40f);

            // ---- 主角 ----
            BuildPlayer(root.transform, RoomView.RoomDepth * 0.34f);

            // ---- 相机 ----
            BuildCamera(root.transform);

            Debug.Log("[RoomDemo] 房间生成完成。WASD / 方向键 四方向移动。");
            return root;
        }
    }
}