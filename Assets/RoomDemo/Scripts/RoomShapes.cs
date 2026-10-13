using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 程序化生成俯视斜角视角用的全部精灵。纯色块，零美术资源。
    ///
    /// 关键手法：地板和墙面用"逆投影"烘焙成一张贴图。
    /// 对每个像素反解出它属于房间里的哪个格子 / 哪条木板，
    /// 再按格子着色。这样透视网格完全由贴图承担，运行时只剩普通的 SpriteRenderer。
    /// </summary>
    public static class RoomShapes
    {
        public const float PPU = 64f;

        static Sprite _floor, _backWall, _wallLeft, _wallRight, _rect, _ellipse, _shadow;

        /// <summary>带透视的整块地板（含棋盘格与砖缝），锚点在屏幕底部中心。</summary>
        public static Sprite Floor
        {
            get { if (_floor == null) _floor = BakeFloor(); return _floor; }
        }

        /// <summary>后墙（竖条纹木板 + 踢脚线），锚点在底部中心。</summary>
        public static Sprite BackWall
        {
            get { if (_backWall == null) _backWall = BakeBackWall(); return _backWall; }
        }

        /// <summary>左墙（梯形），锚点在包围盒左下角。</summary>
        public static Sprite WallLeft
        {
            get { if (_wallLeft == null) _wallLeft = BakeSideWall(-1f); return _wallLeft; }
        }

        /// <summary>右墙（梯形），锚点在包围盒左下角。</summary>
        public static Sprite WallRight
        {
            get { if (_wallRight == null) _wallRight = BakeSideWall(1f); return _wallRight; }
        }

        /// <summary>竖直矩形（家具、角色躯干），锚点在底部中心。</summary>
        public static Sprite Rect
        {
            get { if (_rect == null) _rect = BakeRect(64, 64); return _rect; }
        }

        /// <summary>椭圆（树冠、圆形装饰），锚点在中心。</summary>
        public static Sprite Ellipse
        {
            get { if (_ellipse == null) _ellipse = BakeEllipse(64, 64); return _ellipse; }
        }

        /// <summary>半透明椭圆（角色脚下的影子），锚点在中心。</summary>
        public static Sprite Shadow
        {
            get { if (_shadow == null) _shadow = BakeShadow(64, 32); return _shadow; }
        }

        // ---------------------------------------------------------------

        /// <summary>房间最远处的透视缩放，用来对齐墙和地板。</summary>
        static float BackScale => RoomView.Scale(RoomView.RoomDepth);

        static float FloorTopY => RoomView.FloorY(RoomView.RoomDepth);
        static float WallTopY => FloorTopY + RoomView.WallHeight * BackScale;

        static Sprite BakeFloor()
        {
            int w = Mathf.CeilToInt(RoomView.RoomWidth * PPU);
            int h = Mathf.CeilToInt(FloorTopY * PPU);

            var tex = NewTexture(w, h, "RoomFloorTex");
            var px = new Color32[w * h];

            Color plankA = RoomPalette.FloorA;
            Color plankB = RoomPalette.FloorB;
            Color seam = RoomPalette.FloorSeam;

            for (int y = 0; y < h; y++)
            {
                // 屏幕纵坐标
                float screenY = RoomView.FrontY + (y + 0.5f) / PPU;

                // 逆投影：屏幕高度 → 该处的透视缩放 → 纵深
                float s = (RoomView.HorizonY - screenY) / (RoomView.HorizonY - RoomView.FrontY);
                if (s <= 0.0001f) { Fill(px, w, h, y, x0: -1, new Color32(0, 0, 0, 0)); continue; }
                float depth = (1f / s - 1f) / RoomView.Persp;

                if (depth < 0f || depth > RoomView.RoomDepth)
                {
                    for (int x = 0; x < w; x++) px[y * w + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float floorY = RoomView.FloorY(depth);
                for (int x = 0; x < w; x++)
                {
                    float screenX = -RoomView.HalfWidth + (x + 0.5f) / PPU;
                    float worldX = screenX / s;

                    if (Mathf.Abs(worldX) > RoomView.HalfWidth)
                    {
                        px[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // 落在哪个格子里
                    float gxF = worldX + 0.5f;
                    float gyF = depth + 0.5f;
                    int gx = Mathf.FloorToInt(gxF);
                    int gy = Mathf.FloorToInt(gyF);
                    float fx = gxF - gx;
                    float fy = gyF - gy;

                    Color c = ((gx + gy) & 1) == 0 ? plankA : plankB;

                    // 砖缝：格子边界附近压暗，让网格清晰可读
                    const float seamX = 0.055f;
                    const float seamY = 0.085f;
                    if (fx < seamX || fx > 1f - seamX || fy < seamY || fy > 1f - seamY)
                        c = seam;

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels32(px);
            return Finish(tex, w, h, new Vector2(w * 0.5f, 0f), "RoomFloor");
        }

        static Sprite BakeBackWall()
        {
            float s = BackScale;
            float wallW = RoomView.RoomWidth * s;
            float wallH = RoomView.WallHeight * s;

            int w = Mathf.CeilToInt(wallW * PPU);
            int h = Mathf.CeilToInt(wallH * PPU);

            var tex = NewTexture(w, h, "RoomBackWallTex");
            var px = new Color32[w * h];

            Color a = RoomPalette.WallA;
            Color b = RoomPalette.WallB;
            Color seam = RoomPalette.WallSeam;
            Color baseboard = RoomPalette.Baseboard;
            const float baseboardRatio = 0.12f;

            for (int y = 0; y < h; y++)
            {
                // 相对墙底的高度（0..1）
                float t = (y + 0.5f) / h;

                for (int x = 0; x < w; x++)
                {
                    // 反解到世界 X，用来确定竖条纹位置
                    float worldX = (-wallW * 0.5f + (x + 0.5f) / PPU) / s;
                    float plankCoord = worldX / 0.42f;
                    int plank = Mathf.FloorToInt(plankCoord);
                    float frac = plankCoord - plank;

                    Color c = (plank & 1) == 0 ? a : b;
                    if (frac < 0.07f || frac > 0.93f) c = seam;
                    if (t < baseboardRatio) c = baseboard;

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels32(px);
            return Finish(tex, w, h, new Vector2(w * 0.5f, 0f), "RoomBackWall");
        }

        /// <summary>侧墙：从像素反解出纵深和高度，填出梯形轮廓。</summary>
        static Sprite BakeSideWall(float side)
        {
            float sBack = BackScale;

            // 包围盒：前缘贴着房间外沿，后缘收窄
            float frontX = side * RoomView.HalfWidth;
            float backX = side * RoomView.HalfWidth * sBack;
            float minX = Mathf.Min(frontX, backX);
            float boxW = Mathf.Abs(frontX - backX);
            float boxH = WallTopY;

            int w = Mathf.CeilToInt(boxW * PPU);
            int h = Mathf.CeilToInt(boxH * PPU);

            var tex = NewTexture(w, h, "RoomSideWallTex");
            var px = new Color32[w * h];

            Color a = RoomPalette.WallA;
            Color b = RoomPalette.WallB;
            Color seam = RoomPalette.WallSeam;
            Color baseboard = RoomPalette.Baseboard;
            const float baseboardRatio = 0.12f;

            for (int y = 0; y < h; y++)
            {
                float screenY = (y + 0.5f) / PPU;

                for (int x = 0; x < w; x++)
                {
                    float screenX = minX + (x + 0.5f) / PPU;

                    // 由屏幕 X 反解缩放（墙平面内 X 与缩放成正比）
                    float s = screenX / frontX;
                    if (s <= 0.0001f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }

                    float depth = (1f / s - 1f) / RoomView.Persp;
                    if (depth < 0f || depth > RoomView.RoomDepth)
                    {
                        px[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float height = (screenY - RoomView.FloorY(depth)) / s;
                    if (height < 0f || height > RoomView.WallHeight)
                    {
                        px[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float t = height / RoomView.WallHeight;

                    // 横向条纹：沿纵深方向铺板
                    float boardCoord = depth / 0.5f;
                    int board = Mathf.FloorToInt(boardCoord);
                    float frac = boardCoord - board;

                    Color c = (board & 1) == 0 ? a : b;
                    if (frac < 0.06f || frac > 0.94f) c = seam;
                    if (t < baseboardRatio) c = baseboard;

                    px[y * w + x] = c;
                }
            }

            tex.SetPixels32(px);
            // 锚点放在包围盒左下角，配合 RoomView.LeftWallPivot / RightWallPivot
            return Finish(tex, w, h, Vector2.zero, "RoomSideWall");
        }

        static Sprite BakeRect(int w, int h)
        {
            var tex = NewTexture(w, h, "RoomRectTex");
            var px = new Color32[w * h];
            var c = new Color32(255, 255, 255, 255);
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels32(px);
            // 底部中心：家具直接站在地面上
            return Finish(tex, w, h, new Vector2(w * 0.5f, 0f), "RoomRect");
        }

        static Sprite BakeEllipse(int w, int h)
        {
            var tex = NewTexture(w, h, "RoomEllipseTex");
            var px = new Color32[w * h];
            FillEllipse(px, w, h, w * 0.5f, h * 0.5f, w * 0.5f, h * 0.5f, new Color32(255, 255, 255, 255));
            tex.SetPixels32(px);
            return Finish(tex, w, h, new Vector2(w * 0.5f, h * 0.5f), "RoomEllipse");
        }

        static Sprite BakeShadow(int w, int h)
        {
            var tex = NewTexture(w, h, "RoomShadowTex");
            var px = new Color32[w * h];
            FillEllipse(px, w, h, w * 0.5f, h * 0.5f, w * 0.45f, h * 0.42f, new Color32(0, 0, 0, 255));
            tex.SetPixels32(px);
            return Finish(tex, w, h, new Vector2(w * 0.5f, h * 0.5f), "RoomShadow");
        }

        static void FillEllipse(Color32[] px, int w, int h, float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    px[y * w + x] = (dx * dx + dy * dy <= 1f) ? c : new Color32(0, 0, 0, 0);
                }
            }
        }

        static void Fill(Color32[] px, int w, int h, int y, int x0, Color32 c)
        {
            for (int x = 0; x < w; x++) px[y * w + x] = c;
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            // 注意：不能用 HideFlags.DontSave，否则无法序列化进场景文件
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        static Sprite Finish(Texture2D tex, int w, int h, Vector2 pivotPixels, string name)
        {
            tex.Apply(false, false);
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                new Vector2(pivotPixels.x / w, pivotPixels.y / h),
                PPU, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}