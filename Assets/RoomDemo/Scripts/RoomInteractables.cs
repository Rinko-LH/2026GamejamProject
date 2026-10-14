using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 可互动的场景物品基类。色块占位期只需要"走近 + 按键 → 触发"。
    /// </summary>
    public abstract class RoomInteractable : MonoBehaviour
    {
        [Tooltip("交互距离（只看水平方向，主角走到物品所在的列附近即可）")]
        public float interactRange = 1.4f;

        public RoomPlayer player;

        /// <summary>是否还能交互。被拿走的物品要能主动失效。</summary>
        public bool canInteract = true;

        /// <summary>
        /// 主角是否在交互范围内。
        ///
        /// 关键：只比较<b>水平 X 距离</b>，不比较 Y。
        /// 因为这个 3/4 视角里，桌面上的家具 Y 值在 2.7 左右，
        /// 而主角站在地面上 Y 最多只能走到 1.56 —— 这是固定存在的纵向差，
        /// 用二维距离判定的话无论怎么走近都够不到，E 永远没反应。
        /// </summary>
        public bool PlayerInRange
        {
            get
            {
                if (!canInteract || player == null) return false;
                return HorizontalDistance(player.transform.position, transform.position) < interactRange;
            }
        }

        /// <summary>只算 X 方向的距离（该视角下 X 就是房间左右）。</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x);
        }

        protected virtual void Update()
        {
            if (PlayerInRange && Input.GetKeyDown(KeyCode.E))
                Interact();
        }

        /// <summary>执行互动。</summary>
        public abstract void Interact();
    }

    /// <summary>
    /// 画笔。策划案：主角在整理书架时偶然发现画笔。
    /// 拿之前有提示，拿之后挂到玩家身上（色块挂手上）。
    /// </summary>
    public class BrushPickup : RoomInteractable
    {
        [Header("引用")]
        public DrawingBoardInteract board;
        public SpriteRenderer[] visualsToHide;

        [Header("显示文本")]
        public string notFoundLine = "奇怪，画笔怎么不见了，应该是落在房间某处了吧";
        public string foundLine = "在书架上找到了画笔，回去画画吧";

        /// <summary>拿到画笔时触发。</summary>
        public event System.Action PickedUp;

        public bool HasBeenTaken { get; private set; }

        public override void Interact()
        {
            if (HasBeenTaken) return;

            HasBeenTaken = true;
            canInteract = false;

            if (board != null) board.GiveBrush();

            if (visualsToHide != null)
            {
                foreach (var sr in visualsToHide)
                    if (sr != null) sr.enabled = false;
            }

            Debug.Log($"[画笔] {foundLine}");
            PickedUp?.Invoke();
        }

        /// <summary>重置，供测试反复跑。</summary>
        public void ResetPickup()
        {
            HasBeenTaken = false;
            canInteract = true;
            if (visualsToHide != null)
            {
                foreach (var sr in visualsToHide)
                    if (sr != null) sr.enabled = true;
            }
        }
    }

    /// <summary>
    /// 通用道具拾取：零食 / 水等，用于支线补充状态。
    /// 现在只做最小可用版本，等支线玩法展开再细化。
    /// </summary>
    public class ConsumablePickup : RoomInteractable
    {
        public PlayerStats stats;

        public enum Effect { Water, Satiety, Spirit }
        public Effect effect = Effect.Satiety;

        [Tooltip("补充多少点")]
        public float amount = 20f;

        [Tooltip("是否要消耗游戏内时间")]
        public bool costTime = true;
        public float costMinutes = 20f;

        public DayTimer timer;

        public override void Interact()
        {
            if (stats == null) return;

            var channel = effect switch
            {
                Effect.Water => stats.Water,
                Effect.Satiety => stats.Satiety,
                _ => stats.Spirit
            };

            channel.Add(amount);

            if (costTime && timer != null) timer.SpendMinutes(costMinutes);

            canInteract = false;
            gameObject.SetActive(false);
        }
    }
}