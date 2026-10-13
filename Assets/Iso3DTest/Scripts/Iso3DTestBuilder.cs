using UnityEngine;

namespace Iso3DTest
{
    /// <summary>
    /// 3D 方案最小验证：用 Cube + Unlit 材质搭一个斜角房间。
    ///
    /// 这个脚本刻意写得非常直白，就是想验证三件事：
    ///   1. 3D Renderer 下立方体能否正常渲染（不再是空白）
    ///   2. 遮挡是否由深度缓冲自动完成（完全不需要 sortingOrder）
    ///   3. 碰撞是否由BoxCollider 自动完成（完全不需要手写范围夹取）
    ///
    /// 如果这三项都通过，就说明 3D 路线可行，现有 2D 方案里
    /// 那 27 处 sortingOrder 和全部 lossyScale 换算都可以删掉。
    /// </summary>
    public static class Iso3DTestBuilder
    {
        public const float RoomSize = 12f;
        public const float WallHeight = 3f;

        /// <summary>房间内所有家具的碰撞体都挂在这层，纯色块演示用不到物理分层。</summary>
        public static GameObject Build()
        {
            var existing = GameObject.Find("Iso3D_Root");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("Iso3D_Root");

            // ---- 材质：Unlit 保证纯色块，颜色不受灯光配置影响 ----
            var matFloor = MakeMaterial("M_Floor", new Color(0.62f, 0.53f, 0.40f));
            var matFloorAlt = MakeMaterial("M_FloorAlt", new Color(0.56f, 0.47f, 0.35f));
            var matWall = MakeMaterial("M_Wall", new Color(0.56f, 0.64f, 0.48f));
            var matWood = MakeMaterial("M_Wood", new Color(0.66f, 0.48f, 0.33f));
            var matCloth = MakeMaterial("M_Cloth", new Color(0.62f, 0.76f, 0.79f));
            var matPillow = MakeMaterial("M_Pillow", new Color(0.95f, 0.93f, 0.86f));
            var matGreen = MakeMaterial("M_Green", new Color(0.31f, 0.62f, 0.35f));
            var matPot = MakeMaterial("M_Pot", new Color(0.77f, 0.47f, 0.36f));
            var matBody = MakeMaterial("M_Body", new Color(0.36f, 0.50f, 0.71f));
            var matSkin = MakeMaterial("M_Skin", new Color(0.94f, 0.79f, 0.64f));
            var matHair = MakeMaterial("M_Hair", new Color(0.29f, 0.23f, 0.20f));
            var matPants = MakeMaterial("M_Pants", new Color(0.29f, 0.31f, 0.38f));

            // ---- 地面：棋盘格，但只做静态摆放，不吃透视 ----
            // 注意：3D 里地面是平的 Plane，格子是等比平铺，
            // 不再有 2D 方案里那种"棋盘格随纵深收缩"的效果。
            var floor = new GameObject("Floor");
            floor.transform.SetParent(root.transform, false);
            int n = 6;
            float cell = RoomSize / n;
            for (int x = 0; x < n; x++)
            {
                for (int z = 0; z < n; z++)
                {
                    var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    t.name = $"Floor_{x}_{z}";
                    t.transform.SetParent(floor.transform, false);
                    t.transform.position = new Vector3(
                        -RoomSize * 0.5f + cell * (x + 0.5f),
                        -0.05f,
                        -RoomSize * 0.5f + cell * (z + 0.5f));
                    t.transform.localScale = new Vector3(cell, 0.1f, cell);
                    // 地面不需要碰撞体，删掉省性能
                    Object.DestroyImmediate(t.GetComponent<Collider>());
                    t.GetComponent<MeshRenderer>().sharedMaterial = ((x + z) & 1) == 0 ? matFloor : matFloorAlt;
                }
            }

            // ---- 三面墙 ----
            // 注意：3D 里墙是真实立方体，会实打实挡住视线。
            // 斜角视角下后墙要够高才有"房间感"，但又不能挡住家具。
            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform, false);
            float h = RoomSize * 0.5f;

            // 后墙最高
            MakeWall(walls.transform, "WallBack", new Vector3(0f, WallHeight * 0.5f, h),
                new Vector3(RoomSize, WallHeight, 0.3f), matWall);

            // 两侧墙压低到 1.2，避免遮挡室内
            float sideH = 1.2f;
            MakeWall(walls.transform, "WallLeft", new Vector3(-h, sideH * 0.5f, 0f),
                new Vector3(0.3f, sideH, RoomSize), matWall);
            MakeWall(walls.transform, "WallRight", new Vector3(h, sideH * 0.5f, 0f),
                new Vector3(0.3f, sideH, RoomSize), matWall);

            // ---- 家具：每个都带 BoxCollider，用来验证碰撞 ----
            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);

