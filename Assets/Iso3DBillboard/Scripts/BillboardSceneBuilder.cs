using UnityEngine;

namespace Iso3DBillboard
{
    /// <summary>
    /// 2.5D 验证场景：**3D 世界 + 2D 立绘**。
    ///
    /// 这是给"美术只会画 2D"这个前提设计的方案：
    ///   · 地面、墙 → 真 3D（Cube + Unlit 材质），提供空间和遮挡
    ///   · 家具、角色 → 2D 立绘（Quad + SpriteRenderer + YBillboard）
    ///   · 碰撞 → BoxCollider，与贴图形状完全解耦
    ///
    /// 美术的工作流零变化：还是画正面 PNG，切图，导入 Unity。
    /// Unity 侧只有 Quad + SpriteRenderer，美术完全感知不到 3D。
    /// </summary>
    public static class BillboardSceneBuilder
    {
        public const float RoomSize = 12f;
        public const float WallHeight = 3f;
        /// <summary>相机俯角。越小越接近"平面化"的 2D 观感（参考图约 20°）。</summary>
        public const float CamPitch = 20f;

        public static GameObject Build()
        {
            var existing = GameObject.Find("BB_Root");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("BB_Root");

            // 材质只有地面和墙需要（立绘用 SpriteRenderer，不需要材质）
            var matFloor = MakeMaterial("BB_M_Floor", new Color(0.62f, 0.53f, 0.40f));
            var matFloorAlt = MakeMaterial("BB_M_FloorAlt", new Color(0.56f, 0.47f, 0.35f));
            var matWall = MakeMaterial("BB_M_Wall", new Color(0.56f, 0.64f, 0.48f));

            // ---- 地面（3D）----
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
                        -RoomSize * 0.5f + cell * (x + 0.5f), -0.05f,
                        -RoomSize * 0.5f + cell * (z + 0.5f));
                    t.transform.localScale = new Vector3(cell, 0.1f, cell);
                    Object.DestroyImmediate(t.GetComponent<Collider>());
                    t.GetComponent<MeshRenderer>().sharedMaterial = ((x + z) & 1) == 0 ? matFloor : matFloorAlt;
                }
            }

            // ---- 墙（3D）----
            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform, false);
            float h = RoomSize * 0.5f;

            // 后墙：只做矮墙，纯粹为了勾出房间轮廓。
            // 立绘本身就有完整轮廓（美术画的），墙太高反而会挡住家具。
            float backH = 1.0f;
            MakeWall(walls.transform, "WallBack", new Vector3(0f, backH * 0.5f, h),
                new Vector3(RoomSize, backH, 0.25f), matWall);

            // 侧墙压得很矮，纯粹是地面色块的高度感提示
            float sideH = 0.35f;
            MakeWall(walls.transform, "WallLeft", new Vector3(-h, sideH * 0.5f, 0f),
                new Vector3(0.25f, sideH, RoomSize), matWall);
            MakeWall(walls.transform, "WallRight", new Vector3(h, sideH * 0.5f, 0f),
                new Vector3(0.25f, sideH, RoomSize), matWall);

