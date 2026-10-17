using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo.Quest
{
    /// <summary>
    /// 客厅里的水源（S4 的房间入口）：走近按 E 进入接水特写。
    /// 只负责"触发进入"，接水玩法本身在 <see cref="WaterCloseup"/> 里。
    /// </summary>
    public class WaterSource : RoomInteractable, IResettable
    {
        [Tooltip("接水特写管理器（装配菜单写入）")]
        public WaterCloseup closeup;

        public override void Interact()
        {
            if (!canInteract) return;
            if (closeup != null) closeup.EnterCloseup();
        }

        public void ResetForNewRun() { }
    }
}
