using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 画笔拾取与画板解锁。支持特写中鼠标点击，以及旧场景的任务显形、按 E 拾取。
    /// </summary>
    public class BrushPickup : RoomInteractable, IResettable
    {
        [Header("引用")]
        public DrawingBoardInteract board;

        [Tooltip("画笔的可见渲染器。被发现时点亮、被拾取时熄灭")]
        public SpriteRenderer[] visuals;

        [Tooltip("仅由整理特写处理鼠标点击，不响应房间内的 E 键，也不等待书架任务显形。将画笔放在杂物下方即可形成遮挡。")]
        public bool closeupClickOnly;

        [Header("显形")]
        [Tooltip("旧版房间交互：勾选后必须先完成指定任务才显形。仅特写点击模式下忽略此设置，由杂物遮挡画笔。")]
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
        bool _pointerHovered;
        InteractionOutline _pointerOutline;

        /// <summary>旧版房间交互中，画笔显形后进入触发范围就提示玩家可拾取。</summary>
        protected override bool ShouldShowOutline =>
            (closeupClickOnly && _pointerHovered && !HasBeenTaken && IsRevealed && canInteract)
            || (!closeupClickOnly && (base.ShouldShowOutline || (!CloseupActive && IsRevealed && canInteract && PlayerInRange)));

        public void SetHovered(bool hovered)
        {
            _pointerHovered = hovered && closeupClickOnly && !HasBeenTaken && IsRevealed && canInteract;
            if (_pointerOutline == null) _pointerOutline = GetComponent<InteractionOutline>();
            if (_pointerHovered && _pointerOutline == null)
                _pointerOutline = gameObject.AddComponent<InteractionOutline>();
            if (_pointerOutline == null) return;
            _pointerOutline.Initialize();
            _pointerOutline.SetOutlined(_pointerHovered);
        }

        void OnDisable() => SetHovered(false);

        void Awake()
        {
            // 藏起来：关渲染器 + 关交互（PlayerInRange 依赖 canInteract）。
            // 只关 renderer.enabled，不 SetActive——SetActive(false) 会让本物体
            // 的 Update / 事件订阅一起停掉，进度事件就收不到了。
            if (visuals == null || visuals.Length == 0)
                visuals = GetComponentsInChildren<SpriteRenderer>(true);
            if (interactionZone == null) interactionZone = GetComponent<Collider2D>();
            ResetPickup();
        }

        /// <summary>
        /// 必须 override，不能写 new/隐式的 Update——
        /// 基类 RoomInteractable.Update 是 protected virtual，在里面惰性订阅 E 键。
        /// 直接写 <c>void Update()</c> 会把它<b>隐藏</b>掉，E 键从此收不到，
        /// 画笔就再也拿不起来了（这个坑一旦踩到症状是"走近了按 E 没反应"）。
        /// </summary>
        protected override void Update()
        {
            if (closeupClickOnly) return;
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
            if (closeupClickOnly || e.Id != revealOnQuestId) return;
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
            SetHovered(false);
            SetVisuals(false);

            if (board != null) board.GiveBrush();

            if (reportId != QuestId.None) Services.Progress?.Report(reportId);

            // 拾取台词：正在播别的台词时不抢队列（避免把当前对白顶掉）
            if (Services.Dialogue != null && !Services.Dialogue.IsPlaying)
                Services.Dialogue.Play(new[] { new DialogueLine { text = foundLine, autoAdvance = 3f } });

            Debug.Log($"[画笔] {foundLine}");
            PickedUp?.Invoke();
        }

        /// <summary>特写控制器负责遮挡判定，本方法只判断画笔自身是否可被点击。</summary>
        public bool ContainsPointer(Vector2 point)
        {
            if (!closeupClickOnly || !isActiveAndEnabled || HasBeenTaken || !_revealed || !canInteract)
                return false;
            return VisibleSprite != null && interactionZone != null && interactionZone.isActiveAndEnabled
                && interactionZone.OverlapPoint(point);
        }

        public SpriteRenderer VisibleSprite
        {
            get
            {
                if (visuals != null)
                    foreach (var sprite in visuals)
                        if (sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy
                            && sprite.sprite != null) return sprite;
                return null;
            }
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
            SetHovered(false);
            HasBeenTaken = false;
            if (startHidden && !closeupClickOnly) Hide();
            else { _revealed = true; canInteract = true; SetVisuals(true); }
        }

        /// <summary>R 重开时归还画笔并重新藏起来（IResettable）。</summary>
        public void ResetForNewRun() => ResetPickup();
    }
}
