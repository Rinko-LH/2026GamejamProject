using System.Collections.Generic;
using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo.Quest
{
    /// <summary>
    /// 家具收拾入口与奖励结算：配置特写时按 E 进入鼠标整理玩法。
    ///
    /// 全部杂物归位后恢复状态、上报进度、播台词。未配置特写时保留两段式 E 键收拾。
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
        [Header("特写")]
        [Tooltip("按 E 进入此特写，全部杂物归位后结算；留空保留房间内两段式收拾。")]
        public TidyCloseup closeup;

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

        [Header("猫打乱（P2 / 策划案 2.4）")]
        [Tooltip("被猫弄乱后可重新收拾。桌面=true；书架必须=false（画笔链不能被猫打断）")]
        public bool catMessable = false;

        [Header("偶然发现（策划案 3.5）")]
        [Tooltip("收拾完成时翻出矿泉水/零食的概率（数值案 20%），不消耗时间")]
        public float discoveryChance = 0.2f;

        [Header("收拾物与高亮")]
        [Tooltip("收拾物容器，子物体会在执行时被隐藏。装配菜单生成 itemCount 个占位色块")]
        public Transform itemsRoot;
        [Tooltip("第一次 E 时显示的高亮提示物，第二次 E 时隐藏（可选）")]
        public GameObject highlight;

        [Tooltip("收拾物数量（用户拍板 = 3；文案预留，后期美术替换具体物件）")]
        public int itemCount = 3;

        bool _armed;
        readonly List<GameObject> _items = new List<GameObject>();
        bool _subscribedProgress;

        void Awake()
        {
            if (itemsRoot != null)
                for (int i = 0; i < itemsRoot.childCount; i++)
                    _items.Add(itemsRoot.GetChild(i).gameObject);
        }

        protected override void Update()
        {
            base.Update(); // 基类惰性订阅 E 键（漏调则交互失效）

            // 进度总线惰性订阅：ProgressTracker 由 GameManager.Awake 注册，
            // 播放中域重载后实例会换新，这里按帧补订。
            if (!_subscribedProgress && ProgressTracker.Instance != null)
            {
                ProgressTracker.Instance.QuestCompleted += OnQuestCompleted;
                _subscribedProgress = true;
            }
        }

        void OnDestroy()
        {
            if (_subscribedProgress && ProgressTracker.Instance != null)
                ProgressTracker.Instance.QuestCompleted -= OnQuestCompleted;
        }

        /// <summary>
        /// 猫打乱上报（R1 解耦：猫只 Report，收拾系统自行决定复原谁）。
        /// 已收拾完且允许被弄乱 → 恢复可交互，玩家需重新收拾。
        /// </summary>
        void OnQuestCompleted(QuestCompletedEvent e)
        {
            if (e.Id != QuestId.CatMischief || !catMessable || canInteract) return;
            MessUp();
        }

        /// <summary>被猫弄乱：收拾物回场（重进特写自动显物）、可再次交互、播提示台词。</summary>
        void MessUp()
        {
            canInteract = true;
            _armed = false;
            foreach (var it in _items)
                if (it != null) it.SetActive(true);
            if (highlight != null) highlight.SetActive(false);
            if (closeup != null) closeup.NotifyMessUp(this);
            Services.Dialogue?.Play(new[]
            {
                new DialogueLine { text = "……猫把刚收拾好的桌面又扒乱了。", autoAdvance = 3f }
            });
            Debug.Log("[收拾] 被猫弄乱，恢复可收拾：" + name);
        }

        public override void Interact()
        {
            if (!canInteract) return;
            if (closeup != null)
            {
                closeup.Enter(this);
                return;
            }

            if (!_armed)
            {
                // 没有特写的家具保留原有两段式交互。
                _armed = true;
                if (highlight != null) highlight.SetActive(true);
                return;
            }

            // 无特写时的第二段：执行收拾。
            ExecuteTidy();
        }

        /// <summary>结算一次收拾奖励；鼠标特写保留已摆好的杂物。</summary>
        public void ExecuteTidy(bool preserveItems = false)
        {
            if (!canInteract) return;
            var sink = Services.Stats ?? (IStatSource)stats;
            if (sink == null)
            {
                Debug.LogError("[收拾] 缺少玩家状态服务。", this);
                return;
            }
            canInteract = false;
            if (!preserveItems)
                foreach (var it in _items)
                    if (it != null) it.SetActive(false);

            sink.Add(effect, amount);

            if (reportId != QuestId.None) Services.Progress?.Report(reportId);

            if (highlight != null) highlight.SetActive(false);
            // 偶然发现（策划案 3.5）：20% 概率翻出矿泉水/零食，不消耗时间。
            // 台词并入同一次 Play——DialogueManager.Play 是替换不是排队，二次调用会吞掉前面的收拾台词。
            var allLines = new List<DialogueLine>(lines ?? new DialogueLine[0]);
            if (Random.value < discoveryChance)
            {
                bool water = Random.value < 0.5f;
                sink.Add(water ? StatKind.Water : StatKind.Satiety, water ? 20f : 10f);
                Services.Progress?.Report(QuestId.RandomFind);
                allLines.Add(new DialogueLine
                {
                    text = water
                        ? "在杂物里翻出一瓶没开封的矿泉水，先喝了两口。（水分 +20）"
                        : "杂物堆里居然藏了包没过期的零食，垫垫肚子。（饱食 +10）",
                    autoAdvance = 3f
                });
                Debug.Log("[收拾] 偶然发现：" + (water ? "矿泉水 +20 水分" : "零食 +10 饱食"));
            }

            if (allLines.Count > 0)
                Services.Dialogue?.Play(allLines.ToArray());
        }

        /// <summary>中途退出特写（尚未执行）时复位点亮态，回到房间仍可再进。</summary>
        public void CancelArm()
        {
            _armed = false;
            if (highlight != null) highlight.SetActive(false);
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
