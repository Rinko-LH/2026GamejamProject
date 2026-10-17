using System;
using RoomDemo.Input;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>游戏流程状态机（策划案 1.1）：开场独白 → 自由探索 → 结算。全局单例，驱动一天的开始与结束。</summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameFlowState { Opening, Exploring, Settlement }

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

        [Header("调试 HUD")]
        [Tooltip("Demo 占位 UI：左上角显示流程状态/时钟/剩余秒数/状态值，F1 可隐藏。正式 UI 做好后可整段删除")]
        public bool showDebugHud = true;

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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
                    im.DebugToggleHud -= OnDebugToggleHud; im.DebugToggleHud += OnDebugToggleHud;
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
                im.DebugToggleHud -= OnDebugToggleHud;
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
                      $"水分 {stats?.Water.Value:F0}% / 饱食度 {stats?.Satiety.Value:F0}% / 精神 {stats?.Spirit.Value:F0}%（结局逻辑待补充）");
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
        }

        [ContextMenu("重置整局")]
        public void ResetRun()
        {
            paintingsCompleted = 0;
            if (stats != null) stats.ResetStats();
            if (board != null) board.ResetBoards();
            EnterOpening();
        }

        // ---------- 调试 HUD ----------
        // Demo 阶段没有正式 UI，用 IMGUI 在屏幕左上角把流程状态、
        // 游戏内时钟、剩余秒数、进度条、状态值打出来，方便手动验收。
        // 正式 UI（状态栏/时钟/结局界面）做好后，OnGUI + HudText 可整段删除。

        void OnGUI()
        {
            if (!showDebugHud) return;

            const float pad = 14f;
            const float lineH = 28f;
            float w = 480f;
            var box = new Rect(10f, 10f, w, lineH * 6f + pad * 2f + 10f);

            // 半透明黑底，保证文字在任何手绘背景上都看得清
            var gc = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = gc;

            float y = 10f + pad;
            HudText(22f, y, "[DEMO HUD] F1 隐藏 ｜ T 快进60分钟 ｜ 结算后按 R 重开", 14); y += lineH - 6f;

            if (State == GameFlowState.Opening)
            {
                bool dlgBusy = dialogue != null && dialogue.IsPlaying;
                string openingLine = dlgBusy
                    ? "状态：开场对白（第 " + (dialogue.CurrentIndex + 1) + "/" + dialogue.lines.Length + " 行，空格推进 · Esc 跳过）"
                    : "状态：开场独白（" + Mathf.Max(0f, openingSeconds - _openingTimer).ToString("F1") + "s 后进入自由探索）";
                HudText(22f, y, openingLine, 20);
            }
            else if (State == GameFlowState.Exploring)
                HudText(22f, y, "状态：自由探索", 20);
            else
                HudText(22f, y, "状态：结算（时间到，按 R 重开）", 20);
            y += lineH;

            if (timer != null)
            {
                HudText(22f, y, "时钟 " + timer.ClockText + " ｜ 剩余 " + Mathf.CeilToInt(timer.RemainingSeconds)
                        + "s / " + timer.roundSeconds.ToString("F0") + "s", 20);
                y += lineH;

                // 进度条：剩余 60 秒以内变红提醒
                var barBg = new Rect(22f, y + 4f, w - 44f, 16f);
                GUI.color = new Color(1f, 1f, 1f, 0.22f);
                GUI.DrawTexture(barBg, Texture2D.whiteTexture);
                GUI.color = timer.RemainingSeconds <= 60f
                    ? new Color(0.95f, 0.35f, 0.30f)
                    : new Color(0.55f, 0.85f, 0.55f);
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * timer.Progress01, barBg.height), Texture2D.whiteTexture);
                GUI.color = gc;
                y += lineH + 6f;
            }

            int total = board != null ? board.TotalPaintings : 3;
            string statsText = stats != null
                ? "水分 " + stats.Water.Value.ToString("F0") + " ｜ 饱食 " + stats.Satiety.Value.ToString("F0")
                  + " ｜ 精神 " + stats.Spirit.Value.ToString("F0")
                : "水分 - ｜ 饱食 - ｜ 精神 -";
            HudText(22f, y, "画作 " + paintingsCompleted + "/" + total + " ｜ " + statsText, 18); y += lineH;

            HudText(22f, y, "操作：WASD 移动 ｜ E 交互（吃喝各扣 10 秒）｜ 1 游戏分钟 = 0.5 秒", 14);
        }

        /// <summary>带深色描边的 HUD 文字（IMGUI 无背景，不加描边在亮背景上看不清）。</summary>
        static void HudText(float x, float y, string text, int size)
        {
            var st = new GUIStyle(GUI.skin.label);
            st.fontSize = size;
            st.fontStyle = FontStyle.Bold;
            var dark = new Color(0f, 0f, 0f, 0.85f);
            st.normal.textColor = dark;
            GUI.Label(new Rect(x - 1f, y, 1400f, 44f), text, st);
            GUI.Label(new Rect(x + 1f, y, 1400f, 44f), text, st);
            GUI.Label(new Rect(x, y - 1f, 1400f, 44f), text, st);
            GUI.Label(new Rect(x, y + 1f, 1400f, 44f), text, st);
            st.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, 1400f, 44f), text, st);
        }

        // ---------- 调试快捷键（1.7 起经 InputManager 派发，正式 UI 做好后连订阅一起删） ----------

        void OnDebugToggleHud() => showDebugHud = !showDebugHud;

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
