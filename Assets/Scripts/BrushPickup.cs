using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画笔（策划案 4.1 / S6）。
    ///
    /// 玩法链路：开场画笔是<b>藏起来的</b>（不在场景里可见）→ 玩家整理书架
    /// （TidyShelf 完成）→ 画笔出现在书架上 → 走近按 E 拾取 → 画板可进。
    ///
    /// 策划案原文：「整理书架 · 精神 +20%，<b>发现画笔</b> · 主线关键道具在此」——
    /// 注意 20% 概率属于另一条支线「偶然发现」（矿泉水 / 零食），
    /// <b>画笔是必出的</b>，否则主线（画画）会被随机数卡死。
    ///
    /// 发现时机走进度总线（R1）：本类订阅 Services.Progress.QuestCompleted，
    /// 收到 <see cref="revealOnQuestId"/> 完成事件才显形。这样 TidySpot 不需要
    /// 反向持有画笔引用，收拾与找笔两条支线保持解耦。
    /// </summary>
    public class BrushPickup : RoomInteractable, IResettable
    {
        [Header("引用")]
        public DrawingBoardInteract board;

        [Tooltip("画笔的可见渲染器。被发现时点亮、被拾取时熄灭")]
        public SpriteRenderer[] visuals;

        [Header("显形")]
        [Tooltip("开局是否藏着。true = 必须先完成 revealOnQuestId 才出现")]
        public bool startHidden = true;

        [Tooltip("完成这条支线时画笔出现（策划案：整理书架）")]
        public QuestId revealOnQuestId = QuestId.TidyShelf;

        [Header("支线接入")]
        [Tooltip("拿到画笔时汇报进度总线（策划案 4.1）")]
        public QuestId reportId = QuestId.BrushFound;

        [Header("显示文本")]
        public string notFoundLine = "奇怪，画笔怎么不见了，应该是落在房间某处了吧";
        public string foundLine = "在书架上找到了画笔，回去画画吧";

        /// <summary>拿到画笔时触发。</summary>
        public event System.Action PickedUp;
        /// <summary>画笔出现在场景里时触发（还没被拿走）。</summary>
        public event System.Action Revealed;

        public bool HasBeenTaken { get; private set; }
        public bool IsRevealed => _revealed;

        bool _revealed;
        bool _progressSubscribed;

        void Awake()
        {
            // 藏起来：关渲染器 + 关交互（PlayerInRange 依赖 canInteract）。
            // 只关 renderer.enabled，不 SetActive——SetActive(false) 会让本物体
            // 的 Update / 事件订阅一起停掉，进度事件就收不到了。
            if (startHidden) Hide();
            else _revealed = true;
        }

        /// <summary>
        /// 必须 override，不能写 new/隐式的 Update——
        /// 基类 RoomInteractable.Update 是 protected virtual，在里面惰性订阅 E 键。
        /// 直接写 <c>void Update()</c> 会把它<b>隐藏</b>掉，E 键从此收不到，
        /// 画笔就再也拿不起来了（这个坑一旦踩到症状是"走近了按 E 没反应"）。
        /// </summary>
        protected override void Update()
        {
            base.Update();
            EnsureProgressSubscribed();
        }

        void OnDestroy()
        {
            var p = Services.Progress;
            if (p != null) p.QuestCompleted -= OnQuestCompleted;
            _progressSubscribed = false;
        }

        /// <summary>惰性订阅进度总线（Services 可能比本对象晚就绪，与既有写法一致）。</summary>
        void EnsureProgressSubscribed()
        {
            if (_progressSubscribed) return;
            var p = Services.Progress;
            if (p == null) return;
            p.QuestCompleted -= OnQuestCompleted;
            p.QuestCompleted += OnQuestCompleted;
            _progressSubscribed = true;
        }

        void OnQuestCompleted(QuestCompletedEvent e)
        {
            if (e.Id != revealOnQuestId) return;
            Reveal();
        }

        /// <summary>让画笔出现在场景里。</summary>
        public void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            canInteract = true;
            SetVisuals(true);
            Debug.Log("[画笔] 整理书架时在书架上发现了画笔");
            Revealed?.Invoke();
        }

        public override void Interact()
        {
            if (HasBeenTaken || !_revealed) return;

            HasBeenTaken = true;
            canInteract = false;
            SetVisuals(false);

            if (board != null) board.GiveBrush();

            if (reportId != QuestId.None) Services.Progress?.Report(reportId);

            // 拾取台词：正在播别的台词时不抢队列（避免把当前对白顶掉）
            if (Services.Dialogue != null && !Services.Dialogue.IsPlaying)
                Services.Dialogue.Play(new[] { new DialogueLine { text = foundLine, autoAdvance = 3f } });

            Debug.Log($"[画笔] {foundLine}");
            PickedUp?.Invoke();
        }

        void Hide()
        {
            _revealed = false;
            canInteract = false;
            SetVisuals(false);
        }

        void SetVisuals(bool visible)
        {
            if (visuals == null) return;
            foreach (var sr in visuals)
                if (sr != null) sr.enabled = visible;
        }

        /// <summary>重置，供测试反复跑。</summary>
        public void ResetPickup()
        {
            HasBeenTaken = false;
            if (startHidden) Hide();
            else { _revealed = true; canInteract = true; SetVisuals(true); }
        }

        /// <summary>R 重开时归还画笔并重新藏起来（IResettable）。</summary>
        public void ResetForNewRun() => ResetPickup();
    }
}