            // 床（靠右后）
            MakeBox(props.transform, "Bed", new Vector3(3.4f, 0.3f, 3.2f), new Vector3(2.4f, 0.6f, 3.4f), matWood);
            MakeBox(props.transform, "BedBedding", new Vector3(3.4f, 0.68f, 3.2f), new Vector3(2.3f, 0.2f, 3.2f), matCloth);
            MakeBox(props.transform, "Pillow", new Vector3(3.4f, 0.82f, 1.9f), new Vector3(1.2f, 0.2f, 0.6f), matPillow);

            // 衣柜（靠左后）
            MakeBox(props.transform, "Wardrobe", new Vector3(-4.2f, 1.1f, 3.6f), new Vector3(1.8f, 2.2f, 1.0f), matWood);

            // 书桌 + 椅子（中间偏后）
            MakeBox(props.transform, "Desk", new Vector3(0.4f, 0.5f, 3.4f), new Vector3(2.4f, 1.0f, 1.0f), matWood);
            MakeBox(props.transform, "Chair", new Vector3(0.4f, 0.4f, 1.9f), new Vector3(0.8f, 0.8f, 0.8f), matWood);

            // 盆栽（左前）
            MakeBox(props.transform, "Pot", new Vector3(-4.0f, 0.35f, -2.6f), new Vector3(0.8f, 0.7f, 0.8f), matPot);
            MakeBox(props.transform, "Leaves", new Vector3(-4.0f, 1.15f, -2.6f), new Vector3(1.3f, 1.0f, 1.3f), matGreen);

            // ---- 角色：方块人 + 碰撞体 + 控制器 ----
            var player = BuildPlayer(root.transform, matBody, matSkin, matHair, matPants);

            // ---- 相机：正交斜角 ----
            BuildCamera(root.transform);

            Debug.Log("[Iso3DTest] 3D 验证场景已生成");
            return root;
        }

        static void MakeWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = name;
            t.transform.SetParent(parent, false);
            t.transform.position = pos;
            t.transform.localScale = scale;
            // 墙不参与碰撞：只要墙挡住视觉，角色的范围由 ClampToRoom 控制
            Object.DestroyImmediate(t.GetComponent<Collider>());
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = name;
            t.transform.SetParent(parent, false);
            t.transform.position = pos;
            t.transform.localScale = scale;
            // 保留 CreatePrimitive 自带的 BoxCollider —— 这正是要验证的东西
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static GameObject BuildPlayer(Transform parent, Material body, Material skin, Material hair, Material pants)
        {
            var root = new GameObject("Player");
            root.transform.SetParent(parent, false);
            // 起点放在房间中间偏前，保证一眼能看见
            root.transform.position = new Vector3(0f, 0f, -0.5f);

            // CharacterController 是 3D 里做"角色+碰撞"的现成方案：
            // 自动与场景碰撞体阻挡，不需要手写位置夹取。
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 0f;      // 地面全平，不需要爬坡
            cc.stepOffset = 0f;

            MakePart(root.transform, "Legs", new Vector3(0f, 0.35f, 0f), new Vector3(0.55f, 0.7f, 0.45f), pants);
            MakePart(root.transform, "Torso", new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 0.8f, 0.45f), body);
            MakePart(root.transform, "Head", new Vector3(0f, 1.65f, 0f), new Vector3(0.55f, 0.55f, 0.55f), skin);
            MakePart(root.transform, "Hair", new Vector3(0f, 1.85f, 0f), new Vector3(0.58f, 0.22f, 0.58f), hair);

            // 朝向指示：一个小方块，放在角色前方
            var face = MakePart(root.transform, "Face", new Vector3(0f, 1.62f, 0.3f), new Vector3(0.22f, 0.2f, 0.1f), hair);

            root.AddComponent<Iso3DPlayer>();
            return root;
        }

        static GameObject MakePart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = name;
            t.transform.SetParent(parent, false);
            t.transform.position = pos;
            t.transform.localScale = scale;
            // 角色部件不需要碰撞体，否则会互相卡住
            Object.DestroyImmediate(t.GetComponent<Collider>());
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        static void BuildCamera(Transform parent)
        {
            var go = new GameObject("IsoCamera");
            go.transform.SetParent(parent, false);

            // 标准等角斜视：X 轴 30°俯角 + Y 轴 45° 旋转。
            // 这一步在 2D 方案里做不到（Persp 是烘焙进贴图的死值），
            // 3D 里相机角度可以随时改，是最大的自由度提升。
            go.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

            // 沿视线反推位置，保证正好看向房间中心。
            // 正交相机下这个距离只影响裁剪，不影响画面大小。
            Vector3 focus = Vector3.zero;
            go.transform.position = focus - go.transform.forward * 30f;

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.19f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
        }

        static Material MakeMaterial(string name, Color color)
        {
            var path = "Assets/Iso3DTest/Materials/" + name + ".mat";
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            // 注意：必须先确保文件夹存在，否则 CreateAsset 失败返回 null，
            // 而后面对 null 材质赋值不报错，会把异常带到很远的地方。
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Iso3DTest/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Iso3DTest", "Materials");

            UnityEditor.AssetDatabase.CreateAsset(mat, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return mat;
        }
    }
}