using System;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 一天的计时器。策划案：画画会消耗时间，时间耗尽则进入结算。
    ///
    /// 这里只管"流逝"，不负责决定流逝多少——消耗多少由调用方
    /// 通过 <see cref="SpendMinutes"/> 主动告知（比如画一笔算几分钟）。
    /// </summary>
    public class DayTimer : MonoBehaviour
    {
        [Header("一天的时间")]
        [Tooltip("一天的分钟数。默认按 8:00 - 24:00，共 960 分钟")]
        public int dayLengthMinutes = 960;

        [Header("时钟显示")]
        public int startHour = 8;
        public int startMinute = 0;

        /// <summary>已经过去的分钟数。</summary>
        public float ElapsedMinutes { get; private set; }

        /// <summary>还剩多少分钟。</summary>
        public float RemainingMinutes => Mathf.Max(0f, dayLengthMinutes - ElapsedMinutes);

        /// <summary>0~1 的进度，1 表示一天结束。</summary>
        public float Progress01 => dayLengthMinutes <= 0 ? 1f : Mathf.Clamp01(ElapsedMinutes / dayLengthMinutes);

        /// <summary>当前游戏内时刻，形如 "09:30"。</summary>
        public string ClockText
        {
            get
            {
                int total = startHour * 60 + startMinute + Mathf.FloorToInt(ElapsedMinutes);
                int hh = (total / 60) % 24;
                int mm = total % 60;
                return $"{hh:00}:{mm:00}";
            }
        }

        /// <summary>一天耗尽时触发。</summary>
        public event Action DayEnded;

        /// <summary>每过一分钟触发，参数为当前时刻文本（可用于打点日志）。</summary>
        public event Action<string> MinuteTicked;

        bool _ended;
        float _lastTickMinute;

        void Awake()
        {
            _lastTickMinute = Mathf.Floor(ElapsedMinutes);
        }

        /// <summary>消耗若干分钟。达到上限时触发 <see cref="DayEnded"/>。</summary>
        public void SpendMinutes(float minutes)
        {
            if (minutes <= 0f) return;

            ElapsedMinutes += minutes;
            float nowTick = Mathf.Floor(ElapsedMinutes);
            if (nowTick > _lastTickMinute)
            {
                for (float t = _lastTickMinute + 1f; t <= nowTick; t++)
                    MinuteTicked?.Invoke(ClockText);
                _lastTickMinute = nowTick;
            }

            if (!_ended && ElapsedMinutes >= dayLengthMinutes)
            {
                _ended = true;
                DayEnded?.Invoke();
            }
        }

        public bool IsDayOver => _ended;

        public void ResetTimer()
        {
            ElapsedMinutes = 0f;
            _ended = false;
            _lastTickMinute = 0f;
        }
    }
}