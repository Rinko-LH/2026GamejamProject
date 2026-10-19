using RoomDemo.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace RoomDemo.UI
{
    /// <summary>进入低落状态时，快速滑出状态栏，停留后缓慢滑回。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class StatusBarPopup : MonoBehaviour, IResettable
    {
        [Header("点击开关")]
        [Tooltip("状态栏蓝框上的按钮。点击展开，再次点击收回；展开后超过停留时长会自动收回。")]
        public Button toggleButton;

        [Header("滑动设置")]
        [Tooltip("当前 RectTransform 的位置作为收起位置。此偏移量决定展开方向和距离；负 X 表示从右侧向左滑出。")]
        public Vector2 revealOffset = new Vector2(-220f, 0f);

        [InspectorName("弹出时长")]
        [Tooltip("快速弹出所需的秒数。使用不受游戏时间缩放影响的真实时间。")]
        [Min(0f)] public float showSeconds = 0.15f;

        [InspectorName("停留时长")]
        [Tooltip("完全展开后停留多少秒，再开始收回。")]
        [Min(0f)] public float holdSeconds = 2f;

        [InspectorName("收回时长")]
        [Tooltip("缓慢收回所需的秒数。")]
        [Min(0f)] public float hideSeconds = 1.2f;

        RectTransform _panel;
        Vector2 _hiddenPosition, _showFrom, _hideFrom;
        bool _initialized;
        bool _manualHide;
        float _animationTime = -1f;
        IStatSource _stats;
        Button _boundButton;

        void OnEnable()
        {
            Initialize();
            ResetForNewRun();
            BindButton();
            BindStats();
        }

        void Initialize()
        {
            if (_initialized) return;
            _panel = GetComponent<RectTransform>();
            _hiddenPosition = _panel.anchoredPosition;
            _initialized = true;
        }

        void Update()
        {
            BindStats();
            BindButton();
            AdvanceAnimation(Time.unscaledDeltaTime);
        }

        void BindButton()
        {
            if (_boundButton == toggleButton) return;
            if (_boundButton != null) _boundButton.onClick.RemoveListener(Toggle);
            _boundButton = toggleButton;
            if (_boundButton == null) return;
            _boundButton.navigation = new Navigation { mode = Navigation.Mode.None };
            _boundButton.onClick.AddListener(Toggle);
        }

        public void Toggle()
        {
            bool openingOrHolding = _animationTime >= 0f && !_manualHide
                && _animationTime < Mathf.Max(0f, showSeconds) + Mathf.Max(0f, holdSeconds);
            if (openingOrHolding) Hide();
            else Show();
        }

        /// <summary>点击关闭时从当前位置收回，避免弹出途中再次点击产生跳变。</summary>
        public void Hide()
        {
            if (!isActiveAndEnabled) return;
            Initialize();
            _hideFrom = _panel.anchoredPosition;
            _manualHide = true;
            _animationTime = 0f;
            AdvanceAnimation(0f);
        }

        void BindStats()
        {
            var next = Services.Stats;
            if (ReferenceEquals(_stats, next)) return;
            if (_stats != null) _stats.LowMoodChanged -= OnLowMoodChanged;
            _stats = next;
            if (_stats == null) return;
            _stats.LowMoodChanged += OnLowMoodChanged;
            // 迟到的订阅也能提醒一次，不必等待下一次阈值跨越。
            if (_stats.IsLowMood) Show();
        }

        void OnLowMoodChanged(bool low)
        {
            if (low) Show();
        }

        /// <summary>再次触发时从当前位置弹出，并重新计算停留时间。</summary>
        public void Show()
        {
            if (!isActiveAndEnabled) return;
            Initialize();
            _manualHide = false;
            _showFrom = _panel.anchoredPosition;
            _animationTime = 0f;
            AdvanceAnimation(0f);
        }

        void AdvanceAnimation(float deltaTime)
        {
            if (_animationTime < 0f) return;
            _animationTime += Mathf.Max(0f, deltaTime);
            float show = Mathf.Max(0f, showSeconds);
            float hold = Mathf.Max(0f, holdSeconds);
            float hide = Mathf.Max(0f, hideSeconds);
            Vector2 shownPosition = _hiddenPosition + revealOffset;

            if (_manualHide)
            {
                if (_animationTime >= hide) ResetForNewRun();
                else _panel.anchoredPosition = Vector2.LerpUnclamped(_hideFrom, _hiddenPosition,
                    Mathf.SmoothStep(0f, 1f, _animationTime / hide));
                return;
            }

            if (_animationTime < show)
            {
                float t = _animationTime / show;
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                _panel.anchoredPosition = Vector2.LerpUnclamped(_showFrom, shownPosition, eased);
            }
            else if (_animationTime < show + hold)
                _panel.anchoredPosition = shownPosition;
            else if (_animationTime < show + hold + hide)
            {
                float t = (_animationTime - show - hold) / hide;
                _panel.anchoredPosition = Vector2.LerpUnclamped(shownPosition, _hiddenPosition,
                    Mathf.SmoothStep(0f, 1f, t));
            }
            else
                ResetForNewRun();
        }

        public void ResetForNewRun()
        {
            Initialize();
            _animationTime = -1f;
            _manualHide = false;
            _panel.anchoredPosition = _hiddenPosition;
        }

        void OnDisable()
        {
            if (_stats != null) _stats.LowMoodChanged -= OnLowMoodChanged;
            _stats = null;
            if (_boundButton != null) _boundButton.onClick.RemoveListener(Toggle);
            _boundButton = null;
            if (_initialized) ResetForNewRun();
            _initialized = false;
        }
    }
}
