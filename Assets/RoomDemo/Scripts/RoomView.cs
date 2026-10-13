using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 俯视斜角（3/4）视角的投影：把"房间空间"映射到屏幕。
    ///
    /// 屏幕 X = 房间左右
    /// 屏幕 Y = 纵深（往里走 → 向上，带透视收缩）
    /// 屏幕 Y = 高度（往上长 → 向上）
    ///
    /// 注意后两者共用同一条屏幕 Y 轴，所以整体观感是"扁平"的 ——
    /// 这正是该视角区别于等轴测的特征，也让它实现起来非常直接。
    /// </summary>
    public static class RoomView
    {
        // ---- 房间尺寸（格）----
        public const float RoomWidth = 11f;
        public const float RoomDepth = 11f;
        public const float HalfWidth = RoomWidth * 0.5f;

        // ---- 投影参数 ----
        /// <summary>地面最前缘的屏幕高度。</summary>
        public const float FrontY = 0f;

        /// <summary>灭点（地平线）高度，越大则纵深方向压缩越明显。</summary>
        public const float HorizonY = 9.5f;

        /// <summary>透视强度。0.032 时房间后缘宽度约为前沿的 74%，接近参考图的收敛程度。</summary>
        public const float Persp = 0.032f;

        /// <summary>墙高（世界单位）。</summary>
        public const float WallHeight = 2.9f;

        /// <summary>某个纵深处物体的透视缩放（同时作用于宽度与高度）。</summary>
        public static float Scale(float depth)
        {
            return 1f / (1f + Mathf.Max(0f, depth) * Persp);
        }

        /// <summary>该纵深处地面的屏幕高度。</summary>
        public static float FloorY(float depth)
        {
            return HorizonY - (HorizonY - FrontY) * Scale(depth);
        }

        /// <summary>房间坐标 → 屏幕坐标。height 为在此基础上额外抬高的高度。</summary>
        public static Vector3 Project(float x, float depth, float height = 0f)
        {
            float s = Scale(depth);
            return new Vector3(x * s, FloorY(depth) + height * s, 0f);
        }

        /// <summary>房间坐标 → 屏幕坐标（只需纵深）。</summary>
        public static Vector3 Project(float x, float depth)
        {
            return Project(x, depth, 0f);
        }

        /// <summary>
        /// 房间坐标 → 屏幕坐标，但 x 保持原始值不缩放。
        /// 放置家具时用这个：横向位置由调用方直接指定，只有纵向吃透视，
        /// 避免"位置被缩放一次、尺寸又被缩放一次"的双重压缩。
        /// </summary>
        public static Vector3 Anchor(float x, float depth)
        {
            return new Vector3(x, FloorY(depth), 0f);
        }

        // ---- 墙面精灵的锚点（世界坐标）----
        // 侧墙精灵的锚点定在自身包围盒的左下角，所以要告诉生成器放在哪。
        public static Vector3 LeftWallPivot => new Vector3(-HalfWidth, 0f, 0f);
        public static Vector3 RightWallPivot => new Vector3(HalfWidth * Scale(RoomDepth), 0f, 0f);

        // ---- 绘制层 ----
        // 重要：Unity 在保存/重载场景时可能重写 sortingOrder 的具体数值，
        // 所以这里必须用"互不重叠的大区间"来划分层级，任何重写都不会破坏相对关系。
        //
        // 地板与墙用固定负值；房间内一切按纵深递增（越靠里层越小=越先画）。
        // 区间划分必须留足余量，避免 Order() 的结果跨入别的带。
        public const int LayerFloor = -20000;
        public const int LayerWall = -19000;
        public const int LayerRug = -18000;
        public const int LayerPropsMin = -15000;
        public const int LayerPropsMax = 15000;

        /// <summary>房间内物体的绘制层。depth 越大（越远）层越小（越先画）。</summary>
        public static int Order(float depth, int extra = 0)
        {
            float t = Mathf.Clamp01(depth / RoomDepth);
            // 反向映射：depth 0 → LayerPropsMax，depth RoomDepth → LayerPropsMin
            int layer = Mathf.RoundToInt(Mathf.Lerp(LayerPropsMax, LayerPropsMin, t));
            return layer + extra;
        }
    }
}