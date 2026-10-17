using System.Collections.Generic;
using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo.Quest
{
    /// <summary>
    /// 收拾玩法（策划案 2.3 / S3）：桌面 / 书架两段式（用户已拍板 E 键两段式）。
    ///
    /// 交互：第一次 E 进入"整理模式"（高亮提示，<see cref="highlight"/> 点亮），
    /// 第二次 E 执行收拾（隐藏收拾物、恢复状态、上报进度、播台词）。
    ///
    /// 铁律合规：
    /// - 状态恢复走 <see cref="IStatSource.Add"/>（支线唯一入口），不直接碰 PlayerStats 通道；
    /// - 进度走 <see cref="Services.Progress.Report"/>；猫打乱（S2）与收拾通过进度总线解耦；
    /// - 有本局状态（_armed）→ 实现 <see cref="IResettable"/>，R 重开由 GameManager 统一遍历重置。
    ///
    /// 占位期：收拾物 = <see cref="itemsRoot"/> 下的色块子物体；高亮 = 可选的 <see cref="highlight"/> 子物体。
    /// </summary>
    public class TidySpot : RoomInteractable, IResettable
    {
        [Header("状态恢复")]
        public PlayerStats stats;
        public StatKind effect = StatKind.Spirit;
        [Tooltip("完成时恢复多少点（数值案 +20 精神）")]
        public float amount = 20f;

        [Header("支线接入")]
        [Tooltip("完成后汇报的支线 id（TidyDesk / TidyShelf）。None = 不计入进度")]
        public QuestId reportId = QuestId.None;

        [Header("台词")]
        [Tooltip("完成收拾时播放的台词，可空（文案预留，后期替换）")]
        public DialogueLine[] lines = new DialogueLine[0];

        [Header("收拾物与高亮")]
        [Tooltip("收拾物容器，子物体会在执行时被隐藏。装配菜单生成 itemCount 个占位色块")]
        public Transform itemsRoot;
        [Tooltip("第一次 E 时显示的高亮提示物，第二次 E 时隐藏（可选）")]
        public GameObject highlight;

        [Tooltip("收拾物数量（用户拍板 = 3；文案预留，后期美术替换具体物件）")]
        public int itemCount = 3;

        bool _armed;
        readonly List<GameObject> _items = new List<GameObject>();

        void Awake()
        {
            if (itemsRoot != null)
                for (int i = 0; i < itemsRoot.childCount; i++)
                    _items.Add(itemsRoot.GetChild(i).gameObject);
        }

        public override void Interact()
        {
            if (!canInteract) return;

            if (!_armed)
            {
                // 第一段：进入整理模式，点亮高亮提示玩家"再按 E 收拾"
                _armed = true;
                if (highlight != null) highlight.SetActive(true);
                return;
            }

            // 第二段：执行收拾
            foreach (var it in _items)
                if (it != null) it.SetActive(false);

            var sink = Services.Stats ?? (IStatSource)stats;
            sink.Add(effect, amount);

            if (reportId != QuestId.None) Services.Progress?.Report(reportId);

            if (lines != null && lines.Length > 0)
                Services.Dialogue?.Play(lines);

            if (highlight != null) highlight.SetActive(false);
            canInteract = false;
        }

        /// <summary>R 重开：收拾物复位、高亮熄灭、可再次交互（IResettable）。</summary>
        public void ResetForNewRun()
        {
            _armed = false;
            canInteract = true;
            foreach (var it in _items)
                if (it != null) it.SetActive(true);
            if (highlight != null) highlight.SetActive(false);
        }
    }
}
