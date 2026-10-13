using UnityEngine;

namespace Iso3DBillboard
{
    /// <summary>
    /// 程序化生成"假立绘"贴图，模拟美术会交付的 2D 正视图素材。
    ///
    /// 目的：验证"3D 世界 + 2D 立绘"这条路线在不依赖任何美术资源时是否可行。
    /// 美术接入后，把这里的生成结果换成真实 sprite sheet 即可，
    /// 场景搭建与碰撞逻辑完全不用改。
    ///
    /// 关键：这些贴图是**正面视角**绘制的（美术习惯画法），
    /// 贴到 Quad 上由Y 轴 billboard 保持垂直，
    /// 这样才能验证"立绘不会在斜角视角下躺平"。
    /// </summary>
    public static class BillboardArt
    {
        public const float PPU = 64f;

        static Sprite _bed, _wardrobe, _desk, _chair, _plant;
        static Sprite _player, _playerFace;

        // ---- 家具立绘 ----

        /// <summary>床：正面视角，带被褥和枕头。</summary>
        public static Sprite Bed
        {
            get { if (_bed == null) _bed = MakeBed(); return _bed; }
        }

        /// <summary>衣柜：正面视角，带门缝和把手。</summary>
        public static Sprite Wardrobe
        {
            get { if (_wardrobe == null) _wardrobe = MakeWardrobe(); return _wardrobe; }
        }

        /// <summary>书桌：正面视角，桌面+桌腿。</summary>
        public static Sprite Desk
        {
            get { if (_desk == null) _desk = MakeDesk(); return _desk; }
        }

        /// <summary>椅子：正面视角。</summary>
        public static Sprite Chair
        {
            get { if (_chair == null) _chair = MakeChair(); return _chair; }
        }

        /// <summary>盆栽：花盆+树冠，正面视角。</summary>
        public static Sprite Plant
        {
            get { if (_plant == null) _plant = MakePlant(); return _plant; }
        }

        // ---- 角色立绘 ----

        /// <summary>角色：正面朝向（朝下/朝镜头）。</summary>
        public static Sprite PlayerFront
        {
            get { if (_player == null) _player = MakePlayer(); return _player; }
        }

        /// <summary>角色朝向指示块。</summary>
        public static Sprite PlayerFace
        {
            get { if (_playerFace == null) _playerFace = MakeFaceBlock(); return _playerFace; }
        }

        // ---- 颜色 ----

        static Color C(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        // =========================================================
        // 生成逻辑
        // =========================================================

        static Sprite MakeBed()
        {
            // 128x96：床宽大于高，符合正面视角
            int w = 128, h = 96;
            var px = new Color32[w * h];

            Color wood = C(0x9C6B3F);
            Color woodDark = C(0x7A5330);
            Color cloth = C(0x9FC3C9);
            Color clothDark = C(0x7FA9B0);
            Color pillow = C(0xF2ECDC);
            Color metal = C(0xB9BEC4);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new Color32(0, 0, 0, 0);

                    // 床架主体（下部1/3）
                    if (y >= 8 && y < 34 && x >= 6 && x < 122)
                        c = To(wood);
                    // 床脚
                    else if (y >= 4 && y < 10 && x >= 8 && x < 22) c = To(woodDark);
                    else if (y >= 4 && y < 10 && x >= 106 && x < 120) c = To(woodDark);

                    // 被褥（中部）
                    if (y >= 30 && y < 76 && x >= 4 && x < 124)
                        c = To((y < 40) ? clothDark : cloth);

                    // 枕头（上部）
                    if (y >= 78 && y < 92 && x >= 20 && x < 108)
                        c = To(pillow);

                    // 床头板
                    if (y >= 74 && y < 94 && x >= 2 && x < 126 && x < 14)
                        c = To(metal);
                    else if (y >= 74 && y < 94 && x > 113) c = To(metal);

                    px[y * w + x] = c;
                }
            }
            return Finish(px, w, h, "Art_Bed");
        }

