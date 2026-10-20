using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>画布外的上一张、下一张及提交按钮；布局在场景中编辑。</summary>
    public sealed class PaintingNavigationUI : MonoBehaviour
    {
        [Tooltip("画板玩法控制器")]
        public DrawingBoardInteract board;
        [Tooltip("按钮容器；仅在画布特写期间显示")]
        public GameObject controls;
        [Tooltip("返回上一张画布的按钮")]
        public Button previousButton;
        [Tooltip("当前画作达标后可用的下一张按钮")]
        public Button nextButton;
        [Tooltip("最后一张显示，全部达标后可用的提交按钮")]
        public Button submitButton;

        void OnEnable() => Refresh();
        void Update() => Refresh();

        public void Refresh()
        {
            bool visible = board != null && board.IsPaintingMode;
            if (controls != null && controls.activeSelf != visible) controls.SetActive(visible);
            if (!visible) return;
            if (previousButton != null) previousButton.interactable = board.CanGoToPreviousBoard;
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(!board.IsLastBoard);
                nextButton.interactable = board.CanGoToNextBoard;
            }
            if (submitButton != null)
            {
                submitButton.gameObject.SetActive(board.IsLastBoard);
                submitButton.interactable = board.CanSubmitPaintings;
            }
        }
    }
}