// ---- 家具（2D 立绘 + 3D 碰撞）----
            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);

            // 立绘的贴图长宽比不同，碰撞盒按"看起来该多大"给
            MakeProp(props.transform, "Bed", BillboardArt.Bed,
                new Vector3(3.2f, 0f, 3.0f), new Vector3(3.0f, 1.5f, 2.0f));

            MakeProp(props.transform, "Wardrobe", BillboardArt.Wardrobe,
                new Vector3(-4.0f, 0f, 3.4f), new Vector3(1.5f, 2.25f, 1.0f));

            MakeProp(props.transform, "Desk", BillboardArt.Desk,
                new Vector3(0.4f, 0f, 3.6f), new Vector3(2.0f, 1.25f, 1.0f));

            MakeProp(props.transform, "Chair", BillboardArt.Chair,
                new Vector3(0.4f, 0f, 1.9f), new Vector3(1.0f, 1.4f, 0.8f));

            MakeProp(props.transform, "Plant", BillboardArt.Plant,
                new Vector3(-3.9f, 0f, -2.8f), new Vector3(1.25f, 1.75f, 1.0f));

            // ---- 角色（2D 立绘 + 3D 碰撞 + 控制器）----
            BuildPlayer(root.transform);

            // ---- 相机 ----
            BuildCamera(root.transform);

            Debug.Log("[Billboard] 2.5D 场景已生成：3D 世界 + 2D 立绘");
            return root;
        }

        static void MakeWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = name;
            t.transform.SetParent(parent, false);
            t.transform.position = pos;
            t.transform.localScale = scale;
            Object.DestroyImmediate(t.GetComponent<Collider>());
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// 放一件带立绘的家具。
        /// 视觉用 Quad+SpriteRenderer，碰撞用独立的 BoxCollider —— 两者解耦，
        /// 所以美术画得再离谱也不影响手感。
        /// </summary>
        static void MakeProp(Transform parent, string name, Sprite art,
            Vector3 pos, Vector3 colliderSize)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            // 立绘面片。
            // 注意：只能用 SpriteRenderer，不能同时挂 MeshFilter/MeshRenderer ——
            // SpriteRenderer 派生自 Renderer，与 MeshFilter 冲突，Unity 会直接拒绝添加。
            // 立绘本来就是"永远朝向相机"的，MeshRenderer 的存在反而没用。
            var quad = new GameObject("Art");
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localPosition = Vector3.zero;

            var sr = quad.AddComponent<SpriteRenderer>();
            sr.sprite = art;

            // 立绘高度按"贴图像素 / PPU"自然得到，这里按碰撞盒高度对齐
            float artHeight = art.rect.height / BillboardArt.PPU;
            float scaleY = colliderSize.y / artHeight;
            quad.transform.localScale = new Vector3(scaleY, scaleY, scaleY);

            // 碰撞体：与立绘形状解耦，直接给一个盒子
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(colliderSize.x / scaleY, colliderSize.y / scaleY, colliderSize.z / scaleY);
            box.center = new Vector3(0f, artHeight * 0.5f, 0f);

            // Billboard 朝向
            var bb = root.AddComponent<YBillboard>();
            bb.SnapToCamera();
        }

        static GameObject BuildPlayer(Transform parent)
        {
            var root = new GameObject("Player");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 0f, -0.6f);

            var art = BillboardArt.PlayerFront;
            float artH = art.rect.height / BillboardArt.PPU;   // 112/64 = 1.75

            var quad = new GameObject("Art");
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            // 同 MakeProp：SpriteRenderer 与 MeshFilter 冲突，这里只用 SpriteRenderer
            var sr = quad.AddComponent<SpriteRenderer>();
            sr.sprite = art;

            // 脸（朝向指示）
            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            face.transform.localPosition = new Vector3(0f, artH * 0.80f, -0.02f);
            var fsr = face.AddComponent<SpriteRenderer>();
            fsr.sprite = BillboardArt.PlayerFace;
            face.transform.localScale = Vector3.one * (0.18f * 64f / 16f);

            // 碰撞：用 CharacterController，高度按角色实际身高
            var cc = root.AddComponent<CharacterController>();
            cc.height = artH * 0.98f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, artH * 0.49f, 0f);
            cc.slopeLimit = 0f;
            cc.stepOffset = 0f;

            var bb = root.AddComponent<YBillboard>();
            bb.SnapToCamera();

            root.AddComponent<BillboardPlayer>();
            return root;
        }

        static void BuildCamera(Transform parent)
        {
            var go = new GameObject("IsoCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);

            // 俯角调小（22°），更接近参考图的"平面化"观感。
            // 在 Cube 版里这个角度不可用，因为 2D 方案的透视是烘焙进贴图的死值。
            go.transform.rotation = Quaternion.Euler(CamPitch, 45f, 0f);
            go.transform.position = Vector3.zero - go.transform.forward * 30f;

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.19f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            go.AddComponent<AudioListener>();
        }

        // ---- 共享资源 ----
        // 说明：这里刻意不建 Quad Mesh。
        // SpriteRenderer 本身就能渲染，而且它是 billboard 友好的：
        // 配合 YBillboard 后立绘自动朝向相机，不需要自己管网格和材质。

        static Material MakeMaterial(string name, Color color)
        {
            var path = "Assets/Iso3DBillboard/Materials/" + name + ".mat";
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            // 注意：必须先确保文件夹存在，否则 CreateAsset 会失败返回 null，
            // 而后面对 null 材质赋值不会立刻报错，会把异常带到很远的地方。
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Iso3DBillboard/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Iso3DBillboard", "Materials");

            UnityEditor.AssetDatabase.CreateAsset(mat, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return mat;
        }
    }
}