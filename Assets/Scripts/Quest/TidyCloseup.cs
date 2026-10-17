using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo.Quest
{
    /// <summary>
    /// 收拾特写（方向 A）：把"收拾"从客厅里抽出来，做成独立特写视图。
    ///
    /// 进入：隐藏 RoomWorld（客厅整体消失，只剩深蓝相机底 + 大棕块背景）、相机聚焦。
    /// 退出（Esc）：恢复客厅与相机。
    /// 两段式语义保留：进入即点亮高亮（对应原第一段），特写里再按 E 执行收拾
    /// （隐藏收拾物、+20 精神、上报、播台词）。
    ///
    /// 公共部分（隐藏客厅 / 相机 / 输入订阅 / 总闸门 / Esc / IResettable）
    /// 全部在基类 <see cref="CloseupBase"/>，这里只留收拾自己的逻辑。
    /// </summary>
    public class TidyCloseup : RoomDemo.CloseupBase
    {
        [Header("收拾物与高亮")]
        [Tooltip("特写里的收拾物容器（执行时隐藏）。杂物只在特写里出现，客厅里不摆")]
        public Transform itemsRoot;
        public GameObject highlight;

        TidySpot _activeSpot;
        bool _armed;

        public bool IsInTidyCloseup => InCloseup;

        /// <summary>进入收拾特写（由 TidySpot.Interact 第一段调用）。</summary>
        public void Enter(TidySpot spot)
        {
            _activeSpot = spot;
            _armed = true; // 进特写即点亮高亮（对应原两段式第一段）
            EnterCloseup();
        }

        protected override void OnEntered()
        {
            _armed = true;
            if (highlight != null) highlight.SetActive(true);
            if (itemsRoot != null)
                foreach (Transform c in itemsRoot)
                    if (c != null) c.gameObject.SetActive(true);
        }

        protected override void HandleInteract()
        {
            // 已收拾则忽略（防止执行后误触再点亮 / 重复结算）
            if (_activeSpot != null && !_activeSpot.canInteract) return;

            if (!_armed)
            {
                _armed = true;
                if (highlight != null) highlight.SetActive(true);
                return;
            }
            Execute();
        }

        void Execute()
        {
            if (_activeSpot != null) _activeSpot.ExecuteTidy();
            if (itemsRoot != null)
                foreach (Transform c in itemsRoot)
                    if (c != null) c.gameObject.SetActive(false);
            if (highlight != null) highlight.SetActive(false);
            _armed = false;
            // 执行后停在特写，Esc 退出；已收拾，再按 E 无效
        }

        protected override void OnExited()
        {
            // 中途 Esc 退出（还没执行）：复位进入态，回到房间仍可再进
            if (_activeSpot != null && _activeSpot.canInteract)
                _activeSpot.CancelArm();
            _activeSpot = null;
            _armed = false;
        }

        public override void ResetForNewRun()
        {
            base.ResetForNewRun();
            _armed = false;
            _activeSpot = null;
            if (itemsRoot != null)
                foreach (Transform c in itemsRoot)
                    if (c != null) c.gameObject.SetActive(true);
            if (highlight != null) highlight.SetActive(false);
        }
    }
}
