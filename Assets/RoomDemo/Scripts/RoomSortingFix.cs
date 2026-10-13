using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 绘制层兜底修正。
    ///
    /// 背景：Unity 在保存/重载场景时，偶尔会把 SpriteRenderer 的 sortingOrder
    /// 重写成意料之外的值（实测见过设成 99999 却被存成 -31073）。
    /// 一旦地板的层号大于家具，整个房间的家具就会被地板盖住。
    ///
    /// 这里在场景加载后的第一帧，按物体名重新推导一遍排序层，
    /// 彻底摆脱对"已序列化 sortingOrder 是否正确"的依赖。
    /// </summary>
    public class RoomSortingFix : MonoBehaviour
    {
        void Start()
        {
            Refresh();
        }

        void LateUpdate()
        {
            // 只在第一帧修正即可，避免每帧遍历开销
            enabled = false;
        }

        [ContextMenu("立即刷新绘制层")]
        public void Refresh()
        {
            ApplyShell();
            ApplyProps();
            ApplyPlayer();
        }

        /// <summary>角色由 RoomPlayer 每帧自行排序，这里只保证初始值正确。</summary>
        void ApplyPlayer()
        {
            var player = transform.Find("Player");
            if (player == null) return;

            var s = player.GetComponent<RoomPlayer>();
            if (s == null) return;

            var srs = player.GetComponentsInChildren<SpriteRenderer>(true);
            int order = RoomView.Order(DepthFromFloorY(player.position.y), 0);
            foreach (var sr in srs)
            {
                switch (sr.name)
                {
                    case "Shadow": sr.sortingOrder = order; break;
                    case "Legs": sr.sortingOrder = order + 1; break;
                    case "Torso": sr.sortingOrder = order + 2; break;
                    case "Head": sr.sortingOrder = order + 3; break;
                    case "Hair": sr.sortingOrder = order + 4; break;
                    case "Face": sr.sortingOrder = order + 5; break;
                }
            }
        }

        void ApplyShell()
        {
            var shell = transform.Find("Shell");
            if (shell == null) return;

            SetOrder(shell.Find("Floor"), RoomView.LayerFloor);
            SetOrder(shell.Find("BackWall"), RoomView.LayerWall);
            SetOrder(shell.Find("WallLeft"), RoomView.LayerWall);
            SetOrder(shell.Find("WallRight"), RoomView.LayerWall);
        }

        void ApplyProps()
        {
            var props = transform.Find("Props");
            if (props == null) return;

            SetOrder(props.Find("Rug"), RoomView.LayerRug);
            SetOrder(props.Find("RugInner"), RoomView.LayerRug + 1);

            // 家具按各自的世界 Y（=该纵深的地面高度）反推层号
            var srs = props.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr.transform.parent != props) continue;   // 只处理顶层家具
                if (sr.name == "Rug" || sr.name == "RugInner") continue;

                float depth = DepthFromFloorY(sr.transform.position.y);
                int baseOrder = RoomView.Order(depth, 0);

                // 同一家具的各部件（被褥/枕头/书/门缝）按名字里的语义微调，
                // 保证细节画在主体之上。
                sr.sortingOrder = baseOrder + DetailBias(sr.name);
            }
        }

        /// <summary>从屏幕高度反推纵深：贴地的物体高度 = FloorY(depth)。</summary>
        static float DepthFromFloorY(float floorY)
        {
            // FloorY = HorizonY - (HorizonY - FrontY) * Scale，Scale = 1/(1+depth*Persp)
            // 反解：s = (HorizonY - floorY)/(HorizonY - FrontY)，depth = (1/s - 1)/Persp
            float s = (RoomView.HorizonY - floorY) / (RoomView.HorizonY - RoomView.FrontY);
            if (s <= 0.0001f) return RoomView.RoomDepth;
            return Mathf.Clamp((1f / s - 1f) / RoomView.Persp, 0f, RoomView.RoomDepth);
        }

        static int DetailBias(string name)
        {
            // 数值越大越靠前
            if (name.Contains("Bedding") || name.Contains("Pillow") ||
                name.Contains("Book") || name.Contains("Seam")) return 2;
            if (name.Contains("Leg")) return 1;
            if (name == "DeskTop") return 3;
            if (name.Contains("Leaves") || name.Contains("LampShade")) return 2;
            return 0;
        }

        static void SetOrder(Transform t, int order)
        {
            if (t == null) return;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = order;
        }
    }
}