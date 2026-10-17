using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画笔。策划案：主角在整理书架时偶然发现画笔。
    /// 拿之前有提示，拿之后挂到玩家身上（色块挂手上）。
    ///
    /// 契约接入（S0）：实现 <see cref="IResettable"/>，R 重开时归还
    /// （修复"拿走不归还"的既有 bug——ResetPickup 原来没人调）；
    /// reportId 默认 BrushFound，拿到即上报进度总线（策划案 4.1）。
    /// </summary>
    public class BrushPickup : RoomInteractable, IResettable
    {
        [Header("引用")]
        public DrawingBoardInteract board;
        public SpriteRenderer[] visualsToHide;

        [Header("支线接入")]
        [Tooltip("拿到画笔时汇报进度总线（策划案 4.1）")]
        public QuestId reportId = QuestId.BrushFound;

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
            if (reportId != QuestId.None) Services.Progress?.Report(reportId);
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

        /// <summary>R 重开时归还画笔（IResettable）。</summary>
        public void ResetForNewRun()
        {
            ResetPickup();
        }
    }
}
