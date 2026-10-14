using System;
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
    public class PlayerStats : MonoBehaviour
    {
        [Header("初始值")]
        public float startWater = 50f;
        public float startSatiety = 20f;   // 策划案：醒来时饥饿 20%
        public float startSpirit = 50f;

        [Header("每秒衰减（正常状态）")]
        public float waterDecayPerSec = 0.35f;
        public float satietyDecayPerSec = 0.30f;
        public float spiritDecayPerSec = 0.25f;

        [Header("衰减倍率")]
        [Tooltip("画画时的衰减倍率，策划案要求更慢")]
        public float drawingDecayScale = 0.35f;
        [Tooltip("做支线时的衰减倍率，策划案要求更快")]
        public float sideQuestDecayScale = 1.6f;

        [Header("过低阈值")]
        public float waterLow = 20f;
        public float satietyLow = 20f;
        public float spiritLow = 25f;

        /// <summary>当前所处的活动类型，决定衰减倍率。</summary>
        public enum Activity { Idle, Drawing, SideQuest }

        public StatChannel Water { get; private set; }
        public StatChannel Satiety { get; private set; }
        public StatChannel Spirit { get; private set; }

        public Activity CurrentActivity { get; private set; } = Activity.Idle;

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

        /// <summary>
        /// 综合状态系数（0.5 ~ 1.0），用于给画画难度加成。
        /// 三项都健康时接近 1.0，有项过低时下降，最低 0.5。
        /// </summary>
        public float ConditionFactor
        {
            get
            {
                EnsureChannels();
                float f = 1f;
                if (Water.IsLow) f -= 0.2f;
                if (Satiety.IsLow) f -= 0.2f;
                if (Spirit.IsLow) f -= 0.2f;
                return Mathf.Clamp(f, 0.5f, 1f);
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