        static Sprite MakeWardrobe()
        {
            int w = 96, h = 144;
            var px = new Color32[w * h];

            Color body = C(0xA97B52);
            Color dark = C(0x8A6242);
            Color seam = C(0x6F4E33);
            Color handle = C(0xD9CBAE);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new Color32(0, 0, 0, 0);
                    if (x >= 8 && x < 88 && y >= 6 && y < 138) c = To(body);

                    // 中间门缝
                    if (x >= 46 && x < 50 && y >= 10 && y < 134) c = To(seam);
                    // 上下分隔线
                    if (y >= 70 && y < 73 && x >= 10 && x < 86) c = To(dark);

                    // 把手
                    if (x >= 40 && x < 44 && y >= 58 && y < 66) c = To(handle);
                    if (x >= 52 && x < 56 && y >= 58 && y < 66) c = To(handle);

                    px[y * w + x] = c;
                }
            }
            return Finish(px, w, h, "Art_Wardrobe");
        }

        static Sprite MakeDesk()
        {
            int w = 128, h = 80;
            var px = new Color32[w * h];

            Color top = C(0xC79A6B);
            Color leg = C(0x8A6242);
            Color drawer = C(0xA97B52);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new Color32(0, 0, 0, 0);

                    // 桌面
                    if (y >= 56 && y < 74 && x >= 4 && x < 124) c = To(top);
                    // 抽屉
                    if (y >= 38 && y < 56 && x >= 12 && x < 116) c = To(drawer);
                    // 抽屉把手
                    if (y >= 45 && y < 49 && x >= 58 && x < 70) c = To(top);
                    // 桌腿
                    if (y >= 4 && y < 38 && x >= 14 && x < 24) c = To(leg);
                    if (y >= 4 && y < 38 && x >= 104 && x < 114) c = To(leg);

                    px[y * w + x] = c;
                }
            }
            return Finish(px, w, h, "Art_Desk");
        }

        static Sprite MakeChair()
        {
            int w = 64, h = 88;
            var px = new Color32[w * h];
            Color wood = C(0xA97B52);
            Color dark = C(0x8A6242);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new Color32(0, 0, 0, 0);
                    // 椅面
                    if (y >= 34 && y < 44 && x >= 8 && x < 56) c = To(wood);
                    // 椅背
                    if (y >= 60 && y < 84 && x >= 14 && x < 50) c = To(wood);
                    // 靠背竖条
                    if (y >= 84 && y < 88 && x >= 12 && x < 52) c = To(dark);
                    // 椅腿
                    if (y >= 6 && y < 34 && x >= 12 && x < 18) c = To(dark);
                    if (y >= 6 && y < 34 && x >= 46 && x < 52) c = To(dark);

                    px[y * w + x] = c;
                }
            }
            return Finish(px, w, h, "Art_Chair");
        }

        static Sprite MakePlant()
        {
            int w = 80, h = 112;
            var px = new Color32[w * h];
            Color pot = C(0xC4795C);
            Color potDark = C(0xA55F45);
            Color leaf = C(0x4F9E58);
            Color leafDark = C(0x3B7C44);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new Color32(0, 0, 0, 0);
                    // 树冠（两个椭圆）
                    bool inLeafA = EllipseTest(x, y, 40, 78, 30, 26);
                    bool inLeafB = EllipseTest(x, y, 26, 56, 18, 16);
                    bool inLeafC = EllipseTest(x, y, 55, 58, 17, 15);
                    if (inLeafA || inLeafB || inLeafC)
                        c = To(inLeafA && !inLeafB && !inLeafC ? leaf : leafDark);

                    // 花盆（梯形）
                    if (y >= 18 && y < 46)
                    {
                        float t = (y - 18) / 28f;
                        int halfW = Mathf.RoundToInt(Mathf.Lerp(16f, 26f, t));
                        if (Mathf.Abs(x - 40) < halfW) c = To(t > 0.75f ? potDark : pot);
                    }
                    // 托盘
                    if (y >= 12 && y < 18 && Mathf.Abs(x - 40) < 28) c = To(potDark);

                    px[y * w + x] = c;
                }
            }
            return Finish(px, w, h, "Art_Plant");
        }

        static Sprite MakePlayer()
        {
            // 正面朝向的方块人，美术会画得更细致，这里用色块示意
            int w = 64, h = 112;
            var px = new Color32[w * h];
            Color skin = C(0xF0C9A4);
            Color hair = C(0x4A3A33);
            Color shirt = C(0x5B7FB5);
            Color shirtDark = C(0x44648C);
            Color pants = C(0x4A5060);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new Color32(0, 0, 0, 0);

                    // 头（圆）
                    if (EllipseTest(x, y, 32, 92, 17, 18)) c = To(skin);
                    // 头发（头顶盖）
                    if (EllipseTest(x, y, 32, 99, 18, 12)) c = To(hair);

                    // 躯干
                    if (y >= 58 && y < 90 && x >= 17 && x < 47)
                        c = To(y < 66 ? shirtDark : shirt);
                    // 手臂
                    if (y >= 60 && y < 84 && (x >= 11 && x < 17 || x >= 47 && x < 53)) c = To(skin);

                    // 腿
                    if (y >= 8 && y < 58)
                    {
                        if (x >= 19 && x < 30) c = To(pants);
                        if (x >= 34 && x < 45) c = To(pants);
                    }
                    // 鞋
                    if (y >= 4 && y < 10 && (x >= 17 && x < 31 || x >= 33 && x < 47)) c = To(hair);

                    px[y * w + x] = c;
                }
            }
            return Finish(px, w, h, "Art_Player");
        }

        static Sprite MakeFaceBlock()
        {
            int s = 16;
            var px = new Color32[s * s];
            var c = To(C(0x3A3028));
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return Finish(px, s, s, "Art_Face");
        }

        // =========================================================
        // 工具
        // =========================================================

        static bool EllipseTest(int x, int y, float cx, float cy, float rx, float ry)
        {
            float dx = (x + 0.5f - cx) / rx;
            float dy = (y + 0.5f - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        static Color32 To(Color c) => c;

        static Sprite Finish(Color32[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name + "Tex",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);

            // 锚点在底部中心：立绘"站在"物体所在位置
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(0.5f, 0f), PPU, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}