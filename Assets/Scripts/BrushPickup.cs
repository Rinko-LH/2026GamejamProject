using UnityEngine;

namespace RoomDemo
{
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
}
