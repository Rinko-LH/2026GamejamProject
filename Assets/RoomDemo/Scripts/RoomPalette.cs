using UnityEngine;

namespace RoomDemo
{
    /// <summary>俯视斜角室内场景的配色。</summary>
    public static class RoomPalette
    {
        // ---- 地板：木色棋盘 ----
        public static readonly Color FloorA = Hex(0xC9A87C);
        public static readonly Color FloorB = Hex(0xBE9B6F);
        public static readonly Color FloorSeam = Hex(0x9C7B54);

        // ---- 墙面：竖条纹 + 踢脚线 ----
        public static readonly Color WallA = Hex(0x8FA37B);
        public static readonly Color WallB = Hex(0x879A72);
        public static readonly Color WallSeam = Hex(0x6E7F5C);
        public static readonly Color Baseboard = Hex(0xD9CBAE);

        // ---- 家具 ----
        public static readonly Color WoodDark = Hex(0x8A6242);
        public static readonly Color WoodMid = Hex(0xA97B52);
        public static readonly Color WoodLight = Hex(0xC79A6B);
        public static readonly Color Bedding = Hex(0x9FC3C9);
        public static readonly Color BeddingAlt = Hex(0x7FA9B0);
        public static readonly Color Pillow = Hex(0xF2ECDC);
        public static readonly Color Metal = Hex(0xB9BEC4);
        // 盆栽用饱和度更高的绿，避免和木地板的暖棕糊在一起
        public static readonly Color Pottery = Hex(0xC4795C);
        public static readonly Color Leaf = Hex(0x4F9E58);
        public static readonly Color LeafDark = Hex(0x3B7C44);
        public static readonly Color Book = Hex(0xC4614F);

        // ---- 角色 ----
        public static readonly Color Skin = Hex(0xF0C9A4);
        public static readonly Color Hair = Hex(0x4A3A33);
        public static readonly Color Shirt = Hex(0x5B7FB5);
        public static readonly Color Pants = Hex(0x4A5060);
        public static readonly Color FaceMark = Hex(0x3A3028);

        // ---- 场景 ----
        public static readonly Color Background = Hex(0x1E2430);
        public static readonly Color Rug = Hex(0xB5735A);
        public static readonly Color RugBorder = Hex(0x9C5C46);

        public static Color Hex(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }
    }
}