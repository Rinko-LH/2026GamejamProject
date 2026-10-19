using System;
using RoomDemo.Contracts;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>三项状态值。策划案里的「水分 / 饱食度 / 精神」。</summary>
    public enum StatKind
    {
        Water,      // 水分
        Satiety,    // 饱食度
        Spirit      // 精神
    }

    /// <summary>
    /// 单项状态值的变化通道。
    /// 用事件而不是轮询，让 UI 和玩法都能在同一帧收到"数值变了"的信号。
    /// </summary>
    public sealed class StatChannel
    {
        public StatKind Kind { get; }

        /// <summary>当前值，0~100。</summary>
        public float Value { get; private set; }

        /// <summary>低于这个值就算"过低"，会触发画画的惩罚。</summary>
        public float LowThreshold { get; }

        public event Action<StatChannel> Changed;

        public StatChannel(StatKind kind, float initial, float lowThreshold)
        {
            Kind = kind;
            Value = Mathf.Clamp(initial, 0f, 100f);
            LowThreshold = lowThreshold;
        }

        public bool IsLow => Value < LowThreshold;

        public void Set(float v)
        {
            float next = Mathf.Clamp(v, 0f, 100f);
            if (Mathf.Approximately(next, Value)) return;
            Value = next;
            Changed?.Invoke(this);
        }

        public void Add(float delta)
        {
            Set(Value + delta);
        }

        /// <summary>静默设置，不触发事件。用于读档或初始化。</summary>
        public void ResetSilently(float v)
        {
            Value = Mathf.Clamp(v, 0f, 100f);
        }

        internal void RaiseChanged()
        {
            Changed?.Invoke(this);
        }
    }

    /// <summary>
    /// 玩家状态：水分 / 饱食度 / 精神。
    ///
    /// 策划案里有两个关键设定，这里都实现了：
    /// 1. 状态值随时间下降；
    /// 2. 「隐藏设定：状态值在画画时掉的慢，在做支线时掉的快」。
    /// 另外策划案明确说了状态过低不判失败，而是"完成画画需要的时间变长"，
    /// 所以这里不提供 Fail 事件，只提供 IsLow 供画画玩法查询。
    /// </summary>
    public class PlayerStats : MonoBehaviour, IStatSource
    {
        [Header("初始值")]
        public float startWater = 50f;
        public float startSatiety = 20f;   // 策划案：醒来时饥饿 20%
        public float startSpirit = 50f;

        [Header("每秒衰减（正常状态）")]
        // 数值重规划案（docs/阶段一-变更留档.md §数值重规划）：
        // 饿 0.35 / 水 0.30 / 精神 0.30；初值 50/20/50；阈值统一 20。
        public float waterDecayPerSec = 0.35f;
        public float satietyDecayPerSec = 0.30f;
        public float spiritDecayPerSec = 0.30f;

        [Header("衰减倍率")]
        // 画画 ×0.5（慢）/ 支线 ×1.5（快）/ 平时 ×1.0
        [Tooltip("画画时的衰减倍率，策划案要求更慢")]
        public float drawingDecayScale = 0.5f;
        [Tooltip("做支线时的衰减倍率，策划案要求更快")]
        public float sideQuestDecayScale = 1.5f;

        [Header("过低阈值")]
        // 三项统一 20（低落机制触发线）；10 以下为告急线（AllCritical）
        public float waterLow = 20f;
        public float satietyLow = 20f;
        public float spiritLow = 20f;

        /// <summary>当前所处的活动类型，决定衰减倍率。</summary>
        public enum Activity { Idle, Drawing, SideQuest }

        public StatChannel Water { get; private set; }
        public StatChannel Satiety { get; private set; }
        public StatChannel Spirit { get; private set; }

        public Activity CurrentActivity { get; private set; } = Activity.Idle;

        /// <summary>上一次的低落状态，用于 LowMoodChanged 跨越检测。</summary>
        bool _wasLowMood;

        /// <summary>任意一项变化时触发，参数是被改动的那一项。</summary>
        public event Action<StatChannel> AnyChanged;

        void Awake()
        {
            EnsureChannels();
        }

        /// <summary>
        /// 建立三个通道。抽出来是因为菜单装配、编辑器工具、单元测试
        /// 都可能在 Awake 之前调用 ResetStats，那时通道还是 null。
        /// </summary>
        void EnsureChannels()
        {
            if (Water != null && Satiety != null && Spirit != null) return;

            Water = new StatChannel(StatKind.Water, startWater, waterLow);
            Satiety = new StatChannel(StatKind.Satiety, startSatiety, satietyLow);
            Spirit = new StatChannel(StatKind.Spirit, startSpirit, spiritLow);

            Water.Changed += OnChannelChanged;
            Satiety.Changed += OnChannelChanged;
            Spirit.Changed += OnChannelChanged;
        }

        void OnDestroy()
        {
            if (Water != null) Water.Changed -= OnChannelChanged;
            if (Satiety != null) Satiety.Changed -= OnChannelChanged;
            if (Spirit != null) Spirit.Changed -= OnChannelChanged;
        }

        void OnChannelChanged(StatChannel ch)
        {
            AnyChanged?.Invoke(ch);

            // 跨过低落阈值（任一 <20%）时派发一次，供低落机制（5.4）做边缘触发
            bool low = IsLowMood;
            if (low != _wasLowMood)
            {
                _wasLowMood = low;
                LowMoodChanged?.Invoke(low);
            }
        }

        void Update()
        {
            if (Water == null) return;

            float scale = DecayScaleFor(CurrentActivity);
            float dt = Time.deltaTime;

            Water.Add(-waterDecayPerSec * scale * dt);
            Satiety.Add(-satietyDecayPerSec * scale * dt);
            Spirit.Add(-spiritDecayPerSec * scale * dt);
        }

        /// <summary>当前活动对应的衰减倍率。</summary>
        public float DecayScaleFor(Activity activity)
        {
            switch (activity)
            {
                case Activity.Drawing: return drawingDecayScale;
                case Activity.SideQuest: return sideQuestDecayScale;
                default: return 1f;
            }
        }

        public void SetActivity(Activity activity)
        {
            CurrentActivity = activity;
        }

        /// <summary>三项里最低的那项，用于"状态很差"的综合判定。</summary>
        public StatChannel Lowest
        {
            get
            {
                EnsureChannels();
                StatChannel min = Water;
                if (Satiety.Value < min.Value) min = Satiety;
                if (Spirit.Value < min.Value) min = Spirit;
                return min;
            }
        }

        // ---------- IStatSource（契约层 S0） ----------
        // 低落机制形态按数值重规划案定：不再用"综合系数"平滑打折，
        // 而是 IsLowMood 阈值开关 + 三处乘数（画画耗时 ×1.5 / 移速 -20% /
        // 画面去饱和），由 S7/S8 消费。ConditionFactor 随之删除——
        // 全项目无消费方，留着会和新的阈值开关机制自相矛盾。

        public float WaterValue => Water != null ? Water.Value : 0f;
        public float SatietyValue => Satiety != null ? Satiety.Value : 0f;
        public float SpiritValue => Spirit != null ? Spirit.Value : 0f;

        /// <summary>任一低于 20%（低落机制触发线）。</summary>
        public bool IsLowMood =>
            (Water != null && Water.IsLow) ||
            (Satiety != null && Satiety.IsLow) ||
            (Spirit != null && Spirit.IsLow);

        /// <summary>三项全部低于 10%（低落结局线）。</summary>
        public bool AllCritical =>
            (Water != null && Water.Value < 10f) &&
            (Satiety != null && Satiety.Value < 10f) &&
            (Spirit != null && Spirit.Value < 10f);

        public event Action<bool> LowMoodChanged;

        /// <summary>恢复/扣减状态。支线（早餐/收拾/接水）一律走这里。</summary>
        public void Add(StatKind kind, float delta)
        {
            EnsureChannels();
            switch (kind)
            {
                case StatKind.Water: Water.Add(delta); break;
                case StatKind.Satiety: Satiety.Add(delta); break;
                default: Spirit.Add(delta); break;
            }
        }

        public void ResetStats()
        {
            EnsureChannels();
            Water.ResetSilently(startWater);
            Satiety.ResetSilently(startSatiety);
            Spirit.ResetSilently(startSpirit);
            Water.RaiseChanged();
            Satiety.RaiseChanged();
            Spirit.RaiseChanged();
        }
    }
}