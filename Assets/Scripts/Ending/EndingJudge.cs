using System;
using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo.Ending
{
    /// <summary>结局类型（策划案 6.1）。判定优先级：低落 &gt; 专注/常规 &gt; 分神。</summary>
    public enum EndingId
    {
        None,          // 未判定（一局开始时的默认值）
        Focus,         // 专注结局：画完全部画作，且画画时长占比 ≥60%
        Normal,        // 常规结局：画完全部画作，但占比 <60%
        Procrastinate, // 分神结局：一天结束，画作未全部完成
        LowMood,       // 低落结局：结算时三项状态全部 <10%
    }

    /// <summary>一次结局判定的产出：类型 + 标题 + 收尾文案 + 关键数值。S10 结算面板消费。</summary>
    public sealed class EndingResult
    {
        public EndingId Id;

        /// <summary>结局名（如「专注结局」）。</summary>
        public string Title;

        /// <summary>一句收尾文案（策划案 6.1「文案方向」列）。</summary>
        public string Line;

        /// <summary>画画时长 / 当局实际时长（0~1）。分母是 DayElapsedSeconds，提前结算时不是 480。</summary>
        public float PaintRatio;
    }

    /// <summary>
    /// 结局判定（S9，策划案 5.2 / 6.1）：纯逻辑，进结算（StateChanged → Settlement 沿）判一次，
    /// 只认边缘不轮询。只读 Services.Flow / Services.Stats / Services.Progress，不碰 GameManager 字段。
    ///
    /// 判定表：
    ///   1. 低落：AllCritical（三项全部 &lt;10%）——优先级最高，落脚是自我接纳；
    ///   2. 专注：画完全部且 占比 = PaintingSeconds / DayElapsedSeconds ≥ 60%；
    ///   3. 常规：画完全部但占比 &lt;60%；
    ///   4. 分神：画作未全部完成（时间耗尽），旁白温和不指责。
    ///
    /// 另带一条流程触发（策划案 6.1「或中途全部掉光」）：探索态三项全部告急时
    /// 提前结束一天——之后走同一结算沿，自然判出低落结局。
    /// </summary>
    public class EndingJudge : MonoBehaviour, IResettable
    {
        public static EndingJudge Instance { get; private set; }

        /// <summary>结算沿判定完成时触发（结算面板可订阅，也可直接读 Current）。</summary>
        public event Action<EndingResult> Judged;

        /// <summary>本局结局。未判定时为 null。</summary>
        public EndingResult Current { get; private set; }

        /// <summary>专注结局的占比线（策划案 6.1：≥60%）。</summary>
        public const float FocusRatio = 0.6f;

        bool _subscribedFlow;
        PlayerStats _subscribedStats;
        bool _criticalFired;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Services.Flow != null) Services.Flow.StateChanged -= OnStateChanged;
            UnsubscribeStats();
        }

        void Update()
        {
            SubscribeFlow();
            SubscribeStats();
        }

        void SubscribeFlow()
        {
            if (_subscribedFlow) return;
            var flow = Services.Flow;
            if (flow == null) return;
            flow.StateChanged -= OnStateChanged;
            flow.StateChanged += OnStateChanged;
            _subscribedFlow = true;
        }

        void SubscribeStats()
        {
            if (_subscribedStats != null) return;
            var stats = Services.Stats as PlayerStats;
            if (stats == null) return;
            stats.AnyChanged -= OnStatChanged;
            stats.AnyChanged += OnStatChanged;
            _subscribedStats = stats;
        }

        void UnsubscribeStats()
        {
            if (_subscribedStats != null)
            {
                _subscribedStats.AnyChanged -= OnStatChanged;
                _subscribedStats = null;
            }
        }

        void OnStateChanged(GameFlowState oldState, GameFlowState newState)
        {
            // 只在 Settlement 沿判一次；离开结算（R 重开）不清这里——
            // 清理由 IResettable 在 ResetRun 里做，时序在进 Opening 之前。
            if (newState != GameFlowState.Settlement) return;
            JudgeNow();
        }

        void OnStatChanged(StatChannel ch)
        {
            if (_criticalFired) return;
            var flow = Services.Flow;
            var stats = Services.Stats;
            if (flow == null || stats == null) return;
            if (flow.State != GameFlowState.Exploring) return;
            if (!stats.AllCritical) return;

            // 中途全部掉光：提前结束一天（策划案 6.1）。只触发一次，
            // 之后走正常结算沿，AllCritical 仍在 → 判低落结局。
            _criticalFired = true;
            Debug.Log("[结局] 三项状态全部告急（<10%）——这一天提前结束了。");
            flow.EndDay();
        }

        /// <summary>结算沿判定。产出写入 Current 并广播 Judged。</summary>
        public void JudgeNow()
        {
            var flow = Services.Flow;
            if (flow == null) return;

            float elapsed = flow.DayElapsedSeconds;
            float paint = flow.PaintingSeconds;
            // 分母是当局实际时长（提前结算后不是 480）。分母≈0 属退化局
            //（如同一帧内灌完即结算）：下笔>0 视为全程在画，否则 0。
            float ratio;
            if (elapsed > 0.01f) ratio = Mathf.Clamp01(paint / elapsed);
            else ratio = paint > 0.01f ? 1f : 0f;
            bool allCritical = Services.Stats != null && Services.Stats.AllCritical;

            var id = JudgeEnding(allCritical, flow.PaintingsCompleted, flow.TotalPaintings, ratio);
            Current = MakeResult(id, ratio);
            Judged?.Invoke(Current);
            Debug.Log($"[结局] 判定：{Current.Title}（画作 {flow.PaintingsCompleted}/{flow.TotalPaintings}，" +
                      $"专注占比 {ratio:P0}，分母 {elapsed:F0}s 不是 480）");
        }

        /// <summary>
        /// 纯函数判定表，供单测直调（不用造整局）。
        /// </summary>
        public static EndingId JudgeEnding(bool allCritical, int completed, int total, float ratio)
        {
            if (allCritical) return EndingId.LowMood;
            bool allDone = total > 0 && completed >= total;
            if (allDone) return ratio >= FocusRatio ? EndingId.Focus : EndingId.Normal;
            return EndingId.Procrastinate;
        }

        static EndingResult MakeResult(EndingId id, float ratio)
        {
            var r = new EndingResult { Id = id, PaintRatio = ratio };
            switch (id)
            {
                case EndingId.Focus:
                    r.Title = "专注结局";
                    r.Line = "今天几乎没怎么走神。画完的时候，窗外的天刚好暗下来。";
                    break;
                case EndingId.Normal:
                    r.Title = "常规结局";
                    r.Line = "画完了，虽然中间岔了好几次。但画完就是画完了。";
                    break;
                case EndingId.Procrastinate:
                    r.Title = "分神结局";
                    r.Line = "画还没画完……没关系，今天已经好好过完了，明天再画也一样。";
                    break;
                case EndingId.LowMood:
                    r.Title = "低落结局";
                    r.Line = "整个人像被掏空了。但没关系的——休息也是一种选择。";
                    break;
                default:
                    r.Title = "结算";
                    r.Line = "";
                    break;
            }
            return r;
        }

        /// <summary>R 重开：清结局 + 复位中途告急触发。</summary>
        public void ResetForNewRun()
        {
            Current = null;
            _criticalFired = false;
        }
    }
}
