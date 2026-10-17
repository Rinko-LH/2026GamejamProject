using System;
using RoomDemo.Contracts;
using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>游戏流程状态机（策划案 1.1）：开场独白 → 自由探索 → 结算。全局单例，驱动一天的开始与结束。</summary>
    public class GameManager : MonoBehaviour, IGameFlow
    {
        public static GameManager Instance { get; private set; }
        public GameFlowState State { get; private set; } = GameFlowState.Opening;

        /// <summary>状态切换时触发，参数为（旧状态, 新状态）。</summary>
        public event Action<GameFlowState, GameFlowState> StateChanged;

        [Header("系统引用")]
        public PlayerStats stats;
        public DayTimer timer;
        public DrawingBoardInteract board;
        public DialogueManager dialogue;

        [Header("开场独白")]
        [Tooltip("独白秒数，到点自动进入自由探索")]
        public float openingSeconds = 3f;

        [Tooltip("开场对白队列（策划案 1.5 / D4）。留空则退化为纯计时独白")]
        public DialogueLine[] openingLines = new DialogueLine[0];

        [Header("结算")]
        public int paintingsCompleted;

        float _openingTimer;
        bool _subscribed;
        bool _inputSubscribed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (stats == null) stats = FindObjectOfType<PlayerStats>();
            if (timer == null) timer = FindObjectOfType<DayTimer>();
            if (board == null) board = FindObjectOfType<DrawingBoardInteract>();
            if (dialogue == null) dialogue = FindObjectOfType<DialogueManager>();

            // 契约注册（S0）：跨系统交互只认 Services.Xxx，不互相 FindObjectOfType。
            // 进度总线找不到就兜底建一个，保证支线 Report 不炸。
            Services.Stats = stats;
            Services.Flow = this;
            Services.Dialogue = dialogue;
            var progress = FindObjectOfType<ProgressTracker>();
            if (progress == null)
            {
                var progressGo = new GameObject("ProgressTracker");
                progress = progressGo.AddComponent<ProgressTracker>();
            }
            Services.Progress = progress;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                // 契约反注册：切场景/卸载时别让别的系统拿到 stale 引用
                Services.Stats = null;
                Services.Flow = null;
                Services.Dialogue = null;
                Services.Progress = null;
            }
            Unsubscribe();
        }

        void Start()
        {
            Subscribe();
            EnterOpening();
        }

        void Update()
        {
            Subscribe(); // 惰性兜底，防时序竞态
            if (State == GameFlowState.Opening)
            {
                _openingTimer += Time.deltaTime;
                // D4：对白播完才进探索；openingSeconds 为最短兜底（无对白内容时
                // 退化为纯计时）。对白每行 autoAdvance 自动推进，正常情况下不会卡住。
                bool dialogueBusy = dialogue != null && dialogue.IsPlaying;
                if (_openingTimer >= openingSeconds && !dialogueBusy) EnterExploring();
            }
        }

        void Subscribe()
        {
            if (!_subscribed)
            {
                if (timer != null) { timer.DayEnded -= OnDayEnded; timer.DayEnded += OnDayEnded; }
                if (board != null) { board.PaintingCompleted -= OnPaintingCompleted; board.PaintingCompleted += OnPaintingCompleted; }
                _subscribed = true;
            }

            // 调试快捷键改走 InputManager（1.7）：Instance 可能还没就绪，每帧重试
            if (!_inputSubscribed)
            {
                var im = InputManager.Instance;
                if (im != null)
                {
                    im.DebugFastForward -= OnDebugFastForward; im.DebugFastForward += OnDebugFastForward;
                    im.DebugRestart -= OnDebugRestart; im.DebugRestart += OnDebugRestart;
                    _inputSubscribed = true;
                }
            }
        }

        void Unsubscribe()
        {
            if (timer != null) timer.DayEnded -= OnDayEnded;
            if (board != null) board.PaintingCompleted -= OnPaintingCompleted;
            var im = InputManager.Instance;
            if (im != null)
            {
                im.DebugFastForward -= OnDebugFastForward;
                im.DebugRestart -= OnDebugRestart;
            }
            _subscribed = false;
            _inputSubscribed = false;
        }

        // ---------- 状态流转 ----------

        public void EnterOpening()
        {
            _openingTimer = 0f;
            if (timer != null) timer.ResetTimer();
            // 先清掉上一局残留队列（ResetRun 时可能正在播对白），再播本局开场对白
            if (dialogue != null) dialogue.Stop();
            if (dialogue != null && openingLines != null && openingLines.Length > 0)
                dialogue.Play(openingLines);
            SetState(GameFlowState.Opening);
            Debug.Log("[流程] 开场独白：「今天要画画来着……」");
        }

        public void EnterExploring()
        {
            SetState(GameFlowState.Exploring);
            Debug.Log("[流程] 自由探索开始");
            if (timer != null) timer.StartCountdown();
        }

        public void EnterSettlement()
        {
            if (timer != null) timer.PauseCountdown();
            int total = board != null ? board.TotalPaintings : 3;
            Debug.Log($"[结算] 时间到。完成画作 {paintingsCompleted}/{total}，时钟 {timer?.ClockText}，" +
                      $"水分 {stats?.Water.Value:F0}% / 饱食 {stats?.Satiety.Value:F0}% / 精神 {stats?.Spirit.Value:F0}%（结局逻辑待补充）");
            SetState(GameFlowState.Settlement);
        }

        /// <summary>一天的结束入口：计时器到点或手动调试都走这里。</summary>
        public void EndDay()
        {
            if (State == GameFlowState.Settlement) return;
            EnterSettlement();
        }

        void SetState(GameFlowState next)
        {
            if (State == next) return;
            var old = State;
            State = next;
            StateChanged?.Invoke(old, next);
        }

        // ---------- 玩法事件 ----------

        public void OnPaintingCompleted(int index)
        {
            paintingsCompleted++;
            int total = board != null ? board.TotalPaintings : 3;
            Debug.Log($"[画板] 第 {index} 幅达标（{paintingsCompleted}/{total}）");

            // 策划案核心循环第 4 条：三幅全部完成即可提前结算。
            // （不提前的话 3×60s=180s 占 480s 只有 37.5%，永远够不到专注结局
            // 的 60% 线——数值重规划案已把这条补上，占比分母改为实际当局时长。）
            if (board != null && board.AllComplete)
            {
                if (board.IsPaintingMode) board.ExitPaintingMode(); // 先回房间再结算
                EndDay();
            }
        }

        // ---------- IGameFlow（契约层 S0） ----------
        // 结局判定（5.2）/ 结算（5.3）只读这些成员，不直接碰 GameManager 字段。

        /// <summary>已完成画作数（画作计数只此一处，支线进度不走这里，防双轨）。</summary>
        public int PaintingsCompleted => paintingsCompleted;

        /// <summary>本局实际下笔累计秒数（数据源在 DrawingBoardInteract）。</summary>
        public float PaintingSeconds => board != null ? board.PaintingSeconds : 0f;

        /// <summary>
        /// 当局实际流逝秒数（提前结算后 = 专注占比的分母）。
        /// 注意不是 roundSeconds：三幅画完提前结算时分母要跟着缩。
        /// </summary>
        public float DayElapsedSeconds =>
            timer != null ? Mathf.Max(0f, timer.roundSeconds - timer.RemainingSeconds) : 0f;

        [ContextMenu("重置整局")]
        public void ResetRun()
        {
            // 进度总线清零（画作走 PaintingsCompleted，支线走 Progress，双轨已拆开）
            if (Services.Progress != null) Services.Progress.ResetAll();

            // 统一重置：遍历场景内所有 IResettable（含未激活物体）。
            // 修复两个既有 bug：吃喝道具 SetActive(false) 后不复活、
            // 画笔拿走不归还（BrushPickup.ResetPickup 从来没人调）。
            foreach (var mb in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb is IResettable r) r.ResetForNewRun();
            }

            paintingsCompleted = 0;
            if (stats != null) stats.ResetStats();
            if (board != null) board.ResetBoards();
            EnterOpening();
        }

        // ---------- 调试快捷键（1.7 起经 InputManager 派发；F1 隐藏 HUD 已随 IMGUI 占位删除） ----------

        void OnDebugFastForward()
        {
            if (State == GameFlowState.Exploring && timer != null)
                timer.SpendGameMinutes(60f); // 快进 60 游戏分钟 = 30 真实秒
        }

        void OnDebugRestart()
        {
            if (State == GameFlowState.Settlement) ResetRun();
        }

        void OnDayEnded() => EndDay();
    }
}
