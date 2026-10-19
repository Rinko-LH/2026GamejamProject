using System;
using UnityEngine;

namespace RoomDemo
{
    /// <summary>
    /// 一天计时器（策划案 1.2）：480 秒真实倒计时，归零触发 DayEnded（GameManager 接住进结算）。
    /// 流逝以游戏内时钟表现：480 秒 ↔ 960 游戏分钟（8:00 → 24:00）。
    /// 策划案里的耗时（如吃喝）一律按【游戏分钟】传入，内部按倍率换算：1 游戏分钟 = 0.5 真实秒。
    /// </summary>
    public class DayTimer : MonoBehaviour
    {
        [Header("倒计时")]
        [Tooltip("一局真实秒数")]
        public float roundSeconds = 480f;

        [Header("游戏内时钟")]
        [Tooltip("一整天折算成多少游戏分钟（8:00→24:00 = 960）")]
        public int dayLengthMinutes = 960;
        public int startHour = 8;
        public int startMinute = 0;

        /// <summary>1 真实秒 = 多少游戏分钟（2.0）。</summary>
        public float GameMinutesPerSecond => dayLengthMinutes > 0 && roundSeconds > 0f ? dayLengthMinutes / roundSeconds : 0f;

        /// <summary>1 游戏分钟 = 多少真实秒（0.5）——策划案耗时换算倍率。</summary>
        public float SecondsPerGameMinute => roundSeconds > 0f && dayLengthMinutes > 0 ? roundSeconds / dayLengthMinutes : 0f;

        public float RemainingSeconds { get; private set; }
        public float Progress01 => roundSeconds <= 0f ? 1f : Mathf.Clamp01(1f - RemainingSeconds / roundSeconds);
        public float ElapsedGameMinutes => (roundSeconds - RemainingSeconds) * GameMinutesPerSecond;
        public bool IsRunning { get; private set; }

        public string ClockText
        {
            get
            {
                int total = startHour * 60 + startMinute + Mathf.FloorToInt(ElapsedGameMinutes);
                return $"{(total / 60) % 24:00}:{total % 60:00}";
            }
        }

        /// <summary>归零时触发一次（→ 进结算）。</summary>
        public event Action DayEnded;

        /// <summary>游戏内每越过一分钟触发一次（打点用）。</summary>
        public event Action<string> MinuteTicked;

        bool _ended;
        int _lastTickMinute;

        void Awake()
        {
            RemainingSeconds = roundSeconds;
            _lastTickMinute = Mathf.FloorToInt(ElapsedGameMinutes);
        }

        void Update()
        {
            if (!IsRunning || _ended) return;
            RemainingSeconds -= Time.deltaTime;
            if (RemainingSeconds < 0f) RemainingSeconds = 0f;

            int nowMinute = Mathf.FloorToInt(ElapsedGameMinutes);
            while (nowMinute > _lastTickMinute)
            {
                _lastTickMinute++;
                MinuteTicked?.Invoke(ClockText);
            }

            if (RemainingSeconds <= 0f)
            {
                _ended = true;
                IsRunning = false;
                DayEnded?.Invoke();
            }
        }

        public void StartCountdown() { if (!_ended) IsRunning = true; }
        public void PauseCountdown() { IsRunning = false; }

        /// <summary>按策划案单位耗时：传游戏分钟，内部按倍率换算（吃喝 20 分钟 → 扣 10 秒）。</summary>
        public void SpendGameMinutes(float gameMinutes)
        {
            if (gameMinutes <= 0f || _ended || !IsRunning) return;
            RemainingSeconds -= gameMinutes * SecondsPerGameMinute;
            if (RemainingSeconds < 0f) RemainingSeconds = 0f;
        }

        public void ResetTimer()
        {
            RemainingSeconds = roundSeconds;
            IsRunning = false;
            _ended = false;
            _lastTickMinute = 0;
        }
    }
}
